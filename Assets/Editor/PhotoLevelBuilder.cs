using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Turns the venue photos into playable levels. Each photo is one level: a flat backdrop
/// on the ball's plane with invisible colliders traced over the real things in it. The
/// levels sit far apart in the scene and the camera is locked inside the current photo.
/// Moving between levels fades through black to the next level's spawn point.
///
/// Tools > Photo Level > Create Photo Level Scene   (copies SampleScene, builds the levels in it)
/// Tools > Photo Level > Rebuild In Open Scene      (re-run after editing the numbers below)
///
/// Level 1 (bar): counter -> coffee machine -> menu board -> neon sign -> shelf top ->
/// gallery -> blue rails. Landing on the top rail goes to the first rail photo.
/// Levels 2-4 (rails): along the top of the mesh railing; off the right edge goes to the next
/// photo, off the left edge goes back to the previous one.
/// Levels 5-9 (lamps): hop from the left big lamp to the right one, which leads to the next
/// photo; falling drops back to the first lamp photo. From the last lamp you can reach the blue rail.
///
/// Coordinates are pixels on the photo scaled to the size given to Level(), x from the left,
/// y from the TOP, exactly as an image editor shows them. Ball scale 1 means BallSize metres
/// wide, and a ball at scale s jumps about 2.3 * s m straight up.
/// </summary>
public static class PhotoLevelBuilder
{
    const string RootName = "PhotoLevel";
    const string Folder = "Assets/PhotoLevel";
    const string ScenePath = "Assets/Scenes/PhotoLevel.unity";
    const string SourceScene = "Assets/Scenes/SampleScene.unity";

    const float Depth = 0.3f;         // thin, so colliders never block clicking on the ball
    const float LedgeThickness = 0.5f;

    // Overall ball size. Every "ball scale" number below is multiplied by this, and the jump
    // is tuned so that a ball of this size still jumps the normal distance. Change this one
    // number to make the ball bigger or smaller everywhere without touching the levels.
    const float BallSize = 0.4f;
    const float LevelSpacing = 40f;   // gap between levels, so one is never visible from another

    // The photo currently being built: its size in pixels, pixels per metre, and where the next one goes.
    static float refWidth, refHeight, pixelsPerUnit, nextLevelX;
    static float PhotoWidth => refWidth / pixelsPerUnit;
    static float PhotoHeight => refHeight / pixelsPerUnit;

    static Transform root, group;
    static int groundLayer;
    static PhysicsMaterial surfaceMaterial;

