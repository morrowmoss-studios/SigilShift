using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class HomePopupController : MonoBehaviour
{
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private bool pauseGameWhileOpen = true;

    void OnEnable()
    {
        if (pauseGameWhileOpen) Time.timeScale = 0f;
        AudioListener.pause = true;  // optional: pause audio too
    }

    void OnDisable()
    {
        if (pauseGameWhileOpen) Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    // Confirm → go to Main Menu
    public void OnConfirmGoHome()
    {
        // Unpause before switching scenes
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(mainMenuScene);
    }

    // Cancel → just close popup and stay on the current level
    public void OnCancelStay()
    {
        // Unpause is handled in OnDisable()
        SceneManager.UnloadSceneAsync(gameObject.scene);
    }
}