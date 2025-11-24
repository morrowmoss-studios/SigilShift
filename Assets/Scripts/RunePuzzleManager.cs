using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text;

[DisallowMultipleComponent]
public class RunePuzzleManager : MonoBehaviour, ISlidingPuzzle
{
    [Header("Board")]
    public int rows = 3;
    public int cols = 3;

    [Header("References")]
    public RuneBoardLoader loader;

    [Tooltip("Blank tile in the SOLVED layout (usually 8 = bottom-right).")]
    public int blankTileIndex = 8;

    [Header("Shuffle")]
    public bool shuffleOnStart = true;
    [Tooltip("How many random valid moves to do when shuffling.")]
    public int shuffleSteps = 90;

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
    private RuneTile[] tiles;
    private Vector3[] slotWorldPos;

    private int blankSlot;
    private bool busy;

    private int[] slotToTile;
    private int[] tileToSlot;

    private Stack<int> undoStack = new Stack<int>();

    // --- Hint memory to avoid ping-pong ---
    int _lastHintTile = -1;

    // --- New: remember the last player move so hints don't suggest the exact undo ---
    int _lastPlayerTile = -1;
    int _lastPlayerBlankSlot = -1; // the blank slot the tile moved into on the last player move


    // ----------  SFX  ----------
    [Header("SFX (Manager)")]
    [SerializeField] private AudioClip slideClip;
    [SerializeField, Range(0f,1f)] private float slideVolume = 0.7f;
    [SerializeField] private Vector2 slidePitchJitter = new Vector2(0.96f, 1.04f);

    [Space(6)]
    [SerializeField] private AudioClip solvedClip;
    [SerializeField, Range(0f,1f)] private float solvedVolume = 0.9f;
    [SerializeField] private Vector2 solvedPitchJitter = new Vector2(0.98f, 1.02f);

    [Header("Hint FX")]
    [SerializeField, Range(0.05f, 0.35f)] private float hintEnlarge = 0.18f; // +18% pop
    [SerializeField, Range(0.2f, 1.2f)]  private float hintDuration = 0.65f;
    [SerializeField, Range(2f, 25f)]     private float hintNudge = 8f;       // world-units * 0.001
    [SerializeField, Range(2f, 25f)]     private float hintRotateDeg = 12f;  // ±deg wiggle

    // --------- Solve Glow ----------
    [Header("Solve Glow")]
    [SerializeField] private Material additiveSpriteMaterial; // set to built-in "Particles/Additive"
    [SerializeField] private Color glowColor = new Color(1.0f, 0.95f, 0.65f, 1f);
    [SerializeField, Range(0.1f, 5f)] private float glowDuration = 1.4f;
    [SerializeField, Range(0f, 0.2f)] private float glowScalePunch = 0.06f; // subtle swell
    [SerializeField] private int glowOrderBoost = 10; // render above tiles
    [SerializeField, Range(0.8f, 1.5f)] float glowBaseScale = 1.02f;
    [SerializeField, Range(0f, 1f)]     float glowMaxAlpha  = 1f;
    [SerializeField] AnimationCurve glowScaleCurve = null;
    [SerializeField] AnimationCurve glowAlphaCurve = null;
    
    // ===== Win Popup / Progress =====
    [Header("Win Popup")]
    [SerializeField] private string winPopupSceneName = "PopUp_Win";
    [SerializeField] private bool loadPopupAdditive = true;
    [SerializeField] private float popupDelayAfterSolve = 0.1f;
    private bool _popupShowing = false;

    [Header("Level Flow")]
    [SerializeField] private string levelSelectSceneName = "LevelSelect";
    [SerializeField] private string levelScenePrefix = "Level_";
    [SerializeField] private int maxLevelNumber = 30;
// ====================================

    private AudioSource _audio;
 
    private void DumpPopupDiagnostics()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== POPUP DIAGNOSTICS ===");

