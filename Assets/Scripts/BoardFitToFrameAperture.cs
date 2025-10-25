using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class BoardFitToFrameAperture : MonoBehaviour
{
    public Camera cam;
    public RectTransform aperture;           // FrameAperture
    [Range(0.75f, 0.99f)] public float padding = 0.95f;
    public Vector2 offset = Vector2.zero;

    Vector3 _baseScale;

    void Awake(){ _baseScale = transform.localScale; }
    void OnEnable(){ StartCoroutine(FitAfterLayout()); }

    IEnumerator FitAfterLayout()
    {
        if (!cam) cam = Camera.main;
        yield return null;                    // wait tiles to spawn
        yield return new WaitForEndOfFrame(); // wait UI layout
        DoFit();
    }

    public void DoFit()
    {
        if (!cam || !aperture) return;

        // aperture world rect
        var corners = new Vector3[4];
        aperture.GetWorldCorners(corners);
        Bounds view = new Bounds(corners[0], Vector3.zero);
        for (int i = 1; i < 4; i++) view.Encapsulate(corners[i]);

        float availW = view.size.x * padding;
        float availH = view.size.y * padding;

        // board bounds
        var rends = GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return;

        Bounds board = new Bounds(rends[0].bounds.center, Vector3.zero);
        foreach (var r in rends) if (r.enabled) board.Encapsulate(r.bounds);

        // scale to fit
        float s = Mathf.Min(availW / board.size.x, availH / board.size.y);
        transform.localScale = _baseScale * s;

        // recalc + center
        board = new Bounds(rends[0].bounds.center, Vector3.zero);
        foreach (var r in rends) if (r.enabled) board.Encapsulate(r.bounds);

        Vector3 delta = view.center - board.center;
        transform.position += new Vector3(delta.x + offset.x, delta.y + offset.y, 0f);
    }
}