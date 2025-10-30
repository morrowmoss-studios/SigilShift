using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class QuitPopup : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] CanvasGroup group;          // CanvasGroup on QuitPopup
    [SerializeField] Button confirmButton;       // your sprite Button
    [SerializeField] Button cancelButton;        // your sprite Button
    [SerializeField] TMP_Text header;            // optional
    [SerializeField] TMP_Text body;              // optional

    [Header("Animation")]
    [SerializeField, Range(0.01f, 0.6f)] float fadeTime = 0.18f;
    [SerializeField, Range(0.85f, 1.2f)] float popScale = 1.03f;

    Vector3 _startScale;

    void Awake()
    {
        if (!group) group = GetComponent<CanvasGroup>();
        _startScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
        HideInstant();

        if (confirmButton) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton)  cancelButton.onClick.AddListener(Hide);
    }

    public void SetTexts(string headerText, string bodyText)
    {
        if (header) header.text = headerText;
        if (body)   body.text   = bodyText;
    }

    // ——— API you’ll call from your Pause/Quit button ———
    public void Show()
    {
        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(CoFade(visible:true));
    }

    public void Hide()  { StopAllCoroutines(); StartCoroutine(CoFade(visible:false)); }
    public void HideInstant()
    {
        gameObject.SetActive(false);
        if (group)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
        transform.localScale = _startScale;
    }

    System.Collections.IEnumerator CoFade(bool visible)
    {
        float t = 0f, dur = Mathf.Max(0.01f, fadeTime);
        float a0 = group.alpha;
        float a1 = visible ? 1f : 0f;
        Vector3 s0 = _startScale * (visible ? 1f/popScale : 1f);
        Vector3 s1 = _startScale * (visible ? 1f : 1f/popScale);

        if (visible)
        {
            group.blocksRaycasts = true;
            group.interactable = true;
            transform.localScale = s0;
        }

        while (t < dur)
        {
            t += Time.unscaledDeltaTime; // unaffected by pause
            float u = Mathf.Clamp01(t / dur);
            group.alpha = Mathf.Lerp(a0, a1, u);
            transform.localScale = Vector3.Lerp(s0, s1, u);
            yield return null;
        }

        group.alpha = a1;
        transform.localScale = s1;

        if (!visible)
        {
            group.blocksRaycasts = false;
            group.interactable = false;
            gameObject.SetActive(false);
        }
    }

    void OnConfirm()
    {
        // call your existing UIManager.QuitGame()
        var ui = FindObjectOfType<UIManager>();
        if (ui) ui.QuitGame();
#if UNITY_EDITOR
        // In case UIManager isn’t in this scene, fall back:
        UnityEditor.EditorApplication.isPlaying = false;
#endif
        Application.Quit();
    }
}
