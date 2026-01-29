using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LevelSelectAutoScroll : MonoBehaviour
{
    [Header("Hook these up")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content; // the content under the ScrollRect

    [Header("Optional tuning")]
    [SerializeField] private float extraPaddingNormalized = 0.05f; // keeps it from sitting exactly at edge
    [SerializeField] private bool centerOnTarget = true;

    private IEnumerator Start()
    {
        // Wait a couple frames so layouts finish (VERY important)
        yield return null;
        yield return null;

        int highest = LevelProgress.GetHighestUnlocked();

        // Find the LevelLockVisual for that level
        LevelLockVisual target = null;
        var all = content.GetComponentsInChildren<LevelLockVisual>(true);
        foreach (var v in all)
        {
            if (v.levelIndex == highest)
            {
                target = v;
                break;
            }
        }

        if (target == null)
        {
            Debug.LogWarning($"[LevelSelectAutoScroll] Couldn't find LevelLockVisual for level {highest}");
            yield break;
        }

        ScrollTo(target.GetComponent<RectTransform>());
    }

    private void ScrollTo(RectTransform target)
    {
        // Convert target position into normalized scroll position.
        // Works for vertical scrolling. If yours is horizontal, tell me and I’ll flip it.

        Canvas.ForceUpdateCanvases();

        float contentHeight = content.rect.height;
        float viewportHeight = scrollRect.viewport.rect.height;

        if (contentHeight <= viewportHeight)
        {
            // nothing to scroll
            return;
        }

        // anchoredPosition.y is how far content is shifted; target anchoredPosition is inside content
        float targetY = Mathf.Abs(target.anchoredPosition.y);

        float normalized = targetY / (contentHeight - viewportHeight);

        if (centerOnTarget)
        {
            float halfViewport = viewportHeight * 0.5f;
            normalized = (targetY - halfViewport) / (contentHeight - viewportHeight);
        }

        normalized = Mathf.Clamp01(normalized);

        // ScrollRect verticalNormalizedPosition is inverted (1 = top, 0 = bottom)
        float v = 1f - normalized;

        // Apply padding
        v = Mathf.Clamp01(v + extraPaddingNormalized);

        scrollRect.verticalNormalizedPosition = v;
    }
}
