using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// FrogsUI (added Nov 2026): the title screen, pause menu, settings and game-over scores.
//
// Everything here is built in code when the level loads, so the level file itself didn't
// need to change (and the automated play tests keep working the same way).
//
//   Title screen  - how to play (keyboard AND touch/mouse), difficulty, best score, Play
//   Pause menu    - Esc / P / the II button. Tabs: Sound, Graphics, Controls, Game
//   Game over     - your score, your best for this difficulty, and "New best!"
//
// Menus so good they'll make you hop-timistic.
public partial class FrogsUI : MonoBehaviour
{
    public static FrogsUI Instance { get; private set; }

    // Set by "Restart" so the next level load skips the title and starts straight away.
    private static bool startOnNextLoad;

    // ---- look ----
    private static readonly Color Ink = new Color(0.07f, 0.10f, 0.07f, 0.94f);
    private static readonly Color Card = new Color(0.11f, 0.16f, 0.10f, 0.97f);
    private static readonly Color ButtonColor = new Color(0.20f, 0.28f, 0.17f, 1f);
        private static readonly Color Accent = new Color(0.55f, 0.80f, 0.29f, 1f);   // lily-pad green
    private static readonly Color AccentDark = new Color(0.36f, 0.55f, 0.16f, 1f);
    private static readonly Color Gold = new Color(1f, 0.82f, 0.32f, 1f);
    private static readonly Color Soft = new Color(0.82f, 0.88f, 0.78f, 1f);
    private static readonly Color Muted = new Color(0.62f, 0.70f, 0.58f, 1f);

    private Font font;
    private RectTransform root;

    private GameObject titlePanel;
    private GameObject menuPanel;
    private GameObject hud;
    private GameObject gameOverPanel;
    private Text fpsText;
    private Text levelText;
    private Text statusText;
    private Text toastText;
    private Button zapButton;
    private Button dodgeButton, tongueButton;
    private Text dodgeLabel;
    private Text zapLabel;
    private float toastUntil;
    private Text titleBestText;
    private Text titleDifficultyText;
    private Text menuTitle;
    private Text gameOverText;
    private Button resumeButton;
    private Button restartButton;
    private Text quitButtonLabel;

    private readonly Dictionary<string, GameObject> tabs = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, Image> tabButtons = new Dictionary<string, Image>();
    private readonly List<Action> refreshers = new List<Action>();
    private string currentTab = "Shop";

    private bool paused;
    private string capturingAction; // key remapping: which action is waiting for a key
    private static KeyCode[] allKeys;

