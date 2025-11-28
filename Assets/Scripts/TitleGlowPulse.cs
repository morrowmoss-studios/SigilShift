using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TitleGlowPulse : MonoBehaviour
{
    [Header("Pulse Settings")]
    public float pulseSpeed = 0.8f;
    [Range(0.9f, 1.2f)] public float minScale = 1.0f;
    [Range(0.9f, 1.5f)] public float maxScale = 1.06f;

    [Header("Alpha")]
    [Range(0f, 1f)] public float minAlpha = 0.35f;
    [Range(0f, 1f)] public float maxAlpha = 0.85f;

    RectTransform _rect;
    Image _image;
    Vector3 _baseScale;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _image = GetComponent<Image>();
        _baseScale = _rect.localScale;
    }

    void Update()
    {
        // use unscaled time so it keeps pulsing even if timescale changes
        float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;

        // scale pulse
        float s = Mathf.Lerp(minScale, maxScale, t);
        _rect.localScale = _baseScale * s;

        // alpha pulse
        if (_image != null)
        {
            var c = _image.color;
            c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
            _image.color = c;
        }
    }
}