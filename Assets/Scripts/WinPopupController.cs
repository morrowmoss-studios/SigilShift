using UnityEngine;
using UnityEngine.SceneManagement;

public class WinPopupController : MonoBehaviour
{
    [Header("Scene Flow")]
    [SerializeField] private string levelSelectSceneName = "LevelSelect";
    [SerializeField] private string levelScenePrefix = "Level_";
    [SerializeField] private int maxLevelNumber = 30;

    [Header("Popup Root Object Name")]
    [SerializeField] private string popupRootName = "WinPopUp"; 
    [SerializeField] private int popupSortOrder = 20000;

    private void Awake()
    {
        MakePopupVisible();
    }

    private void Start()
    {
        // Belt + suspenders in case hierarchy shifts on first frame
        MakePopupVisible();
    }

    private void MakePopupVisible()
    {
        // Find the root GO that actually contains your UI
        GameObject popupRoot = GameObject.Find(popupRootName);

        if (popupRoot == null)
        {
            Debug.LogWarning($"[WinPopupController] Couldn't find '{popupRootName}' in PopUp_Win scene.");
            return;
        }

        // 1) Ensure root is active
        if (!popupRoot.activeSelf)
            popupRoot.SetActive(true);

        // 2) Ensure all canvases under it are enabled + on top
        var canvases = popupRoot.GetComponentsInChildren<Canvas>(true);
        foreach (var c in canvases)
        {
            c.enabled = true;
            c.gameObject.SetActive(true);
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.worldCamera = null;
            c.sortingOrder = popupSortOrder;
        }

        // 3) Fix any CanvasGroup that was saved invisible
        var groups = popupRoot.GetComponentsInChildren<CanvasGroup>(true);
        foreach (var g in groups)
        {
            g.alpha = 1f;
            g.interactable = true;
            g.blocksRaycasts = true;
            g.ignoreParentGroups = true;
        }

        Debug.Log("[WinPopupController] Popup forced visible.");
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
