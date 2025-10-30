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

    // ---------- NEW: Rotation options ----------
    [Header("Rotation")]
    [Tooltip("Allow tiles to rotate when clicked if not adjacent to the blank.")]
    public bool rotationEnabled = true;

    [Tooltip("How many 90° steps per full turn. 1 = disabled, 2 = 180° only, 4 = 90° steps.")]
    [Range(1, 8)] public int rotationQuarterTurns = 4;

    [Tooltip("Randomize tile rotations during Shuffle.")]
    public bool randomizeRotationOnShuffle = true;

    // ---------- NEW: Autosolve rotation correction ----------
    [Header("Autosolve")]
    [Tooltip("When AutoSolve runs, snap all tiles to this rotation step before sliding back.")]
    public bool fixRotationsOnAutoSolve = true;

    [Tooltip("Which rotation step counts as 'upright' for autosolve (usually 0).")]
    [Range(0, 7)] public int autoSolveTargetRotationStep = 0;

    [Tooltip("Pause after snapping rotations (seconds). 0 = no pause.")]
    [Range(0f, 0.5f)] public float autosolveRotationPause = 0.05f;

    // ---- internals ----
    private RuneTile[] tiles;                      // tile index -> RuneTile (0..N-1)
    private Vector3[] slotWorldPos;                // slot index -> world position

    private int blankSlot;                         // current blank slot index
    private bool busy;

    private int[] slotToTile;                      // slot -> tile index (blankSlot stores blankTileIndex)
    private int[] tileToSlot;                      // tile index -> slot

    private Stack<int> undoStack = new Stack<int>();   // history for autosolve

    // ----------  SFX  ----------
    [Header("SFX (Manager)")]
    [SerializeField] private AudioClip slideClip;                 // plays when a slide completes
    [SerializeField, Range(0f,1f)] private float slideVolume = 0.7f;
    [SerializeField] private Vector2 slidePitchJitter = new(0.96f, 1.04f);

    [Space(6)]
    [SerializeField] private AudioClip solvedClip;                // plays once when puzzle is solved
    [SerializeField, Range(0f,1f)] private float solvedVolume = 0.9f;
    [SerializeField] private Vector2 solvedPitchJitter = new(0.98f, 1.02f);
    
    [Header("Hint FX")]
    [SerializeField, Range(0.05f, 0.35f)] private float hintEnlarge = 0.18f; // +18% pop
    [SerializeField, Range(0.2f, 1.2f)]  private float hintDuration = 0.65f;
    [SerializeField, Range(2f, 25f)]     private float hintNudge = 8f;       // world-units * 0.001 (safe tiny push)
    [SerializeField, Range(2f, 25f)]     private float hintRotateDeg = 12f;  // ±deg wiggle

    // --------- Solve Glow ----------
    [Header("Solve Glow")]
    [SerializeField] private Material additiveSpriteMaterial; // set to built-in "Particles/Additive"
    [SerializeField] private Color glowColor = new(1.0f, 0.95f, 0.65f, 1f);
    [SerializeField, Range(0.1f, 5f)] private float glowDuration = 1.4f;
    [SerializeField, Range(0f, 0.2f)] private float glowScalePunch = 0.06f; // subtle swell
    [SerializeField] private int glowOrderBoost = 10; // render above tiles
    [SerializeField, Range(0.8f, 1.5f)] float glowBaseScale = 1.02f;   // overall size vs tile
    [SerializeField, Range(0f, 1f)]     float glowMaxAlpha  = 1f;      // cap brightness
    [SerializeField] AnimationCurve glowScaleCurve = null;              // time -> 0..1
    [SerializeField] AnimationCurve glowAlphaCurve = null;              // time -> 0..1

    // keep your existing glowScalePunch; it’s the amplitude

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

        // force backrefs so clicks reach us
        for (int i = 0; i < tiles.Length; i++)
        {
            if (!tiles[i]) { Debug.LogError($"[RunePuzzleManager] loader.tiles[{i}] is null."); enabled = false; return; }
            tiles[i].manager = this;
        }

        // pull rotation rules from loader.config if present
        if (loader && loader.config)
        {
            rotationEnabled      = loader.config.enableRotation;
            rotationQuarterTurns = Mathf.Max(1, loader.config.quarterTurns);
        }
        // Initialize each tile’s rotation system
        for (int i = 0; i < tiles.Length; i++)
            tiles[i].InitRotationSystem(rotationEnabled ? rotationQuarterTurns : 1);

        _audio = GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.loop = false;
        _audio.spatialBlend = 0f; // 2D
    }

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
        if (Input.GetKeyDown(KeyCode.T))
        {
            var rng = new System.Random();

            foreach (var tile in loader.tiles)
            {
                if (!tile) continue;

                int max = Mathf.Max(1, tile.MaxRotationSteps);
                int step = rng.Next(0, max);
                tile.SetRotationSteps(step);
            }

            Debug.Log("🔄 Randomized tile rotations");
        }
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

        // click-to-rotate if not adjacent
        if (!IsAdjacent(tileSlot, blankSlot))
        {
            if (rotationEnabled && rotationQuarterTurns > 1)
            {
                tile.RotateOnce();
            }
            return;
        }

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

            // also reset rotation visuals
            tiles[tileIdx].InitRotationSystem(rotationEnabled ? rotationQuarterTurns : 1);
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

        // optionally randomize rotation after shuffling
        if (rotationEnabled && rotationQuarterTurns > 1 && randomizeRotationOnShuffle)
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                if (i == blankTileIndex) continue;
                int turns = rng.Next(rotationQuarterTurns);   // 0..quarterTurns-1
                tiles[i].rotationSteps = 0;
                for (int r = 0; r < turns; r++) tiles[i].RotateOnce();
            }
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

        // 0) NEW: snap rotations before sliding back via undoStack
        if (fixRotationsOnAutoSolve)
        {
            int fixedCount = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (i == blankTileIndex) continue;

                var t = tiles[i];
                if (t && t.MaxRotationSteps > 1 && t.rotationSteps != autoSolveTargetRotationStep)
                {
                    t.SetRotationSteps(autoSolveTargetRotationStep); // instant, exact snap
                    fixedCount++;
                }
            }

            if (fixedCount > 0)
            {
                if (autosolveRotationPause > 0f)
                    yield return new WaitForSeconds(autosolveRotationPause);

                Debug.Log($"[AutoSolve] Fixed rotation on {fixedCount} tiles → proceeding to slide rewind.");
            }
        }

        // 1) Rewind the recorded path from Shuffle (position solve)
        while (undoStack.Count > 0)
        {
            int tileIdx = undoStack.Pop();
            RuneTile tile = tiles[tileIdx];

            Vector3 target = slotWorldPos[blankSlot];
            bool done = false;
            tile.SlideTo(target, () =>
            {
                CommitMove(tileIdx, oldSlot: tileToSlot[tileIdx], pushHistory: false);
                PlaySlideSfx();
                done = true;
            });

            while (!done) yield return null;
            if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay);
        }

        busy = false;
        OnSolved();
    } 

    // ===============================
    //  HINT v2: bigger, clearer, rotation-aware
    // ===============================
    public void ShowHint()
    {
        if (busy || tiles == null || tiles.Length == 0) return;

        // 1) Prioritize tiles already in correct slot but wrong rotation
        if (rotationEnabled && rotationQuarterTurns > 1)
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                if (i == blankTileIndex) continue;
                if (tileToSlot[i] == i)   // correct slot
                {
                    var t = tiles[i];
                    if (t && t.MaxRotationSteps > 1 && t.rotationSteps != 0) // needs rotation
                    {
                        StartCoroutine(CoHintRotate(t));
                        return;
                    }
                }
            }
        }

        // 2) Otherwise: nudge the best neighbor of the blank to slide
        var neigh = GetNeighborSlots(blankSlot);
        if (neigh.Count == 0) return;

        int bestTile = -1;
        int bestScore = int.MaxValue;

        foreach (int nSlot in neigh)
        {
            int tIdx = slotToTile[nSlot];
            if (tIdx == blankTileIndex) continue;

            var t = tiles[tIdx];
            if (!t) continue;

            // Current distance to its goal
            int cur = Manhattan(t.currentPos, t.correctPos);

            // If moved into the blank, new pos = blank slot
            int br = blankSlot / cols, bc = blankSlot % cols;
            int newDist = Manhattan(new Vector2Int(bc, br), t.correctPos);

            // Score prefers a strict improvement; ties choose the smaller newDist
            int score = newDist * 10 + Mathf.Max(0, newDist - cur);
            if (score < bestScore)
            {
                bestScore = score;
                bestTile = tIdx;
            }
        }

        if (bestTile >= 0)
        {
            // direction from tile to blank in world space (small nudge)
            Vector3 dir = (slotWorldPos[blankSlot] - tiles[bestTile].transform.position).normalized;
            StartCoroutine(CoHintSlide(tiles[bestTile], dir));
        }
    }

    // Manhattan distance helper
    int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    // --- VISUALS: create a temporary overlay so we don't touch real tile/collider ---
    SpriteRenderer MakeOverlayFor(RuneTile tile, out Transform fxRoot)
    {
        fxRoot = null;
        if (!tile) return null;

        var src = tile.GetComponent<SpriteRenderer>();
        if (!src || !src.sprite) return null;

        var go = new GameObject("HintFX");
        go.transform.SetParent(tile.transform, worldPositionStays: false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = Vector3.one;
        fxRoot = go.transform;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite         = src.sprite;
        sr.sortingLayerID = src.sortingLayerID;
        sr.sortingOrder   = src.sortingOrder + glowOrderBoost;
        sr.material       = additiveSpriteMaterial ? additiveSpriteMaterial : src.sharedMaterial;

        var c = glowColor; c.a = 0f;
        sr.color = c;
        return sr;
    }

    // Slide-style hint: enlarge + nudge toward the blank
    System.Collections.IEnumerator CoHintSlide(RuneTile tile, Vector3 worldDir)
    {
        var sr = MakeOverlayFor(tile, out var fx);
        if (!sr) yield break;

        // tiny nudge distance scaled to your scene (~0.008 world units by default)
        float nudgeDist = hintNudge * 0.001f;

        float dur = Mathf.Max(0.2f, hintDuration);
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);

            // ease in/out
            float a = Mathf.Sin(u * Mathf.PI);                    // alpha 0→1→0
            float s = 1f + hintEnlarge * Mathf.Sin(u * Mathf.PI); // grow & return
            Vector3 off = worldDir * (nudgeDist * Mathf.Sin(u * Mathf.PI));

            // apply
            var c = sr.color; c.a = a;
            sr.color = c;
            fx.localScale    = Vector3.one * s;
            fx.localPosition = off;

            yield return null;
        }
        if (fx) Destroy(fx.gameObject);
    }

    // Rotation-style hint: enlarge + small rotate wiggle
    System.Collections.IEnumerator CoHintRotate(RuneTile tile)
    {
        var sr = MakeOverlayFor(tile, out var fx);
        if (!sr) yield break;

        float dur = Mathf.Max(0.2f, hintDuration);
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);

            float a = Mathf.Sin(u * Mathf.PI);                        // alpha 0→1→0
            float s = 1f + hintEnlarge * Mathf.Sin(u * Mathf.PI);     // scale pop
            float rot = Mathf.Sin(u * Mathf.PI * 2f) * hintRotateDeg; // wiggle ±deg

            var c = sr.color; c.a = a;
            sr.color = c;
            fx.localScale    = Vector3.one * s;
            fx.localRotation = Quaternion.Euler(0, 0, rot);

            yield return null;
        }
        if (fx) Destroy(fx.gameObject);
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

    void MoveTileIntoBlank_Instant(int tileIdx, bool pushHistory)
    {
        int fromSlot = tileToSlot[tileIdx];
        tiles[tileIdx].SetWorldPos(slotWorldPos[blankSlot], instant: true);
        CommitMove(tileIdx, oldSlot: fromSlot, pushHistory: pushHistory);
    }

    bool IsSolved()
    {
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
    
    // Expose the manager's AudioSource (optional convenience)
    public AudioSource SfxSource => _audio;

    // Global SFX mute/unmute for this puzzle instance (manager + any child sources)
    public void ApplySfxMute(bool mute)
    {
        if (_audio) _audio.mute = mute;

        // Also catch any tile/local sources (RuneTile may add one if parent missing)
        var all = GetComponentsInChildren<AudioSource>(true);
        foreach (var a in all)
        {
            if (a) a.mute = mute;
        }
    }

    IEnumerator CoSolveGlow()
    {
        var root = new GameObject("SolveGlow");
        root.transform.SetParent(transform, worldPositionStays: true);

        var overlays = new List<(SpriteRenderer sr, Vector3 baseScale)>(tiles.Length);

        for (int slot = 0; slot < tiles.Length; slot++)
        {
            if (slot == blankTileIndex) continue;

            var tile  = tiles[slot];
            var srcSR = tile ? tile.GetComponent<SpriteRenderer>() : null;
            if (!srcSR || !srcSR.sprite) continue;

            var go = new GameObject($"Glow_{slot}");
            go.transform.SetParent(root.transform, worldPositionStays: true);
            go.transform.position = slotWorldPos[slot];
            go.transform.rotation = tile.transform.rotation;

            var glowSR = go.AddComponent<SpriteRenderer>();
            glowSR.sprite         = srcSR.sprite;
            glowSR.sortingLayerID = srcSR.sortingLayerID;
            glowSR.sortingOrder   = srcSR.sortingOrder + glowOrderBoost;
            glowSR.material       = additiveSpriteMaterial ? additiveSpriteMaterial : srcSR.sharedMaterial;

            var c = glowColor; c.a = 0f;
            glowSR.color = c;

            Vector3 baseScale = tile.transform.localScale;
            go.transform.localScale = baseScale;

            overlays.Add((glowSR, baseScale));
        }

        float dur   = Mathf.Max(0.1f, glowDuration);
        float punch = Mathf.Clamp(glowScalePunch, 0f, 0.3f); // clamp to 0.3x for safety
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);

            float a = Mathf.Sin(u * Mathf.PI);   // alpha wave
            float k = a * a;                     // smoother scale wave

            // limit total swell so glow edges don't overlap too much
            float s = Mathf.Min(1f + punch * k, 1.1f); // hard cap at 10% growth

            foreach (var (sr, baseScale) in overlays)
            {
                if (!sr) continue;

                var col = sr.color;
                col.a = glowColor.a * a;
                sr.color = col;

                sr.transform.localScale = baseScale * s;
            }

            yield return null;
        }

        if (root) Destroy(root);
    }
}
 