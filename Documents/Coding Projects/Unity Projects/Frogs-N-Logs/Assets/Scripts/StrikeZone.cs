using UnityEngine;

// StrikeZone (added Nov 2026): a telegraphed attack. A ring appears on the ground and fills
// up; when it's full, whatever is inside gets struck. Hop out of the ring (or dodge-hop
// through it) in time!
//   Pike   Lily Pad Lake: a big fish lunges out of the water with a splash
//   Owl    The Great Owl's dive in the Winter Freeze finale (BossOwl.cs)
public class StrikeZone : MonoBehaviour
{
    public enum Kind { Pike, Owl }

    public Kind Type { get; private set; }
    public float Radius { get; private set; }
    public bool Struck { get; private set; }
    public static int Active { get; private set; }
    public static event System.Action<StrikeZone> Striking;

    private float warn, born;
    private LineRenderer ring;
    private Transform fill;
    private Transform fish;

    public static StrikeZone Create(Vector3 position, float radius, float warnSeconds, Kind kind)
    {
        var go = new GameObject(kind == Kind.Pike ? "Pike Strike" : "Owl Dive");
        go.transform.position = position + Vector3.up * 0.06f;
        var z = go.AddComponent<StrikeZone>();
        z.Type = kind;
        z.Radius = radius;
        z.warn = warnSeconds;
        z.born = Time.time;

        Color warnColor = kind == Kind.Pike ? new Color(0.2f, 0.75f, 1f) : new Color(1f, 0.25f, 0.2f);
        z.ring = go.AddComponent<LineRenderer>();
        z.ring.loop = true;
        z.ring.useWorldSpace = false;
        z.ring.positionCount = 48;
        z.ring.widthMultiplier = 0.12f;
        z.ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        z.ring.sharedMaterial = PowerUpSpawner.Glow(warnColor, 2f);
        for (int i = 0; i < 48; i++)
        {
            float a = i / 48f * Mathf.PI * 2f;
            z.ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(disc.GetComponent<Collider>());
        disc.transform.SetParent(go.transform, false);
        disc.GetComponent<Renderer>().sharedMaterial = SnakeHazard.Toon(kind == Kind.Pike ? new Color(0.1f, 0.25f, 0.4f) : new Color(0.08f, 0.06f, 0.1f), 0f);
        disc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        z.fill = disc.transform;
        z.fill.localScale = new Vector3(0.01f, 0.005f, 0.01f);
        Active++;
        return z;
    }

    void OnDestroy() { Active = Mathf.Max(0, Active - 1); }

    void Update()
    {
        float t = (Time.time - born) / warn;
        if (!Struck)
        {
            float d = Radius * 2f * Mathf.Clamp01(t);
            fill.localScale = new Vector3(d, 0.005f, d);
            ring.widthMultiplier = 0.08f + 0.08f * Mathf.Abs(Mathf.Sin(Time.time * (6f + 10f * t)));
            if (t >= 1f) Strike();
            return;
        }
        // After the strike: the pike drops back into the water, then everything fades away.
        float after = Time.time - born - warn;
        if (fish != null) fish.localPosition = new Vector3(0f, Mathf.Sin(Mathf.Clamp01(after / 0.6f) * Mathf.PI) * 1.4f - 0.3f, 0f);
        fill.localScale *= 0.9f;
        if (after > 0.7f) Destroy(gameObject);
    }

    private void Strike()
    {
        Struck = true;
        ring.enabled = false;
        if (Striking != null) Striking(this);
        AudioDirector.Play(Type == Kind.Pike ? "Splash" : "Strike", 0.9f);
        if (Type == Kind.Pike)
        {
            // A cartoon pike leaps out.
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(transform, false);
            body.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 75f);
            body.transform.localScale = new Vector3(0.6f, 1.1f, 0.5f);
            body.GetComponent<Renderer>().sharedMaterial = SnakeHazard.Toon(new Color(0.45f, 0.55f, 0.4f), 0.7f);
            fish = body.transform;
        }

        GameObject frog = GameObject.FindWithTag("Player");
        if (frog == null || !frog.activeInHierarchy) return;
        Vector3 d = frog.transform.position - transform.position;
        d.y = 0f;
        if (d.magnitude > Radius) return;
        PlayerHealth health = frog.GetComponent<PlayerHealth>();
        if (health != null) health.Catch(null);
    }
}
