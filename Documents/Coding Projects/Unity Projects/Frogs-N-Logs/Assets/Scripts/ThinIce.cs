using UnityEngine;

// ThinIce (added Nov 2026): Winter Freeze's pale patches of thin ice. Stand on one too long
// (under a second) and it cracks: you fall through and are stuck for a moment - a bad time
// with an owl around. Keep hopping! The ice freezes over again after a while.
public class ThinIce : MonoBehaviour
{
    private float standing;
    private float brokenAt = -100f;
    private Renderer patch;
    private static Material iceMat, holeMat;

    public const float CrackSeconds = 0.75f;
    public const float StuckSeconds = 1.2f;

    public static ThinIce Create(Vector3 position, Transform parent)
    {
        var root = new GameObject("Thin Ice");
        root.transform.SetParent(parent, false);
        root.transform.position = position + Vector3.up * 0.03f;
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(disc.GetComponent<Collider>());
        disc.transform.SetParent(root.transform, false);
        disc.transform.localScale = new Vector3(2.6f, 0.01f, 2.6f);
        var ice = root.AddComponent<ThinIce>();
        ice.patch = disc.GetComponent<Renderer>();
        if (iceMat == null)
        {
            Texture2D tex = Resources.Load<Texture2D>("Biomes/Ice");
            iceMat = tex != null ? new Material(Stylizer.Toon(tex, new Color(0.75f, 0.9f, 1f), 0f, false, 1f)) : SnakeHazard.Toon(new Color(0.7f, 0.88f, 1f), 1f);
            iceMat.hideFlags = HideFlags.DontSave;
            holeMat = SnakeHazard.Toon(new Color(0.08f, 0.16f, 0.28f), 0.8f);
        }
        ice.patch.sharedMaterial = iceMat;
        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.2f;
        trigger.center = new Vector3(0f, 0.4f, 0f);
        return ice;
    }

    private bool Broken { get { return Time.time - brokenAt < 15f; } }

    void Update()
    {
        patch.sharedMaterial = Broken ? holeMat : iceMat;
        // Shiver while it's about to crack.
        float shake = !Broken && standing > 0f ? Mathf.Sin(Time.time * 60f) * 0.04f * (standing / CrackSeconds) : 0f;
        patch.transform.localPosition = new Vector3(shake, 0f, 0f);
    }

    void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player") || Broken) return;
        GameState game = GameState.Current;
        if (game == null || !game.IsPlaying) return;
        if (PlayerMovement.Dodging) return; // leaping over it is fine
        standing += Time.fixedDeltaTime;
        if (standing < CrackSeconds) return;
        standing = 0f;
        brokenAt = Time.time;
        PlayerMovement.StuckUntil = Time.time + StuckSeconds;
        AudioDirector.Play("IceCrack", 0.9f);
        AudioDirector.Play("Splash", 0.6f);
        FrogEffects.Say("Crack! Stuck in the ice!");
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) standing = 0f;
    }
}