    // ---------------------------------------------------------------- setup

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureForScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForScene(scene);
    }

    // One set of menus per level, living in that level (so they reset when it reloads).
    private static void EnsureForScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        bool hasGame = false;
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.GetComponentInChildren<GameState>(true) != null) { hasGame = true; break; }
        }
        if (!hasGame) return; // not a game level
        if (Instance != null && Instance.gameObject.scene == scene) return;
        var go = new GameObject("Frogs UI");
        SceneManager.MoveGameObjectToScene(go, scene);
        // The round's helpers live alongside the menus, so they all reset together.
        go.AddComponent<Stylizer>();      // the cartoon look + new models (first, so the rest sees them)
        go.AddComponent<PlayArea>();      // the pond grows each level
        go.AddComponent<Biome>();         // lake, autumn and winter worlds (levels 4-6)
        go.AddComponent<Weather>();       // the odd rainstorm
        go.AddComponent<BossOwl>();       // the Great Owl finale
        go.AddComponent<FrogTongue>();    // tongue flick
        go.AddComponent<ClockPilot>();    // clock mode's autopilot
        go.AddComponent<GameDirector>();
        go.AddComponent<LevelManager>();
        go.AddComponent<HazardSpawner>();
        go.AddComponent<PowerUpSpawner>();
        go.AddComponent<FrogsUI>();
    }

    void Awake()
    {
        Instance = this;
        // A fresh level never starts as a clock by accident (Start() turns it on if asked).
        ClockMode.Active = false;
        AudioListener.volume = 1f;
        GameSettings.Changed += RefreshAll;
        ResponsiveCanvas.OrientationChanged += RefreshAll;
        Shop.Changed += RefreshAll;
        GameState.RoundStarted += OnRoundStarted;
        GameState.RoundEnded += OnRoundEnded;
        FrogEffects.Announce += Toast;
        LevelManager.LevelChanged += OnLevelChanged;
        LevelManager.Finished += OnSurvivedTheNight;
    }

    void OnDestroy()
    {
        GameSettings.Changed -= RefreshAll;
        ResponsiveCanvas.OrientationChanged -= RefreshAll;
        Shop.Changed -= RefreshAll;
        GameState.RoundStarted -= OnRoundStarted;
        GameState.RoundEnded -= OnRoundEnded;
        FrogEffects.Announce -= Toast;
        LevelManager.LevelChanged -= OnLevelChanged;
        LevelManager.Finished -= OnSurvivedTheNight;
        if (Instance == this) Instance = null;
        // Never leave the game frozen if the level unloads while paused.
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    void Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        font = FindGameFont();
        // The level's own score / "Game Over!" text covers the whole screen; make it
        // click-through so it never blocks taps meant for steering or for these menus.
        foreach (Graphic g in FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            g.raycastTarget = false;
        }
        EnsureEventSystem();
        BuildCanvas();
        BuildHud();
        MiniMap.Create(root, font);
        BuildTitle();
        BuildMenu();
        BuildGameOver();
        BuildCredits();
        BuildClock();
        RefreshAll();

        if (startOnNextLoad)
        {
            startOnNextLoad = false;
            ShowTitle(false);
            if (GameState.Current != null) GameState.Current.RequestStart();
        }
        else
        {
            ShowTitle(true);
            // "Open as clock": a spare screen goes straight into clock mode.
            if (GameSettings.ClockAtStart && !ClockMode.ExitedThisSession) StartCoroutine(EnterClockSoon());
        }
    }

    private Font FindGameFont()
    {
        // Reuse the game's own rounded font (Varela Round) from the score text.
        foreach (Text t in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.font != null) return t.font;
        }
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static void EnsureEventSystem()
    {
        EventSystem existing = FindFirstObjectByType<EventSystem>();
        GameObject es = existing != null ? existing.gameObject : new GameObject("EventSystem");
        if (existing == null) es.AddComponent<EventSystem>();
        // FIX (Nov 2026, found in the live play test): use our click handler, which never
        // ignores clicks/taps (see FrogsInputModule.cs), instead of Unity's standard one.
        foreach (BaseInputModule other in es.GetComponents<BaseInputModule>())
        {
            if (!(other is FrogsInputModule)) other.enabled = false;
        }
        if (es.GetComponent<FrogsInputModule>() == null) es.AddComponent<FrogsInputModule>();
    }

    private void BuildCanvas()
    {
        var canvasGo = new GameObject("Frogs UI Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.AddComponent<ResponsiveCanvas>(); // landscape or portrait scaling
        if (Camera.main != null && Camera.main.GetComponent<ScreenFitCamera>() == null)
            Camera.main.gameObject.AddComponent<ScreenFitCamera>(); // same view on any screen shape
        // Nov 2026: keep menus and buttons clear of phone notches and rounded corners.
        var safe = new GameObject("Safe Area", typeof(RectTransform));
        safe.transform.SetParent(canvasGo.transform, false);
        root = safe.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        safe.AddComponent<SafeAreaFitter>();
    }

    // ---------------------------------------------------------------- per frame

    void Update()
    {
        // Key remapping: wait for the next key press.
        if (capturingAction != null)
        {
            CaptureKey();
            return;
        }

        if (ClockShowing)
        {
            UpdateClock();
            return;
        }

        if (KeyBindings.PausePressed() && !CreditsShowing)
        {
            if (menuPanel.activeSelf) CloseMenu();
            else if (GameState.Current != null && GameState.Current.IsPlaying) OpenMenu(true);
        }

        UpdateHudText();
        UpdateCredits();

        if (fpsText != null)
        {
            bool show = GameSettings.ShowFps;
            if (fpsText.gameObject.activeSelf != show) fpsText.gameObject.SetActive(show);
            if (show)
            {
                fpsText.text = Mathf.RoundToInt(GraphicsManager.Fps) + " FPS  ·  " + GraphicsManager.ActiveTier
                               + (GameSettings.Graphics == GraphicsTier.Auto ? " (auto)" : "");
            }
        }

        // Pause automatically if the browser tab/app loses focus mid-round.
        // (Handled in OnApplicationPause/Focus below.)
    }

    void OnApplicationFocus(bool hasFocus)
    {
#if UNITY_EDITOR
        return; // (the automated tests run without window focus)
#else
        if (!hasFocus && !ClockMode.Active && GameState.Current != null && GameState.Current.IsPlaying && !paused && menuPanel != null)
        {
            OpenMenu(true);
        }
#endif
    }

    // ---------------------------------------------------------------- game events

    private void OnRoundStarted()
    {
        ShowTitle(false);
        hud.SetActive(!ClockMode.Active);
        gameOverPanel.SetActive(false);
    }

    private void OnRoundEnded(int score, bool newBest)
    {
        hud.SetActive(false);
        if (menuPanel.activeSelf) CloseMenu();
        Difficulty d = GameSettings.Difficulty;
        string reached = LevelManager.Instance != null && LevelManager.Instance.Level > 1
            ? "Made it to Level " + LevelManager.Instance.Level + ": " + LevelManager.Instance.LevelName + "\n" : "";
        gameOverText.text = reached + "You ate " + score + (score == 1 ? " fly" : " flies")
                            + "\nBest on " + d + ": " + GameSettings.GetBestScore(d)
                            + (newBest && score > 0 ? "\n<color=#FFD152>New best!</color>" : "");
        gameOverPanel.SetActive(true);
    }

    // ---------------------------------------------------------------- title

    private void BuildTitle()
    {
        titlePanel = Panel("Title Screen", root, new Color(0f, 0f, 0f, 0.45f));
        Stretch(titlePanel);

        RectTransform card = CardColumn("Title Card", titlePanel.transform, 760f, 22f, 18f);

        var title = Label(card, "FROGS-N-LOGS", 64, Accent, FontStyle.Bold, TextAnchor.MiddleCenter);
        title.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
        Label(card, "Eat the flies. Don't get eaten by the bird.", 24, Soft, FontStyle.Normal, TextAnchor.MiddleCenter);

        // How to play: both control styles, side by side.
        RectTransform how = Row("How to play", card, 16f);
        ControlsBox(how, "KEYBOARD",
            "Move:  W A S D  or  arrow keys\nTongue:  E     Dodge hop:  Shift\nStart:  Space     Pause:  Esc or P");
        ControlsBox(how, "TOUCH & MOUSE",
            "Move:  hold where you want to hop\nTONGUE and DODGE buttons\nStart:  tap Play     Pause:  II");
        // Phones held upright: stack the two boxes so everything can be bigger.
        refreshers.Add(() => SetRowVertical(how, ScreenLayout.IsPortrait));
        portraitRows.Add(how);

        // Difficulty
        Label(card, "DIFFICULTY", 18, Muted, FontStyle.Bold, TextAnchor.MiddleCenter);
        RectTransform diffRow = Row("Difficulty", card, 10f);
        foreach (Difficulty d in new[] { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard })
        {
            Difficulty captured = d;
            Button b = MakeButton(diffRow, d.ToString(), 24, () => GameSettings.Difficulty = captured, 150f, 54f);
            Image img = b.GetComponent<Image>();
            refreshers.Add(() => SetSelected(img, GameSettings.Difficulty == captured));
        }
        // Level select: start at any level you've reached, with your best stars for each.
        Label(card, "START AT LEVEL", 18, Muted, FontStyle.Bold, TextAnchor.MiddleCenter);
        var levelsGo = new GameObject("Level Select", typeof(RectTransform));
        levelsGo.transform.SetParent(card, false);
        var grid = levelsGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(118f, 66f);
        grid.spacing = new Vector2(6f, 6f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 6;
        grid.childAlignment = TextAnchor.MiddleCenter;
        var gridLayout = levelsGo.AddComponent<LayoutElement>();
        gridLayout.preferredWidth = 6 * 118f + 5 * 6f;
        refreshers.Add(() =>
        {
            int cols = ScreenLayout.IsPortrait ? 3 : 6;
            grid.constraintCount = cols;
            int rows = Mathf.CeilToInt(LevelProgress.LevelCount / (float)cols);
            gridLayout.preferredWidth = cols * 118f + (cols - 1) * 6f;
            gridLayout.preferredHeight = rows * 66f + (rows - 1) * 6f;
        });
        string[] shortNames = { "Pond", "Swamp", "Night", "Lake", "Autumn", "Winter" };
        for (int i = 1; i <= LevelProgress.LevelCount; i++)
        {
            int level = i;
            Button b = MakeButton(levelsGo.transform, "", 16, () =>
            {
                if (LevelProgress.IsUnlocked(level)) LevelProgress.StartLevel = level;
            }, 0f, 0f);
            Text t = b.GetComponentInChildren<Text>();
            t.rectTransform.offsetMin = new Vector2(0f, 18f);
            Image img = b.GetComponent<Image>();
            Image[] stars = StarRow(b.transform);
            refreshers.Add(() =>
            {
                bool open = LevelProgress.IsUnlocked(level);
                b.interactable = open;
                t.text = open ? level + "  " + shortNames[level - 1] : level + "  (locked)";
                t.color = open ? Soft : Muted;
                SetSelected(img, LevelProgress.StartLevel == level);
                int got = LevelProgress.Stars(level);
                for (int s = 0; s < 3; s++) stars[s].color = s < got ? Gold : new Color(1f, 1f, 1f, open ? 0.18f : 0.06f);
            });
        }

        Text daily = Label(card, "", 19, Soft, FontStyle.Normal, TextAnchor.MiddleCenter);
        refreshers.Add(() => daily.text = "<color=#8BC34A><b>TODAY'S CHALLENGE</b></color>  " + DailyChallenge.Goal
            + (DailyChallenge.Done ? "   <color=#FFD152>Done!</color>" : "   <color=#9EB394>(+" + DailyChallenge.Reward + " flies)</color>"));
        titleDifficultyText = Label(card, "", 20, Muted, FontStyle.Italic, TextAnchor.MiddleCenter);
        titleBestText = Label(card, "", 22, Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
        refreshers.Add(() =>
        {
            Difficulty d = GameSettings.Difficulty;
            titleDifficultyText.text = DifficultyRules.Describe(d);
            int best = GameSettings.GetBestScore(d);
            titleBestText.text = best > 0 ? "Best on " + d + ": " + best + (best == 1 ? " fly" : " flies") : "No best score yet. Go get 'em!";
        });

        RectTransform buttons = Row("Title Buttons", card, 14f);
        Button play = MakeButton(buttons, "PLAY", 34, () =>
        {
            if (GameState.Current != null) GameState.Current.RequestStart();
        }, 230f, 76f);
        play.GetComponent<Image>().color = Accent;
        play.GetComponentInChildren<Text>().color = Ink;
        MakeButton(buttons, "Settings", 24, () => OpenMenu(false), 160f, 76f);
        MakeButton(buttons, "Credits", 24, () => ShowCredits(false), 140f, 76f);
        // Nov 2026: the frog plays by itself under a big clock (FrogsUIClock.cs).
        MakeButton(buttons, "Clock", 24, EnterClock, 130f, 76f);
    }

    private readonly List<RectTransform> portraitRows = new List<RectTransform>();

    // Switch a row of boxes between side by side (landscape) and stacked (portrait).
    private static void SetRowVertical(RectTransform row, bool vertical)
    {
        bool isVertical = row.GetComponent<VerticalLayoutGroup>() != null;
        if (isVertical == vertical) return;
        HorizontalOrVerticalLayoutGroup old = row.GetComponent<HorizontalOrVerticalLayoutGroup>();
        float spacing = old != null ? old.spacing : 12f;
        if (old != null) DestroyImmediate(old);
        HorizontalOrVerticalLayoutGroup g = vertical
            ? (HorizontalOrVerticalLayoutGroup)row.gameObject.AddComponent<VerticalLayoutGroup>()
            : row.gameObject.AddComponent<HorizontalLayoutGroup>();
        g.spacing = spacing;
        g.childAlignment = TextAnchor.MiddleCenter;
        g.childControlWidth = true;
        g.childControlHeight = true;
        g.childForceExpandWidth = false;
        g.childForceExpandHeight = false;
        LayoutRebuilder.MarkLayoutForRebuild(row);
    }

    // Three little stars along the bottom of a button (level select).
    private Image[] StarRow(Transform button)
    {
        var row = new GameObject("Stars", typeof(RectTransform));
        row.transform.SetParent(button, false);
        var rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 5f);
        rt.sizeDelta = new Vector2(0f, 18f);
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 3f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = false;
        h.childControlHeight = false;
        h.childForceExpandWidth = false;
        var stars = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var s = new GameObject("Star", typeof(RectTransform));
            s.transform.SetParent(row.transform, false);
            s.GetComponent<RectTransform>().sizeDelta = new Vector2(17f, 17f);
            stars[i] = s.AddComponent<Image>();
            stars[i].sprite = StarSprite();
            stars[i].raycastTarget = false;
        }
        return stars;
    }

    private static Sprite starSprite;

    private static Sprite StarSprite()
    {
        if (starSprite != null) return starSprite;
        const int n = 48;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                // A five-pointed star: radius wobbles between inner and outer by angle.
                float fx = (x + 0.5f) / n * 2f - 1f, fy = (y + 0.5f) / n * 2f - 1f;
                float ang = Mathf.Atan2(fx, fy);
                float r = Mathf.Sqrt(fx * fx + fy * fy);
                float k = Mathf.Abs(Mathf.Repeat(ang / (Mathf.PI * 2f / 5f), 1f) - 0.5f) * 2f; // 0 at points, 1 between
                float edge = Mathf.Lerp(0.95f, 0.42f, k);
                float a = Mathf.Clamp01((edge - r) * 14f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        starSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return starSprite;
    }

    private void ControlsBox(RectTransform parent, string heading, string body)
    {
        RectTransform box = CardColumn(heading, parent, 340f, 14f, 6f, new Color(1f, 1f, 1f, 0.06f));
        Label(box, heading, 18, Accent, FontStyle.Bold, TextAnchor.MiddleLeft);
        Label(box, body, 19, Soft, FontStyle.Normal, TextAnchor.UpperLeft);
    }

    private void ShowTitle(bool show)
    {
        titlePanel.SetActive(show);
        // The 2017 "0 Flies" counter peeked out from behind the title card on some screens.
        foreach (ScoreCounter counter in FindObjectsByType<ScoreCounter>(FindObjectsSortMode.None))
        {
            Graphic g = counter.GetComponent<Graphic>();
            if (g != null) g.enabled = !show;
        }
        hud.SetActive(!show && GameState.Current != null && GameState.Current.IsPlaying);
        GameState.MenuOpen = show && menuPanel.activeSelf;
    }

    // ---------------------------------------------------------------- HUD

    private void BuildHud()
    {
        hud = new GameObject("HUD", typeof(RectTransform));
        hud.transform.SetParent(root, false);
        Stretch(hud);

        Button pause = MakeButton(hud.GetComponent<RectTransform>(), "II", 30, () => OpenMenu(true), 72f, 72f);
        var rt = pause.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20f, -20f);
        pause.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);

        zapButton = MakeButton(hud.GetComponent<RectTransform>(), "ZAP", 24, () =>
        {
            if (GameDirector.Instance != null) GameDirector.Instance.FireLaser();
        }, 110f, 110f);
        var zrt = zapButton.GetComponent<RectTransform>();
        zrt.anchorMin = zrt.anchorMax = new Vector2(1f, 0f);
        zrt.pivot = new Vector2(1f, 0f);
        zrt.anchoredPosition = new Vector2(-24f, 150f);
        zapButton.GetComponent<Image>().color = new Color(0.75f, 0.12f, 0.18f, 0.75f);
        zapLabel = zapButton.GetComponentInChildren<Text>();

        // Nov 2026: dodge hop and tongue flick buttons (keyboard: Shift and E).
        dodgeButton = MakeButton(hud.GetComponent<RectTransform>(), "DODGE", 22, () =>
        {
            if (PlayerMovement.Instance != null) PlayerMovement.Instance.Dodge();
        }, 110f, 110f);
        var drt = dodgeButton.GetComponent<RectTransform>();
        drt.anchorMin = drt.anchorMax = new Vector2(1f, 0f);
        drt.pivot = new Vector2(1f, 0f);
        drt.anchoredPosition = new Vector2(-24f, 24f);
        dodgeButton.GetComponent<Image>().color = new Color(0.25f, 0.55f, 0.9f, 0.7f);
        dodgeLabel = dodgeButton.GetComponentInChildren<Text>();

        tongueButton = MakeButton(hud.GetComponent<RectTransform>(), "TONGUE", 20, () =>
        {
            if (FrogTongue.Instance != null) FrogTongue.Instance.Flick();
        }, 110f, 110f);
        var trt = tongueButton.GetComponent<RectTransform>();
        trt.anchorMin = trt.anchorMax = new Vector2(1f, 0f);
        trt.pivot = new Vector2(1f, 0f);
        trt.anchoredPosition = new Vector2(-150f, 24f);
        tongueButton.GetComponent<Image>().color = new Color(0.95f, 0.4f, 0.55f, 0.7f);

        fpsText = Label(hud.GetComponent<RectTransform>(), "", 20, Soft, FontStyle.Bold, TextAnchor.LowerLeft);
        var frt = fpsText.rectTransform;
        frt.anchorMin = frt.anchorMax = new Vector2(0f, 0f);
        frt.pivot = new Vector2(0f, 0f);
        frt.anchoredPosition = new Vector2(20f, 16f);
        frt.sizeDelta = new Vector2(420f, 30f);
        Destroy(fpsText.GetComponent<LayoutElement>());
        fpsText.gameObject.AddComponent<Shadow>();

        // Level + fly goal (top centre) and power-up status underneath.
        levelText = HudText("Level", 26, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(900f, 34f), Soft);
        statusText = HudText("Status", 22, new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(900f, 30f), Gold);
        hud.SetActive(false);

        // Big announcements ("Level 2: The Swamp", "Golden fly! +5") - outside the HUD so
        // they can show over the game-over screen too.
        toastText = HudText("Toast", 44, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1100f, 60f), Gold);
        toastText.transform.SetParent(root, false);
        toastText.gameObject.SetActive(false);
    }

    private Text HudText(string name, int size, Vector2 anchor, Vector2 position, Vector2 box, Color color)
    {
        Text t = Label(hud.GetComponent<RectTransform>(), "", size, color, FontStyle.Bold, TextAnchor.MiddleCenter);
        t.name = name;
        Destroy(t.GetComponent<LayoutElement>());
        var rt = t.rectTransform;
        // Nov 2026: stretch across the screen (clear of the pause button) and shrink long lines
        // to fit, so nothing runs off the edge of a narrow phone held upright.
        rt.anchorMin = new Vector2(0f, anchor.y);
        rt.anchorMax = new Vector2(1f, anchor.y);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(-200f, box.y);
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = Mathf.Max(12, size / 2);
        t.resizeTextMaxSize = size;
        var outline = t.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        return t;
    }

    public void Toast(string message)
    {
        if (toastText == null || ClockMode.Active) return;
        toastText.text = message;
        toastText.color = Gold;
        toastText.gameObject.SetActive(true);
        toastUntil = Time.unscaledTime + 2.2f;
    }

    private void OnLevelChanged(int level, string name)
    {
        Toast("Level " + level + ": " + name);
    }

    private void UpdateHudText()
    {
        if (toastText != null && toastText.gameObject.activeSelf)
        {
            float left = toastUntil - Time.unscaledTime;
            if (left <= 0f) toastText.gameObject.SetActive(false);
            else
            {
                Color c = toastText.color;
                c.a = Mathf.Clamp01(left / 0.5f);
                toastText.color = c;
            }
        }
        if (!hud.activeSelf || levelText == null) return;

        LevelManager levels = LevelManager.Instance;
        if (levels != null)
        {
            int goal = levels.NextGoal;
            string next = levels.IsFinalLevel ? "face the Great Owl" : "reach " + LevelManager.Names[levels.Level];
            string goalText;
            if (levels.BossActive && BossOwl.Active != null) goalText = "     Survive the Great Owl: " + Mathf.CeilToInt(BossOwl.Active.SecondsLeft) + "s";
            else if (goal > 0) goalText = "     " + ScoreCounter.score + " / " + goal + " flies to " + next;
            else goalText = "     Bonus round: how long can you last?";
            levelText.text = "Level " + levels.Level + " · " + levels.LevelName + goalText;
        }

        string status = "";
        if (FrogEffects.Boosted) status += "Speed boost " + Mathf.CeilToInt(FrogEffects.BoostSecondsLeft) + "s   ";
        if (FrogEffects.ShieldActive) status += "Lily-pad shield up   ";
        if (BirdMovement.IsStunned && BossOwl.Active == null) status += "Bird is dizzy!   ";
        if (HidingSpot.FrogHidden) status += "Hidden in the reeds " + Mathf.CeilToInt(HidingSpot.HiddenSecondsLeft) + "s   ";
        if (PlayerMovement.Stuck) status += "Stuck in the ice!   ";
        if (FlyKind.StungUntil > Time.time) status += "Stung!   ";
        if (Weather.Raining) status += "Rainstorm   ";
        if (GameDirector.Instance != null && GameDirector.Instance.Combo >= 2) status += "Combo x" + GameDirector.Instance.Combo + "   ";
        if (Cheats.Enabled) status += "<color=#FF9EE5>Cheats on</color>   ";
        statusText.text = status.TrimEnd();

        if (dodgeLabel != null)
        {
            float wait = PlayerMovement.DodgeReadyAt - Time.time;
            dodgeLabel.text = wait > 0f ? wait.ToString("0.0") : "DODGE";
        }

        if (zapButton != null)
        {
            bool hasLaser = Shop.HasLaser;
            if (zapButton.gameObject.activeSelf != hasLaser) zapButton.gameObject.SetActive(hasLaser);
            if (hasLaser && GameDirector.Instance != null)
            {
                float wait = GameDirector.Instance.LaserReadyAt - Time.time;
                zapLabel.text = wait > 0f ? Mathf.CeilToInt(wait) + "s" : "ZAP";
            }
        }
    }

    // ---------------------------------------------------------------- credits

    private GameObject creditsPanel;
    private RectTransform creditsScroll;
    private Text creditsHeading;
    private GameObject creditsEndButtons;
    private bool creditsRolling;
    private bool creditsFromEnding;

    public bool CreditsShowing { get { return creditsPanel != null && creditsPanel.activeSelf; } }

    private void BuildCredits()
    {
        creditsPanel = Panel("Credits", root, new Color(0.03f, 0.06f, 0.03f, 0.92f));
        Stretch(creditsPanel);

        creditsHeading = Label(creditsPanel.transform, "", 40, Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
        Destroy(creditsHeading.GetComponent<LayoutElement>());
        var hrt = creditsHeading.rectTransform;
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.anchoredPosition = new Vector2(0f, -24f);
        hrt.sizeDelta = new Vector2(0f, 60f);

        // A mask window the credits scroll through.
        var window = new GameObject("Credits Window", typeof(RectTransform));
        window.transform.SetParent(creditsPanel.transform, false);
        var wrt = window.GetComponent<RectTransform>();
        wrt.anchorMin = new Vector2(0.1f, 0.16f);
        wrt.anchorMax = new Vector2(0.9f, 0.86f);
        wrt.offsetMin = wrt.offsetMax = Vector2.zero;
        window.AddComponent<RectMask2D>();

        Text body = Label(window.transform, Credits.Text, 26, Soft, FontStyle.Normal, TextAnchor.UpperCenter);
        Destroy(body.GetComponent<LayoutElement>());
        creditsScroll = body.rectTransform;
        creditsScroll.anchorMin = new Vector2(0f, 1f);
        creditsScroll.anchorMax = new Vector2(1f, 1f);
        creditsScroll.pivot = new Vector2(0.5f, 1f);
        creditsScroll.sizeDelta = new Vector2(0f, 2400f);
        body.lineSpacing = 1.1f;

        creditsEndButtons = new GameObject("Credits Buttons", typeof(RectTransform));
        creditsEndButtons.transform.SetParent(creditsPanel.transform, false);
        var brt = creditsEndButtons.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0f, 24f);
        var h = creditsEndButtons.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 16f;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        var fit = creditsEndButtons.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Button keep = MakeButton(creditsEndButtons.transform, "Keep playing", 24, CloseCredits, 220f, 64f);
        keep.GetComponent<Image>().color = Accent;
        keep.GetComponentInChildren<Text>().color = Ink;
        MakeButton(creditsEndButtons.transform, "Back to title", 24, () =>
        {
            CloseCredits();
            if (creditsFromEnding) ReloadLevel(false);
        }, 220f, 64f);
        creditsPanel.SetActive(false);
    }

    // fromEnding: the roll after surviving the Night (pauses the game, offers Keep playing).
    public void ShowCredits(bool fromEnding)
    {
        creditsFromEnding = fromEnding;
        creditsHeading.text = fromEnding ? "You beat the Great Owl!" : "Credits";
        creditsEndButtons.transform.GetChild(0).gameObject.SetActive(fromEnding);
        creditsEndButtons.transform.GetChild(1).GetComponentInChildren<Text>().text = fromEnding ? "Back to title" : "Back";
        creditsScroll.anchoredPosition = new Vector2(0f, -560f);
        creditsRolling = true;
        creditsPanel.SetActive(true);
        GameState.MenuOpen = true;
        if (fromEnding)
        {
            Time.timeScale = 0f;
            paused = true;
            hud.SetActive(false);
            AudioDirector.Play("Win", 0.9f);
        }
        if (AudioDirector.Instance != null) AudioDirector.Instance.PlayMusic(AudioDirector.TitleTrack);
    }

    public void CloseCredits()
    {
        creditsPanel.SetActive(false);
        creditsRolling = false;
        GameState.MenuOpen = titlePanel.activeSelf && menuPanel.activeSelf;
        if (creditsFromEnding)
        {
            paused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            bool inRound = GameState.Current != null && GameState.Current.IsPlaying;
            hud.SetActive(inRound);
            if (inRound && AudioDirector.Instance != null) AudioDirector.Instance.PlayMusic(AudioDirector.LevelTracks[2]);
        }
    }

    private void OnSurvivedTheNight()
    {
        Achievements.Unlock("survived");
        ShowCredits(true);
    }

    private void UpdateCredits()
    {
        if (!creditsRolling || creditsScroll == null) return;
        Vector2 p = creditsScroll.anchoredPosition;
        p.y += 55f * Time.unscaledDeltaTime;          // gentle roll upward
        if (p.y > creditsScroll.rect.height * 0.62f) p.y = -560f; // loop
        creditsScroll.anchoredPosition = p;
        // Scroll by hand with the mouse wheel / arrow keys too.
        p.y -= Input.mouseScrollDelta.y * 40f;
        creditsScroll.anchoredPosition = p;
        if (Input.GetKeyDown(KeyCode.Escape)) CloseCredits();
    }

    // ---------------------------------------------------------------- game over

    private void BuildGameOver()
    {
        gameOverPanel = new GameObject("Game Over Scores", typeof(RectTransform));
        gameOverPanel.transform.SetParent(root, false);
        var rt = gameOverPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.05f, 0.5f);
        rt.anchorMax = new Vector2(0.95f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -120f);
        rt.sizeDelta = new Vector2(0f, 160f);
        gameOverText = gameOverPanel.AddComponent<Text>();
        gameOverText.font = font;
        gameOverText.fontSize = 32;
        gameOverText.alignment = TextAnchor.UpperCenter;
        gameOverText.color = Color.white;
        gameOverText.supportRichText = true;
        gameOverText.horizontalOverflow = HorizontalWrapMode.Wrap;
        gameOverText.verticalOverflow = VerticalWrapMode.Overflow;
        gameOverText.resizeTextForBestFit = true;
        gameOverText.resizeTextMinSize = 18;
        gameOverText.resizeTextMaxSize = 32;
        gameOverText.raycastTarget = false;
        gameOverPanel.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
        gameOverPanel.SetActive(false);
    }

    // ---------------------------------------------------------------- pause / settings menu

    private void BuildMenu()
    {
        menuPanel = Panel("Pause Menu", root, new Color(0f, 0f, 0f, 0.55f));
        Stretch(menuPanel);
        RectTransform card = CardColumn("Menu Card", menuPanel.transform, 820f, 22f, 14f);

        menuTitle = Label(card, "Paused", 46, Accent, FontStyle.Bold, TextAnchor.MiddleCenter);

        RectTransform tabRow = Row("Tabs", card, 8f);
        foreach (string name in new[] { "Shop", "Sound", "Graphics", "Controls", "Extras", "Cheats", "Game" })
        {
            string captured = name;
            Button b = MakeButton(tabRow, name, 19, () => ShowTab(captured), 103f, 50f);
            tabButtons[name] = b.GetComponent<Image>();
        }

        // Fixed-height content area so the menu doesn't jump around between tabs.
        var content = new GameObject("Tab Content", typeof(RectTransform));
        content.transform.SetParent(card, false);
        var contentLe = content.AddComponent<LayoutElement>();
        contentLe.preferredHeight = 400f;
        contentLe.preferredWidth = 760f;

        tabs["Shop"] = BuildShopTab(content.transform);
        tabs["Sound"] = BuildSoundTab(content.transform);
        tabs["Graphics"] = BuildGraphicsTab(content.transform);
        tabs["Controls"] = BuildControlsTab(content.transform);
        tabs["Extras"] = BuildExtrasTab(content.transform);
        tabs["Cheats"] = BuildCheatsTab(content.transform);
        tabs["Game"] = BuildGameTab(content.transform);

        RectTransform bottom = Row("Menu Buttons", card, 14f);
        resumeButton = MakeButton(bottom, "Resume", 26, CloseMenu, 220f, 64f);
        resumeButton.GetComponent<Image>().color = Accent;
        resumeButton.GetComponentInChildren<Text>().color = Ink;
        restartButton = MakeButton(bottom, "Restart", 24, () => ReloadLevel(true), 180f, 64f);
        Button quit = MakeButton(bottom, "Quit to title", 24, () => ReloadLevel(false), 220f, 64f);
        quitButtonLabel = quit.GetComponentInChildren<Text>();

        menuPanel.SetActive(false);
        ShowTab(currentTab);
    }

    // Nov 2026: every tab scrolls (drag, swipe or mouse wheel), so a tab with lots in it
    // still fits on small screens. Returns the scrolling content.
    private GameObject TabPage(Transform parent, string name)
    {
        var viewport = new GameObject(name + " Tab", typeof(RectTransform));
        viewport.transform.SetParent(parent, false);
        Stretch(viewport);
        viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // catches drags
        viewport.AddComponent<RectMask2D>();
        var scroll = viewport.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        var page = new GameObject("Content", typeof(RectTransform));
        page.transform.SetParent(viewport.transform, false);
        var prt = page.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0f, 1f);
        prt.anchorMax = new Vector2(1f, 1f);
        prt.pivot = new Vector2(0.5f, 1f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = Vector2.zero;
        page.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = prt;
        var v = page.AddComponent<VerticalLayoutGroup>();
        v.spacing = 12f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        v.padding = new RectOffset(10, 10, 6, 6);
        return page;
    }

    private GameObject BuildShopTab(Transform parent)
    {
        GameObject page = TabPage(parent, "Shop");
        RectTransform rt = page.GetComponent<RectTransform>();
        page.GetComponent<VerticalLayoutGroup>().spacing = 8f;

        Text wallet = Label(rt, "", 24, Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
        refreshers.Add(() => wallet.text = "Fly wallet: " + Shop.Wallet + (Shop.Wallet == 1 ? " fly" : " flies")
                                           + "   <size=16><color=#9EB394>(every fly you eat goes in here)</color></size>");

        var gridGo = new GameObject("Shop Items", typeof(RectTransform));
        gridGo.transform.SetParent(rt, false);
        var grid = gridGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(370f, 52f);
        grid.spacing = new Vector2(10f, 5f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.UpperCenter;
        gridGo.AddComponent<LayoutElement>().preferredHeight = Mathf.CeilToInt(Shop.Items.Length / 2f) * 57f;

        foreach (Shop.Item item in Shop.Items)
        {
            Shop.Item captured = item;
            var cell = new GameObject(item.name, typeof(RectTransform));
            cell.transform.SetParent(gridGo.transform, false);
            cell.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);
            var h = cell.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(10, 6, 4, 4);
            h.spacing = 8f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;

            Text label = Label(cell.transform, "", 19, Soft, FontStyle.Normal, TextAnchor.MiddleLeft);
            label.GetComponent<LayoutElement>().preferredWidth = 220f;
            label.GetComponent<LayoutElement>().flexibleWidth = 1f;
            Button buy = MakeButton(cell.transform, "", 18, () =>
            {
                if (Shop.Owns(captured.id) && captured.group != "upgrade") Shop.Equip(captured.id);
                else if (!Shop.TryBuy(captured.id) && !Shop.IsMaxed(captured)) Toast("Not enough flies yet - keep eating!");
            }, 124f, 0f);
            Text buyText = buy.GetComponentInChildren<Text>();
            Image buyImage = buy.GetComponent<Image>();

            refreshers.Add(() =>
            {
                int level = Shop.LevelOf(captured.id);
                string levels = captured.costs.Length > 1 ? "  (" + level + "/" + captured.costs.Length + ")" : "";
                label.text = "<b>" + captured.name + "</b>" + levels + "\n<size=15><color=#9EB394>" + captured.description + "</color></size>";
                bool owned = Shop.Owns(captured.id);
                if (captured.group == "upgrade")
                {
                    buyText.text = Shop.IsMaxed(captured) ? (captured.costs.Length > 1 ? "Maxed" : "Owned") : "Buy " + Shop.NextCost(captured);
                }
                else
                {
                    bool buddy = captured.group == "helper";
                    buyText.text = owned ? (Shop.IsEquipped(captured.id) ? (buddy ? "With you" : "Wearing") : (buddy ? "Bring" : "Wear"))
                                         : "Buy " + Shop.NextCost(captured);
                }
                bool affordable = Shop.IsMaxed(captured) || owned || Shop.Wallet >= Shop.NextCost(captured);
                SetSelected(buyImage, Shop.IsEquipped(captured.id) || (captured.group == "upgrade" && Shop.IsMaxed(captured)));
                buyText.color = affordable ? Soft : Muted * 0.8f;
            });
        }
        return page;
    }

    private GameObject BuildSoundTab(Transform parent)
    {
        GameObject page = TabPage(parent, "Sound");
        RectTransform rt = page.GetComponent<RectTransform>();

        SliderRow(rt, "Master volume", () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, null, null);
        SliderRow(rt, "Music & swamp sounds", () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v,
            () => GameSettings.MusicOn, on => GameSettings.MusicOn = on);
        SliderRow(rt, "Sound effects", () => GameSettings.EffectsVolume, v => GameSettings.EffectsVolume = v,
            () => GameSettings.EffectsOn, on => GameSettings.EffectsOn = on);
        Label(rt, "Sound effects = hops, bird calls, buzzing flies and sparkles.", 18, Muted, FontStyle.Italic, TextAnchor.MiddleCenter);
        return page;
    }

    private GameObject BuildGraphicsTab(Transform parent)
    {
        GameObject page = TabPage(parent, "Graphics");
        RectTransform rt = page.GetComponent<RectTransform>();

        Label(rt, "QUALITY", 18, Muted, FontStyle.Bold, TextAnchor.MiddleLeft);
        RectTransform q = Row("Quality", rt, 8f);
        foreach (GraphicsTier t in new[] { GraphicsTier.Auto, GraphicsTier.Low, GraphicsTier.Medium, GraphicsTier.High })
        {
            GraphicsTier captured = t;
            Button b = MakeButton(q, t.ToString(), 22, () => GameSettings.Graphics = captured, 150f, 48f);
            Image img = b.GetComponent<Image>();
            refreshers.Add(() => SetSelected(img, GameSettings.Graphics == captured));
        }
        Text qualityNote = Label(rt, "", 17, Muted, FontStyle.Italic, TextAnchor.MiddleLeft);
        refreshers.Add(() => qualityNote.text = GameSettings.Graphics == GraphicsTier.Auto
            ? "Auto picks the best setting for your device after a few seconds of play (now: " + GraphicsManager.ActiveTier + ")."
            : "Low is smoothest on phones and older iPads. High is the full 2017 look.");

        Label(rt, "FILTER", 18, Muted, FontStyle.Bold, TextAnchor.MiddleLeft);
        var gridGo = new GameObject("Filters", typeof(RectTransform));
        gridGo.transform.SetParent(rt, false);
        var grid = gridGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(176f, 46f);
        grid.spacing = new Vector2(8f, 8f);
        grid.childAlignment = TextAnchor.UpperLeft;
        gridGo.AddComponent<LayoutElement>().preferredHeight = 100f;
        foreach (ScreenFilterMode m in Enum.GetValues(typeof(ScreenFilterMode)))
        {
            ScreenFilterMode captured = m;
            Button b = MakeButton(gridGo.GetComponent<RectTransform>(), ScreenFilter.Label(m), 20, () => GameSettings.Filter = captured, 0f, 0f);
            Image img = b.GetComponent<Image>();
            refreshers.Add(() => SetSelected(img, GameSettings.Filter == captured));
        }

        Label(rt, "FRAME RATE", 18, Muted, FontStyle.Bold, TextAnchor.MiddleLeft);
        RectTransform fr = Row("Frame rate", rt, 8f);
        foreach (FrameRateCap c in new[] { FrameRateCap.Fps30, FrameRateCap.Fps60, FrameRateCap.MatchScreen })
        {
            FrameRateCap captured = c;
            string label = c == FrameRateCap.Fps30 ? "30" : c == FrameRateCap.Fps60 ? "60" : "Match screen";
            Button b = MakeButton(fr, label, 22, () => GameSettings.FrameRate = captured, c == FrameRateCap.MatchScreen ? 220f : 110f, 48f);
            Image img = b.GetComponent<Image>();
            refreshers.Add(() => SetSelected(img, GameSettings.FrameRate == captured));
        }
        Text frNote = Label(rt, "", 17, Muted, FontStyle.Italic, TextAnchor.MiddleLeft);
        refreshers.Add(() => frNote.text = GameSettings.FrameRate == FrameRateCap.Fps30
            ? "30 saves battery."
            : GameSettings.FrameRate == FrameRateCap.Fps60 ? "Smooth on every device."
            : "As smooth as your screen allows (this screen: " + GraphicsManager.ScreenRefreshRate + " Hz).");

        ToggleRow(rt, "Show frame rate (FPS)", () => GameSettings.ShowFps, on => GameSettings.ShowFps = on);
        return page;
    }

    private GameObject BuildControlsTab(Transform parent)
    {
        GameObject page = TabPage(parent, "Controls");
        RectTransform rt = page.GetComponent<RectTransform>();
        var v = page.GetComponent<VerticalLayoutGroup>();
        v.spacing = 6f;

        foreach (string action in KeyBindings.Actions)
        {
            string captured = action;
            RectTransform row = Row(action, rt, 12f);
            Text name = Label(row, KeyBindings.Label(action), 22, Soft, FontStyle.Normal, TextAnchor.MiddleLeft);
            name.GetComponent<LayoutElement>().preferredWidth = 300f;
            Button keyButton = MakeButton(row, "", 22, () => BeginCapture(captured), 220f, 44f);
            Text keyLabel = keyButton.GetComponentInChildren<Text>();
            Image keyImage = keyButton.GetComponent<Image>();
            refreshers.Add(() =>
            {
                bool waiting = capturingAction == captured;
                keyLabel.text = waiting ? "Press a key..." : KeyBindings.KeyName(KeyBindings.Get(captured));
                SetSelected(keyImage, waiting);
            });
        }

        RectTransform bottom = Row("Controls Bottom", rt, 12f);
        Label(bottom, "Arrow keys always work too. Esc always pauses.", 17, Muted, FontStyle.Italic, TextAnchor.MiddleLeft)
            .GetComponent<LayoutElement>().preferredWidth = 470f;
        MakeButton(bottom, "Reset keys", 20, KeyBindings.ResetAll, 170f, 42f);
        return page;
    }

    // Nov 2026: helper buddy and mini map options.
    private GameObject BuildExtrasTab(Transform parent)
    {
        GameObject page = TabPage(parent, "Extras");
        RectTransform rt = page.GetComponent<RectTransform>();
        page.GetComponent<VerticalLayoutGroup>().spacing = 8f;

        Label(rt, "HELPER BUDDY", 18, Muted, FontStyle.Bold, TextAnchor.MiddleLeft);
        ToggleRow(rt, "Bring a helper buddy (chases the bird and snakes away)", () => GameSettings.HelperOn, on => GameSettings.HelperOn = on);
        Text buddyNote = Label(rt, "", 17, Muted, FontStyle.Italic, TextAnchor.MiddleLeft);
        refreshers.Add(() =>
        {
            Shop.Item buddy = Shop.Find(Shop.EquippedHelper);
            buddyNote.text = (buddy != null ? buddy.name : "Biscuit the dog") + " is with you. Get more buddies and training in the Shop.";
        });

        Label(rt, "MINI MAP", 18, Muted, FontStyle.Bold, TextAnchor.MiddleLeft);
        ToggleRow(rt, "Show the mini map (tap it to make it bigger)", () => GameSettings.MiniMap, on => GameSettings.MiniMap = on);
        Label(rt, "Show on the map:", 18, Soft, FontStyle.Normal, TextAnchor.MiddleLeft);
        var gridGo = new GameObject("Map Dots", typeof(RectTransform));
        gridGo.transform.SetParent(rt, false);
        var grid = gridGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(240f, 46f);
        grid.spacing = new Vector2(8f, 8f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.UpperLeft;
        gridGo.AddComponent<LayoutElement>().preferredHeight = 100f;
        DotToggle(gridGo.transform, "Flies", () => GameSettings.MapFlies, on => GameSettings.MapFlies = on);
        DotToggle(gridGo.transform, "Power-ups", () => GameSettings.MapPowerUps, on => GameSettings.MapPowerUps = on);
        DotToggle(gridGo.transform, "Bird & snakes", () => GameSettings.MapEnemies, on => GameSettings.MapEnemies = on);
        DotToggle(gridGo.transform, "Drifting logs", () => GameSettings.MapHazards, on => GameSettings.MapHazards = on);
        DotToggle(gridGo.transform, "Helper buddy", () => GameSettings.MapHelper, on => GameSettings.MapHelper = on);
        DotToggle(gridGo.transform, "Big map", () => GameSettings.MiniMapLarge, on => GameSettings.MiniMapLarge = on);
        return page;
    }

    private void DotToggle(Transform parent, string label, Func<bool> get, Action<bool> set)
    {
        Button b = MakeButton(parent, "", 19, () => set(!get()), 0f, 0f);
        Text t = b.GetComponentInChildren<Text>();
        Image img = b.GetComponent<Image>();
        refreshers.Add(() =>
        {
            bool on = get();
            t.text = label + (on ? ": On" : ": Off");
            SetSelected(img, on);
        });
    }

    // Nov 2026: optional cheats (see Cheats.cs).
    private GameObject BuildCheatsTab(Transform parent)
    {
        GameObject page = TabPage(parent, "Cheats");
        RectTransform rt = page.GetComponent<RectTransform>();
        page.GetComponent<VerticalLayoutGroup>().spacing = 8f;

        ToggleRow(rt, "Turn on cheats", () => Cheats.Enabled, on => Cheats.Enabled = on);
        Text note = Label(rt, "", 17, Muted, FontStyle.Italic, TextAnchor.MiddleLeft);
        refreshers.Add(() => note.text = Cheats.Enabled
            ? "Have fun! Best scores aren't saved while Super frog, Slow bird or Fly frenzy is on."
            : "For when you just want to unlock everything and play around.");

        var actions = new List<Button>();
        Label(rt, "UNLOCK", 18, Muted, FontStyle.Bold, TextAnchor.MiddleLeft);
        RectTransform row1 = Row("Unlock", rt, 8f);
        actions.Add(MakeButton(row1, "Unlock everything", 19, () => { Cheats.UnlockEverything(); Toast("Everything in the shop is yours!"); }, 245f, 50f));
        actions.Add(MakeButton(row1, "+1000 flies", 19, () => { Cheats.AddFlies(1000); Toast("+1000 flies in your wallet"); }, 245f, 50f));
        actions.Add(MakeButton(row1, "All achievements", 19, () => { Cheats.UnlockAllAchievements(); Toast("Every achievement unlocked"); }, 245f, 50f));
        RectTransform row2 = Row("Levels", rt, 8f);
        actions.Add(MakeButton(row2, "Skip to the next level", 19, () =>
        {
            if (GameState.Current == null || !GameState.Current.IsPlaying) Toast("Start a round first, then skip ahead");
            else if (!Cheats.SkipLevel()) Toast("You're already on the last level");
            else CloseMenu();
        }, 320f, 50f));

        Label(rt, "WHILE YOU PLAY", 18, Muted, FontStyle.Bold, TextAnchor.MiddleLeft);
        var gridGo = new GameObject("Cheat Toggles", typeof(RectTransform));
        gridGo.transform.SetParent(rt, false);
        var grid = gridGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(240f, 46f);
        grid.spacing = new Vector2(8f, 8f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        gridGo.AddComponent<LayoutElement>().preferredHeight = 46f;
        DotToggle(gridGo.transform, "Super frog", () => Cheats.SuperFrog, on => Cheats.SuperFrog = on);
        DotToggle(gridGo.transform, "Slow bird", () => Cheats.SlowBird, on => Cheats.SlowBird = on);
        DotToggle(gridGo.transform, "Fly frenzy", () => Cheats.FlyFrenzy, on => Cheats.FlyFrenzy = on);
        foreach (Button b in gridGo.GetComponentsInChildren<Button>()) actions.Add(b);
        Label(rt, "Super frog: 60% faster, can't be caught, giant fly magnet, quick laser.", 16, Muted, FontStyle.Normal, TextAnchor.MiddleLeft);

        refreshers.Add(() =>
        {
            foreach (Button b in actions)
            {
                b.interactable = Cheats.Enabled;
                var group = b.GetComponent<CanvasGroup>();
                if (group == null) group = b.gameObject.AddComponent<CanvasGroup>();
                group.alpha = Cheats.Enabled ? 1f : 0.35f;
            }
        });
        return page;
    }

    private GameObject BuildGameTab(Transform parent)
    {
        GameObject page = TabPage(parent, "Game");
        RectTransform rt = page.GetComponent<RectTransform>();

        Label(rt, "DIFFICULTY", 18, Muted, FontStyle.Bold, TextAnchor.MiddleLeft);
        RectTransform diff = Row("Difficulty", rt, 8f);
        foreach (Difficulty d in new[] { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard })
        {
            Difficulty captured = d;
            Button b = MakeButton(diff, d.ToString(), 22, () => GameSettings.Difficulty = captured, 170f, 48f);
            Image img = b.GetComponent<Image>();
            refreshers.Add(() => SetSelected(img, GameSettings.Difficulty == captured));
        }
        Text diffNote = Label(rt, "", 18, Muted, FontStyle.Italic, TextAnchor.MiddleLeft);
        refreshers.Add(() => diffNote.text = DifficultyRules.Describe(GameSettings.Difficulty)
            + (GameState.Current != null && GameState.Current.IsPlaying ? ". The bird changes speed right away; fly numbers update as you eat." : "."));

        Label(rt, "BEST SCORES", 18, Muted, FontStyle.Bold, TextAnchor.MiddleLeft);
        Text bests = Label(rt, "", 24, Gold, FontStyle.Bold, TextAnchor.MiddleLeft);
        refreshers.Add(() => bests.text =
            "Easy: " + GameSettings.GetBestScore(Difficulty.Easy)
            + "      Medium: " + GameSettings.GetBestScore(Difficulty.Medium)
            + "      Hard: " + GameSettings.GetBestScore(Difficulty.Hard));

        Text achTitle = Label(rt, "", 18, Muted, FontStyle.Bold, TextAnchor.MiddleLeft);
        RectTransform achRow = Row("Achievements", rt, 20f);
        achRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
        Text achLeft = Label(achRow, "", 17, Soft, FontStyle.Normal, TextAnchor.UpperLeft);
        Text achRight = Label(achRow, "", 17, Soft, FontStyle.Normal, TextAnchor.UpperLeft);
        achLeft.GetComponent<LayoutElement>().preferredWidth = 360f;
        achRight.GetComponent<LayoutElement>().preferredWidth = 360f;
        refreshers.Add(() =>
        {
            achTitle.text = "ACHIEVEMENTS  " + Achievements.Count + " / " + Achievements.All.Length;
            string left = "", right = "";
            for (int i = 0; i < Achievements.All.Length; i++)
            {
                string[] a = Achievements.All[i];
                bool has = Achievements.Has(a[0]);
                string line = (has ? "<color=#FFD152>+ " + a[1] + "</color>" : "<color=#9EB394>- " + a[1] + "</color>")
                              + "  <size=14><color=#9EB394>" + a[2] + "</color></size>\n";
                if (i % 2 == 0) left += line; else right += line;
            }
            achLeft.text = left.TrimEnd();
            achRight.text = right.TrimEnd();
        });
        return page;
    }

    private void ShowTab(string name)
    {
        currentTab = name;
        foreach (KeyValuePair<string, GameObject> pair in tabs)
        {
            ScrollRect scroll = pair.Value.GetComponentInParent<ScrollRect>(true);
            GameObject page = scroll != null ? scroll.gameObject : pair.Value;
            page.SetActive(pair.Key == name);
            if (scroll != null && pair.Key == name) scroll.verticalNormalizedPosition = 1f;
        }
        foreach (KeyValuePair<string, Image> pair in tabButtons)
        {
            SetSelected(pair.Value, pair.Key == name);
        }
    }

    public void OpenMenu(bool pauseGame)
    {
        bool inRound = GameState.Current != null && GameState.Current.IsPlaying;
        paused = pauseGame && inRound;
        if (paused)
        {
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }
        menuTitle.text = paused ? "Paused" : "Settings";
        resumeButton.GetComponentInChildren<Text>().text = paused ? "Resume" : "Back";
        restartButton.gameObject.SetActive(paused);
        quitButtonLabel.transform.parent.gameObject.SetActive(paused);
        menuPanel.SetActive(true);
        hud.SetActive(false);
        GameState.MenuOpen = true;
        RefreshAll();
    }

    public void CloseMenu()
    {
        capturingAction = null;
        menuPanel.SetActive(false);
        GameState.MenuOpen = false;
        if (paused)
        {
            paused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
        bool inRound = GameState.Current != null && GameState.Current.IsPlaying;
        hud.SetActive(inRound);
    }

    private void ReloadLevel(bool startImmediately)
    {
        startOnNextLoad = startImmediately;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
        else SceneManager.LoadScene(scene.name);
    }

    // ---------------------------------------------------------------- key remapping

    private void BeginCapture(string action)
    {
        capturingAction = action;
        RefreshAll();
    }

    private void CaptureKey()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            capturingAction = null; // cancel
            RefreshAll();
            return;
        }
        if (allKeys == null) allKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));
        foreach (KeyCode key in allKeys)
        {
            if (key >= KeyCode.JoystickButton0) break; // keyboard only
            if (!Input.GetKeyDown(key) || KeyBindings.IsReserved(key)) continue;
            string action = capturingAction;
            capturingAction = null;
            KeyBindings.Set(action, key); // saves + refreshes the menu
            return;
        }
    }

    // ---------------------------------------------------------------- refresh

    private void RefreshAll()
    {
        if (this == null) return;
        foreach (Action refresh in refreshers)
        {
            refresh();
        }
    }

    private static void SetSelected(Image image, bool selected)
    {
        if (image == null) return;
        image.color = selected ? AccentDark : ButtonColor;
        var outline = image.GetComponent<Outline>();
        if (outline != null) outline.enabled = selected;
    }

    // ---------------------------------------------------------------- tiny UI kit

    private static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private GameObject Panel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    // A centred card that grows to fit its contents, stacked top to bottom.
    private RectTransform CardColumn(string name, Transform parent, float width, float padding, float spacing, Color? color = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color ?? Card;
        var v = go.AddComponent<VerticalLayoutGroup>();
        int p = Mathf.RoundToInt(padding);
        v.padding = new RectOffset(p, p, p, p);
        v.spacing = spacing;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        bool topLevel = parent.GetComponent<LayoutGroup>() == null;
        if (topLevel)
        {
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            go.AddComponent<Outline>().effectColor = new Color(0.55f, 0.80f, 0.29f, 0.35f);
            go.AddComponent<FitToParent>(); // always fits the screen, either way up
        }
        return go.GetComponent<RectTransform>();
    }

    private RectTransform Row(string name, Transform parent, float spacing)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var h = go.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        return go.GetComponent<RectTransform>();
    }

    private Text Label(Transform parent, string text, int size, Color color, FontStyle style, TextAnchor anchor)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = anchor;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        go.AddComponent<LayoutElement>();
        return t;
    }

    private Button MakeButton(Transform parent, string text, int size, Action onClick, float width, float height)
    {
        var go = new GameObject("Button " + text, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = ButtonColor;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = Accent;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.enabled = false;
        var b = go.AddComponent<Button>();
        var colors = b.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = Color.white;
        colors.colorMultiplier = 1.2f;
        b.colors = colors;
        b.targetGraphic = img;
        b.onClick.AddListener(() =>
        {
            AudioDirector.Play("Click", 0.6f);
            onClick();
        });
        if (width > 0f || height > 0f)
        {
            var le = go.AddComponent<LayoutElement>();
            if (width > 0f) le.preferredWidth = width;
            if (height > 0f) le.preferredHeight = height;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
        }

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        Stretch(labelGo);
        var t = labelGo.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.color = Soft;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return b;
    }

    private void SliderRow(RectTransform parent, string label, Func<float> get, Action<float> set, Func<bool> getOn, Action<bool> setOn)
    {
        RectTransform row = Row(label, parent, 14f);
        Text name = Label(row, label, 22, Soft, FontStyle.Normal, TextAnchor.MiddleLeft);
        name.GetComponent<LayoutElement>().preferredWidth = 260f;

        // Slider: track, fill and a round-ish handle.
        var sliderGo = new GameObject("Slider", typeof(RectTransform));
        sliderGo.transform.SetParent(row, false);
        var sle = sliderGo.AddComponent<LayoutElement>();
        sle.preferredWidth = 300f;
        sle.preferredHeight = 40f;

        var track = Panel("Track", sliderGo.transform, new Color(1f, 1f, 1f, 0.12f));
        var trt = track.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0.38f);
        trt.anchorMax = new Vector2(1f, 0.62f);
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGo.transform, false);
        var fart = fillArea.GetComponent<RectTransform>();
        fart.anchorMin = new Vector2(0f, 0.38f);
        fart.anchorMax = new Vector2(1f, 0.62f);
        fart.offsetMin = new Vector2(0f, 0f);
        fart.offsetMax = new Vector2(-14f, 0f);
        var fill = Panel("Fill", fillArea.transform, Accent);
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
        fillRt.sizeDelta = new Vector2(14f, 0f);

        var handleArea = new GameObject("Handle Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderGo.transform, false);
        var hart = handleArea.GetComponent<RectTransform>();
        hart.anchorMin = Vector2.zero;
        hart.anchorMax = Vector2.one;
        hart.offsetMin = new Vector2(14f, 0f);
        hart.offsetMax = new Vector2(-14f, 0f);
        var handle = Panel("Handle", handleArea.transform, Soft);
        var hrt = handle.GetComponent<RectTransform>();
        hrt.sizeDelta = new Vector2(28f, 0f);

        var slider = sliderGo.AddComponent<Slider>();
        slider.fillRect = fillRt;
        slider.handleRect = hrt;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(get());
        slider.onValueChanged.AddListener(v => set(v));

        Text percent = Label(row, "", 20, Muted, FontStyle.Bold, TextAnchor.MiddleRight);
        percent.GetComponent<LayoutElement>().preferredWidth = 64f;
        refreshers.Add(() =>
        {
            slider.SetValueWithoutNotify(get());
            percent.text = Mathf.RoundToInt(get() * 100f) + "%";
        });

        if (getOn != null)
        {
            Button toggle = MakeButton(row, "", 20, () => setOn(!getOn()), 96f, 40f);
            Text toggleText = toggle.GetComponentInChildren<Text>();
            Image toggleImage = toggle.GetComponent<Image>();
            refreshers.Add(() =>
            {
                bool on = getOn();
                toggleText.text = on ? "On" : "Off";
                SetSelected(toggleImage, on);
                slider.interactable = on;
            });
        }
        else
        {
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(row, false);
            spacer.AddComponent<LayoutElement>().preferredWidth = 96f;
        }
    }

    private void ToggleRow(RectTransform parent, string label, Func<bool> get, Action<bool> set)
    {
        RectTransform row = Row(label, parent, 14f);
        Text name = Label(row, label, 22, Soft, FontStyle.Normal, TextAnchor.MiddleLeft);
        name.GetComponent<LayoutElement>().preferredWidth = 560f;
        Button toggle = MakeButton(row, "", 20, () => set(!get()), 120f, 44f);
        Text t = toggle.GetComponentInChildren<Text>();
        Image img = toggle.GetComponent<Image>();
        refreshers.Add(() =>
        {
            bool on = get();
            t.text = on ? "On" : "Off";
            SetSelected(img, on);
        });
    }
}
