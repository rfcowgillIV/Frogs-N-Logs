using System;
using UnityEngine;

// LevelManager (added Nov 2026): three levels, each with a fly goal to reach the next.
//
//   Level 1  The Pond   - the classic sunny pond
//   Level 2  The Swamp  - green fog rolls in; logs start drifting through and a snake wakes up
//   Level 3  Night      - moonlight and fog; the bird hunts faster, and keeps getting faster
//                         every 10 flies after that (up to 1.5x). How long can you last?
//
// The fly goals scale with difficulty (Easy needs fewer flies to move on, Hard needs more).
// The level changes in place (lighting, fog, hazards), so there's no loading screen between
// levels and the run keeps its score.
//
// From pond to swamp to moonlit chomp.
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    // Fired when a new level begins: (level number starting at 1, level name).
    public static event Action<int, string> LevelChanged;

    public static readonly string[] Names = { "The Pond", "The Swamp", "Night" };

    public int Level { get; private set; } = 1;
    public string LevelName { get { return Names[Mathf.Clamp(Level - 1, 0, Names.Length - 1)]; } }
    public bool IsFinalLevel { get { return Level >= Names.Length; } }

    // Total flies needed to leave the current level (-1 on the last level).
    public int NextGoal
    {
        get { return IsFinalLevel ? -1 : Goals(GameSettings.Difficulty)[Level - 1]; }
    }

    public static int[] Goals(Difficulty d)
    {
        switch (d)
        {
            case Difficulty.Easy: return new[] { 8, 18 };
            case Difficulty.Hard: return new[] { 12, 28 };
            default: return new[] { 10, 24 };
        }
    }

    // Where the score was when the final level started (for the "faster every 10" ramp).
    private int finalLevelStartScore;

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

    private Look day;
    private Look from;
    private Look to;
    private float blend = 1f;
    private const float BlendSeconds = 2.5f;
    private Light sun;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
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
            ApplyLook(Lerp(from, to, Mathf.SmoothStep(0f, 1f, blend)));
        }

        GameState game = GameState.Current;
        if (game == null || !game.IsPlaying) return;

        int score = ScoreCounter.score;
        if (!IsFinalLevel && score >= NextGoal)
        {
            GoToLevel(Level + 1);
        }

        // Night: the bird speeds up every 10 flies (up to 1.5x).
        if (Level >= 3)
        {
            int steps = Mathf.Max(0, (score - finalLevelStartScore) / 10);
            BirdMovement.SpeedMultiplier = Mathf.Min(1.5f, 1.15f + 0.08f * steps);
        }
    }

    public void GoToLevel(int level)
    {
        level = Mathf.Clamp(level, 1, Names.Length);
        if (level == Level) return;
        Level = level;
        if (IsFinalLevel) finalLevelStartScore = ScoreCounter.score;

        from = Capture();
        to = LookFor(level);
        blend = 0f;

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
        else if (level >= 3)
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
