using UnityEngine;

public class ParallaxFollow : MonoBehaviour
{
    public Transform target;                // drag Main Camera or RuneBoard root
    public Vector2 strength = new(0.02f, 0.015f);
    Vector3 startBg, startTarget;
    void Start(){ if (!target) target = Camera.main.transform; startBg = transform.position; startTarget = target.position; }
    void LateUpdate(){ var d = target.position - startTarget; transform.position = startBg + new Vector3(d.x*strength.x, d.y*strength.y, 0f); }
}