    [MenuItem("Tools/Photo Level/Create Photo Level Scene")]
    public static void CreateScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(ScenePath) && !AssetDatabase.CopyAsset(SourceScene, ScenePath))
        {
            Debug.LogError("Photo Level: could not copy " + SourceScene);
            return;
        }
        var scene = EditorSceneManager.OpenScene(ScenePath);

        // The test floor and rocks from the sample scene would sit inside the photo.
        foreach (string name in new[] { "Environment", "BallMountain" })
        {
            var old = GameObject.Find(name);
            if (old != null) old.SetActive(false);
        }

        Build();
        EditorSceneManager.SaveScene(scene);

        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.All(s => s.path != ScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Photo Level/Remove From Open Scene")]
    public static void Remove()
    {
        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);
    }

    [MenuItem("Tools/Photo Level/Rebuild In Open Scene")]
    public static void Build()
    {
        Remove();
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "PhotoLevel");
        groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0) groundLayer = 3;

        // Minimum, because the ball's own bounciness is 1: this keeps it from bouncing off narrow ledges.
        surfaceMaterial = PhysicsMat("PhotoSurface", 0.2f, 0.8f);
        nextLevelX = 0f;

        var rootGo = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(rootGo, "Build Photo Level");
        root = rootGo.transform;

        // ───────────── Level 1: the bar, counter up to the blue rails ─────────────
        // The ball starts on the counter. It is a little bigger down here and shrinks a bit as it climbs.
        //                 name             photo                        width height px/m  spawn x, floor y, ball scale
        var bar = Level("Level 1 - Bar", "Assets/Photos/IMG_2259.png", 1200, 1600, 95,   680,     1022,    1.3f);

        //     name                    x0    top   x1    bottom  ball scale
        Solid("Counter",               -50, 1022, 1250, 1700,   1.3f);
        Solid("Coffee Machine",        178,  858,  455, 1022,   1.3f);
        Solid("Cake Fridge",           905,  800, 1250, 1022,   1.3f);

        //     name                    x0    x1    top   ball scale
        Ledge("Bottle Shelf",          300,  910,  775,  1.25f);
        Ledge("Menu Board",            250,  965,  655,  1.2f);
        Ledge("Mesh Shelf",            300,  910,  598,  1.15f);
        Ledge("Duct Left",               0,  290,  432,  1.1f);
        Ledge("Duct Right",            920, 1200,  418,  1.1f);
        Ledge("Neon Sign",             475,  735,  398,  1.1f);
        Ledge("Shelf Top",             300,  915,  330,  1.0f);
        Ledge("Gallery Floor",           0, 1200,  290,  0.95f);
        Ledge("Rail Low",                0, 1200,  243,  0.9f);
        Ledge("Rail Middle",             0, 1200,  187,  0.9f);
        var barTopRail =
        Ledge("Rail Top",                0, 1200,  130,  0.9f);

        // ───────────── Levels 2-4: the rail photos ─────────────
        // The near mesh railing is the floor. The blue rails and lamps here are across the hall, out of reach.
        var rail1 = Level("Level 2 - Rail", "Assets/Photos/IMG_2262.png", 1600, 1200, 100, 150, 592, 1.2f);
        EntryFromRight(1450, 592);
        Solid("Mesh Railing",          -50,  592, 1700, 1300,   1.2f);

        var rail2 = Level("Level 3 - Rail", "Assets/Photos/IMG_2263.png", 1600, 1200, 100, 150, 648, 1.2f);
        EntryFromRight(1480, 618);
        Solid("Mesh Railing",          -50,  648, 1200, 1300,   1.2f);
        Solid("Mesh Railing Rise 1",  1200,  640, 1300, 1300,   1.2f);
        Solid("Mesh Railing Rise 2",  1300,  630, 1400, 1300,   1.2f);
        Solid("Mesh Railing Right",   1400,  618, 1700, 1300,   1.2f);

        // In the last rail photo the lamps can be jumped onto: landing on any of them goes to the lamp levels.
        var rail3 = Level("Level 4 - Rail", "Assets/Photos/IMG_2264.png", 1600, 1200, 100, 150, 603, 1.2f);
        Solid("Mesh Railing",          -50,  603, 1700, 1300,   1.2f);
        var rail3Lamps = new[]
        {
        Ledge("Lamp A",                135,  225,  330,  1.2f),
        Ledge("Lamp B",                240,  330,  350,  1.2f),
        Ledge("Lamp C",                995, 1100,  228,  1.2f),
        Ledge("Big Lamp",             1170, 1315,  100,  1.2f),
        };

        // These photos are close-ups, so the ball jumps twice as high here.
        foreach (var rail in new[] { rail1, rail2, rail3 }) rail.jumpHeight = 2f;

        // ───────────── Levels 5-9: the lamp photos ─────────────
        // Each photo has two big lamps with smaller ones between them. The ball starts on the
        // left big lamp ("Lamp Start") and hops across; landing on the right big lamp
        // ("Lamp End") moves on to the next photo, where that lamp is the start. There is
        // no floor: miss a lamp and you fall back to the first lamp photo (level 5).
        //     name                    x0    x1    top   ball scale
        var lamps1 = Level("Level 5 - Lamps", "Assets/Photos/IMG_2235.png", 1600, 1200, 100, 367, 465, 1.3f);
        Ledge("Lamp Start",            285,  450,  465,  1.3f);
        Ledge("Lamp 1",                480,  598,  535,  1.3f);
        Ledge("Lamp 2",                580,  680,  562,  1.3f);
        Ledge("Lamp 3",                905, 1000,  565,  1.3f);
        Ledge("Lamp 4",               1000, 1115,  540,  1.3f);
        var lamps1End =
        Ledge("Lamp End",             1135, 1295,  468,  1.3f);

        var lamps2 = Level("Level 6 - Lamps", "Assets/Photos/IMG_2236.png", 1600, 1200, 100, 173, 430, 1.3f);
        Ledge("Lamp Start",            100,  245,  430,  1.3f);
        Ledge("Lamp 1",                340,  440,  505,  1.3f);
        Ledge("Lamp 2",                455,  555,  485,  1.3f);
        Ledge("Lamp 3",                580,  680,  505,  1.3f);
        Ledge("Lamp 4",               1055, 1155,  507,  1.3f);
        Ledge("Lamp 5",               1162, 1262,  485,  1.3f);
        var lamps2End =
        Ledge("Lamp End",             1295, 1430,  430,  1.3f);

        var lamps3 = Level("Level 7 - Lamps", "Assets/Photos/IMG_2238.png", 1600, 1200, 100, 558, 488, 1.3f);
        Ledge("Lamp Start",            485,  630,  488,  1.3f);
        Ledge("Lamp 1",                628,  720,  543,  1.3f);
        Ledge("Lamp 2",                890,  985,  565,  1.3f);
        Ledge("Lamp 3",                965, 1055,  546,  1.3f);
        var lamps3End =
        Ledge("Lamp End",             1100, 1250,  492,  1.3f);

        var lamps4 = LampsLevel8(out var lamps4End);

        // The last lamp photo: from the right big lamp you can jump down onto the blue rail beside it.
        var lamps5 = Level("Level 9 - Lamps", "Assets/Photos/IMG_2239.png", 1600, 1200, 100, 477, 468, 1.3f);
        Ledge("Lamp Start",            410,  545,  468,  1.3f);
        Ledge("Lamp 1",                565,  655,  522,  1.3f);
        Ledge("Lamp 2",                640,  735,  545,  1.3f);
        Ledge("Lamp 3",                845,  935,  545,  1.3f);
        Ledge("Lamp 4",                925, 1015,  525,  1.3f);
        Ledge("Lamp End",             1020, 1155,  470,  1.3f);
        Ledge("Rail",                 1000, 1150,  678,  1.0f);
        Ledge("Rail Down 1",          1150, 1300,  700,  1.0f);
        Ledge("Rail Down 2",          1300, 1450,  735,  1.0f);
        Ledge("Rail Down 3",          1450, 1600,  775,  1.0f);

        // ───────────── How the levels connect ─────────────
        barTopRail.leadsTo = rail1;   // landing on the bar's top rail
        rail1.right = rail2;          // off the right edge
        rail2.right = rail3;
        rail2.left = rail1;           // and back again off the left edge
        rail3.left = rail2;
        foreach (var lamp in rail3Lamps) lamp.leadsTo = lamps1;   // landing on a lamp in the last rail photo
        lamps1End.leadsTo = lamps2;   // landing on a photo's right big lamp
        lamps2End.leadsTo = lamps3;
        lamps3End.leadsTo = lamps4;
        lamps4End.leadsTo = lamps5;

        foreach (var lamps in new[] { lamps1, lamps2, lamps3, lamps4, lamps5 }) lamps.jumpHeight = 2f;   // same boost as the rail photos

        var levels = new[] { bar, rail1, rail2, rail3, lamps1, lamps2, lamps3, lamps4, lamps5 };
        foreach (var rail in new[] { rail1, rail2, rail3 }) rail.below = bar;                              // falling out of a rail photo: back to the bar
        foreach (var lamps in new[] { lamps1, lamps2, lamps3, lamps4, lamps5 }) lamps.below = lamps1;     // missing a lamp: back to the first lamp photo
        foreach (var level in levels) Seal(level);

        rootGo.AddComponent<PhotoLevelManager>().SetStartLevel(bar);
        PlacePlayer(bar);
        ApplyBallTexture();
        ConfineCamera();

        Selection.activeGameObject = rootGo;
        EditorSceneManager.MarkSceneDirty(rootGo.scene);
        Debug.Log("Photo Level built: " + rootGo.GetComponentsInChildren<PhotoLevelArea>().Length + " levels, " + rootGo.GetComponentsInChildren<PhotoSurface>().Length + " surfaces.");
    }

    const string Level8Name = "Level 8 - Lamps";

    /// <summary>The lamp photo between levels 7 and 9. Returns the level and its right big lamp.</summary>
    static PhotoLevelArea LampsLevel8(out PhotoSurface end)
    {
        //     name                    x0    x1    top   ball scale
        var area = Level(Level8Name, "Assets/Photos/IMG_2241.png", 1600, 1200, 100, 245, 446, 1.3f);
        Ledge("Lamp Start",            165,  325,  446,  1.3f);
        Ledge("Lamp 1",                362,  455,  499,  1.3f);
        Ledge("Lamp 2",                455,  545,  521,  1.3f);
        Ledge("Lamp 3",                915, 1005,  520,  1.3f);
        Ledge("Lamp 4",               1040, 1135,  499,  1.3f);
        Ledge("Lamp 5",               1135, 1225,  519,  1.3f);
        end =
        Ledge("Lamp End",             1335, 1495,  446,  1.3f);
        return area;
    }

    /// <summary>
    /// Adds only level 8 to the scene that is open, between "Level 7 - Lamps" and
    /// "Level 9 - Lamps", leaving everything else in the scene exactly as it is. Running it
    /// again replaces level 8, so the numbers in LampsLevel8 can be tuned and re-applied.
    /// </summary>
    [MenuItem("Tools/Photo Level/Add Level 8 To Open Scene")]
    public static void AddLevel8()
    {
        var rootGo = GameObject.Find(RootName);
        if (rootGo == null) { Debug.LogError("Photo Level: no " + RootName + " object in the open scene."); return; }
        var areas = rootGo.GetComponentsInChildren<PhotoLevelArea>();
        var before = areas.FirstOrDefault(a => a.name == "Level 7 - Lamps");
        var after = areas.FirstOrDefault(a => a.name == "Level 9 - Lamps");
        var old = areas.FirstOrDefault(a => a.name == Level8Name);
        if (before == null || after == null) { Debug.LogError("Photo Level: the scene needs \"Level 7 - Lamps\" and \"Level 9 - Lamps\" to put level 8 between."); return; }

        // Whatever in level 7 sends the ball onwards (its right big lamp) will lead to level 8 instead.
        var entrances = before.GetComponentsInChildren<PhotoSurface>()
            .Where(s => s.leadsTo == after || (old != null && s.leadsTo == old)).ToList();
        if (entrances.Count == 0)
            entrances = before.GetComponentsInChildren<PhotoSurface>().Where(s => s.name == "Lamp End").ToList();

        root = rootGo.transform;
        groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0) groundLayer = 3;
        surfaceMaterial = before.GetComponentInChildren<BoxCollider>().sharedMaterial as PhysicsMaterial;
        // To the right of every other level, so nothing that is already there moves.
        nextLevelX = areas.Where(a => a != old).Max(a => a.transform.localPosition.x + a.size.x) + LevelSpacing;
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var level = LampsLevel8(out var end);
        Undo.RegisterCreatedObjectUndo(level.gameObject, "Add Level 8");
        level.transform.SetSiblingIndex(before.transform.GetSiblingIndex() + 1);
        level.jumpHeight = before.jumpHeight;   // same as its neighbour, including any tuning done in the Inspector
        level.below = before.below;
        end.leadsTo = after;
        foreach (var entrance in entrances)
        {
            Undo.RecordObject(entrance, "Add Level 8");
            entrance.leadsTo = level;
        }
        Seal(level);

        Selection.activeGameObject = level.gameObject;
        EditorSceneManager.MarkSceneDirty(rootGo.scene);
        if (entrances.Count == 0) Debug.LogWarning("Photo Level: nothing in level 7 leads onwards. Set Leads To on its last lamp to " + Level8Name + ".");
        Debug.Log("Photo Level: added " + Level8Name + ". Save the scene to keep it.");
    }

    // ───────────────────────── pieces ─────────────────────────

    /// <summary>
    /// Starts a level: a backdrop quad plus a left wall. Everything added after this belongs to it.
    /// width and height are the pixel size the coordinates are measured in (the photo's shape),
    /// pixelsPerMetre sets how big the photo is in the world, and the spawn point is given in
    /// photo pixels: x, and the y of the surface to stand on.
    /// </summary>
    static PhotoLevelArea Level(string name, string texturePath, float width, float height, float pixelsPerMetre,
        float spawnX, float spawnFloorY, float spawnScale)
    {
        refWidth = width;
        refHeight = height;
        pixelsPerUnit = pixelsPerMetre;

        group = Group(name);
        group.localPosition = new Vector3(nextLevelX, 0f, 0f);
        nextLevelX += PhotoWidth + LevelSpacing;
        var area = group.gameObject.AddComponent<PhotoLevelArea>();
        area.size = new Vector2(PhotoWidth, PhotoHeight);
        area.spawnScale = spawnScale * BallSize;
        var spawn = new GameObject("Spawn").transform;
        spawn.SetParent(group, false);
        spawn.localPosition = new Vector3(X(spawnX), Y(spawnFloorY) + 0.5f * area.spawnScale, 0f);
        area.spawn = spawn;

        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        if (importer != null && (importer.maxTextureSize < 4096 || !importer.mipmapEnabled || importer.filterMode != FilterMode.Trilinear))
        {
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.SaveAndReimport();
        }

        string materialPath = Folder + "/" + Path.GetFileNameWithoutExtension(texturePath) + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        var shader = Shader.Find("PhotoLevel/Backdrop");
        if (shader == null)
        {
            Debug.LogWarning("Photo Level: shader PhotoLevel/Backdrop did not load, using URP Unlit. The ball may clip into the photo.");
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.shader = shader;
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        EditorUtility.SetDirty(material);

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Photo";
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(group, false);
        quad.transform.localPosition = new Vector3(PhotoWidth / 2f, PhotoHeight / 2f, 0f);
        quad.transform.localScale = new Vector3(PhotoWidth, PhotoHeight, 1f);
        var renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return area;
    }

    /// <summary>
    /// Where the ball appears when it comes back into the current level from the one on its right.
    /// Photo pixels: x, and the y of the surface to stand on.
    /// </summary>
    static void EntryFromRight(float x, float floorY)
    {
        var area = group.GetComponent<PhotoLevelArea>();
        var point = new GameObject("Spawn From Right").transform;
        point.SetParent(group, false);
        point.localPosition = new Vector3(X(x), Y(floorY) + 0.5f * area.spawnScale, 0f);
        area.spawnFromRight = point;
    }

    /// <summary>Walls off every side of a level that leads nowhere.</summary>
    static void Seal(PhotoLevelArea area)
    {
        group = area.transform;
        float width = area.size.x, height = area.size.y;
        if (area.above == null) Box("Ceiling", -1f, width + 1f, height, height + 1f, 1f, false);
        if (area.below == null) Box("Bottom", -1f, width + 1f, -1f, 0f, 1f, false);
        // Side walls are taller than the photo so the ball cannot slip out while leaving through the top.
        if (area.left == null) Box("Left Wall", -1f, 0f, -4f, height + 4f, 1f, false);
        if (area.right == null) Box("Right Wall", width, width + 1f, -4f, height + 4f, 1f, false);
    }

    /// <summary>A solid block the ball cannot enter from any side. Pixel rectangle on the current photo.</summary>
    static PhotoSurface Solid(string name, float x0, float top, float x1, float bottom, float ballScale)
    {
        return Box(name, X(x0), X(x1), Y(bottom), Y(top), ballScale, false);
    }

    /// <summary>A thin thing to land on. The ball can jump up through it from below.</summary>
    static PhotoSurface Ledge(string name, float x0, float x1, float top, float ballScale)
    {
        return Box(name, X(x0), X(x1), Y(top) - LedgeThickness, Y(top), ballScale, true);
    }

    /// <summary>The finish: a round trigger at a pixel position.</summary>
    static void Goal(string name, float x, float y, float radius)
    {
        var go = new GameObject(name);
        go.transform.SetParent(group, false);
        go.transform.localPosition = new Vector3(X(x), Y(y), 0f);
        var sphere = go.AddComponent<SphereCollider>();
        sphere.radius = radius / pixelsPerUnit;
        sphere.isTrigger = true;
        go.AddComponent<PhotoGoal>();
    }

    static PhotoSurface Box(string name, float x0, float x1, float y0, float y1, float ballScale, bool oneWay)
    {
        var go = new GameObject(name);
        go.transform.SetParent(group, false);
        go.transform.localPosition = new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, 0f);
        go.layer = groundLayer;
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(x1 - x0, y1 - y0, Depth);
        box.sharedMaterial = surfaceMaterial;
        var surface = go.AddComponent<PhotoSurface>();
        surface.ballScale = ballScale * BallSize;
        surface.oneWay = oneWay;
        return surface;
    }

    static float X(float pixel) => pixel / pixelsPerUnit;
    static float Y(float pixel) => (refHeight - pixel) / pixelsPerUnit;

    static Transform Group(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        return go.transform;
    }

    static void PlacePlayer(PhotoLevelArea level)
    {
        var controller = Object.FindAnyObjectByType<RubberBallController>();
        if (controller == null)
        {
            Debug.LogWarning("Photo Level: no RubberBallController in this scene. Drop the Player prefab onto the Spawn object of " + level.name + " and add a PhotoBall component to it.");
            return;
        }
        Undo.RecordObject(controller.transform, "Place Player");
        controller.transform.position = level.spawn.position;
        controller.transform.localScale = Vector3.one * level.spawnScale;
        if (controller.GetComponent<PhotoBall>() == null) Undo.AddComponent<PhotoBall>(controller.gameObject);

        // Keep the jump distance the levels were measured for, whatever the overall ball size.
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("sizeForNormalJump").floatValue = BallSize;
        serialized.ApplyModifiedProperties();
        // The player is a prefab instance: without this the new position is not saved with the scene.
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller.transform);
    }

    /// <summary>
    /// Puts the bouncy ball picture on the Player prefab: it replaces the plain circle sprite,
    /// and the 3D sphere that was drawn in front of that sprite is switched off so the picture
    /// (which squashes and stretches with the ball) is what you see.
    /// </summary>
    [MenuItem("Tools/Photo Level/Apply Ball Texture")]
    public static void ApplyBallTexture()
    {
        const string texturePath = Folder + "/BouncyBall.png";
        const string prefabPath = "Assets/Prefabs/Player.prefab";
        const float ballPixels = 497f;   // the ball's diameter inside the 512 px image

        var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null) { Debug.LogWarning("Photo Level: " + texturePath + " is missing, ball texture not applied."); return; }
        if (importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != ballPixels || !importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ballPixels;   // so the ball is exactly 1 unit wide, like the old circle
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
        if (sprite == null) { Debug.LogWarning("Photo Level: could not load the ball sprite."); return; }

        var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var circle = prefab.transform.Find("Circle");
            var picture = circle != null ? circle.GetComponent<SpriteRenderer>() : null;
            if (picture == null) { Debug.LogWarning("Photo Level: the Player prefab has no Circle sprite to put the ball texture on."); return; }
            picture.sprite = sprite;
            picture.color = Color.white;
            var sphere = prefab.GetComponent<MeshRenderer>();
            if (sphere != null) sphere.enabled = false;
            PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }
    }

    /// <summary>Keeps the follow camera inside the current photo.</summary>
    static void ConfineCamera()
    {
        var cam = Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>();
        if (cam == null)
        {
            Debug.LogWarning("Photo Level: no CinemachineCamera in this scene, so the camera is not confined to the photo.");
            return;
        }
        if (cam.GetComponent<PhotoCameraConfiner>() == null) Undo.AddComponent<PhotoCameraConfiner>(cam.gameObject);

        // Anything the camera sees beside a tall photo is plain black, not sky.
        var main = Camera.main;
        if (main != null)
        {
            Undo.RecordObject(main, "Black Background");
            main.clearFlags = CameraClearFlags.SolidColor;
            main.backgroundColor = Color.black;
        }
    }

    static PhysicsMaterial PhysicsMat(string name, float bounciness, float friction)
    {
        string path = Folder + "/" + name + ".physicMaterial";
        var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (material == null)
        {
            material = new PhysicsMaterial(name);
            AssetDatabase.CreateAsset(material, path);
        }
        material.bounciness = bounciness;
        material.dynamicFriction = friction;
        material.staticFriction = friction;
        material.bounceCombine = PhysicsMaterialCombine.Minimum;
        material.frictionCombine = PhysicsMaterialCombine.Average;
        EditorUtility.SetDirty(material);
        return material;
    }

    // ───────────────────────── preview ─────────────────────────

    /// <summary>
    /// Renders each level with its colliders drawn over the photo to PhotoLevelPreview_N.png
    /// in the project folder, for checking that they line up.
    /// </summary>
    [MenuItem("Tools/Photo Level/Save Preview Images")]
    public static void SavePreview()
    {
        var level = GameObject.Find(RootName);
        if (level == null) { Debug.LogError("Photo Level: build the level first."); return; }

        var holder = new GameObject("Preview") { hideFlags = HideFlags.HideAndDontSave };
        var paint = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        foreach (var surface in level.GetComponentsInChildren<PhotoSurface>())
        {
            var bounds = surface.GetComponent<Collider>().bounds;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(holder.transform, false);
            quad.transform.position = new Vector3(bounds.center.x, bounds.center.y, -1f);
            quad.transform.localScale = new Vector3(bounds.size.x, bounds.size.y, 1f);
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", surface.oneWay ? new Color(1f, 0.8f, 0f, 0.45f) : new Color(0f, 1f, 0.3f, 0.45f));
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = paint;
            renderer.SetPropertyBlock(block);
        }

        const int width = 1600, height = 1600;
        var target = new RenderTexture(width, height, 24);
        var camera = new GameObject("Preview Camera") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.targetTexture = target;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);

        var areas = level.GetComponentsInChildren<PhotoLevelArea>();
        for (int i = 0; i < areas.Length; i++)
        {
            camera.transform.position = new Vector3((areas[i].Left + areas[i].Right) / 2f, (areas[i].Bottom + areas[i].Top) / 2f, -10f);
            camera.orthographicSize = Mathf.Max(areas[i].size.x, areas[i].size.y) / 2f;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes("PhotoLevelPreview_" + (i + 1) + ".png", image.EncodeToPNG());
        }

        camera.targetTexture = null;
        Object.DestroyImmediate(camera.gameObject);
        Object.DestroyImmediate(holder);
        Object.DestroyImmediate(paint);
        Object.DestroyImmediate(image);
        target.Release();
        Debug.Log("Photo Level: wrote " + areas.Length + " preview images to the project folder.");
    }

    /// <summary>For running headless: build the scene, then save the preview.</summary>
    public static void CreateSceneAndPreview()
    {
        CreateScene();
        SavePreview();
    }
}
