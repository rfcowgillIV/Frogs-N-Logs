using UnityEngine;

// Weather (added Nov 2026): sometimes a rainstorm rolls in (in the Swamp, at Lily Pad Lake
// and in the Autumn Woods). Rain brings out more flies (great for combos), but thunder
// makes the bird restless: it flies 15% faster until the storm passes. Lightning flashes
// light up the pond. A storm lasts about 40 seconds.
public class Weather : MonoBehaviour
{
    public static Weather Instance { get; private set; }
    public static bool Raining { get; private set; }
    public static float BirdFactor { get { return Raining ? 1.15f : 1f; } }
    public static float FlyFactor { get { return Raining ? 1.4f : 1f; } }

    public const float StormSeconds = 40f;
    private float stormEnds, nextCheck, flash;
    private ParticleSystem rain;

    void Awake()
    {
        Instance = this;
        Raining = false;
        nextCheck = float.MaxValue; // no storm until a stormy level rolls one
        LevelManager.LevelChanged += OnLevelChanged;
    }

    void OnDestroy()
    {
        LevelManager.LevelChanged -= OnLevelChanged;
        Raining = false;
        SetExposure(0f);
        if (Instance == this) Instance = null;
    }

    private void OnLevelChanged(int level, string name)
    {
        // Each stormy level has a 40% chance of a storm, 15-35 seconds in.
        bool stormy = level == 2 || level == 4 || level == 5;
        nextCheck = stormy && Random.value < 0.4f ? Time.time + Random.Range(15f, 35f) : float.MaxValue;
        if (!stormy && Raining) EndStorm();
    }

    public void StartStorm(float seconds = StormSeconds)
    {
        Raining = true;
        stormEnds = Time.time + seconds;
        nextCheck = float.MaxValue;
        if (rain == null) rain = MakeFalling("Rain", new Color(0.75f, 0.85f, 1f, 0.55f), 0.05f, 14f, 420f, true);
        rain.gameObject.SetActive(true);
        rain.Play();
        FrogEffects.Say("Rainstorm! More flies... but the bird is restless");
    }

    private void EndStorm()
    {
        Raining = false;
        if (rain != null) { rain.Stop(); rain.gameObject.SetActive(false); }
        SetExposure(0f);
        FrogEffects.Say("The storm has passed");
    }

    void Update()
    {
        GameState game = GameState.Current;
        bool playing = game != null && game.IsPlaying;
        if (!Raining && playing && Time.time >= nextCheck) StartStorm();
        if (!Raining) return;
        if (Time.time >= stormEnds) { EndStorm(); return; }

        // Darker sky, with the odd lightning flash.
        if (Random.value < Time.deltaTime * 0.25f) flash = 1f;
        flash = Mathf.Max(0f, flash - Time.deltaTime * 5f);
        SetExposure(-0.35f + flash * 1.6f);
    }

    private static void SetExposure(float ev)
    {
        if (Stylizer.Grading != null) Stylizer.Grading.postExposure.Override(0.1f + ev);
    }

    // Rain or snow: a box of particles that follows the camera.
    public static ParticleSystem MakeFalling(string name, Color color, float size, float speed, float rate, bool streaks)
    {
        var go = new GameObject(name);
        Camera cam = Camera.main;
        if (cam != null) go.AddComponent<FollowAbove>().target = cam.transform;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.startLifetime = streaks ? 1.2f : 7f;
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = color;
        main.maxParticles = streaks ? 1200 : 900;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = streaks ? 0.4f : 0.02f;
        var emission = ps.emission;
        bool low = GameSettings.Graphics == GraphicsTier.Low;
        emission.rateOverTime = rate * (low ? 0.4f : 1f);
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(44f, 44f, 1f); // flat, once turned to face down
        shape.rotation = new Vector3(90f, 0f, 0f); // emit downwards
        if (!streaks)
        {
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.3f;
        }
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = streaks ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        if (streaks) { r.lengthScale = 4f; r.velocityScale = 0.05f; }
        Material m = Resources.Load<Material>("Generated/ParticleURP");
        if (m != null) r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }
}
