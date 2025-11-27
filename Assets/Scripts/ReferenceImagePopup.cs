using UnityEngine;
using UnityEngine.UI;

public class ReferenceImagePopup : MonoBehaviour
{
    [Header("Popup Wiring")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Image popupImage;
    [SerializeField] private Sprite referenceSprite;

    [Header("Puzzle Lock (optional)")]
    [SerializeField] private RunePuzzleManager puzzleManager;

    private bool isOpen = false;

    void Awake()
    {
        if (!popupRoot)
            popupRoot = gameObject;
    }

    void Start()
    {
        if (popupImage && referenceSprite)
            popupImage.sprite = referenceSprite;

        if (popupRoot)
            popupRoot.SetActive(false);
    }

    // Called by the ImageButton
    public void OpenPopup()
    {
        isOpen = true;
        if (popupRoot)
            popupRoot.SetActive(true);

        if (puzzleManager)
            puzzleManager.SetInputLocked(true);
    }

    // Called when player taps the overlay
    public void ClosePopup()
    {
        isOpen = false;
        if (popupRoot)
            popupRoot.SetActive(false);

        if (puzzleManager)
            puzzleManager.SetInputLocked(false);
    }

    // still here if you ever want a toggle button
    public void TogglePopup()
    {
        if (isOpen) ClosePopup();
        else        OpenPopup();
    }
}