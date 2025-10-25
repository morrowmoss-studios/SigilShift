using UnityEngine;

[ExecuteAlways]
public class MatchSpriteMaskToUIRect : MonoBehaviour
{
    public Camera cam;
    public RectTransform aperture;  // FrameAperture in ForegroundCanvas
    public float padding = 0f;      // world units; 0.02 if you see a 1px seam

    void LateUpdate()
    {
        if (!cam) cam = Camera.main;
        if (!cam || !aperture) return;

        var corners = new Vector3[4];
        aperture.GetWorldCorners(corners); // world space corners (SS-Camera canvas)

        Vector3 bl = corners[0];   // bottom-left
        Vector3 tr = corners[2];   // top-right
        Vector3 center = (bl + tr) * 0.5f;
        Vector3 size = tr - bl;

        size += new Vector3(padding * 2f, padding * 2f, 0f);

        // assumes mask sprite is a 1x1 unit square
        transform.position = new Vector3(center.x, center.y, transform.position.z);
        transform.localScale = new Vector3(size.x, size.y, 1f);
    }
}