using UnityEngine;

// LevelProgress (added Nov 2026): level select and stars, saved per difficulty.
//
// Every level you reach unlocks it on the title screen, so you can start a run there.
// Finishing a level earns up to 3 stars:
//   1 star   finish the level
//   2 stars  ...fast (within the par time: about 4.5 seconds per fly needed)
//   3 stars  ...and get an x5 fly combo somewhere in that level
public static class LevelProgress
{
    public const int LevelCount = 6;

    private static string Key(string what) { return "FNL." + what + "." + GameSettings.Difficulty; }

    // Highest level reached on this difficulty (1 = only the Pond).
    public static int Highest { get { return Mathf.Clamp(PlayerPrefs.GetInt(Key("reached"), 1), 1, LevelCount); } }

    public static bool IsUnlocked(int level) { return level <= Highest || Cheats.Enabled; }

    // The level the next run starts at (picked on the title screen).
    public static int StartLevel
    {
        get
        {
            int l = Mathf.Clamp(PlayerPrefs.GetInt(Key("start"), 1), 1, LevelCount);
            return IsUnlocked(l) ? l : 1;
        }
        set { PlayerPrefs.SetInt(Key("start"), Mathf.Clamp(value, 1, LevelCount)); PlayerPrefs.Save(); GameSettings.NotifyChanged(); }
    }

    public static int Stars(int level) { return PlayerPrefs.GetInt(Key("stars" + level), 0); }

    // Best combo in the current level (GameDirector reports combos here).
    public static int ComboThisLevel;

    public static void Reached(int level)
    {
        ComboThisLevel = 0;
        if (level > Highest)
        {
            PlayerPrefs.SetInt(Key("reached"), Mathf.Clamp(level, 1, LevelCount));
            PlayerPrefs.Save();
        }
    }

    public static float ParSeconds(int flies) { return flies * 4.5f; }

    public static int Complete(int level, float seconds, int flies)
    {
        int stars = 1;
        if (seconds <= ParSeconds(flies)) stars++;
        if (stars == 2 && ComboThisLevel >= 5) stars++;
        if (stars > Stars(level))
        {
            PlayerPrefs.SetInt(Key("stars" + level), stars);
            PlayerPrefs.Save();
        }
        FrogEffects.Say("Level " + level + " done: " + stars + (stars == 1 ? " star" : " stars"));
        if (TotalStars() >= LevelCount * 3) Achievements.Unlock("allstars");
        return stars;
    }

    public static int TotalStars()
    {
        int n = 0;
        for (int l = 1; l <= LevelCount; l++) n += Stars(l);
        return n;
    }
}
