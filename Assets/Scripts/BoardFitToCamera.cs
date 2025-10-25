using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class BoardFitToCamera : MonoBehaviour
{
    public Camera cam;
    [Range(0.5f,1.1f)] public float fill = 0.90f;   // how much of the view the board should occupy
    public Vector2 offset = Vector2.zero;

    void Reset(){ cam = Camera.main; }

    void OnEnable() { StartCoroutine(FitNextFrame()); }   // wait for loader
    IEnumerator FitNextFrame()
    {
        if (!cam) cam = Camera.main;
        // wait a frame so RuneBoardLoader.Start() can place sprites
        yield return null;
        DoFit();
    }

    public void DoFit()
    {
        var renderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length == 0) return;

        // compute current world bounds of all tiles/plate/shadow children
        Bounds b = new Bounds(renderers[0].bounds.center, Vector3.zero);
        foreach (var r in renderers) if (r.enabled && r.sprite) b.Encapsulate(r.bounds);

        float camH = cam.orthographicSize * 2f;
        float camW = camH * cam.aspect;

        float targetW = camW * fill;
        float targetH = camH * fill;

        float s = Mathf.Min(targetW / b.size.x, targetH / b.size.y);

        // apply relative scale around current pivot
        transform.localScale *= s;

        // recentre the whole board
        Vector3 p = transform.position;
        Vector3 delta = b.center - p;
        transform.position = p - delta + new Vector3(offset.x, offset.y, 0f);
    }
}