using UnityEngine;
using TMPro;   // make sure TextMeshPro is available

public class HintButtonControl : MonoBehaviour
{
    [SerializeField] private RunePuzzleManager manager;
    [SerializeField] private TextMeshProUGUI hintCounterLabel; // little "3 / 2 / 1" text

    [Header("Hint Settings")]
    [SerializeField] private int freeHintsPerLevel = 3;   // 3 freebies

    private int usedHintsThisLevel = 0;

    private void Awake()
    {
        if (!manager)
            manager = FindObjectOfType<RunePuzzleManager>();

        UpdateCounter();
    }

    // This is still wired to the Button's OnClick()
    public void OnHintClicked()
    {
        if (!manager) return;

        // 1) Use free hints first
        if (usedHintsThisLevel < freeHintsPerLevel)
        {
            usedHintsThisLevel++;
            manager.ShowHint();
            UpdateCounter();
            return;
        }

        // 2) Out of freebies -> rewarded ad for more
        if (SigilAdsManager.Instance != null)
        {
            SigilAdsManager.Instance.ShowRewardedForHint(() =>
            {
                manager.ShowHint();
                // freebies are already at 0 – just keep the counter at blank
                UpdateCounter();
            });
        }
        else
        {
            // Failsafe: if ads aren't available, still give the hint
            manager.ShowHint();
        }
    }

    private void UpdateCounter()
    {
        if (!hintCounterLabel) return;

        int remaining = Mathf.Max(0, freeHintsPerLevel - usedHintsThisLevel);

        // Show "3, 2, 1", then nothing once they’re out
        hintCounterLabel.text = remaining > 0 ? remaining.ToString() : "";
    }
}