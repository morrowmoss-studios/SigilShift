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

    private bool _closing;

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

        // HARD LOCK board input while this popup exists
        if (puzzleManager != null)
            puzzleManager.SetInputLocked(true);

        if (confirmButton) confirmButton.onClick.AddListener(OnConfirmClicked);
        if (cancelButton)  cancelButton.onClick.AddListener(OnCancelClicked);
    }

    private void OnDestroy()
    {
        if (confirmButton) confirmButton.onClick.RemoveListener(OnConfirmClicked);
        if (cancelButton)  cancelButton.onClick.RemoveListener(OnCancelClicked);

        // Safety unlock: if popup gets destroyed unexpectedly, don't leave the board locked forever
        if (puzzleManager != null)
            puzzleManager.SetInputLocked(false);
    }

    private void OnConfirmClicked()
    {
        if (_closing) return;
        _closing = true;

        // Disable buttons so we can't double click and cause chaos
        if (confirmButton) confirmButton.interactable = false;
        if (cancelButton)  cancelButton.interactable  = false;

        // Keep board locked while ad plays
        if (SigilAdsManager.Instance != null && puzzleManager != null)
        {
            // Reward callback: grant hint + unlock board (your OnRewardHintGranted already unlocks)
            SigilAdsManager.Instance.ShowRewardedForHint(() =>
            {
                puzzleManager.OnRewardHintGranted();   // grants + unlocks
                ClosePopup();
            });
        }
        else
        {
            Debug.LogWarning("[HintAdPopupController] No SigilAdsManager or RunePuzzleManager available. Granting hint directly.");
            if (puzzleManager != null)
                puzzleManager.OnRewardHintGranted();   // grants + unlocks

            ClosePopup();
        }

        // IMPORTANT: do NOT close the popup immediately when ads are real,
        // because reward arrives later. We close inside the callback above.
        // (If ads are simulated in editor, the callback fires instantly anyway.)
    }

    private void OnCancelClicked()
    {
        if (_closing) return;
        _closing = true;

        // Unlock input and close
        if (puzzleManager != null)
            puzzleManager.SetInputLocked(false);

        ClosePopup();
    }

    private void ClosePopup()
    {
        if (gameObject == null) return;

        // Unload this scene (since it's loaded Additively)
        SceneManager.UnloadSceneAsync(gameObject.scene);
    }
}
