using UnityEngine;

[DisallowMultipleComponent]
public class RuneBoardLoader : MonoBehaviour
{
    [Header("Tiles (TL → BR)")]
    public RuneTile[] tiles = new RuneTile[9];

    [Header("Level Config (optional)")]
    public LevelConfig config;  // if assigned, overrides rows/cols/sprites/scale

    [Header("Fallback (Resources)")]
    public string resourcePath = "Sigils/MorrowMossRune"; // used only if no config or texture

    [Header("Sorting / Materials")]
    public Material tileMaterial;   // optional (Sprites/Default)
    public int baseOrder = 100;

    [Header("Layout")]
    [Range(0f, 0.25f)] public float borderPct  = 0.06f; // plate thickness vs tile
    [Range(0f, 0.25f)] public float gapPct     = 0.05f; // gap between tiles

    [Header("Plate + Shadow")]
    public Sprite plateSprite;      // simple rectangle sprite
    public Sprite shadowSprite;     // soft blob/rounded-rect
    public Color plateColor  = new(0.15f, 0.16f, 0.18f, 1f);
    public Color shadowColor = new(0f, 0f, 0f, 0.35f);
    public Vector2 shadowOffset = new(0f, -0.04f);
    public float shadowInsetPct = 0.02f; // shadow slightly smaller than plate

    [Header("Size")]
    [Tooltip("Global multiplier if no config provided.")]
    [Range(0.5f, 1.5f)] public float tileScale = 0.90f; // 1=original, <1 smaller

