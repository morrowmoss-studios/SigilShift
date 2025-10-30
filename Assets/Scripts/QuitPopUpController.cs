using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class QuitPopupController : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private bool savePlayerPrefsOnQuit = true;

    // Button → Confirm
    public void OnConfirmQuit()
    {
        if (savePlayerPrefsOnQuit) PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Button → Cancel
    public void OnCancelQuit()
    {
        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
    }
}