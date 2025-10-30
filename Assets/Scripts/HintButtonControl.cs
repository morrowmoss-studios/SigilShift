using UnityEngine;

public class HintButtonControl : MonoBehaviour
{
    [SerializeField] private RunePuzzleManager manager;

    // Wire this to the Button's OnClick()
    public void OnHintClicked()
    {
        if (!manager) manager = FindObjectOfType<RunePuzzleManager>();
        if (manager)  manager.ShowHint();
    }
}