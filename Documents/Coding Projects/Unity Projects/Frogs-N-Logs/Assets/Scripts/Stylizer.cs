using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Stylizer (added Nov 2026): gives the whole pond its new cartoon look when the level loads.
//
//  - Every model is redrawn with the cartoon shader (Resources/FrogsToon.shader), keeping its
//    own texture and colour.
//  - The pond water becomes the new cartoon water (Resources/FrogsToonWater.shader).
//  - Sunny, saturated lighting with a blue-sky / green-ground ambient light.
//  - A light "look" on top (URP Volume): soft glow, gentle colour boost, a hint of vignette.
//    LevelManager tints it for the Swamp and Night.
//  - The frog and bird are swapped for the new animated models (CharacterVisual.cs), and the
//    edges of the pond get trees, bushes and flowers (SceneDressing.cs).
//
// Everything new is free to use - see CREDITS.md / the in-game Credits.
public class Stylizer : MonoBehaviour
{
    public static Stylizer Instance { get; private set; }

    // The colour-adjustment part of the look, so levels can tint it.
    public static ColorAdjustments Grading { get; private set; }
    public static Bloom Glow { get; private set; }

    private static Shader toonShader;
    private static Shader waterShader;
    private static readonly Dictionary<string, Material> toonCache = new Dictionary<string, Material>();

    void Awake()
    {
        Instance = this;
        StylizeLighting();
        BuildLook();
        StylizeScene();
        SwapCharacters();
        gameObject.AddComponent<SceneDressing>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------ materials

    public static Material Toon(Texture texture, Color color, float cutoff = 0f, bool twoSided = false, float shine = 0.2f)
    {
        string key = (texture != null ? texture.GetInstanceID().ToString() : "none") + "|" + color + "|" + cutoff + "|" + twoSided + "|" + shine;
        Material m;
        if (toonCache.TryGetValue(key, out m) && m != null) return m;
        if (toonShader == null) toonShader = Shader.Find("Frogs/Toon");
        if (toonShader == null) toonShader = Resources.Load<Shader>("FrogsToon");
        m = new Material(toonShader) { hideFlags = HideFlags.DontSave, enableInstancing = true };
        if (texture != null) m.SetTexture("_BaseMap", texture);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Cutoff", cutoff);
        m.SetFloat("_Cull", twoSided ? 0f : 2f);
        m.SetFloat("_Shine", shine);
        toonCache[key] = m;
        return m;
    }

    // Cartoon version of an existing material (keeps its texture and colour).
    public static Material ToonFrom(Material source)
    {
        if (source == null) return Toon(null, Color.white);
        Texture tex = null;
        Color color = Color.white;
        if (source.HasProperty("_BaseMap")) tex = source.GetTexture("_BaseMap");
        if (tex == null && source.HasProperty("_MainTex")) tex = source.GetTexture("_MainTex");
        if (source.HasProperty("_BaseColor")) color = source.GetColor("_BaseColor");
        else if (source.HasProperty("_Color")) color = source.GetColor("_Color");
        string n = source.name.ToLowerInvariant();
        bool leafy = n.Contains("leaf") || n.Contains("leaves") || n.Contains("grass") || n.Contains("flower") || n.Contains("petal");
        return Toon(tex, color, leafy ? 0.45f : 0f, leafy, leafy ? 0.05f : 0.2f);
    }

    private static bool AlreadyStylized(Material m)
    {
        if (m == null || m.shader == null) return false;
        string n = m.shader.name;
        return n.StartsWith("Frogs/") || n.StartsWith("Hidden/Frogs") || n.Contains("Particles") || n.Contains("Terrain") || n.Contains("Sprites");
    }

    public static void ToToon(Renderer r)
    {
        if (r == null || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) return;
        Material[] mats = r.sharedMaterials;
        bool changed = false;
        for (int i = 0; i < mats.Length; i++)
        {
            if (AlreadyStylized(mats[i])) continue;
            mats[i] = ToonFrom(mats[i]);
            changed = true;
        }
        if (changed) r.sharedMaterials = mats;
    }

    public static Material Water()
    {
        if (waterShader == null) waterShader = Shader.Find("Frogs/ToonWater");
        if (waterShader == null) waterShader = Resources.Load<Shader>("FrogsToonWater");
        return new Material(waterShader) { hideFlags = HideFlags.DontSave };
    }

    // ------------------------------------------------------------ scene

    private void StylizeScene()
    {
        Material water = null;
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.GetComponentInParent<Canvas>() != null) continue;
            Material first = r.sharedMaterial;
            bool isWater = (first != null && first.name.ToLowerInvariant().Contains("water"))
                           || r.gameObject.name.ToLowerInvariant().Contains("water");
            if (isWater)
            {
                if (water == null) water = Water();
                r.sharedMaterial = water;
                r.shadowCastingMode = ShadowCastingMode.Off;
                continue;
            }
            if (r is ParticleSystemRenderer)
            {
                FixParticles((ParticleSystemRenderer)r);
                continue;
            }
            ToToon(r);
        }

        Material terrainMat = Resources.Load<Material>("Generated/TerrainURP");
        foreach (Terrain t in Terrain.activeTerrains)
        {
            if (terrainMat != null && (t.materialTemplate == null || t.materialTemplate.shader == null || !t.materialTemplate.shader.isSupported))
            {
                t.materialTemplate = terrainMat;
            }
        }
    }

    private static void FixParticles(ParticleSystemRenderer r)
    {
        Material m = r.sharedMaterial;
        if (m != null && m.shader != null && m.shader.isSupported && m.shader.name.StartsWith("Universal Render Pipeline/")) return;
        Material fallback = Resources.Load<Material>("Generated/ParticleURP");
        if (fallback != null) r.sharedMaterial = fallback;
    }

    // ------------------------------------------------------------ lighting + look

    private void StylizeLighting()
    {
        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional) continue;
            l.color = new Color(1f, 0.95f, 0.86f);
            l.intensity = Mathf.Max(l.intensity, 1.15f);
            l.shadowStrength = 0.65f;
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.76f, 0.95f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.66f, 0.55f);
        RenderSettings.ambientGroundColor = new Color(0.32f, 0.38f, 0.22f);
        RenderSettings.ambientIntensity = 1f;
    }

    private void BuildLook()
    {
        var go = new GameObject("Frogs Look (post effects)");
        go.transform.SetParent(transform, false);
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.hideFlags = HideFlags.DontSave;

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.35f);
        bloom.threshold.Override(1.05f);
        bloom.scatter.Override(0.6f);
        Glow = bloom;

        Tonemapping tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);

        ColorAdjustments grade = profile.Add<ColorAdjustments>(true);
        grade.saturation.Override(18f);
        grade.contrast.Override(8f);
        grade.postExposure.Override(0.1f);
        grade.colorFilter.Override(Color.white);
        Grading = grade;

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.18f);
        vignette.smoothness.Override(0.5f);

        volume.sharedProfile = profile;
    }

    // ------------------------------------------------------------ characters

    private void SwapCharacters()
    {
        GameObject frog = GameObject.FindWithTag("Player");
        if (frog != null) CharacterVisual.Attach(frog, "Frog", "walk", "idle", 0f, 1.0f, 0f);

        BirdMovement bird = FindFirstObjectByType<BirdMovement>();
        if (bird != null) CharacterVisual.Attach(bird.gameObject, "Eagle", "Flying", "Idle", 0f, 1.35f, 0.7f);
    }
}
