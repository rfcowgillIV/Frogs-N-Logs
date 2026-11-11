using System;
using UnityEngine;

// LevelManager (added Nov 2026): six levels, each with a fly goal to reach the next.
//
//   Level 1  The Pond       - the classic sunny pond
//   Level 2  The Swamp      - green fog rolls in; logs drift through and a snake wakes up
//   Level 3  Night          - moonlight and fog; the bird hunts faster every 10 flies
//   Level 4  Lily Pad Lake  - dawn; the lake floods (water slows you, lily pads don't),
//                             a pike lunges from the water, dragonflies are worth 3
//   Level 5  Autumn Woods   - golden afternoon; leaf piles hide bonus flies, giant mushrooms
//                             bounce you, a fox hunts on the ground, watch out for bees
//   Level 6  Winter Freeze  - snowy evening; slippery ice, thin ice cracks, the bird is now a
//                             snowy owl, and at the end the Great Owl swoops in: survive it
//                             and the credits roll.
//
// Each level needs its own number of flies (more on Hard). The level changes in place
// (lighting, ground, weather, hazards), so there's no loading screen and the run keeps its
// score. Levels you've reached can be picked on the title screen (LevelProgress.cs).
//
// From pond to swamp to moonlit chomp.
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    // Fired when a new level begins: (level number starting at 1, level name).
    public static event Action<int, string> LevelChanged;

    // Fired once per run when you beat the last level (and the Great Owl): credits roll!
    public static event Action Finished;
    // Fired when the last level's fly goal is reached: the Great Owl arrives (BossOwl.cs).
    public static event Action BossStarted;
    public bool HasFinished { get; private set; }
    public bool BossActive { get; private set; }

    // Flies needed in the last level before the Great Owl arrives.
    public static int FinalGoal(Difficulty d)
    {
        int[] g = Goals(d);
        return g[g.Length - 1];
    }

    public static readonly string[] Names = { "The Pond", "The Swamp", "Night", "Lily Pad Lake", "Autumn Woods", "Winter Freeze" };

    public int Level { get; private set; } = 1;
    public string LevelName { get { return Names[Mathf.Clamp(Level - 1, 0, Names.Length - 1)]; } }
    public bool IsFinalLevel { get { return Level >= Names.Length; } }

    // The score you need to leave the current level (-1 once the game is finished).
    public int NextGoal
    {
        get
        {
            if (HasFinished || BossActive) return -1;
            return LevelStartScore + Goals(GameSettings.Difficulty)[Level - 1];
        }
    }

    // Flies needed IN each level (not totals), for Easy / Medium / Hard.
    public static int[] Goals(Difficulty d)
    {
        switch (d)
        {
            case Difficulty.Easy: return new[] { 8, 10, 12, 12, 12, 14 };
            case Difficulty.Hard: return new[] { 12, 16, 20, 20, 22, 24 };
            default: return new[] { 10, 14, 16, 16, 18, 20 };
        }
    }

    // The score when the current level began, and how long ago that was (for the stars).
    public int LevelStartScore { get; private set; }
    public float LevelStartTime { get; private set; }

    // Bird speed for each level (Night and Winter also speed up every 10 flies).
    private static readonly float[] BirdSpeed = { 1f, 1.05f, 1.15f, 1.1f, 1.15f, 1.2f };

    // ---- atmosphere ----
    private struct Look
    {
        public bool fog;
        public FogMode fogMode;
        public Color fogColor;
        public float fogDensity;
        public Color ambient;
        public float ambientIntensity;
        public Color sunColor;
        public float sunIntensity;
    }

    private Color fromTint = Color.white;
    private float fromSaturation = 18f;

    private static Color Tint(int level)
    {
        switch (level)
        {
            case 2: return new Color(0.9f, 1f, 0.82f);
            case 3: return new Color(0.72f, 0.8f, 1f);
            case 4: return new Color(0.95f, 0.98f, 1.04f); // fresh, clear morning
            case 5: return new Color(1.05f, 0.93f, 0.78f); // golden afternoon
            case 6: return new Color(0.86f, 0.93f, 1.05f); // cold blue evening
            default: return Color.white;
        }
    }

    private static float Saturation(int level)
    {
        switch (level)
        {
            case 3: return 0f;
            case 5: return 26f;
            case 6: return 4f;
            default: return 18f;
        }
    }

    private Look day;
    private Look from;
    private Look to;
    private float blend = 1f;
    private const float BlendSeconds = 2.5f;
    private Light sun;

    void Awake()
    {
        Instance = this;
        GameState.RoundStarted += OnRoundStarted;
    }

    // A new round: start the clock, and jump to the level picked on the title screen.
    private void OnRoundStarted()
    {
        LevelStartScore = ScoreCounter.score;
        LevelStartTime = Time.time;
        if (ClockMode.Active)
        {
            // Clock mode: the world that suits the time of day and the season.
            GoToLevel(ClockMode.LevelFor(ClockMode.Now));
            return;
        }
        int start = LevelProgress.StartLevel;
        if (start > 1 && Level == 1)
        {
            GoToLevel(start);
            FrogEffects.Say("Starting at Level " + start + ": " + LevelName);
        }
    }

    void OnDestroy()
    {
        GameState.RoundStarted -= OnRoundStarted;
        if (Instance == this) Instance = null;
        // Leave the lighting as we found it.
        if (sun != null) ApplyLook(day);
    }

    void Start()
    {
        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional) { sun = l; break; }
        }
        day = Capture();
        from = to = day;
    }

    void Update()
    {
        if (blend < 1f)
        {
            blend = Mathf.Min(1f, blend + Time.deltaTime / BlendSeconds);
            float k = Mathf.SmoothStep(0f, 1f, blend);
            ApplyLook(Lerp(from, to, k));
            // Tint the cartoon "look" too (Stylizer's colour grading).
            if (Stylizer.Grading != null)
            {
                Stylizer.Grading.colorFilter.Override(Color.Lerp(fromTint, Tint(Level), k));
                Stylizer.Grading.saturation.Override(Mathf.Lerp(fromSaturation, Saturation(Level), k));
            }
        }

        GameState game = GameState.Current;
        if (game == null || !game.IsPlaying) return;

        int score = ScoreCounter.score;
        if (ClockMode.Active)
        {
            // Clock mode: no goals or Great Owl (ClockPilot.cs follows the real time instead),
            // and a calmer bird so the frog can enjoy itself.
            BirdMovement.SpeedMultiplier = 0.7f * Weather.BirdFactor;
            return;
        }
        int goal = NextGoal;
        if (goal >= 0 && score >= goal)
        {
            LevelProgress.Complete(Level, Time.time - LevelStartTime, Goals(GameSettings.Difficulty)[Level - 1]);
            if (!IsFinalLevel)
            {
                GoToLevel(Level + 1);
            }
            else
            {
                BossActive = true;
                Debug.Log("[Levels] Final goal reached: the Great Owl arrives!");
                if (BossStarted != null) BossStarted();
            }
        }

        // The bird's speed for this level; Night and Winter speed up every 10 flies (up to 1.5x).
        float speed = BirdSpeed[Mathf.Clamp(Level - 1, 0, BirdSpeed.Length - 1)];
        if (Level == 3 || Level == 6)
        {
            int steps = Mathf.Max(0, (score - LevelStartScore) / 10);
            speed = Mathf.Min(1.5f, speed + 0.08f * steps);
        }
        BirdMovement.SpeedMultiplier = speed * Weather.BirdFactor;
    }

    // Called by BossOwl.cs when you survive the Great Owl: credits roll!
    public void FinishGame()
    {
        if (HasFinished) return;
        HasFinished = true;
        BossActive = false;
        Debug.Log("[Levels] Beat the Great Owl! Credits roll.");
        if (Finished != null) Finished();
    }

    public void GoToLevel(int level)
    {
        level = Mathf.Clamp(level, 1, Names.Length);
        if (level == Level) return;
        Level = level;
        LevelStartScore = ScoreCounter.score;
        LevelStartTime = Time.time;
        if (!ClockMode.Active) LevelProgress.Reached(level); // the clock doesn't unlock levels

        from = Capture();
        to = LookFor(level);
        blend = 0f;
        if (Stylizer.Grading != null)
        {
            fromTint = Stylizer.Grading.colorFilter.value;
            fromSaturation = Stylizer.Grading.saturation.value;
        }

        Debug.Log("[Levels] Level " + level + ": " + LevelName);
        if (LevelChanged != null) LevelChanged(level, LevelName);
    }

    private Look LookFor(int level)
    {
        Look l = day;
        if (level == 2)
        {
            l.fog = true;
            l.fogMode = FogMode.Exponential;
            l.fogColor = new Color(0.33f, 0.42f, 0.27f);
            l.fogDensity = 0.028f;
            l.ambient = Color.Lerp(day.ambient, new Color(0.35f, 0.45f, 0.28f), 0.5f);
            l.ambientIntensity = day.ambientIntensity * 0.9f;
            l.sunColor = Color.Lerp(day.sunColor, new Color(0.85f, 0.95f, 0.70f), 0.6f);
            l.sunIntensity = day.sunIntensity * 0.8f;
        }
        else if (level == 3)
        {
            l.fog = true;
            l.fogMode = FogMode.Exponential;
            l.fogColor = new Color(0.03f, 0.05f, 0.11f);
            l.fogDensity = 0.032f;
            l.ambient = new Color(0.10f, 0.13f, 0.25f);
            l.ambientIntensity = day.ambientIntensity * 0.45f;
            l.sunColor = new Color(0.55f, 0.66f, 1f);
            l.sunIntensity = day.sunIntensity * 0.35f;
        }
        else if (level == 4)
        {
            // Bright morning on the lake: warm sun, clear blue air (only the faintest haze far off).
            l.fog = true;
            l.fogMode = FogMode.Exponential;
            l.fogColor = new Color(0.72f, 0.84f, 0.96f);
            l.fogDensity = 0.0025f;
            l.ambient = Color.Lerp(day.ambient, new Color(0.62f, 0.72f, 0.88f), 0.4f);
            l.ambientIntensity = day.ambientIntensity * 0.9f;
            l.sunColor = new Color(1f, 0.93f, 0.8f);
            l.sunIntensity = day.sunIntensity * 0.85f;
        }
        else if (level == 5)
        {
            // Golden autumn afternoon.
            l.fog = true;
            l.fogMode = FogMode.Exponential;
            l.fogColor = new Color(0.85f, 0.66f, 0.42f);
            l.fogDensity = 0.010f;
            l.ambient = Color.Lerp(day.ambient, new Color(0.8f, 0.62f, 0.4f), 0.45f);
            l.ambientIntensity = day.ambientIntensity;
            l.sunColor = new Color(1f, 0.82f, 0.55f);
            l.sunIntensity = day.sunIntensity * 0.95f;
        }
        else if (level >= 6)
        {
            // Snowy evening: cool light and a soft white haze.
            l.fog = true;
            l.fogMode = FogMode.Exponential;
            l.fogColor = new Color(0.78f, 0.84f, 0.95f);
            l.fogDensity = 0.018f;
            l.ambient = new Color(0.55f, 0.62f, 0.78f);
            l.ambientIntensity = day.ambientIntensity * 0.85f;
            l.sunColor = new Color(0.8f, 0.86f, 1f);
            l.sunIntensity = day.sunIntensity * 0.6f;
        }
        return l;
    }

    private Look Capture()
    {
        return new Look
        {
            fog = RenderSettings.fog,
            fogMode = RenderSettings.fogMode,
            fogColor = RenderSettings.fogColor,
            fogDensity = RenderSettings.fogDensity,
            ambient = RenderSettings.ambientLight,
            ambientIntensity = RenderSettings.ambientIntensity,
            sunColor = sun != null ? sun.color : Color.white,
            sunIntensity = sun != null ? sun.intensity : 1f,
        };
    }

    private static Look Lerp(Look a, Look b, float t)
    {
        return new Look
        {
            fog = a.fog || b.fog,
            fogMode = b.fog ? b.fogMode : a.fogMode,
            fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
            // fade fog in from nothing if the previous look had none
            fogDensity = Mathf.Lerp(a.fog ? a.fogDensity : 0f, b.fog ? b.fogDensity : 0f, t),
            ambient = Color.Lerp(a.ambient, b.ambient, t),
            ambientIntensity = Mathf.Lerp(a.ambientIntensity, b.ambientIntensity, t),
            sunColor = Color.Lerp(a.sunColor, b.sunColor, t),
            sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t),
        };
    }

    private void ApplyLook(Look l)
    {
        RenderSettings.fog = l.fog;
        RenderSettings.fogMode = l.fogMode;
        RenderSettings.fogColor = l.fogColor;
        RenderSettings.fogDensity = l.fogDensity;
        RenderSettings.ambientLight = l.ambient;
        RenderSettings.ambientIntensity = l.ambientIntensity;
        if (sun != null)
        {
            sun.color = l.sunColor;
            sun.intensity = l.sunIntensity;
        }
    }
}
