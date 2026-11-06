using UnityEngine;

// FlyKind (added Nov 2026): not every fly is the same any more.
//
//   Fly         the classic, worth 1
//   Dragonfly   Lily Pad Lake onwards: blue-green, zips around twice as fast, worth 3
//   Firefly     Night and Winter: glows in the dark, worth 2
//   Bee         Autumn Woods onwards: yellow and black. Don't eat it! A sting costs you
//               2 flies and slows you down for a moment. (Your tongue won't grab bees.)
//
// Flies are reused (FlySpawner), so every time one wakes up it rolls a new kind.
public class FlyKind : MonoBehaviour
{
    public enum Type { Fly, Dragonfly, Firefly, Bee }

    public Type Kind { get; private set; }
    public float SpeedScale { get; private set; } = 1f;

    public int Points
    {
        get
        {
            switch (Kind)
            {
                case Type.Dragonfly: return 3;
                case Type.Firefly: return 2;
                default: return 1;
            }
        }
    }

    // The frog is slowed after a bee sting until this time.
    public static float StungUntil;

    private GameObject glow;
    private Vector3 baseScale = Vector3.one;
    private bool haveBase;

    public static void Assign(GameObject fly)
    {
        if (fly == null) return;
        FlyKind k = fly.GetComponent<FlyKind>();
        if (k == null) k = fly.AddComponent<FlyKind>();
        int level = LevelManager.Instance != null ? LevelManager.Instance.Level : 1;
        float roll = Random.value;
        Type type = Type.Fly;
        if (level >= 5 && roll < 0.12f) type = Type.Bee;
        else if (level >= 4 && roll < 0.30f) type = Type.Dragonfly;
        else if ((level == 3 || level == 6) && roll < 0.45f) type = Type.Firefly;
        k.Set(type);
    }

    public void Set(Type type)
    {
        if (!haveBase) { baseScale = transform.localScale; haveBase = true; }
        Kind = type;
        SpeedScale = type == Type.Dragonfly ? 2f : 1f;
        transform.localScale = baseScale * (type == Type.Dragonfly ? 1.5f : type == Type.Bee ? 1.25f : 1f);

        var block = new MaterialPropertyBlock();
        Color tint;
        switch (type)
        {
            case Type.Dragonfly: tint = new Color(0.2f, 0.85f, 0.85f); break;
            case Type.Firefly: tint = new Color(1f, 0.92f, 0.35f); break;
            case Type.Bee: tint = new Color(1f, 0.78f, 0.08f); break;
            default: tint = Color.white; break;
        }
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (glow != null && r.transform.IsChildOf(glow.transform)) continue;
            if (r is ParticleSystemRenderer) continue;
            if (type == Type.Fly) { r.SetPropertyBlock(null); continue; }
            block.Clear();
            block.SetColor("_BaseColor", tint);
            block.SetColor("_Color", tint);
            block.SetColor("_Recolor", new Color(tint.r, tint.g, tint.b, 1f));
            r.SetPropertyBlock(block);
        }

        // Fireflies (and bees, as a warning) get a soft glow around them.
        bool wantGlow = type == Type.Firefly || type == Type.Bee;
        if (wantGlow && glow == null)
        {
            glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(glow.GetComponent<Collider>());
            glow.name = "Glow";
            glow.transform.SetParent(transform, false);
            glow.transform.localScale = Vector3.one * 1.1f;
            glow.AddComponent<FaceCamera>();
        }
        if (glow != null)
        {
            glow.SetActive(wantGlow);
            if (wantGlow)
            {
                glow.GetComponent<Renderer>().sharedMaterial = type == Type.Firefly
                    ? GlowMaterial(ref fireflyGlow, new Color(1f, 0.9f, 0.3f, 0.9f))
                    : GlowMaterial(ref beeGlow, new Color(1f, 0.35f, 0.1f, 0.5f));
            }
        }
    }

    private static Material fireflyGlow, beeGlow;

    private static Material GlowMaterial(ref Material cache, Color color)
    {
        if (cache == null) cache = PowerUpSpawner.Glow(color, 2f);
        return cache;
    }

    public void Sting()
    {
        ScoreCounter.score = Mathf.Max(0, ScoreCounter.score - 2);
        StungUntil = Time.time + 2f;
        FrogEffects.Say("Ouch! A bee! -2 flies");
        AudioDirector.Play("Ouch", 0.9f);
        if (GameDirector.Instance != null) GameDirector.Instance.Shake(0.25f);
    }
}
