using System;
using UnityEngine;

// GameSettings (added Nov 2026): every player choice in one place, saved between visits.
//
// On the web, PlayerPrefs is stored in the browser (IndexedDB), so a player's difficulty,
// volume, graphics, filter, key bindings and best scores are still there next time they
// open the game. Anything that cares about a setting listens to GameSettings.Changed.
//
// It's not hoarding if it's PlayerPrefs. It's "persistence". Ribbit.
public enum Difficulty { Easy = 0, Medium = 1, Hard = 2 }
public enum GraphicsTier { Auto = 0, Low = 1, Medium = 2, High = 3 }
// Frame rate cap (Nov 2026). MatchScreen = as fast as the screen refreshes (60, 120, 144, 240...).
public enum FrameRateCap { Fps30 = 0, Fps60 = 1, MatchScreen = 2 }
public enum ScreenFilterMode { None = 0, Retro = 1, BlackAndWhite = 2, Night = 3, Cartoon = 4, ColorBlindFriendly = 5, Vintage = 6 }

public static class GameSettings
{
    // Fired after any setting changes, so menus, audio and graphics can update live.
    public static event Action Changed;

    private const string Prefix = "FNL.";

    public static Difficulty Difficulty
    {
        get { return (Difficulty)Mathf.Clamp(GetInt("difficulty", (int)Difficulty.Medium), 0, 2); }
        set { SetInt("difficulty", (int)value); }
    }

    public static GraphicsTier Graphics
    {
        get { return (GraphicsTier)Mathf.Clamp(GetInt("graphics", (int)GraphicsTier.Auto), 0, 3); }
        set { SetInt("graphics", (int)value); }
    }

    public static ScreenFilterMode Filter
    {
        get { return (ScreenFilterMode)Mathf.Clamp(GetInt("filter", 0), 0, 6); }
        set { SetInt("filter", (int)value); }
    }

    public static FrameRateCap FrameRate
    {
        get { return (FrameRateCap)Mathf.Clamp(GetInt("frameRate", (int)FrameRateCap.MatchScreen), 0, 2); }
        set { SetInt("frameRate", (int)value); }
    }

    // ---- Nov 2026: mini map, helper buddy, cheats ----
    public static bool MiniMap { get { return Flag("map", true); } set { SetFlag("map", value); } }
    public static bool MiniMapLarge { get { return Flag("map.large", false); } set { SetFlag("map.large", value); } }
    public static bool MapFlies { get { return Flag("map.flies", true); } set { SetFlag("map.flies", value); } }
    public static bool MapPowerUps { get { return Flag("map.powerups", true); } set { SetFlag("map.powerups", value); } }
    public static bool MapEnemies { get { return Flag("map.enemies", true); } set { SetFlag("map.enemies", value); } }
    public static bool MapHazards { get { return Flag("map.hazards", true); } set { SetFlag("map.hazards", value); } }
    public static bool MapHelper { get { return Flag("map.helper", true); } set { SetFlag("map.helper", value); } }
    public static bool HelperOn { get { return Flag("helperOn", true); } set { SetFlag("helperOn", value); } }

    // ---- Nov 2026: clock mode (ClockMode.cs) ----
    public static bool Clock24h { get { return Flag("clock.24h", false); } set { SetFlag("clock.24h", value); } }
    public static bool ClockSeconds { get { return Flag("clock.seconds", false); } set { SetFlag("clock.seconds", value); } }
    public static bool ClockDate { get { return Flag("clock.date", true); } set { SetFlag("clock.date", value); } }
    public static bool ClockSound { get { return Flag("clock.sound", false); } set { SetFlag("clock.sound", value); } }
    public static bool ClockDim { get { return Flag("clock.dim", true); } set { SetFlag("clock.dim", value); } }
    public static bool ClockAtStart { get { return Flag("clock.atStart", false); } set { SetFlag("clock.atStart", value); } }
    public static int ClockSize
    {
        get { return Mathf.Clamp(GetInt("clock.size", 1), 0, 2); }
        set { SetInt("clock.size", Mathf.Clamp(value, 0, 2)); }
    }

