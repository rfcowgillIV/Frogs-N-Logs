using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// MiniMap (added Nov 2026): a little map in the top-right corner.
//
// The picture is a real top-down snapshot of the pond, taken again whenever the pond grows
// or the level changes (so it turns swampy, then dark at Night). On top of it, dots show:
//   you (green, with an arrow), your helper buddy (light blue), the bird (red),
//   snakes (orange), drifting logs (brown), flies (yellow) and power-ups (gold / blue / green).
// Tap or click the map to make it bigger. Pause > Extras turns the map, and each kind of
// dot, on or off.
public class MiniMap : MonoBehaviour
{
    public static MiniMap Instance { get; private set; }

    private RectTransform frame;
    private RectTransform dotsLayer;
    private RawImage picture;
    private Text caption;
    private RenderTexture snapshot;
    private Camera mapCamera;
    private float mapHalf = 32f;
    private bool snapshotWanted = true;
    private float snapshotAt;
    private Sprite dotSprite, arrowSprite;
    private readonly List<Image> pool = new List<Image>();
    private int used;

    public static int VisibleDots { get; private set; }

    public static readonly Color FrogColor = new Color(0.55f, 1f, 0.35f);
    public static readonly Color HelperColor = new Color(0.55f, 0.85f, 1f);
    public static readonly Color BirdColor = new Color(1f, 0.3f, 0.25f);
    public static readonly Color SnakeColor = new Color(1f, 0.6f, 0.15f);
    public static readonly Color LogColor = new Color(0.6f, 0.4f, 0.22f);
    public static readonly Color FlyColor = new Color(1f, 0.95f, 0.4f);

    public static MiniMap Create(RectTransform root, Font font)
    {
        var go = new GameObject("Mini Map", typeof(RectTransform));
        go.transform.SetParent(root, false);
        var map = go.AddComponent<MiniMap>();
        map.Build(font);
        return map;
    }

    private void Build(Font font)
    {
        Instance = this;
        frame = (RectTransform)transform;
        frame.anchorMin = frame.anchorMax = new Vector2(1f, 1f);
        frame.pivot = new Vector2(1f, 1f);
        frame.anchoredPosition = new Vector2(-14f, -96f);

        var border = gameObject.AddComponent<Image>();
        border.color = new Color(0.55f, 0.80f, 0.29f, 0.9f);
        var button = gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() =>
        {
            GameSettings.MiniMapLarge = !GameSettings.MiniMapLarge;
            AudioDirector.Play("Click", 0.5f);
        });

        var inner = new GameObject("Picture", typeof(RectTransform));
        inner.transform.SetParent(transform, false);
        var irt = (RectTransform)inner.transform;
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(3f, 3f); irt.offsetMax = new Vector2(-3f, -3f);
        picture = inner.AddComponent<RawImage>();
        picture.color = new Color(0.25f, 0.45f, 0.25f);
        picture.raycastTarget = false;
        inner.AddComponent<RectMask2D>();

        var dots = new GameObject("Dots", typeof(RectTransform));
        dots.transform.SetParent(inner.transform, false);
        dotsLayer = (RectTransform)dots.transform;
        dotsLayer.anchorMin = Vector2.zero; dotsLayer.anchorMax = Vector2.one;
        dotsLayer.offsetMin = dotsLayer.offsetMax = Vector2.zero;

        var cap = new GameObject("Caption", typeof(RectTransform));
        cap.transform.SetParent(transform, false);
        var crt = (RectTransform)cap.transform;
        crt.anchorMin = new Vector2(0f, 0f); crt.anchorMax = new Vector2(1f, 0f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = new Vector2(0f, -2f);
        crt.sizeDelta = new Vector2(0f, 22f);
        caption = cap.AddComponent<Text>();
        caption.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        caption.fontSize = 15;
        caption.alignment = TextAnchor.UpperCenter;
        caption.color = new Color(0.85f, 0.95f, 0.8f);
        caption.raycastTarget = false;
        cap.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);

        dotSprite = MakeSprite(false);
        arrowSprite = MakeSprite(true);

