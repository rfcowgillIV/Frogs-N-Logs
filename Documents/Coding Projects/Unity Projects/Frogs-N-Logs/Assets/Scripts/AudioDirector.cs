using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

// AudioDirector (added Nov 2026): the new music and sound effects.
//
// MUSIC by Kevin MacLeod (incompetech.com), licensed under Creative Commons: By Attribution
// 4.0 (http://creativecommons.org/licenses/by/4.0/):
//   Title + credits  "Fluffing a Duck"
//   The Pond         "Carefree"
//   The Swamp        "Sneaky Snitch"
//   Night            "Investigations"
// The tracks live in StreamingAssets/Music and STREAM in after the game has loaded, so they
// don't add anything to the first download. They follow the Music volume slider.
//
// SOUND EFFECTS (all CC0, see CREDITS.md): ribbits and croaks (EZduzziteh), fly gulp (Bobjt),
// menu clicks (Kenney), jingles (Kenney, Fupi), power-up (congusbongus), zap and pop
// (rubberduck), night crickets (Aj_). They follow the Sound effects slider.
public class AudioDirector : MonoBehaviour
{
    public static AudioDirector Instance { get; private set; }

    public const string TitleTrack = "FluffingaDuck";
    public static readonly string[] LevelTracks = { "Carefree", "SneakySnitch", "Investigations" };

    private AudioSource musicA;
    private AudioSource musicB;
    private AudioSource effects;
    private AudioSource ambience;
    private readonly Dictionary<string, AudioClip> tracks = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, AudioClip> sounds = new Dictionary<string, AudioClip>();
    private string wanted;
    private string current;
    private float fade = 1f;

    public string CurrentTrack { get { return current; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (Instance != null) return;
        var go = new GameObject("Audio Director");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AudioDirector>();
    }

    void Awake()
    {
        Instance = this;
        musicA = MakeSource(true);
        musicB = MakeSource(true);
        effects = MakeSource(false);
        ambience = MakeSource(true);
        // Menu clicks and music carry on while the game is paused.
        effects.ignoreListenerPause = true;
        musicA.ignoreListenerPause = true;
        musicB.ignoreListenerPause = true;
        GameSettings.Changed += ApplyVolumes;
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameState.RoundStarted += OnRoundStarted;
        LevelManager.LevelChanged += OnLevelChanged;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameState.RoundStarted -= OnRoundStarted;
        LevelManager.LevelChanged -= OnLevelChanged;
        GameSettings.Changed -= ApplyVolumes;
    }

    // Music (and the night crickets) follow the "Music & swamp sounds" slider.
    private static float MusicScale { get { return GameSettings.MusicOn ? GameSettings.MusicVolume : 0f; } }
    private const float MusicLevel = 0.7f;
    private float ambienceLevel = 0.35f;
    private AudioSource activeMusic;
    private Coroutine crossfade;

    private void ApplyVolumes()
    {
        if (activeMusic != null && crossfade == null) activeMusic.volume = MusicLevel * MusicScale;
        ambience.volume = ambienceLevel * MusicScale;
    }

    private AudioSource MakeSource(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f;
        s.volume = 1f;
        return s;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // The 2017 swamp music is replaced by the new tracks; route ours through its mixer
        // group so the Music slider still controls it. (The swamp ambience stays.)
        foreach (AudioSource old in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if (old == musicA || old == musicB || old == effects || old == ambience) continue;
            var group = old.outputAudioMixerGroup;
            if (group != null && group.name == "Music")
            {
                musicA.outputAudioMixerGroup = group;
                musicB.outputAudioMixerGroup = group;
                old.Stop();
                old.enabled = false;
            }
            if (group != null && group.name == "Background") ambience.outputAudioMixerGroup = group;
        }
        ambience.Stop();
        PlayMusic(TitleTrack);
    }

    private void OnRoundStarted()
    {
        int level = LevelManager.Instance != null ? LevelManager.Instance.Level : 1;
        PlayMusic(LevelTracks[Mathf.Clamp(level - 1, 0, LevelTracks.Length - 1)]);
    }

    private void OnLevelChanged(int level, string name)
    {
        PlayMusic(LevelTracks[Mathf.Clamp(level - 1, 0, LevelTracks.Length - 1)]);
        Play("LevelUp", 0.8f);
        if (level >= 3) PlayAmbience("Crickets", 0.35f);
    }

    // ------------------------------------------------------------ music

    public void PlayMusic(string track)
    {
        wanted = track;
        if (current == track) return;
        AudioClip clip;
        if (tracks.TryGetValue(track, out clip))
        {
            SwapTo(clip, track);
        }
        else
        {
            StartCoroutine(Load(track));
        }
    }

    private IEnumerator Load(string track)
    {
        if (tracks.ContainsKey(track)) yield break;
        tracks[track] = null; // loading
        string path = Application.streamingAssetsPath + "/Music/" + track + ".mp3";
        if (!path.Contains("://")) path = "file://" + path;
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(path, AudioType.MPEG))
        {
            ((DownloadHandlerAudioClip)request.downloadHandler).streamAudio = true;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[Music] Couldn't load " + track + ": " + request.error);
                tracks.Remove(track);
                yield break;
            }
            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            clip.name = track;
            tracks[track] = clip;
            if (wanted == track) SwapTo(clip, track);
        }
    }

    private void SwapTo(AudioClip clip, string track)
    {
        if (clip == null) return;
        current = track;
        AudioSource from = musicA.isPlaying ? musicA : musicB;
        AudioSource to = from == musicA ? musicB : musicA;
        to.clip = clip;
        to.time = 0f;
        to.volume = 0f;
        to.Play();
        if (crossfade != null) StopCoroutine(crossfade);
        crossfade = StartCoroutine(Crossfade(from, to));
    }

    private IEnumerator Crossfade(AudioSource from, AudioSource to)
    {
        activeMusic = to;
        fade = 0f;
        float startFrom = from.volume;
        while (fade < 1f)
        {
            fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / 1.2f);
            to.volume = fade * MusicLevel * MusicScale;
            if (from != to) from.volume = (1f - fade) * startFrom;
            yield return null;
        }
        if (from != to) from.Stop();
        crossfade = null;
        ApplyVolumes();
    }

    // ------------------------------------------------------------ sounds

    private AudioClip Sound(string name)
    {
        AudioClip clip;
        if (!sounds.TryGetValue(name, out clip))
        {
            clip = Resources.Load<AudioClip>("Audio/" + name);
            sounds[name] = clip;
        }
        return clip;
    }

    // Play a sound effect (respects the Sound effects slider and on/off switch).
    public static void Play(string name, float volume = 1f)
    {
        if (Instance == null || !GameSettings.EffectsOn) return;
        AudioClip clip = Instance.Sound(name);
        if (clip == null) return;
        Instance.effects.PlayOneShot(clip, volume * GameSettings.EffectsVolume);
    }

    public static void PlayRandom(float volume, params string[] names)
    {
        if (names.Length == 0) return;
        Play(names[Random.Range(0, names.Length)], volume);
    }

    public void PlayAmbience(string name, float volume)
    {
        AudioClip clip = Sound(name);
        if (clip == null) return;
        ambience.clip = clip;
        ambienceLevel = volume;
        ambience.volume = volume * MusicScale;
        ambience.Play();
    }
}
