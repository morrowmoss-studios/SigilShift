using UnityEngine;
using System.Reflection;

public class GameSettingsBootstrap : MonoBehaviour
{
    void Start()
    {
        // Read saved prefs
        int  sizeIdx = PlayerPrefs.GetInt("SS_PuzzleSizeIndex", 0); // 0=3x3,1=6x6,2=9x9
        bool rotOn   = PlayerPrefs.GetInt("SS_RotationEnabled", 1) == 1;
        bool sfxOn   = PlayerPrefs.GetInt("SS_SFXEnabled", 1) == 1;

        // Map index -> size
        int size = 3;
        switch (Mathf.Clamp(sizeIdx, 0, 2))
        {
            case 1: size = 6; break;
            case 2: size = 9; break;
        }

        var manager = FindObjectOfType<RunePuzzleManager>();
        var loader  = FindObjectOfType<RuneBoardLoader>();

        if (manager && loader)
        {
            // apply dims + rotation
            manager.rows = size;
            manager.cols = size;
            manager.rotationEnabled      = rotOn;
            manager.rotationQuarterTurns = rotOn ? 4 : 1;

            if (loader.config)
            {
                loader.config.rows = size;
                loader.config.cols = size;
                loader.config.enableRotation = rotOn;
                loader.config.quarterTurns   = rotOn ? 4 : 1;
            }

            // If you implemented Loader.RebuildBoard(r,c), call it
            MethodInfo rebuild = typeof(RuneBoardLoader).GetMethod(
                "RebuildBoard", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (rebuild != null && rebuild.GetParameters().Length == 2)
            {
                rebuild.Invoke(loader, new object[] { size, size });
            }

            manager.ResetToSolved();
            manager.ShuffleRandomWalk(manager.shuffleSteps);

            // ---- NEW: apply SFX mute via manager helper (no tags, no clip names)
            manager.ApplySfxMute(!sfxOn);
        }
        else
        {
            // If manager isn't present yet, at least set Time.timeScale etc. if you ever need to.
            // SFX mute will be applied once the manager exists (UIManager also calls it when available).
        }
    }
}
