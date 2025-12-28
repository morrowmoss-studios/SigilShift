using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelLoader : MonoBehaviour
{
    public void LoadLevel(int levelIndex)
    {
        // 🔒 Check if this level is unlocked
        int highestUnlocked = LevelProgress.GetHighestUnlocked();

        if (levelIndex > highestUnlocked)
        {
            Debug.Log($"[LevelLoader] Level {levelIndex} is LOCKED. Highest unlocked is {highestUnlocked}.");
            // You can play a "locked" SFX here later if you want.
            return;
        }

        string sceneName = "Level_" + levelIndex;
        SceneManager.LoadScene(sceneName);
    }
}