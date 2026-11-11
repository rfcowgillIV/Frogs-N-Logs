using System.Collections.Generic;
using UnityEngine;

// Biome (added Nov 2026): turns the pond into each later level's world.
//
//   Lily Pad Lake (4)  fresh high-res grass, the lake floods its banks: wading through water
//                      slows you down, but lily pads are dry ground. Hop pad to pad!
//   Autumn Woods (5)   a carpet of fallen leaves, orange and red trees, leaf piles to rummage
//                      through (some hide a bonus fly), and giant mushrooms that bounce you.
//   Winter Freeze (6)  snow everywhere, the ponds freeze over (ice is slippery!), thin ice
//                      that cracks if you stand on it, falling snow, and the bird becomes a
//                      snowy owl.
//
// Ground textures: ambientCG (CC0), 2048 px for the apps and 1024 px on the web and phones.
// Everything here is put back when the level reloads, so a new run starts at the Pond.
public class Biome : MonoBehaviour
{
    public static Biome Instance { get; private set; }

    // Multiplies the frog's speed (wading through the lake is slower).
    public static float SpeedFactor { get; private set; } = 1f;
    public static bool InWater { get; private set; }
    // LilyPad.cs stamps the time while the frog is on a pad (physics runs less often than
    // frames on fast screens, so a timestamp is steadier than a flag).
    public static float LastOnPad = -10f;
    public static bool OnLilyPad { get { return Time.time - LastOnPad < 0.12f; } }
    public static int Current { get; private set; } = 1;
    public static float WaterLevel { get; private set; } = 0.98f;

    private Terrain terrain;
    private Material originalTerrain;
    private bool originalInstanced;
    private readonly List<Renderer> water = new List<Renderer>();
    private readonly Dictionary<Renderer, Material> originalWater = new Dictionary<Renderer, Material>();
    private readonly Dictionary<Renderer, Vector3> originalWaterPos = new Dictionary<Renderer, Vector3>();
    private float baseWaterLevel = 0.98f;
    private GameObject levelRoot;
    private ParticleSystem snow;
    private Transform frog;
    private int applyLevel = -1;
    private float applyAt;

    void Awake()
    {
        Instance = this;
        SpeedFactor = 1f;
        InWater = false;
        LastOnPad = -10f;
        Current = 1;
        ResetPlants();
        LevelManager.LevelChanged += OnLevelChanged;
    }

