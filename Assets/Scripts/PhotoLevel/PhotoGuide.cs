using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws a dotted line of grey dots through every photo level, showing where to go: from the
/// spawn point, hopping from surface to surface, to the surface that leads to the next level
/// (or to the right edge when the next level is off to the right).
///
/// To see and edit the dots before pressing Play: Tools > Photo Level > Create Guide Dots (or
/// "Rebuild Dots" in this component's menu). That puts the dots in the scene as ordinary
/// objects, a "Guide Dots" group inside each level, which can be moved, deleted or added to by
/// hand. Rebuilding replaces them with a fresh line worked out from the level's PhotoSurfaces.
///
/// A level that has no "Guide Dots" group gets its line worked out when the game starts.
/// </summary>
public class PhotoGuide : MonoBehaviour
{
    [SerializeField] private Color color = new Color(0.55f, 0.55f, 0.55f, 0.9f);
    [SerializeField] private float dotSize = 0.12f;      // metres across
    [SerializeField] private float spacing = 0.7f;       // metres between dots
    [Tooltip("Surfaces narrower than this (lamps) are always landed on in the middle.")]
    [SerializeField] private float narrow = 2f;
    [Tooltip("How much the route avoids moving sideways. Higher = straighter lines, even if that means bigger jumps.")]
    [SerializeField] private float straightness = 3f;
    [Tooltip("How high the ball can jump, in metres, per unit of ball scale and jump height. Hops higher than this are never suggested.")]
    [SerializeField] private float jumpReach = 5.6f;

    struct Top
    {
        public PhotoSurface surface;
        public float x0, x1, y;
        public float Width => x1 - x0;
        public float Centre => (x0 + x1) / 2f;
    }

    Sprite dot;
    Material material;
    int sortingOrder = -1;

    const string HolderName = "Guide Dots";

    void Start()
    {
        // Levels whose dots are already in the scene keep them exactly as they were set up.
        Build(false);
    }

    /// <summary>Throws away the dots of every level and lays them out again. Works without pressing Play.</summary>
    [ContextMenu("Rebuild Dots")]
    public void Rebuild()
    {
        Build(true);
    }

    void Build(bool replace)
    {
        dot = null;
#if UNITY_EDITOR
        // In the editor the dots are saved with the scene, so they need a sprite that is a real asset.
        if (!Application.isPlaying) dot = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
#endif
        if (dot == null) dot = MakeDot();

        // Same material as the ball's picture, drawn just behind it.
        var ball = FindAnyObjectByType<PhotoBall>();
        var ballPicture = ball != null ? ball.GetComponentInChildren<SpriteRenderer>() : null;
        if (ballPicture != null)
        {
            material = ballPicture.sharedMaterial;
            sortingOrder = ballPicture.sortingOrder - 1;
        }

        foreach (var area in FindObjectsByType<PhotoLevelArea>())
        {
            var old = area.transform.Find(HolderName);
            if (old != null)
            {
                if (!replace) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.Undo.DestroyObjectImmediate(old.gameObject);
                else
#endif
                DestroyImmediate(old.gameObject);
            }
            var path = Route(area);
            if (path != null) PlaceDots(area, path);
        }
    }

    /// <summary>The world-space left, right and top of a surface. Worked out from the box itself, so it is right in the editor too.</summary>
    static void Extent(PhotoSurface surface, out float x0, out float x1, out float top)
    {
        var box = surface.GetComponent<BoxCollider>();
        if (box == null)
        {
            var bounds = surface.GetComponent<Collider>().bounds;
            x0 = bounds.min.x; x1 = bounds.max.x; top = bounds.max.y;
            return;
        }
        Vector3 centre = surface.transform.TransformPoint(box.center);
        Vector3 half = Vector3.Scale(box.size, surface.transform.lossyScale) * 0.5f;
        x0 = centre.x - Mathf.Abs(half.x);
        x1 = centre.x + Mathf.Abs(half.x);
        top = centre.y + Mathf.Abs(half.y);
    }