        // list all loaded scenes
        sb.AppendLine($"Loaded scenes: {SceneManager.sceneCount}");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var sc = SceneManager.GetSceneAt(i);
            sb.AppendLine($" - {sc.name} (loaded={sc.isLoaded})");
        }

        // find popup scene root objects
        var popupScene = SceneManager.GetSceneByName("PopUp_Win");
        sb.AppendLine($"PopUp_Win loaded? {popupScene.isLoaded}");

        if (popupScene.isLoaded)
        {
            var roots = popupScene.GetRootGameObjects();
            sb.AppendLine($"PopUp_Win roots: {roots.Length}");
            foreach (var r in roots)
            {
                sb.AppendLine($"  Root: {r.name} active={r.activeInHierarchy} scale={r.transform.localScale}");
            }
        }

        // list all canvases in the game
        var canvases = FindObjectsOfType<Canvas>(true);
        sb.AppendLine($"All canvases in game: {canvases.Length}");
        foreach (var c in canvases)
        {
            sb.AppendLine(
                $"Canvas '{c.name}' | enabled={c.enabled} active={c.gameObject.activeInHierarchy} " +
                $"renderMode={c.renderMode} sortOrder={c.sortingOrder} " +
                $"worldCam={(c.worldCamera ? c.worldCamera.name : "null")} " +
                $"scale={c.transform.localScale}"
            );

            // check for canvas groups on canvas root
            var cg = c.GetComponent<CanvasGroup>();
            if (cg)
                sb.AppendLine($"   CanvasGroup alpha={cg.alpha} interact={cg.interactable} blocksRaycasts={cg.blocksRaycasts}");
        }

        Debug.Log(sb.ToString());
    }


    void Awake()
    {
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

        for (int i = 0; i < tiles.Length; i++)
        {
            if (!tiles[i]) { Debug.LogError($"[RunePuzzleManager] loader.tiles[{i}] is null."); enabled = false; return; }
            tiles[i].manager = this;
        }

        if (loader && loader.config)
        {
            rotationEnabled      = loader.config.enableRotation;
            rotationQuarterTurns = Mathf.Max(1, loader.config.quarterTurns);
        }

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
                tile.RotateOnce();
            return;
        }

        busy = true;

        // NEW: remember where the blank was before this move (so we can detect a direct undo next hint)
        int prevBlankSlot = blankSlot;

        Vector3 target = slotWorldPos[blankSlot];
        tile.SlideTo(target, onComplete: () =>
        {
            // commit mapping (push to history)
            CommitMove(tileIdx, oldSlot: tileSlot, pushHistory: true);

            // NEW: mark the last player move for anti-undo hint logic
            _lastPlayerTile = tileIdx;
            _lastPlayerBlankSlot = prevBlankSlot;

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

        for (int slot = 0; slot < tiles.Length; slot++)
        {
            int tileIdx = slot;
            tiles[tileIdx].SetWorldPos(slotWorldPos[slot], instant: true);
            UpdateTileLogicalIndex(tiles[tileIdx], slot);
            slotToTile[slot] = tileIdx;
            tileToSlot[tileIdx] = slot;

            tiles[tileIdx].InitRotationSystem(rotationEnabled ? rotationQuarterTurns : 1);
        }

        blankSlot = blankTileIndex;
        ApplyBlankVisualState(hide: true);
        undoStack.Clear();
        _lastHintTile = -1;
        _lastPlayerTile = -1;
        _lastPlayerBlankSlot = -1;
    }

    public void ShuffleRandomWalk(int steps)
    {
        if (busy) return;

        ResetToSolved();

        System.Random rng = new System.Random();
        int lastMovedTile = -1;

        for (int k = 0; k < Mathf.Max(1, steps); k++)
        {
            var neigh = GetNeighborSlots(blankSlot);
            if (neigh.Count == 0) break;

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
            MoveTileIntoBlank_Instant(tileIdx, pushHistory: true);
            lastMovedTile = tileIdx;
        }

        if (rotationEnabled && rotationQuarterTurns > 1 && randomizeRotationOnShuffle)
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                if (i == blankTileIndex) continue;
                int turns = rng.Next(rotationQuarterTurns);
                tiles[i].rotationSteps = 0;
                for (int r = 0; r < turns; r++) tiles[i].RotateOnce();
            }
        }

        ApplyBlankVisualState(hide: true);
        _lastHintTile = -1;
        _lastPlayerTile = -1;
        _lastPlayerBlankSlot = -1;
    }

    public void AutoSolve(float stepDelay = 0.02f)
    {
        if (busy) return;
        StartCoroutine(CoAutoSolve(stepDelay));
    }

    IEnumerator CoAutoSolve(float stepDelay)
    {
        busy = true;

        if (fixRotationsOnAutoSolve)
        {
            int fixedCount = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (i == blankTileIndex) continue;
                var t = tiles[i];
                if (t && t.MaxRotationSteps > 1 && t.rotationSteps != autoSolveTargetRotationStep)
                {
                    t.SetRotationSteps(autoSolveTargetRotationStep);
                    fixedCount++;
                }
            }
            if (fixedCount > 0 && autosolveRotationPause > 0f)
                yield return new WaitForSeconds(autosolveRotationPause);
        }

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
   // ===============================