    void Start()
    {
        // Decide source: LevelConfig runtime-slice OR legacy Resources 3x3
        Sprite[] sprites;
        int rows, cols;
        float scale;

        if (config && config.sourceTexture)
        {
            rows = Mathf.Max(2, config.rows);
            cols = Mathf.Max(2, config.cols);
            sprites = RuntimeSlicer.Slice(config.sourceTexture, rows, cols, pixelsPerUnit: 100f);
            scale = Mathf.Clamp(config.tileScale > 0f ? config.tileScale : tileScale, 0.1f, 5f);
        }
        else
        {
            // legacy path: expect exactly 9 slices in Resources
            var legacy = Resources.LoadAll<Sprite>(resourcePath);
            if (legacy == null || legacy.Length != 9)
            {
                Debug.LogError($"[Loader] Expected 9 sprites at Resources/{resourcePath}.");
                return;
            }
            sprites = legacy;
            rows = 3; cols = 3;
            scale = tileScale;
        }

        int need = rows * cols;
        if (tiles == null || tiles.Length != need)
        {
            Debug.LogError($"[Loader] tiles[] must have {need} entries (TL→BR). Found: {(tiles == null ? 0 : tiles.Length)}");
            return;
        }

        // Base size from first sprite
        float ppu  = sprites[0].pixelsPerUnit;
        var   rect = sprites[0].rect;
        Vector2 baseTileWorld = new(rect.width / ppu, rect.height / ppu);

        // Apply global tile scale
        Vector2 tileWorld = baseTileWorld * scale;

        // Spacing derived from (scaled) tile size
        float gapX  = tileWorld.x * gapPct;
        float gapY  = tileWorld.y * gapPct;
        float stepX = tileWorld.x + gapX;
        float stepY = tileWorld.y + gapY;

        // Board extents for centering
        float totalW = cols * tileWorld.x + (cols - 1) * gapX;
        float totalH = rows * tileWorld.y + (rows - 1) * gapY;
        Vector3 origin = new(
            -totalW * 0.5f + tileWorld.x * 0.5f,
             totalH * 0.5f - tileWorld.y * 0.5f,
            0f);

        // Layout tiles
        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            if (!t) { Debug.LogWarning($"[Loader] Tile {i} missing"); continue; }

            // renderer
            var sr = t.GetComponent<SpriteRenderer>() ?? t.gameObject.AddComponent<SpriteRenderer>();
            if (tileMaterial) sr.sharedMaterial = tileMaterial;
            sr.color = Color.white;
            sr.sortingOrder = baseOrder;
            sr.sprite = sprites[i];

            // position (row-major TL→BR)
            int r = i / cols, c = i % cols;
            Vector3 pos = origin + new Vector3(c * stepX, -r * stepY, 0f);
            t.transform.localPosition = SnapToPixels(pos, ppu);

            // uniform scale (sprite + children)
            t.transform.localScale = Vector3.one * scale;

            // collider matches sprite (local size; world gets scaled by transform)
            var col = t.GetComponent<BoxCollider2D>();
            if (col && sr.sprite)
            {
                col.size = sr.sprite.bounds.size; // local-space size at scale=1
                col.offset = Vector2.zero;
            }

            // logical bookkeeping (manager reads these)
            t.correctPos = new Vector2Int(c, r);
            t.currentPos = t.correctPos;
            t.SetLabel(i);
            
            // rotation system init (respect config; 1 = no rotation)
            int qTurns = 1;
            if (config && config.enableRotation)
                qTurns = Mathf.Max(1, config.quarterTurns);

            t.InitRotationSystem(qTurns);
            

            // rotation system init if manager uses it later
            if (config) t.InitRotationSystem(config.enableRotation ? Mathf.Max(1, config.quarterTurns) : 1);
            else        t.InitRotationSystem(1);

            // plate & shadow sized to the scaled tile
            BuildPlateAndShadow(t.transform, tileWorld, ppu, scale);
        }
    }

    static Vector3 SnapToPixels(Vector3 worldPos, float ppu) =>
        new(Mathf.Round(worldPos.x * ppu) / ppu,
            Mathf.Round(worldPos.y * ppu) / ppu,
            worldPos.z);

    // NOTE: parentScale compensates for the parent's localScale so children end up the intended world size
    void BuildPlateAndShadow(Transform tile, Vector2 tileWorld, float ppu, float parentScale)
    {
        // purge everything except Plate/Shadow
        for (int i = tile.childCount - 1; i >= 0; i--)
        {
            var ch = tile.GetChild(i);
            if (ch.name != "Plate" && ch.name != "Shadow")
                DestroyImmediate(ch.gameObject);
        }

        Vector2 plateWorld  = tileWorld * (1f + borderPct);
        Vector2 shadowWorld = plateWorld * (1f - shadowInsetPct);

        // plate
        var plateTr = GetOrMake(tile, "Plate", plateSprite);
        var plateSR = plateTr.GetComponent<SpriteRenderer>();
        plateSR.color = plateColor;
        plateSR.sortingOrder = baseOrder - 2;
        FitSpriteToSize(plateTr, plateSR.sprite, plateWorld, parentScale);
        plateTr.localPosition = Vector3.zero;

        // shadow (kept inside plate)
        var shadowTr = GetOrMake(tile, "Shadow", shadowSprite);
        var shadowSR = shadowTr.GetComponent<SpriteRenderer>();
        shadowSR.color = shadowColor;
        shadowSR.sortingOrder = baseOrder - 3;
        FitSpriteToSize(shadowTr, shadowSR.sprite, shadowWorld, parentScale);
        shadowTr.localPosition = SnapToPixels(new Vector3(shadowOffset.x, shadowOffset.y, 0f), ppu);
    }

    // Compensate for parent scale so targetWorld is achieved in world space
    static void FitSpriteToSize(Transform tr, Sprite s, Vector2 targetWorld, float parentScale)
    {
        if (!s) return;
        Vector2 spriteLocal = s.bounds.size; // size at scale=1
        if (spriteLocal.x <= 0f || spriteLocal.y <= 0f) return;

        float px = targetWorld.x / (spriteLocal.x * Mathf.Max(0.0001f, parentScale));
        float py = targetWorld.y / (spriteLocal.y * Mathf.Max(0.0001f, parentScale));
        tr.localScale = new Vector3(px, py, 1f);
    }

    static Transform GetOrMake(Transform parent, string name, Sprite sprite)
    {
        var t = parent.Find(name);
        if (!t)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<SpriteRenderer>();
            t = go.transform;
        }
        var sr = t.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        return t;
    }
}

// ---------- Runtime slicer (no editor slicing required) ----------
public static class RuntimeSlicer
{
    public static Sprite[] Slice(Texture2D tex, int rows, int cols, float pixelsPerUnit = 100f)
    {
        if (!tex) return null;
        var list = new System.Collections.Generic.List<Sprite>(rows * cols);
        int w = tex.width / Mathf.Max(1, cols);
        int h = tex.height / Mathf.Max(1, rows);

        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            var rect = new Rect(c * w, (rows - 1 - r) * h, w, h); // flip Y for Unity
            var pivot = new Vector2(0.5f, 0.5f);
            var s = Sprite.Create(tex, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            list.Add(s);
        }
        return list.ToArray();
    }
}
