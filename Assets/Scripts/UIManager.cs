using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Reflection;
using TMPro;

[DisallowMultipleComponent]
public class UIManager : MonoBehaviour
{
    // -------------------------------------------------------------
    //  Scene Controls
    // -------------------------------------------------------------
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // -------------------------------------------------------------
    //  Puzzle Settings UI (TMP only)
    // -------------------------------------------------------------
    [Header("Puzzle Settings UI")]
    [Tooltip("TMP Dropdown for selecting puzzle size.")]
    [SerializeField] private TMP_Dropdown sizeTMPDropdown;

    [Tooltip("Checkbox to enable or disable tile rotation.")]
    [SerializeField] private Toggle rotationToggle;

    [Header("Audio Settings UI")]
    [SerializeField] private Toggle sfxToggle;

    [Header("Targets (optional in Settings scene)")]
    [SerializeField] private RunePuzzleManager puzzleManager;   // can be null in Settings scene
    [SerializeField] private RuneBoardLoader  boardLoader;      // can be null in Settings scene
    
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // PlayerPrefs keys
    private const string PP_SIZE_INDEX = "SS_PuzzleSizeIndex";
    private const string PP_ROTATION   = "SS_RotationEnabled";
    private const string PP_SFX        = "SS_SFXEnabled";

    void Start()
    {
        // Auto-find if present (e.g., when this UI lives inside the gameplay scene)
        if (!puzzleManager) puzzleManager = FindObjectOfType<RunePuzzleManager>();
        if (!boardLoader)   boardLoader   = FindObjectOfType<RuneBoardLoader>();

        // --- Dropdown setup (TMP) ---
        if (sizeTMPDropdown)
        {
            var labels = new List<string> { "3 X 3", "4 X 4", "5 X 5" };
            sizeTMPDropdown.ClearOptions();
            sizeTMPDropdown.AddOptions(labels);
            sizeTMPDropdown.onValueChanged.AddListener(OnSizeChanged_TMP);
        }

        if (rotationToggle) rotationToggle.onValueChanged.AddListener(OnRotationToggled);
        if (sfxToggle)      sfxToggle.onValueChanged.AddListener(OnSFXToggled);

        // --- Load saved prefs ---
        int  savedIndex = PlayerPrefs.GetInt(PP_SIZE_INDEX, 0);
        bool savedRot   = PlayerPrefs.GetInt(PP_ROTATION, 0) == 1;
        bool savedSfx   = PlayerPrefs.GetInt(PP_SFX, 1) == 1;

        Debug.Log($"[UI] Start -> loaded prefs sizeIndex={savedIndex}, rot={savedRot}, sfx={savedSfx}");
        
        if (sizeTMPDropdown) sizeTMPDropdown.SetValueWithoutNotify(savedIndex);
        if (rotationToggle)  rotationToggle.SetIsOnWithoutNotify(savedRot);
        if (sfxToggle)       sfxToggle.SetIsOnWithoutNotify(savedSfx);

        // If this UI exists in the gameplay scene, apply immediately; in a standalone Settings scene, this will no-op.
        ApplySettings(savedIndex, savedRot);
        ApplyAudio(savedSfx);
    }

    void OnDestroy()
    {
        if (sizeTMPDropdown) sizeTMPDropdown.onValueChanged.RemoveListener(OnSizeChanged_TMP);
        if (rotationToggle)  rotationToggle.onValueChanged.RemoveListener(OnRotationToggled);
        if (sfxToggle)       sfxToggle.onValueChanged.RemoveListener(OnSFXToggled);
    }

    // -------------------------------------------------------------
    //  Event Handlers
    // -------------------------------------------------------------
    void OnSizeChanged_TMP(int index)
    {
        PlayerPrefs.SetInt(PP_SIZE_INDEX, index);
        ApplySettings(index, GetCurrentRotation());
    }

    void OnRotationToggled(bool on)
    {
        PlayerPrefs.SetInt(PP_ROTATION, on ? 1 : 0);
        ApplySettings(GetCurrentSizeIndex(), on);
    }

    void OnSFXToggled(bool on)
    {
        PlayerPrefs.SetInt(PP_SFX, on ? 1 : 0);
        ApplyAudio(on);
    }