        PlayArea.Changed += WantSnapshot;
        LevelManager.LevelChanged += OnLevelChanged;
        GameSettings.Changed += ApplySettings;
        ApplySettings();
    }

    void OnDestroy()
    {
        PlayArea.Changed -= WantSnapshot;
        LevelManager.LevelChanged -= OnLevelChanged;
        GameSettings.Changed -= ApplySettings;
        if (snapshot != null) { snapshot.Release(); Destroy(snapshot); }
        if (mapCamera != null) Destroy(mapCamera.gameObject);
        if (Instance == this) Instance = null;
    }

    private void OnLevelChanged(int level, string name)
    {
        // Wait for the lighting to change (and the pond to grow) before taking the picture.
        snapshotWanted = true;
        snapshotAt = Time.time + 2.8f;
    }

    // Retake the picture (the world changed, e.g. snow or a flooded lake).
    public void Refresh() { snapshotWanted = true; snapshotAt = Time.time + 0.2f; }

    private void WantSnapshot()
    {
        snapshotWanted = true;
        snapshotAt = Time.time;
    }

    private void ApplySettings()
    {
        float size = GameSettings.MiniMapLarge ? 300f : 170f;
        frame.sizeDelta = new Vector2(size, size);
    }

    // ------------------------------------------------------------ per frame

    void LateUpdate()
    {
        bool show = GameSettings.MiniMap && GameState.Current != null && GameState.Current.IsPlaying && !ClockMode.Active;
        if (frame.GetChild(0).gameObject.activeSelf != show)
        {
            for (int i = 0; i < frame.childCount; i++) frame.GetChild(i).gameObject.SetActive(show);
            GetComponent<Image>().enabled = show;
        }
        if (!show) { VisibleDots = 0; return; }

        if (snapshotWanted && Time.time >= snapshotAt && PlayArea.BuildingDone) TakeSnapshot();

        LevelManager levels = LevelManager.Instance;
        caption.text = levels != null ? levels.LevelName : "";

        used = 0;
        GameObject player = GameObject.FindWithTag("Player");
        if (GameSettings.MapFlies)
        {
            foreach (GameObject fly in FlySpawner.ActiveFlies)
                if (fly != null && fly.activeInHierarchy)
                {
                    FlyKind kind = fly.GetComponent<FlyKind>();
                    FlyKind.Type t = kind != null ? kind.Kind : FlyKind.Type.Fly;
                    Color c = t == FlyKind.Type.Dragonfly ? new Color(0.3f, 1f, 0.95f) : t == FlyKind.Type.Bee ? new Color(1f, 0.45f, 0.1f) : FlyColor;
                    Dot(fly.transform.position, c, t == FlyKind.Type.Fly ? 5f : 7f, false, 0f);
                }
        }
        if (GameSettings.MapPowerUps)
        {
            foreach (PowerUpPickup p in FindObjectsByType<PowerUpPickup>(FindObjectsSortMode.None))
                Dot(p.transform.position, PowerUpColor(p), 9f, false, 0f);
        }
        if (GameSettings.MapHazards)
        {
            foreach (DriftingLog log in FindObjectsByType<DriftingLog>(FindObjectsSortMode.None))
            {
                Image d = Dot(log.transform.position, LogColor, 6f, false, 0f);
                if (d != null)
                {
                    d.rectTransform.sizeDelta = new Vector2(16f, 6f);
                    d.rectTransform.localEulerAngles = new Vector3(0f, 0f, -log.transform.eulerAngles.y + 90f);
                }
            }
        }
        if (GameSettings.MapEnemies)
        {
            foreach (SnakeHazard snake in FindObjectsByType<SnakeHazard>(FindObjectsSortMode.None))
                Dot(snake.transform.position, SnakeColor, 9f, false, 0f);
            if (GroundHunter.Active != null) Dot(GroundHunter.Active.transform.position, SnakeColor, 11f, false, 0f);
            foreach (StrikeZone zone in FindObjectsByType<StrikeZone>(FindObjectsSortMode.None))
                if (!zone.Struck) Dot(zone.transform.position, new Color(1f, 1f, 1f, 0.8f), 14f, false, 0f);
            BirdMovement bird = FindFirstObjectByType<BirdMovement>();
            if (bird != null) Dot(bird.transform.position, BirdIsDizzy() ? new Color(1f, 0.7f, 0.7f) : BirdColor, 12f, false, 0f);
        }
        if (GameSettings.MapHelper && HelperBuddy.Current != null)
            Dot(HelperBuddy.Current.transform.position, HelperColor, 10f, false, 0f);
        if (player != null)
            Dot(player.transform.position, FrogColor, 15f, true, player.transform.eulerAngles.y);

        for (int i = used; i < pool.Count; i++) if (pool[i].enabled) pool[i].enabled = false;
        VisibleDots = used;
    }

    private static bool BirdIsDizzy() { return BirdMovement.IsStunned; }

    private static Color PowerUpColor(PowerUpPickup p)
    {
        switch (p.type)
        {
            case PowerUpType.GoldenFly: return new Color(1f, 0.8f, 0.15f);
            case PowerUpType.SpeedBoost: return new Color(0.35f, 0.85f, 1f);
            default: return new Color(0.4f, 1f, 0.55f);
        }
    }

    private Image Dot(Vector3 world, Color color, float size, bool arrow, float yaw)
    {
        Rect r = dotsLayer.rect;
        float u = Mathf.InverseLerp(-mapHalf, mapHalf, world.x);
        float v = Mathf.InverseLerp(-mapHalf, mapHalf, world.z);
        if (u < 0f || u > 1f || v < 0f || v > 1f) return null;
        if (used >= pool.Count)
        {
            var go = new GameObject("Dot", typeof(RectTransform));
            go.transform.SetParent(dotsLayer, false);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            pool.Add(img);
        }
        Image dot = pool[used++];
        dot.enabled = true;
        dot.sprite = arrow ? arrowSprite : dotSprite;
        dot.color = color;
        float scale = GameSettings.MiniMapLarge ? 1.3f : 1f;
        RectTransform drt = dot.rectTransform;
        drt.sizeDelta = new Vector2(size, size) * scale;
        drt.anchoredPosition = new Vector2(u * r.width, v * r.height);
        drt.localEulerAngles = new Vector3(0f, 0f, arrow ? -yaw : 0f);
        if (dot.transform.GetSiblingIndex() != used - 1) dot.transform.SetSiblingIndex(used - 1);
        return dot;
    }

    // ------------------------------------------------------------ the picture

    private void TakeSnapshot()
    {
        snapshotWanted = false;
        mapHalf = PlayArea.HalfSize + 2f;
        try
        {
            if (snapshot == null)
            {
                snapshot = new RenderTexture(256, 256, 16) { name = "Mini map" };
            }
            if (mapCamera == null)
            {
                var go = new GameObject("Mini Map Camera");
                mapCamera = go.AddComponent<Camera>();
                mapCamera.enabled = false;
                mapCamera.orthographic = true;
                mapCamera.clearFlags = CameraClearFlags.SolidColor;
                mapCamera.backgroundColor = new Color(0.2f, 0.35f, 0.2f);
                mapCamera.cullingMask = ~(1 << 5); // everything but the UI
                mapCamera.nearClipPlane = 1f;
                mapCamera.farClipPlane = 200f;
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                data.renderPostProcessing = false;
                data.renderShadows = false;
            }
            mapCamera.orthographicSize = mapHalf;
            mapCamera.transform.position = new Vector3(0f, 80f, 0f);
            mapCamera.targetTexture = snapshot;

            // Leave the moving things out of the picture (they're shown as dots instead).
            var hidden = new List<Renderer>();
            Hide<PlayerHealth>(hidden); Hide<BirdMovement>(hidden); Hide<FlyPickUp>(hidden);
            Hide<PowerUpPickup>(hidden); Hide<SnakeHazard>(hidden); Hide<DriftingLog>(hidden); Hide<HelperBuddy>(hidden);
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            mapCamera.Render();
            RenderSettings.fog = fog;
            foreach (Renderer r in hidden) if (r != null) r.enabled = true;

            picture.texture = snapshot;
            picture.color = Color.white;
        }
        catch (System.Exception e)
        {
            Debug.Log("[MiniMap] Snapshot skipped: " + e.Message);
        }
    }

    private static void Hide<T>(List<Renderer> hidden) where T : Component
    {
        foreach (T c in FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            foreach (Renderer r in c.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                r.enabled = false;
                hidden.Add(r);
            }
        }
    }

    private static Sprite MakeSprite(bool arrow)
    {
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float fx = (x + 0.5f) / n * 2f - 1f, fy = (y + 0.5f) / n * 2f - 1f;
                float a;
                if (arrow)
                {
                    // A rounded arrowhead pointing up (+y).
                    float t = (fy + 1f) * 0.5f;           // 0 bottom .. 1 top
                    float halfWidth = Mathf.Lerp(0.9f, 0.05f, t);
                    float inside = halfWidth - Mathf.Abs(fx);
                    a = Mathf.Clamp01(inside * 12f) * Mathf.Clamp01((fy + 0.95f) * 12f);
                }
                else
                {
                    a = Mathf.Clamp01((1f - Mathf.Sqrt(fx * fx + fy * fy)) * 10f);
                }
                // dark rim so dots stand out on the grass
                float rim = arrow ? 1f : Mathf.Clamp01((Mathf.Sqrt(fx * fx + fy * fy) - 0.72f) * 8f);
                byte c = (byte)Mathf.RoundToInt(Mathf.Lerp(255f, 40f, rim));
                px[y * n + x] = new Color32(c, c, c, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
