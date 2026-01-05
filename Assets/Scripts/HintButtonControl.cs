using UnityEngine;

public class HintButtonControl : MonoBehaviour
{
    [SerializeField] private RunePuzzleManager manager;

    [Header("Hint Settings")]
    [SerializeField] private int freeHintsPerLevel = 3;   // your 3 freebies

    private int usedHintsThisLevel = 0;

    private void Awake()
    {
        // keep your safety net so it auto-finds the manager
        if (!manager)
            manager = FindObjectOfType<RunePuzzleManager>();
    }

    // This is still what the Button calls in the OnClick()
    public void OnHintClicked()
    {
        if (!manager) return;

        // 1) Use free hints first
        if (usedHintsThisLevel < freeHintsPerLevel)
        {
            usedHintsThisLevel++;
            manager.ShowHint();   // ← exactly what you were doing before
            return;
        }

        // 2) Out of freebies → use rewarded ad
        if (SigilAdsManager.Instance != null)
        {
            // When the ad finishes and reward is granted,
            // SigilAdsManager will call manager.ShowHint() for us.
            SigilAdsManager.Instance.ShowRewardedForHint(manager.ShowHint);
        }
        else
        {
            // Failsafe: if ads aren’t available, just give the hint anyway
            manager.ShowHint();
        }
    }
}