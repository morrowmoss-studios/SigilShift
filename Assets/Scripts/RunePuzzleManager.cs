using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RunePuzzleManager : MonoBehaviour, ISlidingPuzzle
{
    [Header("Board")]
    public int rows = 3;
    public int cols = 3;

    [Header("References")]
    public RuneBoardLoader loader;                 // will auto-find if left null

    [Tooltip("Blank tile in the SOLVED layout (usually 8 = bottom-right).")]
    public int blankTileIndex = 8;

    [Header("Shuffle")]
    public bool shuffleOnStart = true;
    [Tooltip("How many random valid moves to do when shuffling.")]
    public int shuffleSteps = 90;                  // ~10x tiles feels good

    // ---- internals ----
    private RuneTile[] tiles;                      // tile index -> RuneTile (0..8)
    private Vector3[] slotWorldPos;                // slot index -> world position (0..8)

    private int blankSlot;                         // current blank slot index
    private bool busy;

    // live mapping
    // slotToTile[s] = which tile index sits in slot s (blankSlot has blankTileIndex)
    // tileToSlot[t] = which slot index tile t sits in
    private int[] slotToTile;
    private int[] tileToSlot;

    // history of moves (for autosolve); each entry is the tile index that moved into the blank
    private Stack<int> undoStack = new Stack<int>();

    // ----------  SFX  ----------
    [Header("SFX (Manager)")]
    [SerializeField] private AudioClip slideClip;                 // plays when a slide completes
    [SerializeField, Range(0f,1f)] private float slideVolume = 0.7f;
    [SerializeField] private Vector2 slidePitchJitter = new(0.96f, 1.04f);

    [Space(6)]
    [SerializeField] private AudioClip solvedClip;                // plays once when puzzle is solved
    [SerializeField, Range(0f,1f)] private float solvedVolume = 0.9f;
    [SerializeField] private Vector2 solvedPitchJitter = new(0.98f, 1.02f);

    // --------- Solve Glow (overlay only the sigil paint) ----------
    [Header("Solve Glow")]
    [SerializeField] private Material additiveSpriteMaterial; // set to built-in "Particles/Additive"
    [SerializeField] private Color glowColor = new(1.0f, 0.95f, 0.65f, 1f);
    [SerializeField, Range(0.1f, 5f)] private float glowDuration = 1.4f;
    [SerializeField, Range(0f, 0.2f)] private float glowScalePunch = 0.06f; // subtle swell
    [SerializeField] private int glowOrderBoost = 10; // render above tiles
    
    private AudioSource _audio;

    void Awake()
    {
        // Auto-find loader if not assigned
        if (!loader)
            loader = GetComponentInParent<RuneBoardLoader>() ?? FindObjectOfType<RuneBoardLoader>();

        if (!loader)
        {
            Debug.LogError("[RunePuzzleManager] No RuneBoardLoader found/assigned.");
            enabled = false; return;
        }

        tiles = loader.tiles;
        int need = rows * cols;
        if (tiles == null || tiles.Length != need)
        {
            Debug.LogError($"[RunePuzzleManager] loader.tiles must have {need} entries (TL→BR).");
            enabled = false; return;
        }

        // force backrefs so clicks reach 
        for (int i = 0; i < tiles.Length; i++)
        {
            if (!tiles[i]) { Debug.LogError($"[RunePuzzleManager] loader.tiles[{i}] is null."); enabled = false; return; }
            tiles[i].manager = this;
        }

        _audio = GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.loop = false;
        _audio.spatialBlend = 0f; // 2D
    }

    // wait a frame so RuneBoardLoader.Start() finishes laying out tiles
    IEnumerator Start()
    {
        yield return null;

        // snapshot the solved positions exactly as the loader placed them
        slotWorldPos = new Vector3[tiles.Length];
        for (int i = 0; i < tiles.Length; i++)
        {
            slotWorldPos[i] = tiles[i].transform.position;

            int r = i / cols, c = i % cols;
            tiles[i].correctPos = new Vector2Int(c, r);
            tiles[i].currentPos = tiles[i].correctPos;
            tiles[i].SetLabel(i);

            tiles[i].SetWorldPos(slotWorldPos[i], instant: true);
        }

        // init identity mapping (solved)
        slotToTile = new int[tiles.Length];
        tileToSlot = new int[tiles.Length];
        for (int i = 0; i < tiles.Length; i++)
        {
            slotToTile[i] = i;
            tileToSlot[i] = i;
        }

        blankSlot = blankTileIndex;
        ApplyBlankVisualState(hide: true);
        undoStack.Clear();

        if (shuffleOnStart)
            ShuffleRandomWalk(shuffleSteps);
    }

    void Update()
    {
        // TEMP hotkeys for testing
        if (Input.GetKeyDown(KeyCode.R)) ResetToSolved();
        if (Input.GetKeyDown(KeyCode.S)) ShuffleRandomWalk(shuffleSteps);
        if (Input.GetKeyDown(KeyCode.A)) AutoSolve();   // rewind the shuffle
    }

    // ===============================
    // ISlidingPuzzle (called by RuneTile on click)
    // ===============================
    public void TrySlideTile(RuneTile tile)
    {
        if (busy || tile == null) return;

        int tileIdx = GetTileIndex(tile);
        if (tileIdx < 0) return;

        int tileSlot = tileToSlot[tileIdx];
        if (!IsAdjacent(tileSlot, blankSlot)) return;

        busy = true;
        Vector3 target = slotWorldPos[blankSlot];

        tile.SlideTo(target, onComplete: () =>
        {
            // commit mapping (push to history)
            CommitMove(tileIdx, oldSlot: tileSlot, pushHistory: true);

            PlaySlideSfx();
            busy = false;

            if (IsSolved())
                OnSolved();
        });
    }

    // ===============================
    //  Public controls
    // ===============================
    public void ResetToSolved()
    {
        if (busy) return;

        StopAllCoroutines();

        // place tiles into solved order instantly
        for (int slot = 0; slot < tiles.Length; slot++)
        {
            int tileIdx = slot; // identity
            tiles[tileIdx].SetWorldPos(slotWorldPos[slot], instant: true);
            UpdateTileLogicalIndex(tiles[tileIdx], slot);
            slotToTile[slot] = tileIdx;
            tileToSlot[tileIdx] = slot;
        }

        blankSlot = blankTileIndex;
        ApplyBlankVisualState(hide: true);
        undoStack.Clear();
    }

    public void ShuffleRandomWalk(int steps)
    {
        if (busy) return;

        // start from solved first for a clean path (so autosolve rewinds nicely)
        ResetToSolved();

        System.Random rng = new System.Random();
        int lastMovedTile = -1;

        for (int k = 0; k < Mathf.Max(1, steps); k++)
        {
            // gather neighbors of the blank
            var neigh = GetNeighborSlots(blankSlot);
            if (neigh.Count == 0) break;

            // choose a neighbor that doesn't immediately undo the last move if possible
            int chosenSlot = -1;
            if (neigh.Count > 1 && lastMovedTile >= 0)
            {
                // try to avoid selecting the tile we just moved
                List<int> candidates = new List<int>(neigh);
                candidates.RemoveAll(s => slotToTile[s] == lastMovedTile);
                if (candidates.Count > 0)
                    chosenSlot = candidates[rng.Next(candidates.Count)];
            }
            if (chosenSlot < 0)
                chosenSlot = neigh[rng.Next(neigh.Count)];

            int tileIdx = slotToTile[chosenSlot];

            // make the move instantly (no animation during shuffle)
            MoveTileIntoBlank_Instant(tileIdx, pushHistory: true);

            lastMovedTile = tileIdx;
        }

        // keep blank hidden after shuffle
        ApplyBlankVisualState(hide: true);
    }

    public void AutoSolve(float stepDelay = 0.02f)
    {
        if (busy) return;
        StartCoroutine(CoAutoSolve(stepDelay));
    }

    IEnumerator CoAutoSolve(float stepDelay)
    {
        busy = true;

        // We recorded a valid path during Shuffle. Rewind it:
        while (undoStack.Count > 0)
        {
            int tileIdx = undoStack.Pop(); // the tile that previously moved INTO the blank
            RuneTile tile = tiles[tileIdx];

            Vector3 target = slotWorldPos[blankSlot];
            bool done = false;
            tile.SlideTo(target, () =>
            {
                CommitMove(tileIdx, oldSlot: tileToSlot[tileIdx] /* value before commit */, pushHistory: false);
                PlaySlideSfx();
                done = true;
            });

            // wait for the slide to finish
            while (!done) yield return null;
            if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay);
        }

        busy = false;
        OnSolved(); // make sure chime fires even if we were already solved
    }

    // ===============================
    // Helpers
    // ===============================
    void ApplyBlankVisualState(bool hide)
    {
        var blank = tiles[blankTileIndex];

        foreach (var sr in blank.GetComponentsInChildren<SpriteRenderer>(true))
            sr.enabled = !hide;

        var col = blank.GetComponent<BoxCollider2D>();
        if (col) col.enabled = !hide;
    }

    int GetTileIndex(RuneTile tile)
    {
        for (int i = 0; i < tiles.Length; i++)
            if (tiles[i] == tile) return i;
        return -1;
    }

    bool IsAdjacent(int a, int b)
    {
        int ar = a / cols, ac = a % cols;
        int br = b / cols, bc = b % cols;
        return Mathf.Abs(ar - br) + Mathf.Abs(ac - bc) == 1;
    }

    List<int> GetNeighborSlots(int slot)
    {
        int r = slot / cols, c = slot % cols;
        var list = new List<int>(4);
        if (c > 0) list.Add(slot - 1);
        if (c < cols - 1) list.Add(slot + 1);
        if (r > 0) list.Add(slot - cols);
        if (r < rows - 1) list.Add(slot + cols);
        return list;
    }

    void UpdateTileLogicalIndex(RuneTile tile, int slot)
    {
        int r = slot / cols, c = slot % cols;
        tile.currentPos = new Vector2Int(c, r);
    }

    // commit a move where tileIdx goes into the current blankSlot
    void CommitMove(int tileIdx, int oldSlot, bool pushHistory)
    {
        // mapping
        slotToTile[blankSlot] = tileIdx;
        slotToTile[oldSlot] = blankTileIndex;

        tileToSlot[tileIdx] = blankSlot;
        tileToSlot[blankTileIndex] = oldSlot;

        // logical pos updates
        UpdateTileLogicalIndex(tiles[tileIdx], tileToSlot[tileIdx]);
        UpdateTileLogicalIndex(tiles[blankTileIndex], tileToSlot[blankTileIndex]);

        if (pushHistory) undoStack.Push(tileIdx);

        // advance blank
        blankSlot = oldSlot;
    }

    // instant move used for shuffle
    void MoveTileIntoBlank_Instant(int tileIdx, bool pushHistory)
    {
        int fromSlot = tileToSlot[tileIdx];
        tiles[tileIdx].SetWorldPos(slotWorldPos[blankSlot], instant: true);
        CommitMove(tileIdx, oldSlot: fromSlot, pushHistory: pushHistory);
    }

    bool IsSolved()
    {
        // solved when tile index == slot index for all non-blank
        for (int slot = 0; slot < tiles.Length; slot++)
        {
            if (slot == blankSlot) continue;
            if (slotToTile[slot] != slot) return false;
        }
        return (blankSlot == blankTileIndex);
    }

    void OnSolved()
    {
        Debug.Log("<color=#9cffb0>[SigilShift] Puzzle solved!</color>");
        PlaySolvedSfx();
        StartCoroutine(CoSolveGlow());
    }

    // ---------- SFX ----------
    void PlaySlideSfx()
    {
        if (!_audio || !slideClip) return;
        _audio.pitch = Random.Range(slidePitchJitter.x, slidePitchJitter.y);
        _audio.PlayOneShot(slideClip, slideVolume);
        _audio.pitch = 1f;
    }

    void PlaySolvedSfx()
    {
        if (!_audio || !solvedClip) return;
        _audio.pitch = Random.Range(solvedPitchJitter.x, solvedPitchJitter.y);
        _audio.PlayOneShot(solvedClip, solvedVolume);
        _audio.pitch = 1f;
    }
    
    IEnumerator CoSolveGlow()
{
    // one container above the board
    var root = new GameObject("SolveGlow");
    root.transform.SetParent(transform, worldPositionStays: true);

    // build overlay sprites that match each tile exactly
    var overlays = new List<SpriteRenderer>(tiles.Length);
    for (int i = 0; i < tiles.Length; i++)
    {
        if (i == blankTileIndex) continue; // skip the empty slot

        var tile = tiles[i];
        var srcSR = tile.GetComponent<SpriteRenderer>();
        if (!srcSR || !srcSR.sprite) continue;

        var go = new GameObject($"Glow_{i}");
        go.transform.SetParent(root.transform, worldPositionStays: false);
        go.transform.position   = tile.transform.position;
        go.transform.localScale = tile.transform.localScale;
        go.transform.rotation   = tile.transform.rotation;

        var glowSR = go.AddComponent<SpriteRenderer>();
        glowSR.sprite = srcSR.sprite;
        glowSR.sortingLayerID = srcSR.sortingLayerID;
        glowSR.sortingOrder   = srcSR.sortingOrder + glowOrderBoost;
        glowSR.material = additiveSpriteMaterial ? additiveSpriteMaterial : srcSR.sharedMaterial;

        var c = glowColor; c.a = 0f;
        glowSR.color = c;

        overlays.Add(glowSR);
    }

    // animate alpha + a tiny scale swell
    float t = 0f;
    while (t < glowDuration)
    {
        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / glowDuration);

        // ease in/out + little pulse (up then down)
        float a = Mathf.Sin(u * Mathf.PI);      // 0→1→0
        float s = 1f + glowScalePunch * a;

        for (int k = 0; k < overlays.Count; k++)
        {
            var sr = overlays[k];
            if (!sr) continue;

            var col = sr.color; col.a = a;      // additive alpha
            sr.color = col;

            // scale around tile center
            var tr = sr.transform;
            tr.localScale = Vector3.one * s;
        }

        yield return null;
    }

    // cleanup
    if (root) Destroy(root);
}

}
