using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HintAdPopupController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    // Optional: drag in the manager from the scene, or we’ll Find it
    [SerializeField] private RunePuzzleManager puzzleManager;

    private void Awake()
    {
        if (!puzzleManager)
        {
            puzzleManager = FindObjectOfType<RunePuzzleManager>();
            if (!puzzleManager)
            {
                Debug.LogError("[HintAdPopupController] No RunePuzzleManager found in scene.");
            }
        }

        if (confirmButton) confirmButton.onClick.AddListener(OnConfirmClicked);
        if (cancelButton)  cancelButton.onClick.AddListener(OnCancelClicked);
    }

    private void OnDestroy()
    {
        if (confirmButton) confirmButton.onClick.RemoveListener(OnConfirmClicked);
        if (cancelButton)  cancelButton.onClick.RemoveListener(OnCancelClicked);
    }

    private void OnConfirmClicked()
    {
        // Player agreed to watch an ad
        if (SigilAdsManager.Instance != null && puzzleManager != null)
        {
            // This will call puzzleManager.OnRewardHintGranted() when the ad reward is granted
            SigilAdsManager.Instance.ShowRewardedForHint(puzzleManager.OnRewardHintGranted);
        }
        else
        {
            Debug.LogWarning("[HintAdPopupController] No SigilAdsManager or RunePuzzleManager available. Granting hint directly.");
            if (puzzleManager != null)
            {
                puzzleManager.OnRewardHintGranted();
            }
        }

        ClosePopup();
    }

    private void OnCancelClicked()
    {
        // Player said “no thanks” – just unlock input and close
        if (puzzleManager != null)
        {
            puzzleManager.SetInputLocked(false);
        }

        ClosePopup();
    }

    private void ClosePopup()
    {
        // Unload this scene (since it's loaded Additively)
        SceneManager.UnloadSceneAsync("PopUp_HintAd");
    }
}
