using UnityEngine;

public static class LevelProgress
{
    private const string HighestLevelKey = "SS_HighestLevelUnlocked";

    // Default: only Level 1 is unlocked
    public static int GetHighestUnlocked()
    {
        return PlayerPrefs.GetInt(HighestLevelKey, 1);
    }

    // Raise the highest unlocked level if needed
    public static void UnlockUpTo(int levelNumber)
    {
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