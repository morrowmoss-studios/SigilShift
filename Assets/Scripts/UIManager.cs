using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

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

    [Header("Targets")]
    [SerializeField] private RunePuzzleManager puzzleManager;
    [SerializeField] private RuneBoardLoader boardLoader;

    // PlayerPrefs keys
    const string PP_SIZE_INDEX = "SS_PuzzleSizeIndex";
    const string PP_ROTATION   = "SS_RotationEnabled";

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
        if (rotationToggle)
            rotationToggle.onValueChanged.AddListener(OnRotationToggled);

        // --- Load saved prefs ---
        int savedIndex = PlayerPrefs.GetInt(PP_SIZE_INDEX, 0); // 0=3x3 default
        bool savedRot  = PlayerPrefs.GetInt(PP_ROTATION, 1) == 1;

        if (sizeDropdown) sizeDropdown.SetValueWithoutNotify(savedIndex);
#if TMP_PRESENT || UNITY_TEXTMESHPRO
        if (sizeTMPDropdown) sizeTMPDropdown.SetValueWithoutNotify(savedIndex);
#endif
        if (rotationToggle) rotationToggle.SetIsOnWithoutNotify(savedRot);

        ApplySettings(savedIndex, savedRot);
    }

    void OnDestroy()
    {
        if (sizeDropdown) sizeDropdown.onValueChanged.RemoveListener(OnSizeChanged_UGUI);
#if TMP_PRESENT || UNITY_TEXTMESHPRO
        if (sizeTMPDropdown) sizeTMPDropdown.onValueChanged.RemoveListener(OnSizeChanged_TMP);
#endif
        if (rotationToggle) rotationToggle.onValueChanged.RemoveListener(OnRotationToggled);
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
            boardLoader.config.quarterTurns = rotationOn ? 4 : 1;
        }

        // update manager directly
        puzzleManager.rotationEnabled = rotationOn;
        puzzleManager.rotationQuarterTurns = rotationOn ? 4 : 1;

        // rebuild tiles cleanly (re-run the loader)
        // easiest way is to reload the scene or rebuild board here
        // for testing we'll just shuffle/reset current tiles
        puzzleManager.ResetToSolved();
        puzzleManager.ShuffleRandomWalk(puzzleManager.shuffleSteps);

        Debug.Log($"[UI] Applied settings → {newSize}x{newSize}, Rotation {(rotationOn ? "ON" : "OFF")}");
    }

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
