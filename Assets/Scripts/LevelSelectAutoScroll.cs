using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LevelSelectAutoScroll : MonoBehaviour
{
    [Header("Hook these up")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;

    [Header("Optional tuning")]
    [SerializeField] private bool centerOnTarget = true;
    [SerializeField, Range(0f, 1f)] private float extraPaddingNormalized = 0.05f;

    private IEnumerator Start()
    {
        // Let UI layout settle
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;
        Canvas.ForceUpdateCanvases();

        int highest = LevelProgress.GetHighestUnlocked();
        Debug.Log($"[LevelSelectAutoScroll] HighestUnlocked = {highest}");

        // Highest 1 => go to TOP (in your setup TOP == 0)
        if (highest <= 1)
        {
            SetScrollNormalized(0f);
            yield break;
        }

        // Find the target LevelPlate by LevelLockVisual.levelIndex
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
            Debug.LogWarning($"[LevelSelectAutoScroll] Couldn't find LevelLockVisual for level {highest}. Going to top.");
            SetScrollNormalized(0f);
            yield break;
        }

        ScrollTo(target.GetComponent<RectTransform>());
    }

    private void ScrollTo(RectTransform target)
    {
        Canvas.ForceUpdateCanvases();

        var viewport = scrollRect.viewport;

        // Target center in viewport-local space
        Vector3 targetWorld = target.TransformPoint(target.rect.center);
        Vector3 viewportLocal = viewport.InverseTransformPoint(targetWorld);

        // Where do we want it in the viewport?
        float desiredY = centerOnTarget ? 0f : (viewport.rect.height * 0.5f - (viewport.rect.height * extraPaddingNormalized));

        // Positive delta => target is above desired => we need to scroll "up" (in your inverted setup, down is 1)
        float deltaY = viewportLocal.y - desiredY;

        // Convert pixel delta into normalized delta.
        // Scrollable height = content height - viewport height
        float scrollable = Mathf.Max(1f, content.rect.height - viewport.rect.height);
        float normalizedDelta = deltaY / scrollable;

        // IMPORTANT: Your setup is inverted (0 = top, 1 = bottom), so we ADD the delta.
        float newNorm = scrollRect.verticalNormalizedPosition + normalizedDelta;

        // Clamp and apply
        SetScrollNormalized(newNorm);
    }

    private void SetScrollNormalized(float value)
    {
        value = Mathf.Clamp01(value);
        scrollRect.verticalNormalizedPosition = value;
        Canvas.ForceUpdateCanvases();
        Debug.Log($"[LevelSelectAutoScroll] Set verticalNormalizedPosition = {value:0.000}");
    }
}
