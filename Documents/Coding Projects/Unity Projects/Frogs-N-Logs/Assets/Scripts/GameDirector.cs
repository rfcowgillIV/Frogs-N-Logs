using System.Collections.Generic;
using UnityEngine;

// GameDirector (added Nov 2026): ties each round together.
//  - frog speed = shop upgrades x speed-boost power-up
//  - shield bubble, fly magnet, laser eyes, skins and hats (see Shop.cs)
//  - fly wallet: every fly you eat is also added to your shop money
//  - achievements, the first-time tutorial, screen shake and a "ribbit"
// Lives next to the menus (FrogsUI) and the level, hazard and power-up spawners.
public class GameDirector : MonoBehaviour
{
    public static GameDirector Instance { get; private set; }

    private GameObject bubble;
    private Transform frog;
    private int lastScore;
    private float roundTime;

    // ---- laser ----
    public const float LaserCooldown = 20f;
    public const float LaserRange = 18f;
    public float LaserReadyAt { get; private set; }
    public bool LaserReady { get { return Shop.HasLaser && Time.time >= LaserReadyAt; } }
    private LineRenderer beam;
    private float beamUntil;

    // ---- looks ----
    private GameObject hat;
    private string appliedHat;

    // ---- tutorial ----
    private readonly Queue<string> tutorialSteps = new Queue<string>();
    private float nextTutorialStep;

    // ---- effects ----
    private AudioSource voice;
    private static AudioClip ribbit;
    private float shakeUntil;
    private Transform shakeCamera;
    private Vector3 shakeOffset;

    void Awake()
    {
        Instance = this;
        GameState.RoundStarted += OnRoundStarted;
        GameState.RoundEnded += OnRoundEnded;
        LevelManager.LevelChanged += OnLevelChanged;
        Shop.Changed += ApplyLooks;
        Achievements.Unlocked += OnAchievement;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        GameState.RoundStarted -= OnRoundStarted;
        GameState.RoundEnded -= OnRoundEnded;
        LevelManager.LevelChanged -= OnLevelChanged;
        Shop.Changed -= ApplyLooks;
        Achievements.Unlocked -= OnAchievement;
    }

