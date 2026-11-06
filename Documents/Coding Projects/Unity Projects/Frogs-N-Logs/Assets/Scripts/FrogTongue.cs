using UnityEngine;

// FrogTongue (added Nov 2026): the tongue flick. Press E (gamepad B, or the TONGUE button)
// and the frog's sticky tongue snaps out to grab the nearest fly up to 5 m away.
// It won't grab bees. Recharges in under a second. Slurp.
public class FrogTongue : MonoBehaviour
{
    public static FrogTongue Instance { get; private set; }

    public const float Range = 5f;
    public const float Cooldown = 0.7f;
    public float ReadyAt { get; private set; }

    private Transform frog;
    private LineRenderer line;
    private FlyPickUp target;
    private Vector3 targetPoint;
    private float flickStart = -10f;
    private const float OutSeconds = 0.09f, BackSeconds = 0.12f;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private Transform Frog
    {
        get
        {
            if (frog == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p != null) frog = p.transform;
            }
            return frog;
        }
    }

    void Update()
    {
        GameState game = GameState.Current;
        if (game != null && game.IsPlaying && !PauseLocked() && KeyBindings.TonguePressed()) Flick();
        Animate();
    }

    private static bool PauseLocked() { return Time.timeScale == 0f; }

    // Returns true if a fly was in reach.
    public bool Flick()
    {
        if (Time.time < ReadyAt || Frog == null || !Frog.gameObject.activeInHierarchy) return false;
        GameState game = GameState.Current;
        if (game == null || !game.IsPlaying) return false;
        ReadyAt = Time.time + Cooldown;

        FlyPickUp best = null;
        float bestD = Range;
        Vector3 mouth = Mouth();
        foreach (GameObject fly in FlySpawner.ActiveFlies)
        {
            if (fly == null || !fly.activeInHierarchy) continue;
            FlyKind kind = fly.GetComponent<FlyKind>();
            if (kind != null && kind.Kind == FlyKind.Type.Bee) continue;
            FlyPickUp pickup = fly.GetComponentInChildren<FlyPickUp>();
            if (pickup == null) continue;
            float d = Vector3.Distance(Flat(pickup.transform.position), Flat(Frog.position));
            if (d < bestD) { bestD = d; best = pickup; }
        }

        AudioDirector.Play("Tongue", 0.8f);
        target = best;
        targetPoint = best != null ? best.transform.position : mouth + Frog.forward * 2f;
        flickStart = Time.time;
        if (best == null) return false;

        // Turn to face it.
        Vector3 look = Flat(best.transform.position - Frog.position);
        if (look.sqrMagnitude > 0.01f) Frog.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        return true;
    }

    private Vector3 Mouth()
    {
        return Frog != null ? Frog.position + Vector3.up * 0.45f + Frog.forward * 0.35f : Vector3.zero;
    }

    private void Animate()
    {
        float t = Time.time - flickStart;
        bool showing = t >= 0f && t < OutSeconds + BackSeconds && Frog != null;
        if (line == null)
        {
            var go = new GameObject("Frog Tongue");
            go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.widthMultiplier = 0.12f;
            line.numCapVertices = 4;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.sharedMaterial = SnakeHazard.Toon(new Color(1f, 0.45f, 0.55f), 0.6f);
        }
        line.enabled = showing;
        if (!showing) return;

        if (target != null && target.gameObject.activeInHierarchy) targetPoint = target.transform.position;
        float reach = t < OutSeconds ? t / OutSeconds : 1f - (t - OutSeconds) / BackSeconds;
        Vector3 mouth = Mouth();
        line.SetPosition(0, mouth);
        line.SetPosition(1, Vector3.Lerp(mouth, targetPoint, Mathf.Clamp01(reach)));

        // Got it at full stretch.
        if (t >= OutSeconds && target != null)
        {
            if (target.Eat())
            {
                int caught = PlayerPrefs.GetInt("FNL.tongueCatches", 0) + 1;
                PlayerPrefs.SetInt("FNL.tongueCatches", caught);
                if (caught >= 25) Achievements.Unlock("tongue");
            }
            target = null;
        }
    }

    private static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }
}
