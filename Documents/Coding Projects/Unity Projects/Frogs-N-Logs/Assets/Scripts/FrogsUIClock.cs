using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The clock screen (added Nov 2026), part of FrogsUI. See ClockMode.cs for what clock mode is.
//
//   - Big time and date float over the pond and drift very slowly around the screen, so
//     nothing stays in one spot long enough to burn into an OLED screen or TV.
//   - Tap or click anywhere for the options: 12 / 24 hour, seconds, date, size, sound,
//     night dimming, and "start in clock mode" for a screen that's always a clock.
//   - Any key, gamepad button or the Exit button goes back to the title screen.
//   - Keeps the screen awake while it's showing; full screen on the web.
public partial class FrogsUI
{
    private GameObject clockPanel;
    private RectTransform clockFace;
    private Text clockTime;
    private Text clockDate;
    private Text clockHint;
    private Image clockDim;
    private GameObject clockOptions;
    private float clockOptionsUntil;
    private float clockHintUntil;
    private float clockEnteredAt;
    private int clockShownSecond = -1;
    private int clockSavedSleep = SleepTimeout.SystemSetting;
    private bool clockMadeFullScreen;

    private const int ClockTimeSize = 150;
    private const int ClockDateSize = 46;

    // ---------------------------------------------------------------- building

    private void BuildClock()
    {
        clockPanel = new GameObject("Clock", typeof(RectTransform));
        clockPanel.transform.SetParent(root, false);
        Stretch(clockPanel);

        // A tap anywhere opens the options.
        GameObject catcher = Panel("Clock Tap Area", clockPanel.transform, new Color(0f, 0f, 0f, 0f));
        Stretch(catcher);
        catcher.AddComponent<Button>().onClick.AddListener(() => ShowClockOptions(!clockOptions.activeSelf));

        GameObject dim = Panel("Clock Night Dim", clockPanel.transform, new Color(0f, 0f, 0f, 0f));
        Stretch(dim);
        clockDim = dim.GetComponent<Image>();
        clockDim.raycastTarget = false;

        // The time and date.
        var face = new GameObject("Clock Face", typeof(RectTransform));
        face.transform.SetParent(clockPanel.transform, false);
        clockFace = face.GetComponent<RectTransform>();
        clockFace.anchorMin = clockFace.anchorMax = clockFace.pivot = new Vector2(0.5f, 0.5f);
        var v = face.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.MiddleCenter;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = v.childForceExpandHeight = false;
        v.spacing = -6f;
        var fit = face.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        clockTime = Label(clockFace, "", ClockTimeSize, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        clockDate = Label(clockFace, "", ClockDateSize, new Color(0.92f, 0.97f, 0.88f), FontStyle.Bold, TextAnchor.MiddleCenter);
        ClockMode.AmPmSize = 60;
        foreach (Text t in new[] { clockTime, clockDate })
        {
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var shadow = t.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(3f, -4f);
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0.04f, 0.08f, 0.04f, 0.6f);
        }

        clockHint = Label(clockPanel.transform, "Tap for clock options   ·   any key to exit", 22, Soft, FontStyle.Bold, TextAnchor.UpperCenter);
        var hintRt = clockHint.rectTransform;
        hintRt.anchorMin = new Vector2(0f, 1f);
        hintRt.anchorMax = new Vector2(1f, 1f);
        hintRt.pivot = new Vector2(0.5f, 1f);
        hintRt.sizeDelta = new Vector2(0f, 40f);
        hintRt.anchoredPosition = new Vector2(0f, -18f);
        clockHint.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);

