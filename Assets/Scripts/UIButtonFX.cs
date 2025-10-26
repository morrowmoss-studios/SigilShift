using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button), typeof(Image))]
public class UIButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    public AudioClip hoverClip, clickClip;
    [Range(0,1)] public float hoverVol = 0.5f, clickVol = 0.7f;
    public float hoverScale = 1.05f, tween = 0.08f;

    Vector3 baseScale;
    AudioSource src;

    void Awake()
    {
        baseScale = transform.localScale;
        src = FindFirstObjectByType<AudioSource>(); // use a single UI AudioSource in scene
        if (!src) { var go = new GameObject("UI_Audio"); src = go.AddComponent<AudioSource>(); src.spatialBlend = 0; }
    }

    public void OnPointerEnter(PointerEventData e) => StartCoroutine(ScaleTo(baseScale * hoverScale));
    public void OnPointerExit (PointerEventData e) => StartCoroutine(ScaleTo(baseScale));
    public void OnPointerDown (PointerEventData e) { if (clickClip) src.PlayOneShot(clickClip, clickVol); }

    System.Collections.IEnumerator ScaleTo(Vector3 t)
    {
        Vector3 a = transform.localScale;
        float    s = 0f;
        while (s < 1f)
        {
            s += Time.unscaledDeltaTime / Mathf.Max(0.001f, tween);
            transform.localScale = Vector3.Lerp(a, t, Mathf.SmoothStep(0,1,s));
            yield return null;
        }
        transform.localScale = t;
    }
}