using UnityEngine;

public class GameSettingsBootstrap : MonoBehaviour
{
    // Match UIManager keys
    private const string PP_SIZE_INDEX = "SS_PuzzleSizeIndex";
    private const string PP_ROTATION   = "SS_RotationEnabled";
    private const string PP_SFX        = "SS_SFXEnabled";

    [SerializeField] private RuneBoardLoader loader;

    void Awake()
    {
        // --- Read saved prefs for board + rotation ---
        int  sizeIdx = PlayerPrefs.GetInt(PP_SIZE_INDEX, 0);   // 0=3x3, 1=5x5, 2=7x7
        bool rotOn   = PlayerPrefs.GetInt(PP_ROTATION, 0) == 1;

        // Map index -> board size
        int size = 3;
        switch (Mathf.Clamp(sizeIdx, 0, 2))
        {
            case 1: size = 4; break;
            case 2: size = 5; break;
        }

        // Find loader if not wired in Inspector
        if (!loader)
            loader = FindObjectOfType<RuneBoardLoader>();

        if (!loader)
        {
            Debug.LogError("[Bootstrap] No RuneBoardLoader found in scene.");
            return;
        }

        // Push settings into LevelConfig BEFORE RunePuzzleManager.Awake / loader.Start
        if (loader.config)
        {
            loader.config.rows = size;
            loader.config.cols = size;

            loader.config.enableRotation = rotOn;
            loader.config.quarterTurns   = rotOn ? 4 : 1;
        }
        
        DontDestroyOnLoad(gameObject);

        Debug.Log($"[Bootstrap] Awake -> applied config rows/cols={size}x{size}, rotation={rotOn}");
    }

    void Start()
    {
        // --- Optional: apply SFX mute once the manager exists ---
        bool sfxOn = PlayerPrefs.GetInt(PP_SFX, 1) == 1;

        var manager = FindObjectOfType<RunePuzzleManager>();
        if (manager)
        {
            manager.ApplySfxMute(!sfxOn);
            Debug.Log($"[Bootstrap] Start -> applied SFX mute = {!sfxOn}");
        }
    }
}