using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class LevelLockVisual : MonoBehaviour
{
    [Header("Level Info")]
    public int levelIndex = 1;           // 1..30, set per button in Inspector

    [Header("Visuals")]
    public GameObject lockIcon;          // padlock child
    public Image buttonImage;            // ring/number image to tint (optional)
    public Color lockedColor = Color.grey;
    public Color unlockedColor = Color.white;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();

        // auto-grab image on this object if you forget to assign one
        if (buttonImage == null)
            buttonImage = GetComponent<Image>();
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        int highest = LevelProgress.GetHighestUnlocked();
        bool isUnlocked = levelIndex <= highest;

        if (lockIcon != null)
            lockIcon.SetActive(!isUnlocked);

        if (buttonImage != null)
            buttonImage.color = isUnlocked ? unlockedColor : lockedColor;

        if (_button != null)
            _button.interactable = isUnlocked;

        Debug.Log($"[LevelLockVisual] {gameObject.name} | levelIndex={levelIndex}, " +
                  $"highest={highest}, isUnlocked={isUnlocked}, lockActive={!isUnlocked}");
    }

}