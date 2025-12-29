using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelIntroTutorial : MonoBehaviour
{
    // set by UIManager.OpenHowToPlay() when coming from the main menu
    public static bool FromHowToPlay = false;

    private const string PREF_KEY = "SS_LevelIntro_Shown";

    [Header("Panels in order")]
    public GameObject[] panels;   // Slide, Rot, Reset, Preview, Hint, Home

    int _index = -1;
    bool _active = false;

    void Start()
    {
        // turn everything off initially
        if (panels != null)
        {
            foreach (var p in panels)
                if (p) p.SetActive(false);
        }

        bool alreadyShown = PlayerPrefs.GetInt(PREF_KEY, 0) == 1;

        // If we came from level select and it's already shown once -> skip straight to Level_1
        if (!FromHowToPlay && alreadyShown)
        {
            SceneManager.LoadScene("Level_1");
            return;
        }

        // Otherwise, show the tutorial sequence
        ShowPanel(0);
        _active = true;
    }

    void Update()
    {
        if (!_active) return;

        bool tapped =
            Input.GetMouseButtonDown(0) ||
            (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);

        if (!tapped) return;

        Advance();
    }

    void ShowPanel(int index)
    {
        _index = Mathf.Clamp(index, 0, (panels?.Length ?? 1) - 1);

        if (panels == null) return;

        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i]) panels[i].SetActive(i == _index);
        }
    }

    void Advance()
    {
        if (panels == null || panels.Length == 0) return;

        int next = _index + 1;
        if (next >= panels.Length)
        {
            FinishTutorial();
        }
        else
        {
            ShowPanel(next);
        }
    }

    void FinishTutorial()
    {
        _active = false;

        // hide all panels
        if (panels != null)
        {
            foreach (var p in panels)
                if (p) p.SetActive(false);
        }

        // If we came from the main menu (How To Play), do NOT change the “seen” flag
        if (FromHowToPlay)
        {
            FromHowToPlay = false;               // reset for next time
            SceneManager.LoadScene("MainMenu");  // or "MainMenu" / "Path" scene, whatever you use
            return;
        }

        // If we came from Level 1 via LevelLoader (first time), save the flag and go to Level 1
        PlayerPrefs.SetInt(PREF_KEY, 1);
        PlayerPrefs.Save();

        SceneManager.LoadScene("Level_1");
    }
}