    void Start()
    {
        terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            originalTerrain = terrain.materialTemplate;
            originalInstanced = terrain.drawInstanced;
        }
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            Material m = r.sharedMaterial;
            if (m != null && m.shader != null && m.shader.name == "Frogs/ToonWater")
            {
                water.Add(r);
                originalWater[r] = m;
                originalWaterPos[r] = r.transform.position;
            }
        }
        if (water.Count > 0) baseWaterLevel = water[0].bounds.max.y;
        WaterLevel = baseWaterLevel;
    }

    void OnDestroy()
    {
        LevelManager.LevelChanged -= OnLevelChanged;
        ResetPlants();
        PlayerMovement.Grip = 1f;
        SpeedFactor = 1f;
        if (Instance == this) Instance = null;
    }

    private void OnLevelChanged(int level, string name)
    {
        // Change the world halfway through the lighting fade, so it blends in.
        applyLevel = level;
        applyAt = Time.time + 1.1f;
    }

    void Update()
    {
        if (applyLevel > 0 && Time.time >= applyAt)
        {
            Apply(applyLevel);
            applyLevel = -1;
        }
        UpdateFrog();
    }

    // For the play tests: switch straight to a level's world.
    public void ApplyNow(int level) { Apply(level); applyLevel = -1; }

    // ------------------------------------------------------------ switching worlds

    private void Apply(int level)
    {
        if (level == Current) return;
        Current = level;
        if (levelRoot != null) Destroy(levelRoot);
        levelRoot = new GameObject("Biome: " + (level - 1 < LevelManager.Names.Length ? LevelManager.Names[level - 1] : "?"));
        ResetPlants();
        RestoreWater();
        SetSnow(false);
        PlayerMovement.Grip = 1f;

        switch (level)
        {
            case 4: BuildLake(); break;
            case 5: BuildAutumn(); break;
            case 6: BuildWinter(); break;
            default: SetGround(null, Color.white, 1f); break;
        }
        if (MiniMap.Instance != null) MiniMap.Instance.Refresh();
    }

    private void BuildLake()
    {
        SetGround("Biomes/Ground_Lake", new Color(0.95f, 1f, 0.92f), 36f);
        RaiseWater(0.2f);   // ankle-deep: the frog wades rather than disappears
        // Clear blue lake water (less see-through, so the grass under it doesn't turn it green).
        TintWater(new Color(0.36f, 0.74f, 0.98f, 0.72f), new Color(0.10f, 0.40f, 0.86f, 0.88f));
        // Lily pads on the water: dry ground to hop across (and a few flowers).
        int placed = 0;
        for (int i = 0; i < 400 && placed < 46; i++)
        {
            Vector3 p = PlayArea.RandomPoint(3f);
            float ground = PowerUpSpawner.GroundHeight(p);
            if (ground > WaterLevel - 0.12f) continue;
            p.y = WaterLevel + 0.02f;
            LilyPad.Create(p, Random.Range(0.75f, 1.25f), placed % 5 == 0, levelRoot.transform);
            placed++;
        }
        Debug.Log("[Biome] Lily Pad Lake: " + placed + " lily pads");
    }

    private void BuildAutumn()
    {
        SetGround("Biomes/Ground_Autumn", Color.white, 30f);
        foreach (Material m in SceneDressing.Leaves) Recolor(m, new Color(1f, 0.48f, 0.12f), 0.92f);
        foreach (Material m in SceneDressing.Plants) Recolor(m, new Color(0.95f, 0.72f, 0.3f), 0.6f);
        for (int i = 0; i < 22; i++) LeafPile.Create(Spot(4f), levelRoot.transform);
        for (int i = 0; i < 7; i++) BouncePad.Create(Spot(6f), levelRoot.transform);
    }

    private void BuildWinter()
    {
        SetGround("Biomes/Ground_Snow", new Color(0.97f, 0.98f, 1f), 26f);
        foreach (Material m in SceneDressing.Leaves) Recolor(m, new Color(0.93f, 0.96f, 1f), 0.8f);
        foreach (Material m in SceneDressing.Plants) Recolor(m, new Color(0.9f, 0.94f, 1f), 0.75f);
        FreezeWater();
        for (int i = 0; i < 12; i++) ThinIce.Create(Spot(5f), levelRoot.transform);
        SetSnow(true);
        // The bird puts on its winter coat: a snowy owl.
        BirdMovement bird = FindFirstObjectByType<BirdMovement>();
        if (bird != null)
        {
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in bird.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(block);
                block.SetColor("_Recolor", new Color(0.97f, 0.97f, 1f, 0.85f));
                r.SetPropertyBlock(block);
            }
        }
    }

    private Vector3 Spot(float clearOfFrog)
    {
        Vector3 frogPos = frog != null ? frog.position : Vector3.zero;
        Vector3 p = Vector3.zero;
        for (int i = 0; i < 30; i++)
        {
            p = PlayArea.RandomPoint(4f);
            if (Vector3.Distance(Flat(p), Flat(frogPos)) >= clearOfFrog && PowerUpSpawner.GroundHeight(p) > WaterLevel - 0.05f) break;
        }
        p.y = PowerUpSpawner.GroundHeight(p);
        return p;
    }

    // ------------------------------------------------------------ ground, water, plants

    private void SetGround(string texture, Color tint, float tiling)
    {
        if (terrain == null) return;
        if (texture == null)
        {
            terrain.materialTemplate = originalTerrain;
            terrain.drawInstanced = originalInstanced;
            return;
        }
        Texture2D tex = Resources.Load<Texture2D>(texture);
        if (tex == null) return;
        Material m = new Material(Stylizer.Toon(tex, tint, 0f, false, 0.05f)) { name = "Ground " + texture, hideFlags = HideFlags.DontSave };
        m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
        m.SetFloat("_RimStrength", 0.05f);
        terrain.drawInstanced = false; // the cartoon shader draws the ground as one mesh
        terrain.materialTemplate = m;
    }

    private void RaiseWater(float by)
    {
        foreach (Renderer r in water)
        {
            if (r == null) continue;
            r.transform.position = originalWaterPos[r] + Vector3.up * by;
        }
        WaterLevel = baseWaterLevel + by;
    }

    // Clearer, bluer lake water (the toon water shader's shallow and deep colours).
    private void TintWater(Color shallow, Color deep)
    {
        foreach (Renderer r in water)
        {
            if (r == null) continue;
            Material m = new Material(originalWater[r]) { hideFlags = HideFlags.DontSave };
            m.SetColor("_ShallowColor", shallow);
            m.SetColor("_DeepColor", deep);
            // Calmer lake: wider, softer ripples.
            // Calm lake: no ripple lines, just the sparkles and soft colour pools.
            m.SetColor("_RippleColor", shallow);
            r.sharedMaterial = m;
        }
    }

    private void FreezeWater()
    {
        Texture2D ice = Resources.Load<Texture2D>("Biomes/Ice");
        Material m = new Material(Stylizer.Toon(ice, new Color(0.82f, 0.94f, 1f), 0f, false, 0.9f)) { name = "Ice", hideFlags = HideFlags.DontSave };
        m.SetTextureScale("_BaseMap", new Vector2(40f, 40f));
        m.SetFloat("_RimStrength", 0.5f);
        foreach (Renderer r in water) if (r != null) r.sharedMaterial = m;
    }

    private void RestoreWater()
    {
        foreach (Renderer r in water)
        {
            if (r == null) continue;
            r.sharedMaterial = originalWater[r];
            r.transform.position = originalWaterPos[r];
        }
        WaterLevel = baseWaterLevel;
    }

    private static void Recolor(Material m, Color c, float amount)
    {
        if (m != null && m.HasProperty("_Recolor")) m.SetColor("_Recolor", new Color(c.r, c.g, c.b, amount));
    }

    // The leaf materials are shared and outlive the level, so always put them back.
    private static void ResetPlants()
    {
        foreach (Material m in SceneDressing.Leaves) Recolor(m, Color.white, 0f);
        foreach (Material m in SceneDressing.Plants) Recolor(m, Color.white, 0f);
    }

    // ------------------------------------------------------------ snow

    private void SetSnow(bool on)
    {
        if (on && snow == null) snow = Weather.MakeFalling("Snowfall", new Color(1f, 1f, 1f, 0.9f), 0.12f, 1.4f, 140f, false);
        if (snow != null)
        {
            if (on) snow.Play(); else snow.Stop();
            snow.gameObject.SetActive(on);
        }
    }

    // ------------------------------------------------------------ what the frog is standing in

    private void UpdateFrog()
    {
        if (frog == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p == null) return;
            frog = p.transform;
        }
        float ground = PowerUpSpawner.GroundHeight(frog.position);
        InWater = ground < WaterLevel - 0.08f;

        if (Current == 4)
        {
            // Wading is slow; lily pads are dry.
            SpeedFactor = InWater && !OnLilyPad ? 0.62f : 1f;
            PlayerMovement.Grip = 1f;
        }
        else if (Current == 6)
        {
            // Frozen ponds are very slippery; snow is a little slippery.
            SpeedFactor = 1f;
            PlayerMovement.Grip = InWater ? 0.1f : 0.55f;
        }
        else
        {
            SpeedFactor = 1f;
            PlayerMovement.Grip = 1f;
        }
    }

    private static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }
}
