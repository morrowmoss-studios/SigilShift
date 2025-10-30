using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelHUD : MonoBehaviour
{
    [SerializeField] private string homePopupScene = "HomePopup"; // exact scene name

    public void OnHomeButtonPressed()
    {
        // Load popup on top of the level; level stays loaded (and gets paused by the popup)
        SceneManager.LoadScene(homePopupScene, LoadSceneMode.Additive);
    }
}