using System;
using System.Collections.Generic;
using UnityEngine;

// Shop (added Nov 2026): permanent upgrades, bought with the flies you eat.
//
// Every fly you eat also goes into your fly wallet (your score and high scores are never
// spent - the wallet is separate). Open the pause menu > Shop to spend it:
//
//   Strong legs  (3 levels)  hop 8% faster per level
//   Fly magnet   (3 levels)  nearby flies drift toward you (3.5 m, 5.5 m, 8 m)
//   Double flies (once)      every fly counts twice (golden flies give 10!)
//   Laser eyes   (once)      press L / X on a gamepad / the zap button to stun the bird
//                            for 4 seconds (recharges in 20 s)
//   Skins and hats           look your best while you dodge
//
// Everything is saved in the browser, so upgrades carry over to every future visit.
// Shop till you hop.
public static class Shop
{
    public static event Action Changed;

    public class Item
    {
        public string id;
        public string name;
        public string description;
        public int[] costs;      // one cost per level
        public string group;     // "upgrade", "skin" or "hat"
        public Color tint;       // skins only
    }

    public static readonly Item[] Items =
    {
        new Item { id = "speed", name = "Strong legs", description = "Hop 8% faster per level", costs = new[] { 20, 45, 90 }, group = "upgrade" },
        new Item { id = "magnet", name = "Fly magnet", description = "Nearby flies drift to you", costs = new[] { 25, 55, 110 }, group = "upgrade" },
        new Item { id = "double", name = "Double flies", description = "Every fly counts twice", costs = new[] { 150 }, group = "upgrade" },
        new Item { id = "laser", name = "Laser eyes", description = "Zap the bird dizzy (L / X / zap)", costs = new[] { 120 }, group = "upgrade" },
        new Item { id = "skin.classic", name = "Classic green", description = "The original frog", costs = new int[0], group = "skin", tint = Color.white },
        new Item { id = "skin.gold", name = "Golden frog", description = "Shiny!", costs = new[] { 60 }, group = "skin", tint = new Color(1f, 0.82f, 0.3f) },
        new Item { id = "skin.blue", name = "Blue dart frog", description = "Cool and deadly-looking", costs = new[] { 60 }, group = "skin", tint = new Color(0.35f, 0.6f, 1.25f) },
        new Item { id = "skin.pink", name = "Pink frog", description = "Pretty in pond", costs = new[] { 60 }, group = "skin", tint = new Color(1.3f, 0.6f, 0.85f) },
        new Item { id = "skin.shadow", name = "Shadow frog", description = "Hard to see at night", costs = new[] { 80 }, group = "skin", tint = new Color(0.35f, 0.35f, 0.42f) },
        new Item { id = "hat.none", name = "No hat", description = "Au naturel", costs = new int[0], group = "hat" },
        new Item { id = "hat.top", name = "Top hat", description = "Very distinguished", costs = new[] { 40 }, group = "hat" },
        new Item { id = "hat.crown", name = "Crown", description = "Ruler of the pond", costs = new[] { 100 }, group = "hat" },
        // Nov 2026: helper buddies (they chase the bird and snake away) and their training.
        new Item { id = "helper.dog", name = "Biscuit the dog", description = "Your loyal helper buddy", costs = new int[0], group = "helper" },
        new Item { id = "helper.cat", name = "Marmalade the cat", description = "Quick to pounce", costs = new[] { 90 }, group = "helper" },
        new Item { id = "helper.wolf", name = "Shadow the wolf", description = "Scares predators longer", costs = new[] { 180 }, group = "helper" },
        new Item { id = "helper.training", name = "Buddy training", description = "Helper recovers faster", costs = new[] { 30, 60, 120 }, group = "upgrade" },
        new Item { id = "helper.guard", name = "Guard zone", description = "Helper guards a bigger area", costs = new[] { 25, 55, 100 }, group = "upgrade" },
    };

    // ---- wallet ----
    public static int Wallet
    {
        get { return PlayerPrefs.GetInt("FNL.wallet", 0); }
        private set { PlayerPrefs.SetInt("FNL.wallet", Mathf.Max(0, value)); }
    }

    public static void Earn(int flies)
    {
        if (flies <= 0) return;
        Wallet += flies;
        PlayerPrefs.SetInt("FNL.lifetimeFlies", PlayerPrefs.GetInt("FNL.lifetimeFlies", 0) + flies);
        Notify(false);
    }

    // ---- ownership ----
    public static int LevelOf(string id)
    {
        Item item = Find(id);
        if (item != null && item.costs.Length == 0) return 1; // free items are always owned
        return PlayerPrefs.GetInt("FNL.shop." + id, 0);
    }

    public static bool Owns(string id) { return LevelOf(id) > 0; }

    public static Item Find(string id)
    {
        foreach (Item item in Items) if (item.id == id) return item;
        return null;
    }

    public static bool IsMaxed(Item item) { return item.costs.Length == 0 || LevelOf(item.id) >= item.costs.Length; }

    public static int NextCost(Item item)
    {
        int level = LevelOf(item.id);
        return level < item.costs.Length ? item.costs[level] : -1;
    }

