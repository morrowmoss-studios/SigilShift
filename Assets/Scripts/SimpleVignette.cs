using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class SimpleVignette : MonoBehaviour
{
    [Range(0f,1f)] public float inner = 0.62f;   // larger = thinner vignette
    [Range(0f,1f)] public float softness = 0.36f;

    void OnEnable()
    {
        int s = 1024; // crisp edges
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false){ wrapMode = TextureWrapMode.Clamp };
        var center = new Vector2(0.5f, 0.5f);
        for (int y=0;y<s;y++)
        for (int x=0;x<s;x++){
            var uv = new Vector2((x+0.5f)/s, (y+0.5f)/s);
            float d = Vector2.Distance(uv, center);
            float a = Mathf.SmoothStep(inner, inner - softness, d);
            tex.SetPixel(x,y,new Color(0,0,0,a));
        }
        tex.Apply(false,false);
        GetComponent<Image>().sprite = Sprite.Create(tex, new Rect(0,0,s,s), new Vector2(0.5f,0.5f), 100f);
    }
}