using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Reflection; // for optional RebuildBoard reflection

#if TMP_PRESENT || UNITY_TEXTMESHPRO
using TMPro;
#endif

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
    //  Puzzle Settings UI
    // -------------------------------------------------------------
    [Header("Puzzle Settings UI")]
    [Tooltip("Standard Unity Dropdown for selecting puzzle size.")]
    [SerializeField] private Dropdown sizeDropdown;
#if TMP_PRESENT || UNITY_TEXTMESHPRO
    [Tooltip("TMP Dropdown (optional). If assigned, used instead of UGUI dropdown.")]
    [SerializeField] private TMP_Dropdown sizeTMPDropdown;
#endif
    [Tooltip("Checkbox to enable or disable tile rotation.")]
    [SerializeField] private Toggle rotationToggle;

    [Header("Audio Settings UI")]
    [SerializeField] private Toggle sfxToggle;

    [Header("Targets")]
    [SerializeField] private RunePuzzleManager puzzleManager;
    [SerializeField] private RuneBoardLoader boardLoader;

    // PlayerPrefs keys
    const string PP_SIZE_INDEX = "SS_PuzzleSizeIndex";
    const string PP_ROTATION   = "SS_RotationEnabled";
    const string PP_SFX        = "SS_SFXEnabled";

    void Start()
    {
        // Auto-find references if needed
        if (!puzzleManager) puzzleManager = FindObjectOfType<RunePuzzleManager>();
        if (!boardLoader)   boardLoader   = FindObjectOfType<RuneBoardLoader>();

        // --- Dropdown setup ---
        var labels = new List<string> { "3 × 3", "6 × 6", "9 × 9" };

        if (sizeDropdown)
        {
            sizeDropdown.ClearOptions();
            sizeDropdown.AddOptions(labels);
            sizeDropdown.onValueChanged.AddListener(OnSizeChanged_UGUI);
        }
#if TMP_PRESENT || UNITY_TEXTMESHPRO
        if (sizeTMPDropdown)
        {
            sizeTMPDropdown.ClearOptions();
            sizeTMPDropdown.AddOptions(labels);
            sizeTMPDropdown.onValueChanged.AddListener(OnSizeChanged_TMP);
        }
#endif
        if (rotationToggle) rotationToggle.onValueChanged.AddListener(OnRotationToggled);
        if (sfxToggle)      sfxToggle.onValueChanged.AddListener(OnSFXToggled);

        // --- Load saved prefs ---
        int  savedIndex = PlayerPrefs.GetInt(PP_SIZE_INDEX, 0); // 0=3x3 default
        bool savedRot   = PlayerPrefs.GetInt(PP_ROTATION, 1) == 1;
        bool savedSfx   = PlayerPrefs.GetInt(PP_SFX, 1) == 1;

        if (sizeDropdown)     sizeDropdown.SetValueWithoutNotify(savedIndex);
#if TMP_PRESENT || UNITY_TEXTMESHPRO
        if (sizeTMPDropdown)  sizeTMPDropdown.SetValueWithoutNotify(savedIndex);
#endif
        if (rotationToggle)   rotationToggle.SetIsOnWithoutNotify(savedRot);
        if (sfxToggle)        sfxToggle.SetIsOnWithoutNotify(savedSfx);

        // Apply now (works whether this is a standalone Settings scene or overlay)
        ApplySettings(savedIndex, savedRot);
        ApplyAudio(savedSfx);
    }

    void OnDestroy()
    {
        if (sizeDropdown) sizeDropdown.onValueChanged.RemoveListener(OnSizeChanged_UGUI);
#if TMP_PRESENT || UNITY_TEXTMESHPRO
        if (sizeTMPDropdown) sizeTMPDropdown.onValueChanged.RemoveListener(OnSizeChanged_TMP);
#endif
        if (rotationToggle) rotationToggle.onValueChanged.RemoveListener(OnRotationToggled);
        if (sfxToggle)      sfxToggle.onValueChanged.RemoveListener(OnSFXToggled);
    }

    // -------------------------------------------------------------
    //  Event Handlers
    // -------------------------------------------------------------
    void OnSizeChanged_UGUI(int index)
    {
        PlayerPrefs.SetInt(PP_SIZE_INDEX, index);
        ApplySettings(index, GetCurrentRotation());
    }

#if TMP_PRESENT || UNITY_TEXTMESHPRO
    void OnSizeChanged_TMP(int index)
    {
        PlayerPrefs.SetInt(PP_SIZE_INDEX, index);
        ApplySettings(index, GetCurrentRotation());
    }
#endif

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
        if (!puzzleManager || !boardLoader) return;

        int newSize = 3;
        switch (Mathf.Clamp(sizeIndex, 0, 2))
        {
            case 0: newSize = 3; break;
            case 1: newSize = 6; break;
            case 2: newSize = 9; break;
        }

        // update board + manager
        puzzleManager.rows = newSize;
        puzzleManager.cols = newSize;

        // if a config is assigned, update it too
        if (boardLoader.config)
        {
            boardLoader.config.rows = newSize;
            boardLoader.config.cols = newSize;
            boardLoader.config.enableRotation = rotationOn;
            boardLoader.config.quarterTurns   = rotationOn ? 4 : 1;
        }

        // update manager directly
        puzzleManager.rotationEnabled      = rotationOn;
        puzzleManager.rotationQuarterTurns = rotationOn ? 4 : 1;

        // Try to call RuneBoardLoader.RebuildBoard(newSize, newSize) if you added it.
        MethodInfo rebuild = typeof(RuneBoardLoader).GetMethod("RebuildBoard", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (rebuild != null && rebuild.GetParameters().Length == 2)
        {
            rebuild.Invoke(boardLoader, new object[] { newSize, newSize });
        }

        // Fallback or post-rebuild: reset & shuffle
        puzzleManager.ResetToSolved();
        puzzleManager.ShuffleRandomWalk(puzzleManager.shuffleSteps);

        Debug.Log($"[UI] Applied settings → {newSize}x{newSize}, Rotation {(rotationOn ? "ON" : "OFF")}");
    }

    void ApplyAudio(bool sfxOn)
    {
        MuteByTag("SFX", !sfxOn);
        Debug.Log($"[UI] Audio → SFX {(sfxOn ? "ON" : "OFF")}");
    }

    void MuteByTag(string tag, bool mute)
    {
        var objs = GameObject.FindGameObjectsWithTag(tag);
        foreach (var go in objs)
        {
            var src = go.GetComponent<AudioSource>();
            if (src) src.mute = mute;
        }
    }

    // -------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------
    int GetCurrentSizeIndex()
    {
#if TMP_PRESENT || UNITY_TEXTMESHPRO
        if (sizeTMPDropdown) return sizeTMPDropdown.value;
#endif
        return sizeDropdown ? sizeDropdown.value : 0;
    }

    bool GetCurrentRotation()
    {
        return rotationToggle ? rotationToggle.isOn : true;
    }
}
