using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the "Ball Mountain" climbing map out of the Pandazole low-poly packs.
/// Tools > Ball Mountain > Build Map (re-running replaces the previous build).
///
/// All coordinates below are in map space: x = right, y = up, y 0 = ground top,
/// the ball plays on z = 0. The ball's best launch is 7 m/s, i.e. about 2.5 m
/// straight up or 5 m flat, so a jump (dx, dy) is possible while
/// dy + sqrt(dx^2 + dy^2) stays under 5. Easy jumps here sit near 3.3, hard ones 4.2 - 4.5.
/// </summary>
public static class BallMountainBuilder
{
    const string RootName = "BallMountain";
    const string PackRoot = "Assets/Pandazole_Lowpoly_Asset_Bundle";
    const string GeneratedFolder = "Assets/BallMountain";

    const float Depth = 6f;      // z size of everything the ball can touch
    const float ZCenter = 0.5f;  // pushed back a little so more of it sits behind the ball
    const float LeftWall = -12f, RightWall = 36f;

    static Dictionary<string, string> prefabPaths;
    static System.Random rng;
    static int groundLayer;
    static Transform root;

    struct Deco
    {
        public string Name; public float MinH, MaxH;
        public Deco(string name, float minH, float maxH) { Name = name; MinH = minH; MaxH = maxH; }
    }

    static readonly Deco[] Meadow =
    {
        new Deco("Bush_01", .7f, 1f), new Deco("Bush_02", .7f, 1f), new Deco("Bush_03", .8f, 1.1f),
        new Deco("Flower_01", .35f, .5f), new Deco("Flower_04", .35f, .5f), new Deco("Flower_07", .35f, .5f),
        new Deco("Flower_10", .35f, .5f), new Deco("Grass_01", .3f, .45f), new Deco("Grass_05", .3f, .45f),
        new Deco("Grass_10", .3f, .45f), new Deco("SoftRock_01", .3f, .5f), new Deco("SoftRock_05", .4f, .7f),
    };
    static readonly Deco[] Isles =
    {
        new Deco("Bush_01", .6f, .9f), new Deco("Flower_02", .35f, .5f), new Deco("Flower_05", .35f, .5f),
        new Deco("Grass_03", .3f, .45f), new Deco("Grass_08", .3f, .45f), new Deco("Mashroom_02", .4f, .7f),
        new Deco("Mashroom_07", .4f, .7f), new Deco("Mashroom_10", .4f, .7f), new Deco("Tree_05_Fall", 2.2f, 2.8f),
        new Deco("Tree_08_Fall", 2.2f, 2.8f), new Deco("SoftRock_03", .4f, .7f),
    };
    static readonly Deco[] Crystal =
    {
        new Deco("MineralNode_01_A", .7f, 1f), new Deco("MineralNode_02_B", .6f, .9f), new Deco("MineralNode_03_C", .7f, 1f),
        new Deco("Jem_03", .6f, .9f), new Deco("Jem_04", .5f, .8f), new Deco("HardRock_04", .6f, 1f),
        new Deco("HardRock_12", .6f, 1f), new Deco("Thorns_01", .5f, .7f), new Deco("Thorns_02", .5f, .7f),
    };
    static readonly Deco[] Summit =
    {
        new Deco("Skull_01", .3f, .45f), new Deco("Skull_05", .35f, .5f), new Deco("Bones_02", .8f, 1.1f),
        new Deco("HardRock_07", .7f, 1.1f), new Deco("HardRock_20", .7f, 1.1f), new Deco("Jem_03", .5f, .8f),
        new Deco("Tree_12_Winter", 1.8f, 2.4f),
    };

    [MenuItem("Tools/Ball Mountain/Build Map")]
    public static void Build()
    {
        Remove();
        FixPackMaterials();
        IndexPrefabs();
        rng = new System.Random(1234);
        groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0) groundLayer = 3;

        var rootGo = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(rootGo, "Build Ball Mountain");
        root = rootGo.transform;
        // The sample scene's floor top is at y = -0.5; sit 2 cm above it to avoid z-fighting.
        root.position = new Vector3(0f, -0.48f, 0f);

