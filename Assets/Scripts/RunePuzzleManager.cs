using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; 

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

    // ===== NEW: Win Popup / Progress =====
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

    private AudioSource _audio;

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
        _popupShowing = false; // ===== NEW
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
        _popupShowing = false; // ===== NEW
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
    //  HINT v2...
    // ===============================
    public void ShowHint()
    {
        if (busy || tiles == null || tiles.Length == 0) return;

        // (your hint code unchanged)
        // ...
    }

    // Manhattan distance helper
    int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    // (your hint visuals unchanged)
    // ...

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
        Debug.Log("<color=#9cffb0>[SigilShift] Puzzle solved!</color>");
        PlaySolvedSfx();
        StartCoroutine(CoSolveGlow());

        // ⭐ ADD THIS ⭐
        Invoke(nameof(ShowWinPopup), 0.4f);
    }
    
    IEnumerator CoShowWinPopupAfterDelay() // ===== NEW
    {
        // wait for glow to finish so the moment feels juicy
        float wait = Mathf.Max(0f, glowDuration) + popupDelayAfterSolve;
        if (wait > 0f) yield return new WaitForSeconds(wait);

        if (!string.IsNullOrEmpty(winPopupSceneName))
        {
            if (loadPopupAdditive)
                SceneManager.LoadScene(winPopupSceneName, LoadSceneMode.Additive);
            else
                SceneManager.LoadScene(winPopupSceneName);
        }
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
        // (your glow code unchanged)
        // ...
        yield break;
    }

    // ===== NEW: button hooks for PopUp_Win =====
    
    private void ShowWinPopup()
    {
        // Loads the popup without closing the level scene
        UnityEngine.SceneManagement.SceneManager.LoadScene("PopUp_Win", UnityEngine.SceneManagement.LoadSceneMode.Additive);
    }
    
    public void ReturnToLevelSelect()
    {
        // If popup was additive, unload it first
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
