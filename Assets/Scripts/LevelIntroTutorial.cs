using UnityEngine;

public class LevelIntroTutorial : MonoBehaviour
{
    // shown-once flag
    private const string PREF_KEY = "SS_LevelIntro_Shown";

    [Header("Panels in order")]
    public GameObject[] panels;   // Tile_PopUp_Slide, Tile_PopUp_Rot, Reset_PopUp, Preview_PopUp, Hint_PopUp, Home_PopUp

    [Header("Puzzle to lock while showing (optional)")]
    public RunePuzzleManager puzzle;   // drag it, or we’ll auto-find

    int _currentIndex = -1;
    bool _active = false;

    void Start()
    {
        // already shown once? make sure everything is off and bail
        if (PlayerPrefs.GetInt(PREF_KEY, 0) == 1)
        {
            SetAllPanels(false);
            enabled = false;
            return;
        }

        // auto-find puzzle if not wired
        if (!puzzle)
            puzzle = FindObjectOfType<RunePuzzleManager>();

        if (puzzle)
            puzzle.SetInputLocked(true);

        _active = true;
        ShowPanel(0);
    }

    void ShowPanel(int index)
    {
        _currentIndex = index;
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i])
                panels[i].SetActive(i == index);
        }
    }

    void SetAllPanels(bool state)
    {
        if (panels == null) return;
        foreach (var p in panels)
            if (p) p.SetActive(state);
    }

    void Update()
    {
        if (!_active) return;

        bool tapped = false;

#if UNITY_IOS || UNITY_ANDROID
        if (Input.touchCount > 0 &&
            Input.GetTouch(0).phase == TouchPhase.Began)
            tapped = true;
#else
        if (Input.GetMouseButtonDown(0))
            tapped = true;
#endif

        if (!tapped) return;

        int next = _currentIndex + 1;
        if (next < panels.Length)
        {
            ShowPanel(next);   // go to next popup
        }
        else
        {
            FinishTutorial();  // done with all of them
        }
    }

    void FinishTutorial()
    {
        _active = false;

        PlayerPrefs.SetInt(PREF_KEY, 1);
        PlayerPrefs.Save();

        if (puzzle)
            puzzle.SetInputLocked(false);

        SetAllPanels(false);
        enabled = false;
    }
}
