using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// GraphicsManager (Nov 2026, now for URP): makes the game run smoothly on any device.
//
// Pause > Graphics has four choices:
//   Auto   - starts on Medium, watches the frame rate for a few seconds of play, and steps
//            down to Low if the device is struggling (or up to High on a fast computer).
//   Low    - draws at 70% resolution (scaled up), no shadows or glow, fewer grass tufts and
//            plants. Best for phones and older iPads.
//   Medium - 85% resolution, short soft shadows, light glow and colour grading.
//   High   - full resolution, longer shadows, smoothed edges, every plant.
//
// URP's "render scale" is the big speed lever: it draws fewer pixels and scales the picture
// up, which keeps menus and text sharp. (The 2017 version's water drew the scene 3 times per
// frame; the new cartoon water costs almost nothing.)
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
                if (averageFps < 40f) autoDecision = GraphicsTier.Low;
                else if (averageFps >= 57f) autoDecision = GraphicsTier.High;
                Debug.Log("[Graphics] Auto: " + averageFps.ToString("0") + " fps on Medium -> " + autoDecision);
                Apply();
            }
        }
    }

    public static GraphicsTier Resolve(GraphicsTier choice)
    {
        return choice == GraphicsTier.Auto ? autoDecision : choice;
    }

    public void Apply()
    {
        GraphicsTier tier = Resolve(GameSettings.Graphics);
        ActiveTier = tier;
        QualitySettings.vSyncCount = 0; // the browser paces frames itself
        Application.targetFrameRate = -1;

        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            urp.renderScale = tier == GraphicsTier.Low ? 0.7f : tier == GraphicsTier.Medium ? 0.85f : 1f;
            urp.shadowDistance = tier == GraphicsTier.Low ? 0f : tier == GraphicsTier.Medium ? 35f : 60f;
            urp.msaaSampleCount = tier == GraphicsTier.High ? 2 : 1;
            urp.supportsHDR = tier != GraphicsTier.Low;
        }
        QualitySettings.lodBias = tier == GraphicsTier.Low ? 0.5f : tier == GraphicsTier.Medium ? 1f : 1.5f;

        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional)
            {
                light.shadows = tier == GraphicsTier.Low ? LightShadows.None : LightShadows.Soft;
            }
        }

        // Glow, colour grading and vignette (FrogsLook) are off on Low.
        foreach (Camera cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = tier != GraphicsTier.Low;
        }

        // The drifting dust storm particles are pretty but costly; Low switches them off.
        foreach (ParticleSystem ps in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (ps.gameObject.name == "DustStorm") ps.gameObject.SetActive(tier != GraphicsTier.Low);
        }

        // Terrain: fewer, closer grass tufts on lower tiers.
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            float[] d;
            if (!terrainDefaults.TryGetValue(terrain, out d))
            {
                d = new[] { terrain.heightmapPixelError, terrain.detailObjectDistance, terrain.detailObjectDensity, terrain.basemapDistance };
                terrainDefaults[terrain] = d;
            }
            float scale = tier == GraphicsTier.Low ? 0.4f : tier == GraphicsTier.Medium ? 0.7f : 1f;
            terrain.heightmapPixelError = tier == GraphicsTier.Low ? 12f : tier == GraphicsTier.Medium ? Mathf.Max(d[0], 8f) : d[0];
            terrain.detailObjectDistance = d[1] * scale;
            terrain.detailObjectDensity = Mathf.Clamp01(d[2] * (tier == GraphicsTier.Low ? 0.5f : tier == GraphicsTier.Medium ? 0.8f : 1f));
            terrain.basemapDistance = d[3] * scale;
            terrain.drawInstanced = true;
        }

        // Fewer decorative plants on Low (see SceneDressing.cs).
        SceneDressing.ApplyTier(tier);
    }
}
