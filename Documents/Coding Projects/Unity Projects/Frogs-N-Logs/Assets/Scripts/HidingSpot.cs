using UnityEngine;

// HidingSpot (added Nov 2026): clumps of tall reeds around the pond. Duck into one and keep
// still (or creep slowly) and the bird and the fox lose sight of you - they search around
// where they last saw you. You can only hide for 6 seconds before you're spotted, and then
// that clump is no good for a little while.
public class HidingSpot : MonoBehaviour
{
    public const float MaxHideSeconds = 6f;
    public const float SneakSpeed = 2.2f;   // move slower than this to stay hidden
    public const float Cooldown = 8f;

    private static float lastInside = -10f;
    private static HidingSpot current;
    private static float hiddenFor;
    private float spottedAt = -100f;

    // Is the frog hidden right now? (Read by BirdMovement and GroundHunter.)
    public static bool FrogHidden { get; private set; }
    public static float HiddenSecondsLeft { get { return Mathf.Max(0f, MaxHideSeconds - hiddenFor); } }

    public static HidingSpot Create(Vector3 position, Transform parent)
    {
        var root = new GameObject("Hiding Spot (reeds)");
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        GameObject reeds = Resources.Load<GameObject>("Nature/Grass_Common_Tall");
        GameObject fern = Resources.Load<GameObject>("Nature/Fern_1");
        for (int i = 0; i < 7; i++)
        {
            GameObject prefab = i % 3 == 2 && fern != null ? fern : reeds;
            if (prefab == null) continue;
            float a = i / 7f * Mathf.PI * 2f;
            float r = i == 0 ? 0f : Random.Range(0.5f, 1.1f);
            Vector3 p = position + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            p.y = PowerUpSpawner.GroundHeight(p);
            GameObject g = Instantiate(prefab, p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), root.transform);
            g.transform.localScale *= Random.Range(1.5f, 2.1f);
            foreach (Collider c in g.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (Renderer rend in g.GetComponentsInChildren<Renderer>()) SceneDressing.DressPlant(rend);
        }
        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.5f;
        trigger.center = new Vector3(0f, 0.5f, 0f);
        return root.AddComponent<HidingSpot>();
    }

    void Awake()
    {
        FrogHidden = false;
        hiddenFor = 0f;
        lastInside = -10f;
    }

    void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (Time.time - spottedAt < Cooldown) return;
        PlayerMovement move = other.GetComponentInParent<PlayerMovement>();
        if (move == null || move.CurrentSpeed > SneakSpeed) return;
        lastInside = Time.time;
        current = this;
    }

    // One place updates the shared "hidden" state (the first spot in the scene).
    void Update()
    {
        if (current != null && current != this) return;
        bool inside = Time.time - lastInside < 0.15f;
        GameState game = GameState.Current;
        if (game == null || !game.IsPlaying) inside = false;
        if (inside)
        {
            hiddenFor += Time.deltaTime;
            if (hiddenFor >= MaxHideSeconds && current != null)
            {
                current.spottedAt = Time.time;
                FrogEffects.Say("Spotted! Find another hiding place");
                hiddenFor = 0f;
                inside = false;
                lastInside = -10f;
            }
        }
        else
        {
            hiddenFor = Mathf.Max(0f, hiddenFor - Time.deltaTime * 0.5f);
        }
        if (inside && !FrogHidden) FrogEffects.Say("Hiding... keep still");
        FrogHidden = inside;
    }
}
