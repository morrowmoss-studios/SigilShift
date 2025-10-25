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

    void Start()
    {
        var slices = Resources.LoadAll<Sprite>(resourcePath);
        if (slices == null || slices.Length != 9)
        {
            Debug.LogError($"[Loader] Expected 9 sprites at Resources/{resourcePath}.");
            return;
        }

        float ppu = slices[0].pixelsPerUnit;
        var rect = slices[0].rect;
        Vector2 tileWorld = new(rect.width / ppu, rect.height / ppu);

        float gapX = tileWorld.x * gapPct;
        float gapY = tileWorld.y * gapPct;
        float stepX = tileWorld.x + gapX;
        float stepY = tileWorld.y + gapY;

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
            sr.color = Color.white;                     // ← full vibrance
            sr.sortingOrder = baseOrder;
            sr.sprite = slices[i];                      // ← NO masking/bake

            int r = i / 3, c = i % 3;
            Vector3 pos = origin + new Vector3(c * stepX, -r * stepY, 0f);
            t.transform.localPosition = SnapToPixels(pos, ppu);

            var col = t.GetComponent<BoxCollider2D>();
            if (col != null && sr.sprite != null)
            {
                col.size = sr.sprite.bounds.size;
                col.offset = Vector2.zero;
            }

            t.correctPos = new Vector2Int(c, r);
            t.currentPos = t.correctPos;
            t.SetLabel(i);

            BuildPlateAndShadow(t.transform, tileWorld, ppu);
        }
    }

    static Vector3 SnapToPixels(Vector3 worldPos, float ppu) =>
        new(Mathf.Round(worldPos.x * ppu) / ppu,
            Mathf.Round(worldPos.y * ppu) / ppu,
            worldPos.z);

    void BuildPlateAndShadow(Transform tile, Vector2 tileWorld, float ppu)
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
        FitSpriteToSize(plateTr, plateSR.sprite, plateWorld);
        plateTr.localPosition = Vector3.zero;

        // shadow (kept inside plate)
        var shadowTr = GetOrMake(tile, "Shadow", shadowSprite);
        var shadowSR = shadowTr.GetComponent<SpriteRenderer>();
        shadowSR.color = shadowColor;
        shadowSR.sortingOrder = baseOrder - 3;
        FitSpriteToSize(shadowTr, shadowSR.sprite, shadowWorld);
        shadowTr.localPosition = SnapToPixels(new Vector3(shadowOffset.x, shadowOffset.y, 0f), ppu);
    }

    static void FitSpriteToSize(Transform tr, Sprite s, Vector2 targetWorld)
    {
        if (!s) return;
        Vector2 spriteWorld = s.bounds.size;
        if (spriteWorld.x <= 0f || spriteWorld.y <= 0f) return;
        tr.localScale = new Vector3(targetWorld.x / spriteWorld.x, targetWorld.y / spriteWorld.y, 1f);
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