        // The options card, along the bottom.
        RectTransform card = CardColumn("Clock Options", clockPanel.transform, 720f, 18f, 12f);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0f);
        card.pivot = new Vector2(0.5f, 0f);
        card.anchoredPosition = new Vector2(0f, 20f);
        clockOptions = card.gameObject;
        Label(card, "CLOCK", 18, Muted, FontStyle.Bold, TextAnchor.MiddleCenter);

        RectTransform row1 = Row("Clock Row 1", card, 10f);
        ClockToggle(row1, () => GameSettings.Clock24h ? "24 hour" : "12 hour", () => GameSettings.Clock24h = !GameSettings.Clock24h);
        ClockToggle(row1, () => GameSettings.ClockSeconds ? "Seconds: on" : "Seconds: off", () => GameSettings.ClockSeconds = !GameSettings.ClockSeconds);
        ClockToggle(row1, () => GameSettings.ClockDate ? "Date: on" : "Date: off", () => GameSettings.ClockDate = !GameSettings.ClockDate);
        ClockToggle(row1, () => "Size: " + ClockMode.SizeNames[GameSettings.ClockSize], () => GameSettings.ClockSize = (GameSettings.ClockSize + 1) % 3);

        RectTransform row2 = Row("Clock Row 2", card, 10f);
        ClockToggle(row2, () => GameSettings.ClockSound ? "Sound: on" : "Sound: off", () => GameSettings.ClockSound = !GameSettings.ClockSound);
        ClockToggle(row2, () => GameSettings.ClockDim ? "Dim at night: on" : "Dim at night: off", () => GameSettings.ClockDim = !GameSettings.ClockDim, 230f);
        ClockToggle(row2, () => GameSettings.ClockAtStart ? "Open as clock: on" : "Open as clock: off", () => GameSettings.ClockAtStart = !GameSettings.ClockAtStart, 240f);

        RectTransform row3 = Row("Clock Row 3", card, 10f);
        Button exit = MakeButton(row3, "Exit clock", 24, ExitClock, 240f, 58f);
        exit.GetComponent<Image>().color = Accent;
        exit.GetComponentInChildren<Text>().color = Ink;

        refreshers.Add(() =>
        {
            clockShownSecond = -1; // redraw the time with the new settings
            ApplyClockSound();
        });

        clockOptions.SetActive(false);
        clockPanel.SetActive(false);
    }

    private void ClockToggle(RectTransform row, Func<string> label, Action toggle, float width = 170f)
    {
        Button b = MakeButton(row, "", 19, () => { toggle(); ShowClockOptions(true); }, width, 50f);
        Text t = b.GetComponentInChildren<Text>();
        refreshers.Add(() => t.text = label());
    }

    // ---------------------------------------------------------------- in and out

    public bool ClockShowing { get { return ClockMode.Active && clockPanel != null && clockPanel.activeSelf; } }

    public void EnterClock()
    {
        GameState game = GameState.Current;
        if (game == null || game.HasStarted) return;
        if (menuPanel.activeSelf) CloseMenu();
        ClockMode.Active = true;
        clockEnteredAt = Time.unscaledTime;
        clockShownSecond = -1;

        clockSavedSleep = Screen.sleepTimeout;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        ApplyClockSound();

        game.RequestStart(); // the frog wakes up; ClockPilot does the steering
        hud.SetActive(false);
        foreach (ScoreCounter counter in FindObjectsByType<ScoreCounter>(FindObjectsSortMode.None))
        {
            Graphic g = counter.GetComponent<Graphic>();
            if (g != null) g.enabled = false;
        }
        clockPanel.SetActive(true);
        clockOptions.SetActive(false);
        clockHintUntil = Time.unscaledTime + 6f;
        clockHint.gameObject.SetActive(true);
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!Screen.fullScreen) { Screen.fullScreen = true; clockMadeFullScreen = true; }
#endif
        if (ClockPilot.Instance != null) ClockPilot.Instance.FollowTheRealTime(true);
        Debug.Log("[Clock] Clock mode on: " + ClockMode.TimeText(ClockMode.Now) + ", level " + ClockMode.LevelFor(ClockMode.Now));
    }

    public void ExitClock()
    {
        if (!ClockMode.Active) return;
        ClockMode.Active = false;
        ClockMode.ExitedThisSession = true;
        Screen.sleepTimeout = clockSavedSleep;
        AudioListener.volume = 1f;
        if (clockMadeFullScreen) { Screen.fullScreen = false; clockMadeFullScreen = false; }
        Debug.Log("[Clock] Clock mode off");
        ReloadLevel(false); // back to a fresh title screen
    }

    private IEnumerator EnterClockSoon()
    {
        // Let the level's own Start() methods run first.
        yield return null;
        yield return null;
        EnterClock();
    }

    private void ApplyClockSound()
    {
        AudioListener.volume = ClockMode.Active && !GameSettings.ClockSound ? 0f : 1f;
    }

    private void ShowClockOptions(bool show)
    {
        clockOptions.SetActive(show);
        clockOptionsUntil = Time.unscaledTime + 8f;
        if (show) clockHint.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- every frame

    private void UpdateClock()
    {
        DateTime now = ClockMode.Now;

        // Any key or gamepad button leaves (but not the click that opened it, or a tap).
        bool pointer = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
        if (Time.unscaledTime - clockEnteredAt > 1f && Input.anyKeyDown && !pointer)
        {
            ExitClock();
            return;
        }

        if (now.Second != clockShownSecond)
        {
            clockShownSecond = now.Second;
            clockTime.text = ClockMode.TimeText(now);
            clockDate.text = ClockMode.DateText(now);
            clockDate.gameObject.SetActive(GameSettings.ClockDate);
        }

        if (clockOptions.activeSelf && Time.unscaledTime > clockOptionsUntil) clockOptions.SetActive(false);
        if (clockHint.gameObject.activeSelf && Time.unscaledTime > clockHintUntil) clockHint.gameObject.SetActive(false);

        // Night dimming.
        float dim = GameSettings.ClockDim && ClockMode.IsDimTime(now) ? 0.55f : 0f;
        clockDim.color = new Color(0f, 0f, 0f, Mathf.MoveTowards(clockDim.color.a, dim, Time.unscaledDeltaTime * 0.2f));

        // Size, fitted to the screen, then a slow drift (one lap takes several minutes).
        Vector2 area = root.rect.size;
        Vector2 size = clockFace.rect.size;
        if (size.x <= 0f || size.y <= 0f || area.x <= 0f) return;
        float scale = Mathf.Min(ClockMode.Sizes[GameSettings.ClockSize], (area.x - 48f) / size.x, (area.y - 48f) / size.y);
        clockFace.localScale = new Vector3(scale, scale, 1f);
        Vector2 room = new Vector2(Mathf.Max(0f, area.x - size.x * scale), Mathf.Max(0f, area.y - size.y * scale)) * 0.5f - new Vector2(24f, 24f);
        room = Vector2.Max(room, Vector2.zero);
        float t = Time.unscaledTime;
        clockFace.anchoredPosition = new Vector2(
            Mathf.Sin(t * Mathf.PI * 2f / 420f) * room.x * 0.85f,
            Mathf.Sin(t * Mathf.PI * 2f / 660f + 1f) * room.y * 0.85f);
    }
}
