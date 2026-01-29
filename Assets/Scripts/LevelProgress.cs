using UnityEngine;

public static class LevelProgress
{
    private const string HighestLevelKey = "SS_HighestLevelUnlocked";

    // ================================
    //  BETA TOGGLE
    // ================================
    // Set this TRUE while testing / beta:
    //  - All levels will behave as unlocked
    //  - UnlockUpTo() won't write anything to PlayerPrefs
    //
    // Set to FALSE for your real release build.
    public static bool betaAllUnlocked = false;

    // Default: only Level 1 is unlocked (in non-beta)
    public static int GetHighestUnlocked()
    {
        if (betaAllUnlocked)
        {
            // Pretend everything is unlocked.
            // 999 is just a big number greater than any real level index.
            return 999;
        }

        return PlayerPrefs.GetInt(HighestLevelKey, 1);
    }

    // Raise the highest unlocked level if needed
    public static void UnlockUpTo(int levelNumber)
    {
        if (betaAllUnlocked)
        {
            // In beta, don't touch saved progress at all.
            Debug.Log($"[LevelProgress] (BETA) UnlockUpTo({levelNumber}) ignored – betaAllUnlocked is TRUE.");
            return;
        }

        int current = GetHighestUnlocked();
        if (levelNumber > current)
        {
            PlayerPrefs.SetInt(HighestLevelKey, levelNumber);
            PlayerPrefs.Save();

            Debug.Log($"[LevelProgress] Highest unlocked set to {levelNumber}");
        }
        else
        {
            Debug.Log($"[LevelProgress] UnlockUpTo({levelNumber}) called, " +
                      $"but current highest is {current} – no change.");
        }
    }

    // Optional: call this once from a debug menu if you want to wipe progress
    public static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(HighestLevelKey);
    }
}