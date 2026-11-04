using UnityEngine;

// Cheats (added Nov 2026): Pause > Cheats, for anyone who just wants to unlock everything
// and play around. Off by default.
//
//   Unlock everything   every shop upgrade maxed, every skin, hat and helper buddy
//   +1000 flies         straight into your fly wallet
//   All achievements    unlocks every achievement
//   Skip to next level  jump ahead (Pond -> Swamp -> Night)
//   Super frog          hop 60% faster, can't be caught, huge fly magnet, laser recharges fast
//   Slow bird           the bird flies at half speed
//   Fly frenzy          twice as many flies
//
// While Super frog, Slow bird or Fly frenzy is on, best scores aren't saved, so the real
// records stay fair. Everything else you unlock with cheats is yours to keep.
// Up, up, down, down, left, right, left, right, ribbit, croak.
public static class Cheats
{
    public static bool Enabled
    {
        get { return GameSettings.Flag("cheats", false); }
        set
        {
            GameSettings.SetFlag("cheats", value);
            if (!value) { SuperFrog = false; SlowBird = false; FlyFrenzy = false; }
        }
    }

    public static bool SuperFrog { get { return Enabled && GameSettings.Flag("cheat.super", false); } set { GameSettings.SetFlag("cheat.super", value); } }
    public static bool SlowBird { get { return Enabled && GameSettings.Flag("cheat.slowbird", false); } set { GameSettings.SetFlag("cheat.slowbird", value); } }
    public static bool FlyFrenzy { get { return Enabled && GameSettings.Flag("cheat.frenzy", false); } set { GameSettings.SetFlag("cheat.frenzy", value); } }

    // True while a cheat that changes how the round plays is on (best scores aren't saved).
    public static bool AffectsScore { get { return SuperFrog || SlowBird || FlyFrenzy; } }

    public static void UnlockEverything()
    {
        foreach (Shop.Item item in Shop.Items)
        {
            if (item.costs.Length > 0) PlayerPrefs.SetInt("FNL.shop." + item.id, item.costs.Length);
        }
        PlayerPrefs.Save();
        Shop.NotifyChanged();
    }

    public static void AddFlies(int flies)
    {
        Shop.Grant(flies);
    }

    public static void UnlockAllAchievements()
    {
        foreach (string[] a in Achievements.All) Achievements.Unlock(a[0]);
    }

    public static bool SkipLevel()
    {
        LevelManager levels = LevelManager.Instance;
        if (levels == null || levels.IsFinalLevel) return false;
        levels.GoToLevel(levels.Level + 1);
        return true;
    }

    // Used by the frog's speed, the fly magnet and the laser.
    public const float SuperSpeed = 1.6f;
    public const float SuperMagnet = 10f;
    public const float SuperLaserCooldown = 4f;
}
