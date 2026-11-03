using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.SceneManagement;
using UnityStandardAssets.Water;

// GraphicsManager (added Nov 2026): makes the game run smoothly on any device.
//
// Pause > Graphics has four choices:
//   Auto   - starts on Medium, watches the frame rate for a few seconds of play, and steps
//            down to Low if the device is struggling (or up to High on a fast computer).
//   Low    - no water reflections, shadows or post effects; fewer grass tufts.
//            Best for phones and older iPads.
//   Medium - simple water reflection at a small size, short soft shadows, colour grading.
//   High   - the full 2017 look: reflective + refractive water, long shadows, everything.
//
// Why it was slow before: the web version ran on "High", where the water re-draws the
// entire scene twice every frame (once for the reflection, once for the refraction), and
// iPads draw 4x the pixels because of their Retina screens. (The web page now caps that at
// 1.25x when it loads - see web-fullscreen.py. It's set once, at load, because changing it
// mid-game made taps land in the wrong place.)
//
// Frame rate, meet frog rate.
public class GraphicsManager : MonoBehaviour
{
    public static GraphicsManager Instance { get; private set; }

    // The tier actually in use right now (never Auto).
    public static GraphicsTier ActiveTier { get; private set; }

    // Smoothed frames per second, for the on-screen counter.
    public static float Fps { get; private set; }

    // Auto mode decides once per visit, then sticks with it.
    private static GraphicsTier autoDecision = GraphicsTier.Medium;
    private static bool autoDecided;

    private float measureTime;
    private int measureFrames;
    private float fpsAccumulator;
    private int fpsFrames;

    // Original terrain settings, so High can restore the full look.
    private readonly Dictionary<Terrain, float[]> terrainDefaults = new Dictionary<Terrain, float[]>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (Instance != null) return;
        var go = new GameObject("Graphics Manager");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<GraphicsManager>();
    }

    void Awake()
    {
        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameSettings.Changed += Apply;
        GameState.RoundStarted += OnRoundStarted;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameSettings.Changed -= Apply;
        GameState.RoundStarted -= OnRoundStarted;
    }

    void Start()
    {
        Apply();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        terrainDefaults.Clear();
        Apply();
    }

    private void OnRoundStarted()
    {
        // Measure the frame rate during real play, not on the title screen.
        measureTime = 0f;
        measureFrames = 0;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // On-screen counter: update about twice a second.
        fpsAccumulator += dt;
        fpsFrames++;
        if (fpsAccumulator >= 0.5f)
        {
            Fps = fpsFrames / fpsAccumulator;
            fpsAccumulator = 0f;
            fpsFrames = 0;
        }

        // Auto mode: after ~5 seconds of actual play, pick the right tier for this device.
        if (GameSettings.Graphics == GraphicsTier.Auto && !autoDecided
            && GameState.Current != null && GameState.Current.IsPlaying && Time.timeScale > 0f)
        {
            measureTime += dt;
            measureFrames++;
            if (measureTime >= 5f)
            {
                float averageFps = measureFrames / measureTime;
                autoDecided = true;
                if (averageFps < 40f)
                {
                    autoDecision = GraphicsTier.Low;
                }
                else if (averageFps >= 57f && !IsTouchDevice())
                {
                    autoDecision = GraphicsTier.High;
                }
                Debug.Log("[Graphics] Auto: " + averageFps.ToString("0") + " fps on Medium -> " + autoDecision);
                Apply();
            }
        }
    }

    private static bool IsTouchDevice()
    {
        return Application.isMobilePlatform || Input.touchSupported;
    }

    public static GraphicsTier Resolve(GraphicsTier choice)
    {
        return choice == GraphicsTier.Auto ? autoDecision : choice;
    }

    public void Apply()
    {
        GraphicsTier tier = Resolve(GameSettings.Graphics);
        ActiveTier = tier;

        // Built-in quality levels: 0 = "Low Quality", 1 = "High Quality". We fine-tune below.
        QualitySettings.SetQualityLevel(tier == GraphicsTier.Low ? 0 : 1, true);
        QualitySettings.vSyncCount = 0; // the browser paces frames itself
        Application.targetFrameRate = -1;

        switch (tier)
        {
            case GraphicsTier.Low:
                QualitySettings.globalTextureMipmapLimit = 1; // half-size textures, still crisp enough
                QualitySettings.shadows = ShadowQuality.Disable;
                QualitySettings.antiAliasing = 0;
                QualitySettings.pixelLightCount = 0;
                QualitySettings.lodBias = 0.5f;
                break;
            case GraphicsTier.Medium:
                QualitySettings.globalTextureMipmapLimit = 0;
                QualitySettings.shadows = ShadowQuality.HardOnly;
                QualitySettings.shadowResolution = ShadowResolution.Low;
                QualitySettings.shadowDistance = 35f;
                QualitySettings.shadowCascades = 1;
                QualitySettings.antiAliasing = 0;
                QualitySettings.pixelLightCount = 1;
                QualitySettings.lodBias = 1f;
                break;
            default: // High: keep the full "High Quality" level
                break;
        }

        ApplyToScene(tier);
    }

    private void ApplyToScene(GraphicsTier tier)
    {
        // Water: Simple (no extra cameras) / Reflective (one small extra camera) / Refractive (two).
        foreach (Water water in FindObjectsByType<Water>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            water.gameObject.SetActive(true);
            water.waterMode = tier == GraphicsTier.Low ? Water.WaterMode.Simple
                : tier == GraphicsTier.Medium ? Water.WaterMode.Reflective
                : Water.WaterMode.Refractive;
            water.textureSize = tier == GraphicsTier.High ? 256 : 128;
        }

        // The drifting dust storm particles are pretty but costly; Low switches them off.
        foreach (ParticleSystem ps in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (ps.gameObject.name == "DustStorm")
            {
                ps.gameObject.SetActive(tier != GraphicsTier.Low);
            }
        }

        // Post effects (colour grading, vignette, fog) and HDR: off on Low.
        foreach (Camera cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            var post = cam.GetComponent<PostProcessingBehaviour>();
            if (post != null)
            {
                post.enabled = tier != GraphicsTier.Low;
            }
            if (cam.targetTexture == null && cam.name == "Main Camera")
            {
                cam.allowHDR = tier != GraphicsTier.Low;
                cam.allowMSAA = tier == GraphicsTier.High;
            }
        }

        // Terrain: fewer, closer grass tufts and a coarser ground mesh on lower tiers.
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            float[] d;
            if (!terrainDefaults.TryGetValue(terrain, out d))
            {
                d = new[] { terrain.heightmapPixelError, terrain.detailObjectDistance, terrain.detailObjectDensity, terrain.basemapDistance, terrain.treeDistance };
                terrainDefaults[terrain] = d;
            }
            float scale = tier == GraphicsTier.Low ? 0.4f : tier == GraphicsTier.Medium ? 0.7f : 1f;
            terrain.heightmapPixelError = tier == GraphicsTier.Low ? 12f : tier == GraphicsTier.Medium ? Mathf.Max(d[0], 8f) : d[0];
            terrain.detailObjectDistance = d[1] * scale;
            terrain.detailObjectDensity = Mathf.Clamp01(d[2] * (tier == GraphicsTier.Low ? 0.5f : tier == GraphicsTier.Medium ? 0.8f : 1f));
            terrain.basemapDistance = d[3] * scale;
            terrain.treeDistance = d[4] * scale;
            terrain.drawInstanced = true; // one batch for the ground instead of many
        }
    }
}
