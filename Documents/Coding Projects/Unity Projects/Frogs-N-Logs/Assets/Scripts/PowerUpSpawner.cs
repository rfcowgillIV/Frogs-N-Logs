using System;
using System.Collections.Generic;
using UnityEngine;

// PowerUpSpawner (added Nov 2026) - see PowerUps.cs for what each power-up does.
// Places power-ups around the pond during a round.
public class PowerUpSpawner : MonoBehaviour
{
    private float nextSpawn;
    private readonly List<GameObject> active = new List<GameObject>();
    private static Material glowShared;

    private const float Area = 24f;          // stay inside the walls (+-30)
    private const float MinDistanceFromFrog = 7f;

    void Start()
    {
        ScheduleNext(true);
    }

    private void ScheduleNext(bool first)
    {
        // First one after ~20-26 s, then every 14-22 s.
        nextSpawn = Time.time + (first ? UnityEngine.Random.Range(20f, 26f) : UnityEngine.Random.Range(14f, 22f));
    }

    void Update()
    {
        GameState game = GameState.Current;
        if (game == null || !game.IsPlaying) return;
        active.RemoveAll(g => g == null);
        if (Time.time < nextSpawn) return;
        ScheduleNext(false);
        if (active.Count >= 2) return;

        float roll = UnityEngine.Random.value;
        PowerUpType type = roll < 0.45f ? PowerUpType.GoldenFly : roll < 0.75f ? PowerUpType.SpeedBoost : PowerUpType.Shield;
        if (type == PowerUpType.Shield && FrogEffects.ShieldActive) type = PowerUpType.SpeedBoost;
        Spawn(type);
    }

    public GameObject Spawn(PowerUpType type)
    {
        Vector3 position;
        if (!FindSpot(out position)) return null;
        GameObject go;
        switch (type)
        {
            case PowerUpType.GoldenFly: go = MakeGoldenFly(position); break;
            case PowerUpType.SpeedBoost: go = MakeOrb(position); break;
            default: go = MakeLilyPad(position); break;
        }
        if (go != null)
        {
            active.Add(go);
            AudioManager.Register(go);
        }
        return go;
    }

    public static bool FindSpot(out Vector3 position)
    {
        GameObject frog = GameObject.FindWithTag("Player");
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector3 p = new Vector3(UnityEngine.Random.Range(-Area, Area), 0f, UnityEngine.Random.Range(-Area, Area));
            if (frog != null && Vector3.Distance(new Vector3(frog.transform.position.x, 0f, frog.transform.position.z), p) < MinDistanceFromFrog)
                continue;
            p.y = GroundHeight(p) + 1f;
            position = p;
            return true;
        }
        position = Vector3.zero;
        return false;
    }

    public static float GroundHeight(Vector3 p)
    {
        Terrain t = Terrain.activeTerrain;
        if (t == null) return 0.5f;
        return t.SampleHeight(p) + t.transform.position.y;
    }

    public static Material Glow(Color color, float rim = 1.2f)
    {
        if (glowShared == null)
        {
            Shader shader = Resources.Load<Shader>("FrogsGlow");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            glowShared = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        var m = new Material(glowShared) { hideFlags = HideFlags.HideAndDontSave };
        m.SetColor("_Color", color);
        if (m.HasProperty("_Rim")) m.SetFloat("_Rim", rim);
        return m;
    }

    private static void MakeTrigger(GameObject go, float radius)
    {
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        var sphere = go.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = radius;
    }

    private GameObject MakeOrb(Vector3 position)
    {
        var root = new GameObject("Speed Boost");
        root.transform.position = position;
        var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.transform.SetParent(root.transform, false);
        orb.transform.localScale = Vector3.one * 0.7f;
        orb.GetComponent<Renderer>().sharedMaterial = Glow(new Color(0.25f, 0.8f, 1f, 0.9f), 1.6f);
        // a little "lightning" ring
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.transform.SetParent(root.transform, false);
        ring.transform.localScale = new Vector3(1.1f, 0.02f, 1.1f);
        ring.GetComponent<Renderer>().sharedMaterial = Glow(new Color(1f, 1f, 1f, 0.35f), 0.5f);
        MakeTrigger(root, 0.9f);
        var pickup = root.AddComponent<PowerUpPickup>();
        pickup.type = PowerUpType.SpeedBoost;
        return root;
    }

    private GameObject MakeLilyPad(Vector3 position)
    {
        var root = new GameObject("Lily-pad Shield");
        root.transform.position = position - Vector3.up * 0.55f;
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pad.transform.SetParent(root.transform, false);
        pad.transform.localScale = new Vector3(1.4f, 0.04f, 1.4f);
        pad.GetComponent<Renderer>().sharedMaterial = Glow(new Color(0.35f, 0.85f, 0.3f, 0.95f), 0.9f);
        var flower = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flower.transform.SetParent(root.transform, false);
        flower.transform.localPosition = new Vector3(0.3f, 0.12f, 0.2f);
        flower.transform.localScale = Vector3.one * 0.3f;
        flower.GetComponent<Renderer>().sharedMaterial = Glow(new Color(1f, 0.55f, 0.8f, 1f), 1f);
        MakeTrigger(root, 1.1f);
        var pickup = root.AddComponent<PowerUpPickup>();
        pickup.type = PowerUpType.Shield;
        return root;
    }

    private GameObject MakeGoldenFly(Vector3 position)
    {
        FlySpawner spawner = FindFirstObjectByType<FlySpawner>();
        GameObject prefab = spawner != null ? spawner.FlyPrefab : null;
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, position, Quaternion.identity);
            go.name = "Golden Fly";
            // It's not an ordinary fly: swap the normal "+1" pickup for the golden one.
            foreach (FlyPickUp normal in go.GetComponentsInChildren<FlyPickUp>(true)) DestroyImmediate(normal);
            go.transform.localScale *= 1.6f;
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(block);
                block.SetColor("_Color", new Color(1f, 0.8f, 0.15f));
                block.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.1f));
                r.SetPropertyBlock(block);
            }
            // a soft golden halo so it's easy to spot
            var halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(halo.GetComponent<Collider>());
            halo.transform.SetParent(go.transform, false);
            halo.transform.localScale = Vector3.one * 1.2f;
            halo.GetComponent<Renderer>().sharedMaterial = Glow(new Color(1f, 0.8f, 0.2f, 0.25f), 2f);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Golden Fly";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.5f;
            go.GetComponent<Renderer>().sharedMaterial = Glow(new Color(1f, 0.8f, 0.2f, 1f), 2f);
        }

        // Make sure the golden pickup sits on the same object as a trigger collider.
        Collider trigger = null;
        foreach (Collider c in go.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger) { trigger = c; break; }
        }
        if (trigger == null) { MakeTrigger(go, 0.8f); trigger = go.GetComponent<Collider>(); }
        var pickup = trigger.gameObject.AddComponent<PowerUpPickup>();
        pickup.type = PowerUpType.GoldenFly;
        pickup.lifetime = 8f;
        // Destroying the pickup's object should take the whole fly with it.
        if (trigger.gameObject != go) trigger.gameObject.AddComponent<DestroyRootWithMe>().root = go;
        return go;
    }
}
