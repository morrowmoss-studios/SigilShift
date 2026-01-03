using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using Random = UnityEngine.Random;

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

    [Header("Start Behavior")]
    [SerializeField] private bool waitForTapToStart = true;
    private bool _waitingForFirstTap = false;

    // ---------- Rotation options ----------
    [Header("Rotation")]
    [Tooltip("Allow tiles to rotate when clicked if not adjacent to the blank.")]
    public bool rotationEnabled = false;  // default to off

    [Tooltip("How many 90° steps per full turn. 1 = disabled, 2 = 180° only, 4 = 90° steps.")]
    [Range(1, 8)] public int rotationQuarterTurns = 4;

    [Tooltip("Randomize tile rotations during Shuffle.")]
    public bool randomizeRotationOnShuffle = true;

    // Must match UIManager's keys
    private const string PP_ROTATION = "SS_RotationEnabled";

    // ---------- Autosolve rotation correction ----------
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

    // --- Remember last player move so hints don't suggest exact undo ---
    int _lastPlayerTile = -1;
    int _lastPlayerBlankSlot = -1;

    // ----------  SFX  ----------
    [Header("SFX (Manager)")]
    [SerializeField] private AudioClip slideClip;
    [SerializeField, Range(0f, 1f)] private float slideVolume = 0.7f;
    [SerializeField] private Vector2 slidePitchJitter = new Vector2(0.96f, 1.04f);

    [Space(6)]
    [SerializeField] private AudioClip solvedClip;
    [SerializeField, Range(0f, 1f)] private float solvedVolume = 0.9f;
    [SerializeField] private Vector2 solvedPitchJitter = new Vector2(0.98f, 1.02f);

    [Header("Hint FX")]
    [SerializeField, Range(0.05f, 0.35f)] private float hintEnlarge = 0.18f; // +18% pop
    [SerializeField, Range(0.2f, 1.2f)] private float hintDuration = 0.65f;
    [SerializeField, Range(2f, 25f)] private float hintNudge = 8f;       // world-units * 0.001
    [SerializeField, Range(2f, 25f)] private float hintRotateDeg = 12f;  // ±deg wiggle

    // ---- Input lock (used by reference popup, win popup, etc.) ----
    [HideInInspector] public bool inputLocked = false;

    public void SetInputLocked(bool locked)
    {
        inputLocked = locked;
    }

    // --------- Solve Glow ----------
    [Header("Solve Glow")]
    [SerializeField] private Material additiveSpriteMaterial; // set to built-in "Particles/Additive"
    [SerializeField] private Color glowColor = new Color(1.0f, 0.95f, 0.65f, 1f);
    [SerializeField, Range(0.1f, 5f)] private float glowDuration = 1.4f;
    [SerializeField, Range(0f, 0.2f)] private float glowScalePunch = 0.06f; // subtle swell
    [SerializeField] private int glowOrderBoost = 10; // render above tiles
    [SerializeField, Range(0.8f, 1.5f)] float glowBaseScale = 1.02f;
    [SerializeField, Range(0f, 1f)] float glowMaxAlpha = 1f;
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

    // ===== Debug =====
    [Header("Debug")]
    [SerializeField] private bool debugWinCheck = false;

    private AudioSource _audio;

    void Awake()
    {
        if (!loader)
            loader = GetComponentInParent<RuneBoardLoader>() ?? FindObjectOfType<RuneBoardLoader>();

        if (!loader)
        {
            Debug.LogError("[RunePuzzleManager] No RuneBoardLoader found/assigned.");
            enabled = false;
            return;
        }

        // 1) Start from config defaults
        if (loader.config)
        {
            rows = loader.config.rows;
            cols = loader.config.cols;

            rotationEnabled = loader.config.enableRotation;
            rotationQuarterTurns = Mathf.Max(1, loader.config.quarterTurns);
        }

        // 2) Override from PlayerPrefs
        int rotPref = PlayerPrefs.GetInt(PP_ROTATION, 0);  // 0 = OFF by default
        bool rotOn = rotPref == 1;

        rotationEnabled = rotOn;
        rotationQuarterTurns = rotOn ? 4 : 1;

        if (loader.config)
        {
            loader.config.enableRotation = rotOn;
            loader.config.quarterTurns = rotationQuarterTurns;
        }

        Debug.Log($"[RunePuzzleManager] Awake AFTER prefs: rotationEnabled={rotationEnabled}, quarterTurns={rotationQuarterTurns}");

        tiles = loader.tiles;
        int need = rows * cols;

        // Need *at least* N tiles
        if (tiles == null || tiles.Length < need)
        {
            Debug.LogError($"[RunePuzzleManager] loader.tiles must have AT LEAST {need} entries (TL→BR). Found {(tiles == null ? 0 : tiles.Length)}.");
            enabled = false;
            return;
        }

        // If there are extra tiles, trim to first 'need'
        if (tiles.Length > need)
        {
            Debug.LogWarning($"[RunePuzzleManager] loader.tiles has {tiles.Length} entries; using only first {need} for a {rows}x{cols} board.");
            var trimmed = new RuneTile[need];
            Array.Copy(tiles, trimmed, need);
            tiles = trimmed;
        }

        for (int i = 0; i < tiles.Length; i++)
        {
            if (!tiles[i]) { Debug.LogError($"[RunePuzzleManager] loader.tiles[{i}] is null."); enabled = false; return; }
            tiles[i].manager = this;
        }

        for (int i = 0; i < tiles.Length; i++)
            tiles[i].InitRotationSystem(rotationEnabled ? rotationQuarterTurns : 1);

        // blank tile logic
        if (loader.config && loader.config.blankIndex >= 0)
            blankTileIndex = loader.config.blankIndex;
        else
            blankTileIndex = (rows * cols) - 1;

        _audio = GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.loop = false;
        _audio.spatialBlend = 0f;

        Debug.Log($"[RunePuzzleManager] Awake settings: row={rows} cols={cols} rotationEnabled={rotationEnabled} quarterTurns={rotationQuarterTurns}");
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
        _lastHintTile = -1;
        _lastPlayerTile = -1;
        _lastPlayerBlankSlot = -1;

        ValidateMappings("After Start init");

        if (shuffleOnStart)
        {
            if (waitForTapToStart)
            {
                _waitingForFirstTap = true;
                busy = true;  // block tile moves until we shuffle
                Debug.Log("[RunePuzzleManager] Waiting for first tap to shuffle puzzle.");
            }
            else
            {
                ShuffleRandomWalk(shuffleSteps);
            }
        }
    }

    void Update()
    {
        // First-tap-to-start logic
        if (_waitingForFirstTap)
        {
#if UNITY_ANDROID || UNITY_IOS
            bool tapped = Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
#else
            bool tapped = Input.GetMouseButtonDown(0);
#endif

            if (tapped)
            {
                _waitingForFirstTap = false;
                busy = false;
                ShuffleRandomWalk(shuffleSteps);
                Debug.Log("[RunePuzzleManager] First tap detected — puzzle shuffled.");
            }

            return;
        }

        // debug keys
        if (Input.GetKeyDown(KeyCode.R)) ResetToSolved();
        if (Input.GetKeyDown(KeyCode.S)) ShuffleRandomWalk(shuffleSteps);
        if (Input.GetKeyDown(KeyCode.A)) AutoSolve();
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
    // Short tap: only tries to slide, NEVER rotates.
    // Long press rotation is handled inside RuneTile.
    // ===============================
    public void TrySlideTile(RuneTile tile)
    {
        // basic guards
        if (busy || inputLocked || tile == null)
            return;

        // 🔒 Make sure the board is actually initialized
        if (tiles == null || slotWorldPos == null || slotToTile == null || tileToSlot == null)
        {
            Debug.LogWarning("[RunePuzzleManager] TrySlideTile called before board initialized. Ignoring click.");
            return;
        }

        int tileIdx = GetTileIndex(tile);
        if (tileIdx < 0)
        {
            Debug.LogWarning("[RunePuzzleManager] TrySlideTile: tile not found in tiles[]: " + tile.name);
            return;
        }

        if (tileIdx >= tileToSlot.Length)
        {
            Debug.LogError($"[RunePuzzleManager] TrySlideTile: tileIdx {tileIdx} " +
                           $"out of range for tileToSlot (len={tileToSlot.Length}).");
            return;
        }

        int tileSlot = tileToSlot[tileIdx];

        if (tileSlot < 0 || tileSlot >= slotWorldPos.Length)
        {
            Debug.LogError($"[RunePuzzleManager] TrySlideTile: tileSlot {tileSlot} invalid " +
                           $"for slotWorldPos (len={slotWorldPos.Length}).");
            return;
        }

        // If not adjacent to the blank: do nothing on a tap.
        if (!IsAdjacent(tileSlot, blankSlot))
            return;

        busy = true;

        // remember where the blank was before the move
        int prevBlankSlot = blankSlot;

        if (blankSlot < 0 || blankSlot >= slotWorldPos.Length)
        {
            Debug.LogError($"[RunePuzzleManager] TrySlideTile: BLANK slot {blankSlot} invalid " +
                           $"for slotWorldPos (len={slotWorldPos.Length}).");
            busy = false;
            return;
        }

        Vector3 target = slotWorldPos[blankSlot];

        tile.SlideTo(target, onComplete: () =>
        {
            CommitMove(tileIdx, oldSlot: tileSlot, pushHistory: true);

            _lastPlayerTile = tileIdx;
            _lastPlayerBlankSlot = prevBlankSlot;

            PlaySlideSfx();
            busy = false;

            if (IsSolved())
                OnSolved();
        });
    }

    
    public void NotifyTileRotated(RuneTile tile)
    {
        // If something else is running (autosolve, shuffle, popup), ignore rotation events
        if (busy || inputLocked) return;

        bool solved = IsSolved();

        if (debugWinCheck)
        {
            Debug.Log($"[WIN DEBUG] NotifyTileRotated({(tile ? tile.name : "null")}) -> IsSolved() = {solved}");
        }

        if (solved)
        {
            OnSolved();
        }
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

        ValidateMappings("After ResetToSolved");
    }

    // ===============================
    //  UI Hook – Reset Button
    // ===============================
    public void OnResetButtonPressed()
    {
        if (busy) return;

        StopAllCoroutines();
        ResetToSolved();
        ShuffleRandomWalk(shuffleSteps);
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

        ValidateMappings("After ShuffleRandomWalk");
    }

    public void AutoSolve(float stepDelay = 0.02f)
    {
        if (busy) return;
        StartCoroutine(CoAutoSolve(stepDelay));
    }

    void DebugCheckSolvedState(string context)
    {
        if (!debugWinCheck) return;
        bool solved = IsSolved();
        Debug.Log($"[WIN DEBUG] {context} -> IsSolved() = {solved}");
    }

    IEnumerator CoAutoSolve(float stepDelay)
    {
        busy = true;

        // Snap rotations if configured
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

        DebugCheckSolvedState("Before AutoSolve");

        while (undoStack.Count > 0)
        {
            int tileIdx = undoStack.Pop();
            RuneTile tile = tiles[tileIdx];

            Vector3 target = slotWorldPos[blankSlot];
            bool done = false;

            tile.SlideTo(target, () =>
            {
                // Use same logic as player move
                CommitMove(tileIdx, oldSlot: tileToSlot[tileIdx], pushHistory: false);
                PlaySlideSfx();

                DebugCheckSolvedState($"After AutoSolve move (tileIdx={tileIdx})");

                done = true;
            });

            while (!done) yield return null;
            if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay);
        }

        busy = false;

        if (debugWinCheck)
        {
            bool finalSolved = IsSolved();
            Debug.Log($"[WIN DEBUG] After AutoSolve COMPLETE -> IsSolved() = {finalSolved}");

            if (!finalSolved)
            {
                Debug.LogWarning(
                    "[WIN DEBUG] AutoSolve finished but IsSolved() is FALSE. " +
                    "Internal state is inconsistent – NOT calling OnSolved()."
                );
                yield break;
            }
        }

        OnSolved();
    }

    // ===============================
    //  HINT v2: bigger, clearer, rotation-aware, anti-undo
    // ===============================

    bool NeedsRotationHint(RuneTile t)
    {
        if (!rotationEnabled || rotationQuarterTurns <= 1) return false;
        if (!t) return false;
        if (t.MaxRotationSteps <= 1) return false;

        // we treat "upright" as rotationSteps == 0
        return t.rotationSteps != 0;
    }

    public void ShowHint()
    {
        if (busy || inputLocked || tiles == null || tiles.Length == 0) return;

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
            var tile = tiles[bestTile];
            Vector3 dir = (slotWorldPos[blankSlot] - tile.transform.position).normalized;

            bool needsRot = NeedsRotationHint(tile);
            StartCoroutine(CoHintSlide(tile, dir, needsRot));
            return;
        }

        // --- Pass B: plateau breaker (no strict improvement available) ---
        bestTile = -1;
        bestScore = int.MaxValue;

        foreach (int nSlot in neigh)
        {
            int tIdx = slotToTile[nSlot];
            if (tIdx == blankTileIndex) continue;

            if (tIdx == _lastPlayerTile && nSlot == _lastPlayerBlankSlot)
                continue;

            int br = blankSlot / cols, bc = blankSlot % cols;
            int newDist = Manhattan(new Vector2Int(bc, br), tiles[tIdx].correctPos);

            int score = newDist * 10;
            if (tIdx == _lastHintTile) score += 10;

            if (score < bestScore) { bestScore = score; bestTile = tIdx; }
        }

        if (bestTile >= 0)
        {
            _lastHintTile = bestTile;
            var tile = tiles[bestTile];
            Vector3 dir = (slotWorldPos[blankSlot] - tile.transform.position).normalized;

            bool needsRot = NeedsRotationHint(tile);
            StartCoroutine(CoHintSlide(tile, dir, needsRot));
            return;
        }

        // --- Last-resort fallback: rotation correction if nothing else
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

    int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

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
        go.transform.localScale = Vector3.one;
        fxRoot = go.transform;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = src.sprite;
        sr.sortingLayerID = src.sortingLayerID;
        sr.sortingOrder = src.sortingOrder + glowOrderBoost;
        sr.material = additiveSpriteMaterial ? additiveSpriteMaterial : src.sharedMaterial;

        var c = glowColor; c.a = 0f;
        sr.color = c;
        return sr;
    }

    IEnumerator CoHintSlide(RuneTile tile, Vector3 worldDir, bool alsoRotateWiggle = false)
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

            float rot = 0f;
            if (alsoRotateWiggle)
            {
                rot = Mathf.Sin(u * Mathf.PI * 2f) * hintRotateDeg;
            }

            var c = sr.color; c.a = a;
            sr.color = c;
            fx.localScale = Vector3.one * s;
            fx.localPosition = off;
            fx.localRotation = Quaternion.Euler(0f, 0f, rot);

            yield return null;
        }

        if (fx) Destroy(fx.gameObject);
    }

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

            float a = Mathf.Sin(u * Mathf.PI);
            float s = 1f + hintEnlarge * Mathf.Sin(u * Mathf.PI);
            float rot = Mathf.Sin(u * Mathf.PI * 2f) * hintRotateDeg;

            var c = sr.color; c.a = a;
            sr.color = c;
            fx.localScale = Vector3.one * s;
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

        ValidateMappings("After CommitMove");
    }

    void MoveTileIntoBlank_Instant(int tileIdx, bool pushHistory)
    {
        int fromSlot = tileToSlot[tileIdx];
        tiles[tileIdx].SetWorldPos(slotWorldPos[blankSlot], instant: true);
        CommitMove(tileIdx, oldSlot: fromSlot, pushHistory: pushHistory);
    }

    // ===== Internal state sanity check (no visuals) =====
    void ValidateMappings(string context)
    {
        if (slotToTile == null || tileToSlot == null || tiles == null) return;

        int n = tiles.Length;
        bool ok = true;

        for (int slot = 0; slot < n; slot++)
        {
            int tIdx = slotToTile[slot];
            if (tIdx < 0 || tIdx >= n)
            {
                Debug.LogError($"[STATE BUG] {context}: slotToTile[{slot}] = {tIdx} (out of range)");
                ok = false;
            }
        }

        for (int tIdx = 0; tIdx < n; tIdx++)
        {
            int slot = tileToSlot[tIdx];
            if (slot < 0 || slot >= n)
            {
                Debug.LogError($"[STATE BUG] {context}: tileToSlot[{tIdx}] = {slot} (out of range)");
                ok = false;
            }
        }

        for (int slot = 0; slot < n; slot++)
        {
            int tIdx = slotToTile[slot];
            if (tIdx < 0 || tIdx >= n) continue;

            int backSlot = tileToSlot[tIdx];
            if (backSlot != slot)
            {
                Debug.LogError(
                    $"[STATE BUG] {context}: mismatch pair:" +
                    $"\n   slotToTile[{slot}] = {tIdx}" +
                    $"\n   tileToSlot[{tIdx}] = {backSlot} (expected {slot})"
                );
                ok = false;
            }
        }

        int expectedBlankSlot = tileToSlot[blankTileIndex];
        if (blankSlot != expectedBlankSlot)
        {
            Debug.LogError(
                $"[STATE BUG] {context}: blank mismatch:" +
                $"\n   blankSlot          = {blankSlot}" +
                $"\n   tileToSlot[blank]  = {expectedBlankSlot}" +
                $"\n   blankTileIndex     = {blankTileIndex}"
            );
            ok = false;
        }

        if (!ok)
        {
            Debug.LogError($"[STATE BUG] {context}: INTERNAL STATE CORRUPT.");
        }
    }

    // ===== Solved check (strict, internal state only, with logs) =====
    bool IsSolved()
    {
        bool solved = true;

        for (int slot = 0; slot < tiles.Length; slot++)
        {
            if (slot == blankSlot)
                continue;

            int tileIndex = slotToTile[slot];

            // 1) Correct tile in correct slot?
            if (tileIndex != slot)
            {
                if (debugWinCheck)
                {
                    Debug.LogWarning(
                        $"[WIN DEBUG] Tile mismatch at slot {slot}:" +
                        $"\n   slotToTile[{slot}] = {tileIndex} (expected {slot})" +
                        $"\n   blankSlot          = {blankSlot}" +
                        $"\n   blankTileIndex     = {blankTileIndex}" +
                        $"\n   tileToSlot[tile]   = {SafeSlot(tileIndex)}" +
                        $"\n   tile.name          = {SafeTileName(tileIndex)}" +
                        $"\n   tile.currentPos    = {SafeTileCurrentPos(tileIndex)}" +
                        $"\n   tile.correctPos    = {SafeTileCorrectPos(tileIndex)}"
                    );
                }
                solved = false;
                break;
            }

            // 2) Rotation check
            if (rotationEnabled)
            {
                RuneTile tile = tiles[tileIndex];
                if (tile != null && tile.MaxRotationSteps > 1)
                {
                    if (tile.rotationSteps != 0)
                    {
                        if (debugWinCheck)
                        {
                            Debug.LogWarning(
                                $"[WIN DEBUG] Rotation mismatch at slot {slot}:" +
                                $"\n   tileIndex      = {tileIndex}" +
                                $"\n   tile.name      = {tile.name}" +
                                $"\n   rotationSteps  = {tile.rotationSteps} (expected 0)" +
                                $"\n   MaxRotation    = {tile.MaxRotationSteps}"
                            );
                        }
                        solved = false;
                        break;
                    }
                }
            }
        }

        // 3) Blank slot must also be in its home position
        if (solved && blankSlot != blankTileIndex)
        {
            if (debugWinCheck)
            {
                Debug.LogWarning(
                    "[WIN DEBUG] All tiles OK but blank is wrong:" +
                    $"\n   blankSlot      = {blankSlot}" +
                    $"\n   blankTileIndex = {blankTileIndex}"
                );
            }
            solved = false;
        }

        if (debugWinCheck)
        {
            Debug.Log($"[WIN DEBUG] IsSolved() => {solved}");
        }

        return solved;
    }

    string SafeTileName(int tileIndex)
    {
        if (tiles == null || tileIndex < 0 || tileIndex >= tiles.Length)
            return "NULL/OUT-OF-RANGE";
        return tiles[tileIndex] ? tiles[tileIndex].name : "NULL TILE";
    }

    string SafeSlot(int tileIndex)
    {
        if (tileToSlot == null || tileIndex < 0 || tileIndex >= tileToSlot.Length)
            return "OUT-OF-RANGE";
        return tileToSlot[tileIndex].ToString();
    }

    Vector2Int SafeTileCurrentPos(int tileIndex)
    {
        if (tiles == null || tileIndex < 0 || tileIndex >= tiles.Length || !tiles[tileIndex])
            return new Vector2Int(-999, -999);
        return tiles[tileIndex].currentPos;
    }

    Vector2Int SafeTileCorrectPos(int tileIndex)
    {
        if (tiles == null || tileIndex < 0 || tileIndex >= tiles.Length || !tiles[tileIndex])
            return new Vector2Int(-999, -999);
        return tiles[tileIndex].correctPos;
    }

    void OnSolved()
    {
        if (_popupShowing) return;
        _popupShowing = true;

        int currentLevel = GetCurrentLevelNumber();
        LevelProgress.UnlockUpTo(currentLevel + 1);

        Debug.Log("<color=#9cffb0>[SigilShift] Puzzle solved!</color>");
        PlaySolvedSfx();
        StartCoroutine(CoSolvedSequence());
    }

    IEnumerator CoSolvedSequence()
    {
        yield return StartCoroutine(CoSolveGlow());

        if (popupDelayAfterSolve > 0f)
            yield return new WaitForSecondsRealtime(popupDelayAfterSolve);

        Debug.Log("[SigilShift] About to load win popup: " + winPopupSceneName);
        SceneManager.LoadScene(winPopupSceneName, LoadSceneMode.Additive);
        yield return null;
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

    public AudioSource SfxSource => _audio;

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

            var tile = tiles[slot];
            var srcSR = tile ? tile.GetComponent<SpriteRenderer>() : null;
            if (!srcSR || !srcSR.sprite) continue;

            var go = new GameObject($"Glow_{slot}");
            go.transform.SetParent(root.transform, worldPositionStays: true);
            go.transform.position = slotWorldPos[slot];
            go.transform.rotation = tile.transform.rotation;

            var glowSR = go.AddComponent<SpriteRenderer>();
            glowSR.sprite = srcSR.sprite;
            glowSR.sortingLayerID = srcSR.sortingLayerID;
            glowSR.sortingOrder = srcSR.sortingOrder + glowOrderBoost;
            glowSR.material = additiveSpriteMaterial ? additiveSpriteMaterial : srcSR.sharedMaterial;

            var c = glowColor; c.a = 0f;
            glowSR.color = c;

            Vector3 baseScale = tile.transform.localScale;
            go.transform.localScale = baseScale;

            overlays.Add((glowSR, baseScale));
        }

        float dur = Mathf.Max(0.1f, glowDuration);
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

        return 1;
    }
}
