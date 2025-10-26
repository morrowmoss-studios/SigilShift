using UnityEngine;

public class TitlePulse : MonoBehaviour
{
    [SerializeField] private float pulseSpeed = 1.5f;
    [SerializeField] private float minScale = 0.98f;
    [SerializeField] private float maxScale = 1.02f;
    [SerializeField] private float glowStrength = 0.15f;

    private Vector3 baseScale;
    private UnityEngine.UI.Image img;
    private Color baseColor;

    void Start()
    {
        baseScale = transform.localScale;
        img = GetComponent<UnityEngine.UI.Image>();
        baseColor = img.color;
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        transform.localScale = baseScale * Mathf.Lerp(minScale, maxScale, t);

        if (img)
        {
            Color c = baseColor;
            c.a = Mathf.Lerp(1f - glowStrength, 1f, t);
            img.color = c;
        }
    }
}