using UnityEngine;
using UnityEngine.SceneManagement;

public class WinPopupController : MonoBehaviour
{
    [Header("Scene Flow")]
    [SerializeField] private string levelSelectSceneName = "LevelSelect";
    [SerializeField] private string levelScenePrefix = "Level_";
    [SerializeField] private int maxLevelNumber = 30;

    private void Awake()
    {
        // Nothing needed here.
        // The popup should already be active in its own scene.
    }

    private void Start()
    {
        // Nothing needed here either.
    }

    // === Button hooks ===

    public void OnLevelSelectClicked()
    {
        SceneManager.LoadScene(levelSelectSceneName);
    }

    public void OnContinueClicked()
    {
        int cur = GetCurrentLevelNumber();
        int next = Mathf.Clamp(cur + 1, 1, maxLevelNumber);
        SceneManager.LoadScene(levelScenePrefix + next);
    }

    private int GetCurrentLevelNumber()
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