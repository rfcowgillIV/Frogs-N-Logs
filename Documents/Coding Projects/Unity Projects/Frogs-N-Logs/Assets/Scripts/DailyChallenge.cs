using System;
using UnityEngine;

// DailyChallenge (added Nov 2026): a new little goal every day, shown on the title screen.
// Finish it for 40 bonus flies in your wallet (and the "Daily hopper" achievement).
// Everyone gets the same challenge on the same day.
public static class DailyChallenge
{
    public const int Reward = 40;

    private static readonly string[] Goals =
    {
        "Eat 25 flies in one round",
        "Reach the Swamp",
        "Keep hopping for 90 seconds in one round",
        "Get an x5 fly combo",
        "Collect 3 power-ups in one round",
        "Let your helper buddy chase off 2 predators in one round",
        "Reach the Night",
    };

    public static int PowerUpsThisRound;
    public static int BestComboThisRound;

    // Lets the play tests pick a day.
    public static DateTime? TestDate;
    private static DateTime Today { get { return (TestDate ?? DateTime.Now).Date; } }
    private static string Key { get { return Today.ToString("yyyyMMdd"); } }

    public static int Index
    {
        get
        {
            DateTime d = Today;
            int days = (int)(d - new DateTime(2026, 1, 1)).TotalDays;
            return ((days % Goals.Length) + Goals.Length) % Goals.Length;
        }
    }

    public static string Goal { get { return Goals[Index]; } }
    public static bool Done { get { return PlayerPrefs.GetString("FNL.daily.done", "") == Key; } }

    public static void ResetRound()
    {
        PowerUpsThisRound = 0;
        BestComboThisRound = 0;
    }

    // Called every frame while a round is running.
    public static void Check(int score, float roundTime, int level, int helperChases)
    {
        if (Done) return;
        bool met;
        switch (Index)
        {
            case 0: met = score >= 25; break;
            case 1: met = level >= 2; break;
            case 2: met = roundTime >= 90f; break;
            case 3: met = BestComboThisRound >= 5; break;
            case 4: met = PowerUpsThisRound >= 3; break;
            case 5: met = helperChases >= 2; break;
            default: met = level >= 3; break;
        }
        if (!met) return;
        PlayerPrefs.SetString("FNL.daily.done", Key);
        Shop.Grant(Reward);
        Achievements.Unlock("daily");
        FrogEffects.Say("Daily challenge done! +" + Reward + " flies");
        AudioDirector.Play("Win", 0.7f);
    }
}
