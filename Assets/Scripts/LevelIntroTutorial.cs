using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelIntroTutorial : MonoBehaviour
{
    private const string PREF_KEY = "SS_LevelIntro_Shown";

    [Header("Panels in order")]
    public GameObject[] panels;   // Tile_PopUp_Slide, Tile_PopUp_Rot, Reset, Preview, Hint, Home

    [Header("Which scene to load after tutorial")]
    public string nextSceneName = "Level_1";

    int _currentIndex = -1;
    bool _active = false;

    void Start()
    {
        // If we've already shown this once, skip the whole thing
        if (PlayerPrefs.GetInt(PREF_KEY, 0) == 1)
        {
            SceneManager.LoadScene(nextSceneName);
            return;
        }

        ShowPanel(0);
        _active = true;
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
            ShowPanel(next);
        }
        else
        {
            FinishTutorial();
        }
    }

    void FinishTutorial()
    {
        _active = false;

        PlayerPrefs.SetInt(PREF_KEY, 1);
        PlayerPrefs.Save();

        SetAllPanels(false);
        SceneManager.LoadScene(nextSceneName);
    }
}