    public static bool TryBuy(string id)
    {
        Item item = Find(id);
        if (item == null || IsMaxed(item)) return false;
        int cost = NextCost(item);
        if (Wallet < cost) return false;
        Wallet -= cost;
        PlayerPrefs.SetInt("FNL.shop." + id, LevelOf(id) + 1);
        if (item.group != "upgrade") Equip(id);
        Notify(true);
        Achievements.Unlock("shopper");
        return true;
    }

    // ---- skins & hats ----
    public static string EquippedSkin { get { return PlayerPrefs.GetString("FNL.skin", "skin.classic"); } }
    public static string EquippedHat { get { return PlayerPrefs.GetString("FNL.hat", "hat.none"); } }

    public static void Equip(string id)
    {
        Item item = Find(id);
        if (item == null || !Owns(id)) return;
        if (item.group == "skin") PlayerPrefs.SetString("FNL.skin", id);
        if (item.group == "hat") PlayerPrefs.SetString("FNL.hat", id);
        if (item.group == "helper") PlayerPrefs.SetString("FNL.helper", id);
        Notify(true);
    }

    public static string EquippedHelper { get { return PlayerPrefs.GetString("FNL.helper", "helper.dog"); } }

    public static bool IsEquipped(string id) { return EquippedSkin == id || EquippedHat == id || EquippedHelper == id; }

    // ---- helper buddy ----
    public static float HelperCooldown { get { return new[] { 12f, 9f, 7f, 5f }[Mathf.Clamp(LevelOf("helper.training"), 0, 3)]; } }
    public static float HelperGuardRadius { get { return 7f + 2f * LevelOf("helper.guard"); } }

    // Cheats and rewards (daily challenge) add flies without touching the score.
    public static void Grant(int flies)
    {
        if (flies <= 0) return;
        Wallet += flies;
        Notify(true);
    }

    public static void NotifyChanged() { Notify(true); }

    // ---- what the upgrades do ----
    public static float SpeedMultiplier { get { return (1f + 0.08f * LevelOf("speed")) * (Cheats.SuperFrog ? Cheats.SuperSpeed : 1f); } }
    public static int PointsMultiplier { get { return Owns("double") ? 2 : 1; } }
    public static bool HasLaser { get { return Owns("laser") || Cheats.SuperFrog; } }

    public static float MagnetRadius
    {
        get
        {
            if (Cheats.SuperFrog) return Cheats.SuperMagnet;
            switch (LevelOf("magnet"))
            {
                case 1: return 3.5f;
                case 2: return 5.5f;
                case 3: return 8f;
                default: return 0f;
            }
        }
    }

    private static void Notify(bool save)
    {
        if (save) PlayerPrefs.Save();
        if (Changed != null) Changed();
    }
}

// Achievements (added Nov 2026): little goals, listed in Pause > Game.
public static class Achievements
{
    public static event Action<string> Unlocked; // display name

    public static readonly string[][] All =
    {
        new[] { "first", "First bite", "Eat your first fly" },
        new[] { "fiend", "Fly fiend", "Eat 25 flies in one round" },
        new[] { "swamp", "Swamp thing", "Reach the Swamp" },
        new[] { "night", "Night owl", "Reach the Night level" },
        new[] { "survivor", "Survivor", "Last 60 seconds in one round" },
        new[] { "golden", "Golden touch", "Eat a golden fly" },
        new[] { "dizzy", "Dizzy bird", "Stun the bird 3 times" },
        new[] { "shopper", "Shopper", "Buy something in the shop" },
        new[] { "survived", "Owl dodger", "Survive the Great Owl and see the credits" },
        new[] { "combo", "Combo king", "Reach an x8 fly combo" },
        new[] { "buddy", "Best buddy", "Your helper chases off 5 predators" },
        new[] { "daily", "Daily hopper", "Finish a daily challenge" },
        new[] { "lake", "Lily hopper", "Reach Lily Pad Lake" },
        new[] { "winter", "Snow frog", "Reach the Winter Freeze" },
        new[] { "allstars", "Star frog", "Earn all 3 stars on every level" },
        new[] { "tongue", "Long tongue", "Catch 25 flies with your tongue" },
    };

    public static bool Has(string id) { return PlayerPrefs.GetInt("FNL.ach." + id, 0) == 1; }

    public static int Count
    {
        get
        {
            int n = 0;
            foreach (string[] a in All) if (Has(a[0])) n++;
            return n;
        }
    }

    public static void Unlock(string id)
    {
        if (Has(id)) return;
        PlayerPrefs.SetInt("FNL.ach." + id, 1);
        PlayerPrefs.Save();
        foreach (string[] a in All)
        {
            if (a[0] == id && Unlocked != null) Unlocked(a[1]);
        }
    }

    public static void CountStun()
    {
        int stuns = PlayerPrefs.GetInt("FNL.stuns", 0) + 1;
        PlayerPrefs.SetInt("FNL.stuns", stuns);
        if (stuns >= 3) Unlock("dizzy");
    }
}
