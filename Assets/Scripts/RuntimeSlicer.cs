using UnityEngine;
using System.Collections.Generic;

public static class RuntimeSlicer
{
    public static Sprite[] Slice(Texture2D tex, int rows, int cols, float pixelsPerUnit = 100f)
    {
        var list = new List<Sprite>(rows * cols);
        int w = tex.width / cols;
        int h = tex.height / rows;

        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            var rect = new Rect(c * w, (rows - 1 - r) * h, w, h); // flip Y for Unity
            var pivot = new Vector2(0.5f, 0.5f);
            var s = Sprite.Create(tex, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            list.Add(s);
        }
        return list.ToArray();
    }
}