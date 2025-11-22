using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelLoader : MonoBehaviour
{
    public void LoadLevel(int levelIndex)
    {
        string sceneName = "Level_" + levelIndex;
        SceneManager.LoadScene(sceneName);
    }
}