    /// <summary>The line the ball's underside follows through a level, or null if it leads nowhere.</summary>
    List<Vector2> Route(PhotoLevelArea area)
    {
        // Everything that can be stood on inside the photo (this leaves out the outer walls).
        var tops = new List<Top>();
        foreach (var surface in area.GetComponentsInChildren<PhotoSurface>())
        {
            Extent(surface, out float x0, out float x1, out float top);
            if (top <= area.Bottom + 0.01f || top > area.Top + 0.01f) continue;
            if (x1 <= area.Left || x0 >= area.Right) continue;
            tops.Add(new Top { surface = surface, x0 = Mathf.Max(x0, area.Left), x1 = Mathf.Min(x1, area.Right), y = top });
        }
        if (tops.Count == 0) return null;

        // Start on whatever is under the spawn point.
        Vector2 spawn = area.spawn != null ? area.spawn.position : area.transform.position;
        float footY = spawn.y - 0.5f * area.spawnScale;
        int start = -1;
        for (int i = 0; i < tops.Count; i++)
        {
            if (spawn.x < tops[i].x0 || spawn.x > tops[i].x1) continue;
            if (start < 0 || Mathf.Abs(tops[i].y - footY) < Mathf.Abs(tops[start].y - footY)) start = i;
        }
        if (start < 0) return null;

        // Cheapest chain of hops to every surface. A hop costs its length squared, so the
        // route prefers several short hops over one long leap, plus extra for moving sideways,
        // so it goes straight up or straight across where the ball can make the jump.
        var cost = new float[tops.Count];
        var from = new int[tops.Count];
        var at = new float[tops.Count];      // where the ball stands on each surface when it gets there
        var done = new bool[tops.Count];
        // First only with hops the ball can make. If that reaches no way out, any hop is
        // allowed, so a level always gets a line rather than none.
        int target = -1;
        bool toRightEdge = false;
        for (int attempt = 0; attempt < 2 && target < 0; attempt++)
        {
        bool anyHop = attempt == 1;
        for (int i = 0; i < tops.Count; i++) { cost[i] = float.MaxValue; from[i] = -1; done[i] = false; }
        cost[start] = 0f;
        at[start] = spawn.x;
        while (true)
        {
            int best = -1;
            for (int i = 0; i < tops.Count; i++)
                if (!done[i] && cost[i] < float.MaxValue && (best < 0 || cost[i] < cost[best])) best = i;
            if (best < 0) break;
            done[best] = true;
            for (int i = 0; i < tops.Count; i++)
            {
                if (done[i]) continue;
                float gap = Mathf.Max(0f, tops[i].x0 - tops[best].x1, tops[best].x0 - tops[i].x1);
                float rise = tops[i].y - tops[best].y;
                // Too high or too far for the ball as it is on this surface: a full-strength
                // launch that tops out at `reach` gets to a point `gap` away and `rise` up
                // only if reach >= (rise + distance) / 2.
                float reach = jumpReach * area.jumpHeight * tops[best].surface.ballScale;
                if (!anyHop && rise + Mathf.Sqrt(rise * rise + gap * gap) > 2f * reach) continue;
                Hop(tops[best], tops[i], at[best], out _, out float landX);
                float sideways = landX - at[best];
                float hop = cost[best] + gap * gap + rise * rise + straightness * sideways * sideways;
                if (hop < cost[i]) { cost[i] = hop; from[i] = best; at[i] = landX; }
            }
        }

        // Where the level is left: a surface that leads to another level, else the right edge.
        bool anyExit = false;
        for (int i = 0; i < tops.Count; i++)
        {
            var leadsTo = tops[i].surface.leadsTo;
            if (leadsTo == null || leadsTo == area) continue;
            anyExit = true;
            if (cost[i] < float.MaxValue && (target < 0 || cost[i] < cost[target])) target = i;
        }
        if (!anyExit && area.right != null)
        {
            toRightEdge = true;
            for (int i = 0; i < tops.Count; i++)
            {
                if (tops[i].x1 < area.Right - 0.01f) continue;
                if (cost[i] < float.MaxValue && (target < 0 || cost[i] < cost[target])) target = i;
            }
        }
        }
        if (target < 0) return null;

        var chain = new List<int>();
        for (int i = target; i >= 0; i = from[i]) chain.Insert(0, i);

        // Walk along each surface to where the next hop starts, then arc over to the next one.
        var path = new List<Vector2>();
        var current = new Vector2(spawn.x, tops[start].y);
        path.Add(current);
        for (int step = 1; step < chain.Count; step++)
        {
            Top a = tops[chain[step - 1]], b = tops[chain[step]];
            Hop(a, b, current.x, out float takeOffX, out float landX);
            var takeOff = new Vector2(takeOffX, a.y);
            var landing = new Vector2(landX, b.y);
            if (Mathf.Abs(takeOff.x - current.x) > 0.01f) path.Add(takeOff);

            float across = Mathf.Abs(landing.x - takeOff.x);
            float height = across < 0.05f ? 0f : 0.2f + 0.25f * across + 0.35f * Mathf.Max(0f, landing.y - takeOff.y);
            const int pieces = 16;
            for (int i = 1; i <= pieces; i++)
            {
                float t = i / (float)pieces;
                path.Add(Vector2.Lerp(takeOff, landing, t) + Vector2.up * (4f * height * t * (1f - t)));
            }
            current = landing;
        }
        if (toRightEdge) path.Add(new Vector2(area.Right, tops[target].y));
        return path;
    }

