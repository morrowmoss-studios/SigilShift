using UnityEngine;
using UnityEngine.SceneManagement;

public class WinPopupController : MonoBehaviour
{
    private int currentLevel;
    private const int MAX_LEVEL = 30;

    private void Awake()
    {
        // Figure out current level from the active (underlying) scene
        // Active scene is still the level scene, because PopUp_Win is additive.
        string levelSceneName = SceneManager.GetActiveScene().name;

        currentLevel = ExtractLevelNumber(levelSceneName);

        // Optional: save progress/unlock next
        int unlocked = PlayerPrefs.GetInt("UnlockedLevel", 1);
        if (currentLevel + 1 > unlocked && currentLevel < MAX_LEVEL)
        {
            PlayerPrefs.SetInt("UnlockedLevel", currentLevel + 1);
            PlayerPrefs.Save();
        }
    }

    public void OnLevelSelectClicked()
    {
        // Time.timeScale = 1f; // if you paused time
        SceneManager.LoadScene("LevelSelect");
    }

    public void OnContinueClicked()
    {
        // Time.timeScale = 1f; // if you paused time

        if (currentLevel >= MAX_LEVEL)
        {
            SceneManager.LoadScene("LevelSelect");
            return;
        }

        string nextScene = $"Level_{currentLevel + 1}";
        SceneManager.LoadScene(nextScene);
    }

    private int ExtractLevelNumber(string sceneName)
    {
        // expects "Level_12" etc.
        if (sceneName.StartsWith("Level_"))
        {
            string numPart = sceneName.Substring("Level_".Length);
            if (int.TryParse(numPart, out int n))
                return n;
        }

        Debug.LogWarning($"Could not parse level number from scene name: {sceneName}. Defaulting to 1.");
        return 1;
    }
}