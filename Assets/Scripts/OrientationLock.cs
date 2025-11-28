using UnityEngine;

[DisallowMultipleComponent]
public class OrientationLock : MonoBehaviour
{
    void Awake()
    {
        // Force landscape and kill portrait
        Screen.autorotateToLandscapeLeft  = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.autorotateToPortrait       = false;
        Screen.autorotateToPortraitUpsideDown = false;

        Screen.orientation = ScreenOrientation.LandscapeLeft;

        DontDestroyOnLoad(gameObject);
    }
}