//  HINT v2: bigger, clearer, rotation-aware, anti-undo
// ===============================
public void ShowHint()
{
    if (busy || tiles == null || tiles.Length == 0) return;

    // 0) If any tile is already in its correct slot but rotated wrong, prefer a rotate hint
    if (rotationEnabled && rotationQuarterTurns > 1)
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            if (i == blankTileIndex) continue;
            if (tileToSlot[i] == i)
            {
                var t = tiles[i];
                if (t && t.MaxRotationSteps > 1 && t.rotationSteps != 0)
                {
                    StartCoroutine(CoHintRotate(t));
                    return;
                }
            }
        }
    }

    // Neighbors of the blank (candidates to slide into it)
    var neigh = GetNeighborSlots(blankSlot);
    if (neigh.Count == 0) return;

    // --- Pass A: strict improvement only ---
    int bestTile = -1;
    int bestScore = int.MaxValue;

    foreach (int nSlot in neigh)
    {
        int tIdx = slotToTile[nSlot];
        if (tIdx == blankTileIndex) continue;

        var t = tiles[tIdx];
        if (!t) continue;

        // HARD BLOCK: never suggest the exact undo of the last player move
        // (same tile back into the slot it just came from)
        if (tIdx == _lastPlayerTile && nSlot == _lastPlayerBlankSlot)
            continue;

        int cur = Manhattan(t.currentPos, t.correctPos);
        int br = blankSlot / cols, bc = blankSlot % cols;
        int newDist = Manhattan(new Vector2Int(bc, br), t.correctPos);

        if (newDist >= cur) continue; // require a strict improvement

        int improvement = cur - newDist;               // >= 1
        int score = (newDist * 10) - (improvement * 100);
        if (tIdx == _lastHintTile) score += 25;        // avoid hinting same tile twice

        if (score < bestScore) { bestScore = score; bestTile = tIdx; }
    }

    if (bestTile >= 0)
    {
        _lastHintTile = bestTile;
        Vector3 dir = (slotWorldPos[blankSlot] - tiles[bestTile].transform.position).normalized;
        StartCoroutine(CoHintSlide(tiles[bestTile], dir));
        return;
    }

    // --- Pass B: plateau breaker (no strict improvement available) ---
    bestTile = -1;
    bestScore = int.MaxValue;

    foreach (int nSlot in neigh)
    {
        int tIdx = slotToTile[nSlot];
        if (tIdx == blankTileIndex) continue;

        // HARD BLOCK: never suggest the exact undo of the last player move
        if (tIdx == _lastPlayerTile && nSlot == _lastPlayerBlankSlot)
            continue;

        int br = blankSlot / cols, bc = blankSlot % cols;
        int newDist = Manhattan(new Vector2Int(bc, br), tiles[tIdx].correctPos);

        int score = newDist * 10;                 // prefer smaller resulting distance
        if (tIdx == _lastHintTile) score += 10;   // mild anti-yo-yo

        if (score < bestScore) { bestScore = score; bestTile = tIdx; }
    }

    if (bestTile >= 0)
    {
        _lastHintTile = bestTile;
        Vector3 dir = (slotWorldPos[blankSlot] - tiles[bestTile].transform.position).normalized;
        StartCoroutine(CoHintSlide(tiles[bestTile], dir));
        return;
    }

    // --- Last-resort fallback: if plateau-breaker found nothing and rotation is enabled,
    // check again for any rotation correction we can hint (edge cases).
    if (rotationEnabled && rotationQuarterTurns > 1)
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            if (i == blankTileIndex) continue;
            if (tileToSlot[i] == i)
            {
                var t = tiles[i];
                if (t && t.MaxRotationSteps > 1 && t.rotationSteps != 0)
                {
                    StartCoroutine(CoHintRotate(t));
                    return;
                }
            }
        }
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
    IEnumerator CoHintSlide(RuneTile tile, Vector3 worldDir)
    {
        var sr = MakeOverlayFor(tile, out var fx);
        if (!sr) yield break;

        float nudgeDist = hintNudge * 0.001f;
        float dur = Mathf.Max(0.2f, hintDuration);
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);

            float a = Mathf.Sin(u * Mathf.PI);
            float s = 1f + hintEnlarge * Mathf.Sin(u * Mathf.PI);
            Vector3 off = worldDir * (nudgeDist * Mathf.Sin(u * Mathf.PI));

            var c = sr.color; c.a = a;
            sr.color = c;
            fx.localScale    = Vector3.one * s;
            fx.localPosition = off;

            yield return null;
        }
        if (fx) Destroy(fx.gameObject);
    }

    // Rotation-style hint: enlarge + small rotate wiggle
    IEnumerator CoHintRotate(RuneTile tile)
    {
        var sr = MakeOverlayFor(tile, out var fx);
        if (!sr) yield break;

        float dur = Mathf.Max(0.2f, hintDuration);
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);

            float a  = Mathf.Sin(u * Mathf.PI);
            float s  = 1f + hintEnlarge * Mathf.Sin(u * Mathf.PI);
            float rot = Mathf.Sin(u * Mathf.PI * 2f) * hintRotateDeg;

            var c = sr.color; c.a = a;
            sr.color = c;
            fx.localScale    = Vector3.one * s;
            fx.localRotation = Quaternion.Euler(0, 0, rot);

            yield return null;
        }
        if (fx) Destroy(fx.gameObject);
    }

    // ===============================
    // Misc helpers
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
        slotToTile[blankSlot] = tileIdx;
        slotToTile[oldSlot] = blankTileIndex;

        tileToSlot[tileIdx] = blankSlot;
        tileToSlot[blankTileIndex] = oldSlot;

        UpdateTileLogicalIndex(tiles[tileIdx], tileToSlot[tileIdx]);
        UpdateTileLogicalIndex(tiles[blankTileIndex], tileToSlot[blankTileIndex]);

        if (pushHistory) undoStack.Push(tileIdx);

        blankSlot = oldSlot;

        _lastHintTile = -1;
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
        if (_popupShowing) return;
        _popupShowing = true;
        
        Debug.Log("<color=#9cffb0>[SigilShift] Puzzle solved!</color>");
        PlaySolvedSfx();
        StartCoroutine(CoSolvedSequence());

    }
    
    IEnumerator CoSolvedSequence()
    {
        // 1) Let glow play fully
        yield return StartCoroutine(CoSolveGlow());

        // 2) Small beat after glow
        if (popupDelayAfterSolve > 0f)
            yield return new WaitForSecondsRealtime(popupDelayAfterSolve);

        Debug.Log("[SigilShift] About to load win popup: " + winPopupSceneName);

        // 3) Show popup
        SceneManager.LoadScene(winPopupSceneName, LoadSceneMode.Additive);

        // wait 1 frame so Unity actually finishes loading
        yield return null;

        DumpPopupDiagnostics();
    }

    
    private void ShowWinPopup()
    {
        if (string.IsNullOrEmpty(winPopupSceneName)) return;

        if (loadPopupAdditive)
            SceneManager.LoadScene(winPopupSceneName, LoadSceneMode.Additive);
        else
            SceneManager.LoadScene(winPopupSceneName);
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
        var all = GetComponentsInChildren<AudioSource>(true);
        foreach (var a in all) if (a) a.mute = mute;
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
        float punch = Mathf.Clamp(glowScalePunch, 0f, 0.3f);
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);

            float a = Mathf.Sin(u * Mathf.PI);
            float k = a * a;

            float s = Mathf.Min(1f + punch * k, 1.1f);

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
    
    public void ReturnToLevelSelect()
    {
        if (loadPopupAdditive && SceneManager.GetSceneByName(winPopupSceneName).isLoaded)
            SceneManager.UnloadSceneAsync(winPopupSceneName);

        SceneManager.LoadScene(levelSelectSceneName);
    }

    public void LoadNextLevel()
    {
        int cur = GetCurrentLevelNumber();
        int next = Mathf.Clamp(cur + 1, 1, maxLevelNumber);

        if (loadPopupAdditive && SceneManager.GetSceneByName(winPopupSceneName).isLoaded)
            SceneManager.UnloadSceneAsync(winPopupSceneName);

        SceneManager.LoadScene(levelScenePrefix + next);
    }

    int GetCurrentLevelNumber()
    {
        string name = SceneManager.GetActiveScene().name;

        if (name.StartsWith(levelScenePrefix))
        {
            string numStr = name.Substring(levelScenePrefix.Length);
            if (int.TryParse(numStr, out int n)) return n;
        }

        return 1; // fallback if name is weird
    }

    
}