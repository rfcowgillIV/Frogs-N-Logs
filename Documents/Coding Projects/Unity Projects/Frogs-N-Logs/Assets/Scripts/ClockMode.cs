using System;
using UnityEngine;

// Clock mode (added Nov 2026): Frogs-N-Logs as a living clock for a spare screen.
//
// The frog plays by itself (ClockPilot.cs), the current time and date float over the pond
// (FrogsUIClock.cs), and the world follows the real time of day and season:
//
//   after dark (9 pm - 6 am)   Night
//   dusk (6 pm - 9 pm)         The Swamp
//   daytime                    Winter Freeze (Dec-Feb), Lily Pad Lake (Mar-May),
//                              The Pond (Jun-Aug), Autumn Woods (Sep-Nov)
//
// Nothing in clock mode counts: no flies for the wallet, no achievements, no levels
// unlocked, no best scores. And the frog can't lose; the bird just bounces off, dizzy.
public static class ClockMode
{
    // True while the clock is showing.
    public static bool Active;

    // Once someone leaves the clock, "Start in clock mode" doesn't pull them back in until
    // the game is opened again.
    public static bool ExitedThisSession;

    // For the automated play tests: pretend it's this time.
    public static DateTime? TestNow;

    public static DateTime Now { get { return TestNow ?? DateTime.Now; } }

    // The level that suits this moment.
    public static int LevelFor(DateTime t)
    {
        int h = t.Hour;
        if (h >= 21 || h < 6) return 3;         // Night
        if (h >= 18) return 2;                  // The Swamp at dusk
        switch (t.Month)
        {
            case 12: case 1: case 2: return 6;  // Winter Freeze
            case 3: case 4: case 5: return 4;   // Lily Pad Lake
            case 9: case 10: case 11: return 5; // Autumn Woods
            default: return 1;                  // The Pond in summer
        }
    }

    public static string TimeText(DateTime t)
    {
        bool h24 = GameSettings.Clock24h;
        bool seconds = GameSettings.ClockSeconds;
        string time = h24 ? t.ToString(seconds ? "HH:mm:ss" : "HH:mm") : t.ToString(seconds ? "h:mm:ss" : "h:mm");
        return h24 ? time : time + "<size=" + AmPmSize + ">  " + t.ToString("tt", System.Globalization.CultureInfo.InvariantCulture) + "</size>";
    }

    // Set by the clock screen so AM/PM sits smaller than the time.
    public static int AmPmSize = 60;

    public static string DateText(DateTime t)
    {
        return t.ToString("dddd, MMMM d", System.Globalization.CultureInfo.InvariantCulture);
    }

    // Late at night the clock can dim itself (11 pm - 6 am).
    public static bool IsDimTime(DateTime t)
    {
        return t.Hour >= 23 || t.Hour < 6;
    }

    // How big the clock is (Size button): small, medium, large.
    public static readonly float[] Sizes = { 0.7f, 1f, 1.35f };
    public static readonly string[] SizeNames = { "Small", "Medium", "Large" };
}
