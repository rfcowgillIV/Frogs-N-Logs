using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// AudioManager (added Nov 2026): the volume sliders and on/off switches in Pause > Sound.
//
// Every sound in Frogs-N-Logs already goes through a group in Sound/MasterMix.mixer.
// Sounds in the "Music" and "Background" (swamp ambience) groups count as MUSIC;
// everything else (hops, bird calls, fly buzz, sparkles) counts as SOUND EFFECTS.
// Each sound keeps the volume it was designed with, scaled by the player's settings.
//
// Turn it up to eleven, or down to a quiet croak.
public static class AudioManager
{
    // The original (designed) volume of every sound we've seen.
    private static readonly Dictionary<AudioSource, float> baseVolumes = new Dictionary<AudioSource, float>();
    private static readonly List<AudioSource> deadSources = new List<AudioSource>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameSettings.Changed -= ApplyAll;
        GameSettings.Changed += ApplyAll;
        RegisterEverything();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RegisterEverything();
    }

    private static void RegisterEverything()
    {
        foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Remember(source);
        }
        ApplyAll();
    }

    // Call for anything created during play that might make sound (flies, sparkles).
    public static void Register(GameObject go)
    {
        if (go == null) return;
        foreach (AudioSource source in go.GetComponentsInChildren<AudioSource>(true))
        {
            Remember(source);
            Apply(source);
        }
    }

    public static bool IsMusic(AudioSource source)
    {
        var group = source.outputAudioMixerGroup;
        if (group == null) return false;
        string name = group.name;
        return name == "Music" || name == "Background";
    }

    private static void Remember(AudioSource source)
    {
        // The new music/effects player scales its own volumes (AudioDirector.cs).
        if (source != null && source.GetComponent<AudioDirector>() != null) return;
        if (source != null && !baseVolumes.ContainsKey(source))
        {
            baseVolumes[source] = source.volume;
        }
    }

    public static void ApplyAll()
    {
        AudioListener.volume = GameSettings.MasterVolume;
        deadSources.Clear();
        foreach (KeyValuePair<AudioSource, float> pair in baseVolumes)
        {
            if (pair.Key == null)
            {
                deadSources.Add(pair.Key);
                continue;
            }
            Apply(pair.Key);
        }
        foreach (AudioSource dead in deadSources)
        {
            baseVolumes.Remove(dead);
        }
    }

    private static void Apply(AudioSource source)
    {
        float baseVolume;
        if (!baseVolumes.TryGetValue(source, out baseVolume)) return;
        bool music = IsMusic(source);
        float scale = music
            ? (GameSettings.MusicOn ? GameSettings.MusicVolume : 0f)
            : (GameSettings.EffectsOn ? GameSettings.EffectsVolume : 0f);
        source.volume = baseVolume * scale;
    }
}
