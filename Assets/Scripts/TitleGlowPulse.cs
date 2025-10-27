using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class TitleGlowPulse : MonoBehaviour
{
    [Header("Alpha Pulse")]
    [Range(0f, 1f)] public float minAlpha = 0.12f;
    [Range(0f, 1f)] public float maxAlpha = 0.32f;
    [Range(0.2f, 4f)] public float speed = 1.2f;

    [Header("Subtle Scale Sway")]
    [Range(0f, 0.08f)] public float scalePunch = 0.02f; // 2% swell

    Image img;
    Color baseColor;
    Vector3 baseScale;

    void Awake()
    {
        img = GetComponent<Image>();
        baseColor = img.color;
        baseScale = transform.localScale;
    }

    void Update()
    {
        // unscaled so it pulses even if timeScale=0 (menus)
        float t = (Mathf.Sin(Time.unscaledTime * speed) + 1f) * 0.5f;

        // alpha pulse
        var c = baseColor;
        c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
        img.color = c;

        // micro scale swell around 1.0
        float s = 1f + (t - 0.5f) * 2f * scalePunch;
        transform.localScale = baseScale * s;
    }
}