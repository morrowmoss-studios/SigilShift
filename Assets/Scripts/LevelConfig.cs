using UnityEngine;

[CreateAssetMenu(menuName = "SigilShift/Level Config")]
public class LevelConfig : ScriptableObject
{
    [Header("Board")]
    public int rows = 3;
    public int cols = 3;

    [Header("Art")]
    public Texture2D sourceTexture;     // single big sigil image (Read/Write enabled)
    public float tileScale = 0.85f;     // like your current loader’s scale

    [Header("Rotation")]
    public bool enableRotation = false;
    [Range(1, 4)] public int quarterTurns = 4; // 4 = 0/90/180/270 (set 2 for 0/180 only)

    [Header("Blank")]
    public int blankIndex = -1;         // -1 = last tile; else 0..rows*cols-1

    [Header("Shuffle")]
    public int shuffleSteps = 90;
}