    /// <summary>Where a hop from one surface to another starts and ends, for a ball standing at x: straight up or down when one is above the other.</summary>
    void Hop(Top a, Top b, float x, out float takeOffX, out float landX)
    {
        float lo = Mathf.Max(a.x0, b.x0), hi = Mathf.Min(a.x1, b.x1);
        takeOffX = Stand(a, lo <= hi ? Mathf.Clamp(x, lo, hi) : (b.x0 > a.x1 ? a.x1 : a.x0));
        landX = Stand(b, takeOffX);
    }

    /// <summary>Where on a surface the ball stands when it wants to be at x: the middle of a lamp, otherwise a little in from the edge.</summary>
    float Stand(Top top, float x)
    {
        if (top.Width < narrow) return top.Centre;
        float margin = 0.25f;
        return Mathf.Clamp(x, top.x0 + margin, top.x1 - margin);
    }

    void PlaceDots(PhotoLevelArea area, List<Vector2> path)
    {
        var holder = new GameObject(HolderName).transform;
        holder.SetParent(area.transform, false);
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RegisterCreatedObjectUndo(holder.gameObject, "Guide Dots");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(holder.gameObject.scene);
        }
#endif
        float scale = dotSize / dot.bounds.size.x;   // whatever size the sprite is, the dot comes out dotSize across
        float lift = 0.5f * area.spawnScale;   // the line follows the middle of the ball, not its underside
        float z = area.transform.position.z;

        float untilNext = spacing;             // the first dot is one step away from the ball
        for (int i = 1; i < path.Count; i++)
        {
            Vector2 a = path[i - 1], b = path[i];
            float length = Vector2.Distance(a, b);
            float along = untilNext;
            while (along <= length)
            {
                Vector2 point = Vector2.Lerp(a, b, along / length);
                var go = new GameObject("Dot");
                go.transform.SetParent(holder, false);
                go.transform.position = new Vector3(point.x, point.y + lift, z);
                go.transform.localScale = Vector3.one * scale;
                var picture = go.AddComponent<SpriteRenderer>();
                picture.sprite = dot;
                picture.color = color;
                picture.sortingOrder = sortingOrder;
                if (material != null) picture.sharedMaterial = material;
                along += spacing;
            }
            untilNext = along - length;
        }
    }

    /// <summary>A white, soft-edged circle exactly one unit wide.</summary>
    static Sprite MakeDot()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color32[size * size];
        float radius = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                byte alpha = (byte)(255f * Mathf.Clamp01(radius - distance));
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
