using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class StudioSplash : MonoBehaviour
{
    public float fadeDuration = 1f;
    public float waitDuration = 1.5f;
    public string nextSceneName = "MainMenu";

    Image img;

    void Start()
    {
        img = GetComponent<Image>();
        img.color = new Color(1, 1, 1, 0);
        StartCoroutine(FadeSequence());
    }

    void Update()
    {
        // Skip the splash screen immediately if the user taps/clicks
        if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    IEnumerator FadeSequence()
    {
        // Fade In
        for (float t = 0; t < fadeDuration; t += Time.deltaTime)
        {
            float k = t / fadeDuration;
            img.color = new Color(1, 1, 1, k);
            yield return null;
        }
        img.color = Color.white;

        // Wait
        yield return new WaitForSeconds(waitDuration);

        // Fade Out
        for (float t = 0; t < fadeDuration; t += Time.deltaTime)
        {
            float k = 1f - (t / fadeDuration);
            img.color = new Color(1, 1, 1, k);
            yield return null;
        }
        img.color = new Color(1, 1, 1, 0);

        // Load Main Menu
        SceneManager.LoadScene(nextSceneName);
    }
}