    public static bool Flag(string key, bool fallback) { return GetInt(key, fallback ? 1 : 0) == 1; }
    public static void SetFlag(string key, bool value) { SetInt(key, value ? 1 : 0); }

    public static bool ShowFps
    {
        get { return GetInt("showFps", 0) == 1; }
        set { SetInt("showFps", value ? 1 : 0); }
    }

    // Volumes are 0..1. "On" switches let players mute a group without losing their slider spot.
    public static float MasterVolume { get { return GetFloat("vol.master", 1f); } set { SetFloat("vol.master", value); } }
    public static float MusicVolume { get { return GetFloat("vol.music", 0.8f); } set { SetFloat("vol.music", value); } }
    public static float EffectsVolume { get { return GetFloat("vol.sfx", 1f); } set { SetFloat("vol.sfx", value); } }
    public static bool MusicOn { get { return GetInt("on.music", 1) == 1; } set { SetInt("on.music", value ? 1 : 0); } }
    public static bool EffectsOn { get { return GetInt("on.sfx", 1) == 1; } set { SetInt("on.sfx", value ? 1 : 0); } }

    // Best score per difficulty, so a record on Easy never overwrites a record on Hard.
    public static int GetBestScore(Difficulty difficulty)
    {
        return GetInt("best." + (int)difficulty, 0);
    }

    // Returns true if this score is a new best (and saves it).
    public static bool SubmitScore(Difficulty difficulty, int score)
    {
        if (score <= GetBestScore(difficulty))
        {
            return false;
        }
        SetInt("best." + (int)difficulty, score);
        return true;
    }

    // Key bindings live in KeyBindings.cs but are saved here with everything else.
    public static KeyCode GetKey(string action, KeyCode fallback)
    {
        int value = GetInt("key." + action, (int)fallback);
        return Enum.IsDefined(typeof(KeyCode), value) ? (KeyCode)value : fallback;
    }

    public static void SetKey(string action, KeyCode key)
    {
        SetInt("key." + action, (int)key);
    }

    public static void ClearKey(string action)
    {
        PlayerPrefs.DeleteKey(Prefix + "key." + action);
        Save();
    }

    // ---- storage helpers ----
    private static int GetInt(string key, int fallback) { return PlayerPrefs.GetInt(Prefix + key, fallback); }
    private static float GetFloat(string key, float fallback) { return Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + key, fallback)); }

    private static void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(Prefix + key, value);
        Save();
    }

    private static void SetFloat(string key, float value)
    {
        PlayerPrefs.SetFloat(Prefix + key, Mathf.Clamp01(value));
        Save();
    }

    // For settings stored elsewhere (e.g. LevelProgress): save and tell the menus to refresh.
    public static void NotifyChanged() { Save(); }

    private static void Save()
    {
        PlayerPrefs.Save();
        if (Changed != null)
        {
            Changed();
        }
    }
}

// How each difficulty changes the game. Medium is the original 2017 game.
public static class DifficultyRules
{
    public static float BirdSpeedMultiplier(Difficulty d)
    {
        switch (d)
        {
            case Difficulty.Easy: return 0.75f;
            case Difficulty.Hard: return 1.3f;
            default: return 1f;
        }
    }

    public static int FlyMinimum(Difficulty d, int mediumMinimum)
    {
        switch (d)
        {
            case Difficulty.Easy: return mediumMinimum + 4;
            case Difficulty.Hard: return Mathf.Max(6, mediumMinimum - 4);
            default: return mediumMinimum;
        }
    }

    public static string Describe(Difficulty d)
    {
        switch (d)
        {
            case Difficulty.Easy: return "Slower bird, more flies";
            case Difficulty.Hard: return "Faster bird, fewer flies";
            default: return "The classic game";
        }
    }
}
