using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelLoader : MonoBehaviour
{
    private const string INTRO_PREF_KEY = "SS_LevelIntro_Shown";

    public void LoadLevel(int levelIndex)
    {
        // 🔒 check progression
        int highestUnlocked = LevelProgress.GetHighestUnlocked();

        if (levelIndex > highestUnlocked)
        {
            Debug.Log($"[LevelLoader] Level {levelIndex} is LOCKED. Highest unlocked is {highestUnlocked}.");
            return;
        }

        // 🧾 first–time tutorial ONLY for level 1
        if (levelIndex == 1 && PlayerPrefs.GetInt(INTRO_PREF_KEY, 0) == 0)
        {
            LevelIntroTutorial.FromHowToPlay = false;   // coming from Level Select
            SceneManager.LoadScene("Level_Intro");
            return;
        }

        // normal path
        string sceneName = "Level_" + levelIndex;
        SceneManager.LoadScene(sceneName);
    }
}