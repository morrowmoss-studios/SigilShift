using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class FitToCamera : MonoBehaviour
{
    public enum Mode { FitInside, Cover }        // Inside = show more; Cover = fill screen
    public Mode mode = Mode.Cover;

    public Camera cam;
    [Tooltip("Scale factor after fitting. <1 zooms out, >1 zooms in.")]
    [Range(0.5f, 1.5f)] public float factor = 1.00f;  // e.g. 0.92 for more art, 1.02 for safe cover
    public Vector2 offset = Vector2.zero;             // nudge without moving camera

    void Reset(){ cam = Camera.main; }
    void Start()
    {
        if (!cam) cam = Camera.main;

        var sr = GetComponent<SpriteRenderer>();
        if (!sr || !sr.sprite) return;

        // camera size in world units
        float camH = cam.orthographicSize * 2f;
        float camW = camH * cam.aspect;

        // sprite size in world units
        Vector2 art = sr.sprite.bounds.size;

        // choose fit
        float baseScale = (mode == Mode.FitInside)
            ? Mathf.Min(camW / art.x, camH / art.y)
            : Mathf.Max(camW / art.x, camH / art.y);

        float s = baseScale * factor;
        transform.localScale = new Vector3(s, s, 1f);
        transform.localPosition = new Vector3(offset.x, offset.y, transform.localPosition.z);
    }
}