        // Minimum, because the ball's own bounciness is 1: anything else lets it bounce forever.
        var bouncy = PhysicsMat("MushroomBounce", 0.85f, 0f, PhysicsMaterialCombine.Minimum, PhysicsMaterialCombine.Minimum);
        var slick = PhysicsMat("SlideIce", 0f, 0f, PhysicsMaterialCombine.Minimum, PhysicsMaterialCombine.Minimum);

        BuildBounds();
        BuildMeadow(bouncy);
        BuildIsles();
        BuildCrystalHeights(bouncy);
        BuildSummitClimb(slick);
        BuildBackdrop();

        Selection.activeGameObject = rootGo;
        EditorSceneManager.MarkSceneDirty(rootGo.scene);
        Debug.Log("Ball Mountain built: " + rootGo.GetComponentsInChildren<Transform>().Length + " objects. Ball starts at x = 0 on the meadow.");
    }

    [MenuItem("Tools/Ball Mountain/Remove Map")]
    public static void Remove()
    {
        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);
    }

    // ───────────────────────── sections ─────────────────────────

    static void BuildBounds()
    {
        var g = Group("0 - Ground and Walls");
        // One box under the whole meadow so the ball never catches a seam between tiles.
        Box(g, "Ground Collider", LeftWall - 4, RightWall + 4, -2f, 0f);
        string[] tiles = { "TileGround_01", "TileGround_02", "TileGround_03" };
        for (int i = 0; i < 9; i++)
        {
            float x0 = LeftWall - 3 + i * 6f;
            NoCollide(Slab(g, tiles[i % 3], x0, x0 + 6f, -0.01f, 1.5f));
        }
        // Cliff walls with an exact flat face on the play side, stacked in 6 m bands.
        for (int i = 0; i < 6; i++)
        {
            string tile = i % 2 == 0 ? "TileGround_04" : "TileGround_05";
            Slab(g, tile, LeftWall - 5, LeftWall, 6f * (i + 1), 6f);
            Slab(g, tile, RightWall, RightWall + 5, 6f * (i + 1), 6f);
        }
    }

    // EASY - flat ground, low crates and logs, a practice mushroom, wide stairs.
    static void BuildMeadow(PhysicsMaterial bouncy)
    {
        var g = Group("1 - Meadow (EASY)");

        Prop(g, "Prop_WoodenBox_01", 6.5f, 0f, new Vector3(1.2f, 1.2f, 1.6f));       // hop over or onto
        Prop(g, "Prop_WoodenBox_04", 9.0f, 0f, new Vector3(2.2f, 1.2f, 1.6f));       // step...
        Prop(g, "Prop_WoodenBox_02", 10.7f, 0f, new Vector3(1.2f, 1.2f, 1.6f));      // ...up a two-crate wall
        Prop(g, "Prop_WoodenBox_03", 10.7f, 1.2f, new Vector3(1.2f, 1.2f, 1.6f));
        Prop(g, "Log_02", 14.5f, 0f, new Vector3(0.9f, 0.9f, 4f));                   // rollers to clear
        Prop(g, "Log_04", 17.5f, 0f, new Vector3(1.3f, 1.3f, 4f));
        Mushroom(g, 20.8f, 0f, 2.2f, 1.4f, bouncy);                                  // free bounce practice

        // Stairs up to the isles, rises of 1.2 - 1.3. Kept narrow so the first isle,
        // which doubles back over them, leaves headroom for every step.
        Slab(g, "TileGround_01", 28f, 30f, 1.2f, 1.2f);
        Slab(g, "TileGround_02", 30f, 32f, 2.5f, 2.5f);
        Slab(g, "TileGround_01", 32f, 34f, 3.8f, 3.8f);
        Slab(g, "TileGround_02", 34f, RightWall, 5.0f, 5.0f);

        Dress(g, Meadow, LeftWall + 1, 27f, 0f, 38);
        string[] trees = { "Tree_01_Spring", "Tree_02_Spring", "Tree_03_Spring", "Tree_02_Spring", "Tree_01_Spring", "Tree_03_Spring" };
        float[] treeX = { -9.5f, -5f, 2.5f, 8f, 13f, 19f };
        for (int i = 0; i < trees.Length; i++) Decor(g, trees[i], treeX[i], 0f, 2.6f, Range(3.2f, 4.1f));
        for (int i = 0; i < 4; i++) Decor(g, "Env_WoodFence_02", -9f + i * 3.02f, 0f, 3.3f, 1.2f, 0f);
        Decor(g, "Prop_Haystack_04", -3f, 0f, 2.2f, 1.2f);
        Decor(g, "Prop_Haystack_02", -1.6f, 0f, 2.6f, 1f);
        Decor(g, "Prop_Wheelbarrow", 3.8f, 0f, 2.4f, 0.9f);
        for (int i = 0; i < 4; i++) Dress(g, Meadow, 28.2f + i * 2f, 29.8f + i * 2f, new[] { 1.2f, 2.5f, 3.8f, 5f }[i], 1, 0.8f);
        // A few low tufts in front of the play plane.
        for (int i = 0; i < 9; i++) Decor(g, i % 2 == 0 ? "Grass_05" : "Grass_10", Range(LeftWall + 1, 27f), 0f, Range(-2.3f, -1.7f), 0.3f);
    }

    // EASY+ - wide floating islands heading back left, 1.5 - 2 m gaps, gentle rise.
    static void BuildIsles()
    {
        var g = Group("2 - Floating Isles (EASY+)");

        Island(g, "TileGround_01", 28.5f, 32.6f, 6.3f, 0.5f, 0.7f);
        Island(g, "TileGround_03", 22.5f, 26f, 5.8f, 0.8f);
        Island(g, "TileGround_01", 17.5f, 20.5f, 6.0f, 0.8f);
        Prop(g, "Prop_WoodenBox_01", 18.1f, 6.0f, new Vector3(1.2f, 1.2f, 1.6f));   // step up to the next isle
        Island(g, "TileGround_03", 11f, 15.5f, 7.4f, 0.8f);
        Prop(g, "Log_01", 13f, 7.4f, new Vector3(0.9f, 0.9f, 4f));
        Island(g, "TileGround_01", 5f, 9f, 7.6f, 0.8f);
        Island(g, "TileGround_03", -2f, 3f, 7.8f, 0.8f);
        Island(g, "TileGround_01", -7f, -3.5f, 8.0f, 0.8f);
        // Base camp: two ledges on the left cliff, the last safe spot before the hard part.
        Slab(g, "TileGround_02", LeftWall, -8.5f, 9.2f, 0.9f);
        Slab(g, "TileGround_02", LeftWall, -10.2f, 10.5f, 1.3f);

        Dress(g, Isles, 28.8f, 32.3f, 6.3f, 3);
        Dress(g, Isles, 22.8f, 25.7f, 5.8f, 3);
        Dress(g, Isles, 19f, 20.3f, 6.0f, 2);
        Dress(g, Isles, 11.3f, 15.2f, 7.4f, 4);
        Dress(g, Isles, 5.3f, 6.6f, 7.6f, 2);
        Dress(g, Isles, -1.7f, 2.7f, 7.8f, 4);
        Dress(g, Isles, -6.7f, -3.8f, 8.0f, 2);
        Decor(g, "Tent_01", -10.3f, 9.2f, 2.3f, 1.2f, 200f);
        Decor(g, "Campfire", -9.1f, 9.2f, 1.7f, 0.25f);
    }

    // HARD - small pads, near-max jumps, a 4.6 m gap you bounce across, a tall wall hop.
    static void BuildCrystalHeights(PhysicsMaterial bouncy)
    {
        var g = Group("3 - Crystal Heights (HARD)");

        Island(g, "TileGround_04", -8.2f, -6.4f, 11.7f, 0.5f, 0f);
        Island(g, "TileGround_04", -4.4f, -2.8f, 12.6f, 0.6f, 0.6f);
        Island(g, "TileGround_05", -0.5f, 5.5f, 12.8f, 0.7f, 0.9f);
        Prop(g, "HardRock_04", 2.5f, 12.8f, new Vector3(1f, 0.9f, 2.6f));           // spike to clear mid-platform

        // The bounce gap: 4.6 m is just possible flat-out, the mushroom below is the sane way.
        Island(g, "TileGround_04", 7f, 8.6f, 9.8f, 0.5f, 0.5f);
        Mushroom(g, 7.8f, 9.8f, 2.2f, 1.8f, bouncy);

        Island(g, "TileGround_05", 10.1f, 14.5f, 12.8f, 0.7f, 0.9f);
        Island(g, "TileGround_04", 15.5f, 18f, 14.5f, 0.7f, 0.9f);                   // 1.7 m wall hop
        Island(g, "TileGround_05", 21f, 24f, 14.5f, 0.7f, 0.9f);                     // overshoot = the slide

        Dress(g, Crystal, -0.2f, 1.6f, 12.8f, 2, 1.1f);
        Dress(g, Crystal, 3.6f, 5.2f, 12.8f, 2, 1.1f);
        Dress(g, Crystal, 10.4f, 14.2f, 12.8f, 4, 1.1f);
        Dress(g, Crystal, 15.8f, 17.7f, 14.5f, 2, 1.1f);
        Dress(g, Crystal, 21.3f, 23.7f, 14.5f, 3, 1.1f);
    }

    // VERY HARD - the climb hangs over a frictionless slide that dumps you back at the isles.
    static void BuildSummitClimb(PhysicsMaterial slick)
    {
        var g = Group("4 - The Slide and Summit (VERY HARD)");

        var slide = Ramp(g, "TileGround_03", 24f, 14.5f, 34f, 8.7f, 0.7f);
        foreach (var c in slide.GetComponentsInChildren<Collider>()) c.sharedMaterial = slick;

        Island(g, "TileGround_04", 25.5f, 27.5f, 15.8f, 0.6f, 0f);
        Island(g, "TileGround_04", 29.5f, 31f, 17.1f, 0.6f, 0f);
        Slab(g, "TileGround_05", 33f, RightWall, 18.2f, 0.8f);                       // breather on the cliff
        Island(g, "TileGround_04", 30f, 31.5f, 19.7f, 0.6f, 0.6f);
        Island(g, "TileGround_02", 22f, 27.7f, 20.6f, 0.9f, 1.4f);                   // summit

        Dress(g, Summit, 33.4f, 35.6f, 18.2f, 3, 2.4f);
        Decor(g, "Tent_02", 23.6f, 20.6f, 2.2f, 1.5f, 160f);
        Decor(g, "Campfire", 25.3f, 20.6f, 1.6f, 0.3f);
        Decor(g, "Prop_WoodenChest_01", 26.4f, 20.6f, 2.3f, 0.7f, 180f);
        Decor(g, "Tree_10_Winter", 22.7f, 20.6f, 2.8f, 3.4f);
        Decor(g, "Jem_03", 24.8f, 20.6f, 2.6f, 0.9f);
        Decor(g, "Jem_04", 27.2f, 20.6f, 1.8f, 0.7f);
    }

    static void BuildBackdrop()
    {
        var g = Group("5 - Backdrop");
        // Stretched rocks far behind the play plane read as the mountain everything hangs off.
        string[] rocks = { "HardRock_08", "HardRock_09", "HardRock_01", "HardRock_05", "HardRock_03", "HardRock_08", "HardRock_09" };
        float[] cx = { -13f, -4f, 5f, 13f, 21f, 29f, 38f };
        float[] h = { 22f, 13f, 17f, 21f, 27f, 31f, 26f };
        for (int i = 0; i < rocks.Length; i++)
        {
            var go = Make(g, rocks[i], new Vector3(0f, i * 53f, 0f), out var b);
            float s = Mathf.Min(13f / b.size.x, h[i] / b.size.y);
            PlaceScaled(go, b, new Vector3(13f / b.size.x, h[i] / b.size.y, s), new Vector3(cx[i], -1f, 11f + (i % 2) * 2f));
            NoCollide(go);
        }
        Decor(g, "Bld_Barn_01", -6f, 0f, 7.5f, 5.5f, 90f);
        Decor(g, "Bld_Silo_01", 0.5f, 0f, 7f, 6f);
        Decor(g, "Bld_FarmMill_01", 15f, 0f, 7.5f, 7f, 180f);
    }

    // ───────────────────────── building blocks ─────────────────────────

    static Transform Group(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        return go.transform;
    }

    /// <summary>Flat-topped collidable block from a ground tile: x0..x1, top at y, given thickness.</summary>
    static GameObject Slab(Transform parent, string tile, float x0, float x1, float top, float thick)
    {
        var go = Make(parent, tile, Vector3.zero, out var b);
        PlaceBox(go, b, new Vector3(x1 - x0, thick, Depth), new Vector3((x0 + x1) / 2f, top - thick, ZCenter));
        return Solid(go);
    }

    /// <summary>A slab with an upside-down rock hung underneath so it reads as a floating island.</summary>
    static void Island(Transform parent, string tile, float x0, float x1, float top, float thick, float rockHeight = 1.3f)
    {
        Slab(parent, tile, x0, x1, top, thick);
        if (rockHeight <= 0f) return;
        string rock = "HardRock_0" + (1 + rng.Next(9));
        var go = Make(parent, rock, new Vector3(180f, 0f, 0f), out var b);
        PlaceBox(go, b, new Vector3((x1 - x0) * 0.96f, rockHeight, Depth * 0.85f),
            new Vector3((x0 + x1) / 2f, top - thick - rockHeight + 0.08f, ZCenter));
        NoCollide(go);
    }

    /// <summary>Collidable prop squeezed into an exact box, centred on the ball plane.</summary>
    static GameObject Prop(Transform parent, string prefab, float centerX, float bottomY, Vector3 size)
    {
        var go = Make(parent, prefab, Vector3.zero, out var b);
        PlaceBox(go, b, size, new Vector3(centerX, bottomY, 0f));
        return Solid(go);
    }

    static void Mushroom(Transform parent, float centerX, float bottomY, float width, float height, PhysicsMaterial bouncy)
    {
        var go = Prop(parent, "Mashroom_05", centerX, bottomY, new Vector3(width, height, width));
        go.name = "Bouncy Mushroom";
        foreach (var c in go.GetComponentsInChildren<Collider>()) c.sharedMaterial = bouncy;
    }

    /// <summary>Tilted slab whose top surface runs from (x0, y0) to (x1, y1).</summary>
    static GameObject Ramp(Transform parent, string tile, float x0, float y0, float x1, float y1, float thick)
    {
        var pivot = new GameObject("Slide");
        pivot.transform.SetParent(parent, false);
        pivot.transform.localPosition = new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, 0f);
        pivot.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(y1 - y0, x1 - x0) * Mathf.Rad2Deg);
        float length = Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1));
        var go = Make(pivot.transform, tile, Vector3.zero, out var b);
        PlaceBox(go, b, new Vector3(length, thick, Depth), new Vector3(0f, -thick, ZCenter));
        Solid(go);
        return pivot;
    }

    /// <summary>Invisible box collider.</summary>
    static void Box(Transform parent, string name, float x0, float x1, float y0, float y1)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, ZCenter);
        go.AddComponent<BoxCollider>().size = new Vector3(x1 - x0, y1 - y0, Depth);
        go.layer = groundLayer;
    }

    /// <summary>Non-colliding scenery scaled uniformly to a height, standing at (x, y, z).</summary>
    static void Decor(Transform parent, string prefab, float x, float y, float z, float height, float yaw = float.NaN)
    {
        if (float.IsNaN(yaw)) yaw = Range(0f, 360f);
        var go = Make(parent, prefab, new Vector3(0f, yaw, 0f), out var b);
        float s = height / b.size.y;
        PlaceScaled(go, b, new Vector3(s, s, s), new Vector3(x, y, z));
        NoCollide(go);
    }

    /// <summary>Scatters themed scenery behind the ball plane along a surface.</summary>
    static void Dress(Transform parent, Deco[] theme, float x0, float x1, float top, int count, float maxHeight = 99f)
    {
        for (int i = 0; i < count; i++)
        {
            var d = theme[rng.Next(theme.Length)];
            float height = Mathf.Min(Range(d.MinH, d.MaxH), maxHeight);
            Decor(parent, d.Name, Range(x0, x1), top, Range(1.7f, 3.1f), height);
        }
    }

    // ───────────────────────── plumbing ─────────────────────────

    /// <summary>
    /// Spawns a pack prefab under an unscaled holder and reports its bounds in holder space.
    /// Scaling the holder (never the rotated prefab) keeps box-fitting free of skew.
    /// </summary>
    static GameObject Make(Transform parent, string prefab, Vector3 euler, out Bounds bounds)
    {
        var holder = new GameObject(prefab);
        holder.transform.SetParent(parent, false);

        GameObject inst;
        if (prefabPaths.TryGetValue(prefab, out var path))
            inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), holder.transform);
        else
        {
            Debug.LogWarning("Ball Mountain: prefab '" + prefab + "' not found in " + PackRoot + ", using a cube.");
            inst = GameObject.CreatePrimitive(PrimitiveType.Cube);
            inst.transform.SetParent(holder.transform, false);
        }
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.Euler(euler);

        bool any = false;
        bounds = new Bounds(Vector3.zero, Vector3.one);
        foreach (var mf in holder.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var toHolder = holder.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) * 2 - 1, (i >> 1 & 1) * 2 - 1, (i >> 2 & 1) * 2 - 1));
                var p = toHolder.MultiplyPoint3x4(corner);
                if (any) bounds.Encapsulate(p); else { bounds = new Bounds(p, Vector3.zero); any = true; }
            }
        }
        bounds.size = Vector3.Max(bounds.size, Vector3.one * 0.001f);
        return holder;
    }

    static void PlaceBox(GameObject holder, Bounds b, Vector3 size, Vector3 bottomCenter)
    {
        PlaceScaled(holder, b, new Vector3(size.x / b.size.x, size.y / b.size.y, size.z / b.size.z), bottomCenter);
    }

    static void PlaceScaled(GameObject holder, Bounds b, Vector3 scale, Vector3 bottomCenter)
    {
        holder.transform.localScale = scale;
        holder.transform.localPosition = bottomCenter - Vector3.Scale(scale, new Vector3(b.center.x, b.min.y, b.center.z));
    }

    static GameObject Solid(GameObject go)
    {
        if (go.GetComponentsInChildren<Collider>().Length == 0)
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                mf.gameObject.AddComponent<MeshCollider>().convex = true;
        foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.layer = groundLayer;
        return go;
    }

    static void NoCollide(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
    }

    static float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

    static void IndexPrefabs()
    {
        prefabPaths = new Dictionary<string, string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PackRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            if (!prefabPaths.ContainsKey(name)) prefabPaths.Add(name, path);
        }
    }

    /// <summary>
    /// The packs ship Built-in "Standard" materials, which render magenta under URP.
    /// Switch them to URP/Lit, keeping their palette texture and tint.
    /// </summary>
    static void FixPackMaterials()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) return; // not a URP project, leave the materials alone
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { PackRoot }))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat == null || mat.shader == null || mat.shader.name != "Standard") continue;
            var texture = mat.mainTexture;
            var color = mat.HasProperty("_Color") ? mat.color : Color.white;
            mat.shader = lit;
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(mat);
            Debug.Log("Ball Mountain: converted " + mat.name + " to URP/Lit.", mat);
        }
        AssetDatabase.SaveAssets();
    }

    static PhysicsMaterial PhysicsMat(string name, float bounciness, float friction, PhysicsMaterialCombine bounceCombine, PhysicsMaterialCombine frictionCombine)
    {
        if (!AssetDatabase.IsValidFolder(GeneratedFolder)) AssetDatabase.CreateFolder("Assets", "BallMountain");
        string path = GeneratedFolder + "/" + name + ".physicMaterial";
        var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (mat == null)
        {
            mat = new PhysicsMaterial(name);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.bounciness = bounciness;
        mat.dynamicFriction = friction;
        mat.staticFriction = friction;
        mat.bounceCombine = bounceCombine;
        mat.frictionCombine = frictionCombine;
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
