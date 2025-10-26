using UnityEngine;

[DisallowMultipleComponent]
public class RuneBoardLoader : MonoBehaviour
{
    [Header("Tiles (TL → BR)")]
    public RuneTile[] tiles = new RuneTile[9];

    [Header("Rune Slice Source (Resources)")]
    public string resourcePath = "Sigils/MorrowMossRune";

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
    [Range(0.5f, 1.5f)] public float tileScale = 0.90f; // 1=original, <1 smaller

    void Start()
    {
        var slices = Resources.LoadAll<Sprite>(resourcePath);
        if (slices == null || slices.Length != 9)
        {
            Debug.LogError($"[Loader] Expected 9 sprites at Resources/{resourcePath}.");
            return;
        }

        // Base tile size in world units from sprite
        float ppu  = slices[0].pixelsPerUnit;
        var   rect = slices[0].rect;
        Vector2 baseTileWorld = new(rect.width / ppu, rect.height / ppu);

        // Apply global tile scale
        Vector2 tileWorld = baseTileWorld * tileScale;

        // Spacing derived from (scaled) tile size
        float gapX  = tileWorld.x * gapPct;
        float gapY  = tileWorld.y * gapPct;
        float stepX = tileWorld.x + gapX;
        float stepY = tileWorld.y + gapY;

        // Board extents for centering
        float totalW = 3 * tileWorld.x + 2 * gapX;
        float totalH = 3 * tileWorld.y + 2 * gapY;
        Vector3 origin = new(
            -totalW * 0.5f + tileWorld.x * 0.5f,
             totalH * 0.5f - tileWorld.y * 0.5f,
            0f);

        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            if (!t) { Debug.LogWarning($"[Loader] Tile {i} missing"); continue; }

            var sr = t.GetComponent<SpriteRenderer>() ?? t.gameObject.AddComponent<SpriteRenderer>();
            if (tileMaterial) sr.sharedMaterial = tileMaterial;
            sr.color = Color.white;
            sr.sortingOrder = baseOrder;
            sr.sprite = slices[i];

            // Position in a 3x3 (row-major TL→BR)
            int r = i / 3, c = i % 3;
            Vector3 pos = origin + new Vector3(c * stepX, -r * stepY, 0f);
            t.transform.localPosition = SnapToPixels(pos, ppu);

            // Scale the whole tile (sprite + children)
            t.transform.localScale = Vector3.one * tileScale;

            // Collider matches sprite; transform scale will be applied automatically
            var col = t.GetComponent<BoxCollider2D>();
            if (col && sr.sprite)
            {
                col.size = sr.sprite.bounds.size; // local size; world gets scaled with the transform
                col.offset = Vector2.zero;
            }

            // Logical bookkeeping for manager
            t.correctPos = new Vector2Int(c, r);
            t.currentPos = t.correctPos;
            t.SetLabel(i);

            // Build plate & shadow sized to the scaled tile
            BuildPlateAndShadow(t.transform, tileWorld, ppu, tileScale);
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
