using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class TitleGlow : MonoBehaviour
{
    [Header("Glow Settings")]
    public Color glowColor = new Color(0.6f, 1f, 0.9f, 0.25f);
    [Range(0f, 0.5f)] public float glowStrength = 0.2f;
    public bool hoverGlow = true;
    public float pulseSpeed = 0.8f;

    private Image image;
    private Material mat;
    private float time;

    void Awake()
    {
        image = GetComponent<Image>();

        // try additive particle shader (works great on UI)
        var shader = Shader.Find("UI/Particles/Additive");
        mat = shader ? new Material(shader) : new Material(Shader.Find("UI/Default"));
        mat.mainTexture = image.sprite.texture;
        image.material = mat;
    }

    void Update()
    {
        if (!hoverGlow) return;

        time += Time.unscaledDeltaTime * pulseSpeed;
        float a = (Mathf.Sin(time) * 0.5f + 0.5f) * glowStrength;
        mat.color = new Color(glowColor.r, glowColor.g, glowColor.b, a);
    }
}