    void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) frog = player.transform;
        lastScore = ScoreCounter.score;
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        AudioManager.Register(gameObject);
        ApplyLooks();
    }

    // ---------------------------------------------------------------- round events

    private void OnRoundStarted()
    {
        roundTime = 0f;
        lastScore = ScoreCounter.score;
        PlayRibbit(1f);
        if (PlayerPrefs.GetInt("FNL.tutorial", 0) == 0)
        {
            tutorialSteps.Clear();
            tutorialSteps.Enqueue(IsTouch() ? "Hold your finger where you want to hop" : "Use W A S D or the arrow keys to hop");
            tutorialSteps.Enqueue("Eat flies to score - and to fill your fly wallet");
            tutorialSteps.Enqueue("Watch out for the bird!");
            tutorialSteps.Enqueue("Pause (Esc or II) to visit the Shop");
            nextTutorialStep = Time.time + 0.8f;
        }
    }

    private void OnRoundEnded(int score, bool newBest)
    {
        tutorialSteps.Clear();
        PlayerPrefs.SetInt("FNL.tutorial", 1);
        PlayerPrefs.Save();
        AudioDirector.Play("Croak", 0.8f);
        AudioDirector.Play("GameOver", 0.7f);
        Shake(0.45f);
    }

    private void OnLevelChanged(int level, string name)
    {
        if (level >= 2) Achievements.Unlock("swamp");
        if (level >= 3) Achievements.Unlock("night");
    }

    private void OnAchievement(string name)
    {
        FrogEffects.Say("Achievement: " + name + "!");
    }

    private static bool IsTouch()
    {
        return Application.isMobilePlatform || Input.touchSupported;
    }

    // ---------------------------------------------------------------- per frame

    void Update()
    {
        PlayerMovement.SpeedMultiplier = Shop.SpeedMultiplier * (FrogEffects.Boosted ? FrogEffects.BoostMultiplier : 1f);
        UpdateBubble();

        GameState game = GameState.Current;
        bool playing = game != null && game.IsPlaying;
        if (playing)
        {
            roundTime += Time.deltaTime;
            if (roundTime >= 60f) Achievements.Unlock("survivor");

            // Wallet + achievements follow the score.
            int score = ScoreCounter.score;
            if (score > lastScore)
            {
                Shop.Earn(score - lastScore);
                lastScore = score;
                if (score >= 1) Achievements.Unlock("first");
                if (score >= 25) Achievements.Unlock("fiend");
            }

            UpdateMagnet();
            if (KeyBindings.LaserPressed()) FireLaser();
            UpdateTutorial();
        }

        if (beam != null && beam.enabled && Time.time > beamUntil) beam.enabled = false;
    }

    void LateUpdate()
    {
        // Screen shake runs after the follow camera has moved.
        if (shakeCamera != null)
        {
            shakeCamera.position -= shakeOffset;
            shakeOffset = Vector3.zero;
            if (Time.time < shakeUntil)
            {
                float strength = (shakeUntil - Time.time) * 0.8f;
                shakeOffset = Random.insideUnitSphere * strength;
                shakeCamera.position += shakeOffset;
            }
            else
            {
                shakeCamera = null;
            }
        }
    }

    public void Shake(float seconds)
    {
        if (Camera.main == null) return;
        shakeCamera = Camera.main.transform;
        shakeUntil = Time.time + seconds;
    }

    private void UpdateTutorial()
    {
        if (tutorialSteps.Count == 0 || Time.time < nextTutorialStep) return;
        FrogEffects.Say(tutorialSteps.Dequeue());
        nextTutorialStep = Time.time + 3.2f;
    }

    // ---------------------------------------------------------------- shield bubble

    private void UpdateBubble()
    {
        bool show = frog != null && frog.gameObject.activeInHierarchy
                    && (FrogEffects.ShieldActive || Time.time < FrogEffects.InvulnerableUntil);
        if (show && bubble == null)
        {
            bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bubble.name = "Shield Bubble";
            Destroy(bubble.GetComponent<Collider>());
            bubble.GetComponent<Renderer>().sharedMaterial = PowerUpSpawner.Glow(new Color(0.4f, 1f, 0.5f, 0.18f), 1.8f);
            bubble.transform.localScale = Vector3.one * 2.2f;
        }
        if (bubble != null)
        {
            bubble.SetActive(show);
            if (show)
            {
                bubble.transform.position = frog.position + Vector3.up * 0.4f;
                // Flicker during the short safe time after the shield pops.
                if (!FrogEffects.ShieldActive)
                {
                    bubble.SetActive(Mathf.Repeat(Time.time * 8f, 1f) > 0.4f);
                }
            }
        }
    }

    // ---------------------------------------------------------------- fly magnet

    private void UpdateMagnet()
    {
        float radius = Shop.MagnetRadius;
        if (radius <= 0f || frog == null || !frog.gameObject.activeInHierarchy) return;
        Vector3 target = frog.position;
        foreach (GameObject fly in FlySpawner.ActiveFlies)
        {
            if (fly == null || !fly.activeInHierarchy) continue;
            Vector3 p = fly.transform.position;
            Vector3 flat = new Vector3(target.x - p.x, 0f, target.z - p.z);
            float d = flat.magnitude;
            if (d < radius && d > 0.05f)
            {
                // Pull harder the closer it gets.
                float pull = Mathf.Lerp(6f, 2f, d / radius) * Time.deltaTime;
                fly.transform.position = p + flat / d * Mathf.Min(pull, d);
            }
        }
    }

    // ---------------------------------------------------------------- laser eyes

    // Zap the bird (or the snake, whichever is closer and in range). Returns true if it hit.
    public bool FireLaser()
    {
        if (!Shop.HasLaser || frog == null || !frog.gameObject.activeInHierarchy) return false;
        if (!LaserReady)
        {
            FrogEffects.Say("Laser recharging... " + Mathf.CeilToInt(LaserReadyAt - Time.time) + "s");
            return false;
        }

        Transform target = null;
        float best = LaserRange;
        BirdMovement bird = FindFirstObjectByType<BirdMovement>();
        if (bird != null)
        {
            float d = Vector3.Distance(bird.transform.position, frog.position);
            if (d < best) { best = d; target = bird.transform; }
        }
        SnakeHazard snake = FindFirstObjectByType<SnakeHazard>();
        if (snake != null)
        {
            float d = Vector3.Distance(snake.transform.position, frog.position);
            if (d < best) { best = d; target = snake.transform; }
        }
        if (target == null)
        {
            FrogEffects.Say("Nothing in laser range");
            return false;
        }

        LaserReadyAt = Time.time + LaserCooldown;
        AudioDirector.Play("Zap", 0.8f);
        if (target.GetComponent<BirdMovement>() != null)
        {
            BirdMovement.Stun(4f);
            Achievements.CountStun();
        }
        else
        {
            target.GetComponent<SnakeHazard>().Stun(4f);
        }
        DrawBeam(frog.position + Vector3.up * 0.6f, target.position + Vector3.up * 0.8f);
        FrogEffects.Say("ZAP!");
        return true;
    }

    private void DrawBeam(Vector3 from, Vector3 to)
    {
        if (beam == null)
        {
            var go = new GameObject("Laser Beam");
            go.transform.SetParent(transform, false);
            beam = go.AddComponent<LineRenderer>();
            beam.positionCount = 2;
            beam.startWidth = 0.18f;
            beam.endWidth = 0.08f;
            beam.sharedMaterial = PowerUpSpawner.Glow(new Color(1f, 0.15f, 0.2f, 0.9f), 0.5f);
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.receiveShadows = false;
        }
        beam.SetPosition(0, from);
        beam.SetPosition(1, to);
        beam.enabled = true;
        beamUntil = Time.time + 0.35f;
    }

    // ---------------------------------------------------------------- skins & hats

    private void ApplyLooks()
    {
        if (frog == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) return;
            frog = player.transform;
        }

        Shop.Item skin = Shop.Find(Shop.EquippedSkin);
        Color tint = skin != null ? skin.tint : Color.white;
        var block = new MaterialPropertyBlock();
        foreach (Renderer r in frog.GetComponentsInChildren<Renderer>(true))
        {
            if (hat != null && r.transform.IsChildOf(hat.transform)) continue;
            r.GetPropertyBlock(block);
            if (tint == Color.white) block.Clear();
            else
            {
                block.SetColor("_Color", tint);
                block.SetColor("_BaseColor", tint);
            }
            r.SetPropertyBlock(block);
        }

        string hatId = Shop.EquippedHat;
        if (hatId == appliedHat) return;
        appliedHat = hatId;
        if (hat != null) Destroy(hat);
        hat = null;
        if (hatId == "hat.none") return;
        hat = BuildHat(hatId);
    }

    private GameObject BuildHat(string id)
    {
        // Sit the hat on top of the frog's head, and let it ride along with the head bone.
        // Prefer the new cartoon model's head bone (names vary in case), then the old one.
        Transform head = FindChild(frog, "head");
        Bounds bounds = new Bounds(frog.position, Vector3.one);
        bool first = true;
        foreach (Renderer r in frog.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (first) { bounds = r.bounds; first = false; }
            else bounds.Encapsulate(r.bounds);
        }
        float size = Mathf.Clamp(bounds.size.x * 0.45f, 0.25f, 1.2f);
        Vector3 top = head != null ? new Vector3(head.position.x, bounds.max.y, head.position.z) : new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);

        var root = new GameObject("Frog Hat");
        root.transform.position = top;
        if (id == "hat.top")
        {
            Color black = new Color(0.08f, 0.08f, 0.1f);
            Piece(root, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(1.4f, 0.04f, 1.4f) * size, black);
            Piece(root, PrimitiveType.Cylinder, new Vector3(0f, 0.45f * size, 0f), new Vector3(0.85f, 0.45f, 0.85f) * size, black);
            Piece(root, PrimitiveType.Cylinder, new Vector3(0f, 0.12f * size, 0f), new Vector3(0.87f, 0.08f, 0.87f) * size, new Color(0.75f, 0.1f, 0.15f));
        }
        else
        {
            Color gold = new Color(1f, 0.78f, 0.2f);
            Piece(root, PrimitiveType.Cylinder, new Vector3(0f, 0.12f * size, 0f), new Vector3(1f, 0.14f, 1f) * size, gold);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.45f, 0.38f, Mathf.Sin(a) * 0.45f) * size;
                Piece(root, PrimitiveType.Sphere, p, Vector3.one * 0.2f * size, i % 2 == 0 ? gold : new Color(0.9f, 0.15f, 0.3f));
            }
        }
        root.transform.SetParent(head != null ? head : frog, true);
        return root;
    }

    private static void Piece(GameObject parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = SnakeHazard.Toon(color, 0.6f);
        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static Transform FindChild(Transform root, string nameEndsWith)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.EndsWith(nameEndsWith, System.StringComparison.OrdinalIgnoreCase) && IsNewModel(t)) return t;
        }
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.EndsWith(nameEndsWith, System.StringComparison.OrdinalIgnoreCase)) return t;
        }
        return null;
    }

    // ---------------------------------------------------------------- ribbit

    private static bool IsNewModel(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent)
        {
            if (p.name.EndsWith("(new model)")) return true;
        }
        return false;
    }

    // A real recorded ribbit (CC0, see Credits); falls back to the home-made one below.
    private void PlayRibbit(float pitch)
    {
        if (AudioDirector.Instance != null && Resources.Load<AudioClip>("Audio/Ribbit1") != null)
        {
            AudioDirector.PlayRandom(0.6f, "Ribbit1", "Ribbit2", "Ribbit3");
            return;
        }
        if (voice == null) return;
        if (ribbit == null) ribbit = MakeRibbit();
        voice.pitch = pitch;
        voice.PlayOneShot(ribbit, 0.55f);
    }

    private static AudioClip MakeRibbit()
    {
        const int rate = 22050;
        int length = (int)(rate * 0.42f);
        var samples = new float[length];
        float phase = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)rate;
            // two pulses: 0.00-0.14 s and 0.20-0.38 s
            float local = t < 0.17f ? t / 0.14f : (t - 0.2f) / 0.18f;
            bool on = (t < 0.14f) || (t >= 0.2f && t < 0.38f);
            if (!on) { samples[i] = 0f; continue; }
            float envelope = Mathf.Sin(Mathf.Clamp01(local) * Mathf.PI);
            float freq = Mathf.Lerp(t < 0.17f ? 210f : 240f, 130f, Mathf.Clamp01(local));
            phase += freq / rate;
            // a buzzy, throaty tone: a few harmonics plus a 40 Hz flutter
            float tone = Mathf.Sin(phase * 2f * Mathf.PI) + 0.5f * Mathf.Sin(phase * 4f * Mathf.PI) + 0.25f * Mathf.Sin(phase * 6f * Mathf.PI);
            float flutter = 0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * 40f);
            samples[i] = tone * flutter * envelope * 0.35f;
        }
        AudioClip clip = AudioClip.Create("Ribbit", length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