    // -------------------------------------------------------------
    //  Core Apply Logic
    // -------------------------------------------------------------
    void ApplySettings(int sizeIndex, bool rotationOn)
    {
        // If we're in the Settings scene (no puzzle present), just return—settings are still saved.
        if (!puzzleManager || !boardLoader) return;

        int newSize = 3;
        switch (Mathf.Clamp(sizeIndex, 0, 2))
        {
            case 1: newSize = 4; break;
            case 2: newSize = 5; break;
        }

        // update board + manager
        puzzleManager.rows = newSize;
        puzzleManager.cols = newSize;

        if (boardLoader.config)
        {
            boardLoader.config.rows = newSize;
            boardLoader.config.cols = newSize;
            boardLoader.config.enableRotation = rotationOn;
            boardLoader.config.quarterTurns   = rotationOn ? 4 : 1;
        }

        puzzleManager.rotationEnabled      = rotationOn;
        puzzleManager.rotationQuarterTurns = rotationOn ? 4 : 1;

        // optional: call loader.RebuildBoard(r,c) if you added it
        MethodInfo rebuild = typeof(RuneBoardLoader).GetMethod("RebuildBoard",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (rebuild != null && rebuild.GetParameters().Length == 2)
        {
            rebuild.Invoke(boardLoader, new object[] { newSize, newSize });
        }

        puzzleManager.ResetToSolved();
        puzzleManager.ShuffleRandomWalk(puzzleManager.shuffleSteps);

        Debug.Log($"[UI] Applied settings → {newSize}x{newSize}, Rotation {(rotationOn ? "ON" : "OFF")}");
    }

    void ApplyAudio(bool sfxOn)
    {
        var mgr = FindObjectOfType<RunePuzzleManager>();
        if (mgr) mgr.ApplySfxMute(!sfxOn);
        // If we're in the standalone Settings scene, mgr will be null—no action needed.
    }


    // -------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------
    int GetCurrentSizeIndex() => sizeTMPDropdown ? sizeTMPDropdown.value : 0;

    bool GetCurrentRotation() => rotationToggle ? rotationToggle.isOn : false; // default to off

    // -------------------------------------------------------------
    //  Save + Button convenience
    // -------------------------------------------------------------
    public void SaveCurrentSettings()
    {
        int indexFromDropdown = sizeTMPDropdown ? sizeTMPDropdown.value : -1;

        int sizeIndex = GetCurrentSizeIndex();
        bool rotOn    = GetCurrentRotation();
        bool sfxOn    = sfxToggle ? sfxToggle.isOn : true;

        Debug.Log($"[UI] SaveCurrentSettings -> " +
                  $"sizeIndex={sizeIndex}, dropdownRef={(sizeTMPDropdown ? sizeTMPDropdown.name : "NULL")}, " +
                  $"dropdownValue={indexFromDropdown}, rotOn={rotOn}, sfxOn={sfxOn}");

        PlayerPrefs.SetInt(PP_SIZE_INDEX, sizeIndex);
        PlayerPrefs.SetInt(PP_ROTATION,   rotOn ? 1 : 0);
        PlayerPrefs.SetInt(PP_SFX,        sfxOn ? 1 : 0);

        PlayerPrefs.Save();
    }
    
    public void ConfirmAndGo(string sceneName)
    {
        Debug.Log("[UI] ConfirmAndGo called, saving settings then loading " + sceneName);
        SaveCurrentSettings();
        LoadScene(sceneName);
    }


    public void CancelAndGo(string sceneName)
    {
        LoadScene(sceneName);
    }
    
// --- Quit Popup hooks ---
    public void ConfirmQuit()            // hooked to Confirm button
    {
        // optional: if this scene ever shows settings, you can persist them here
        if (Application.isPlaying)
            PlayerPrefs.Save();

        QuitGame();                      // uses your existing method
    }

    public void CancelQuit()             // hooked to Cancel button
    {
        // go back to main menu (or wherever you want)
        LoadScene(mainMenuSceneName);    // uses your existing method
    }

    public void OpenAbout()
    {
        SceneManager.LoadScene("AboutGame", LoadSceneMode.Additive);
    }

}
