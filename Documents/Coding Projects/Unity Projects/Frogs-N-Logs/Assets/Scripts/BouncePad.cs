using UnityEngine;

// BouncePad (added Nov 2026): Autumn Woods' giant springy mushrooms. Hop onto one and it
// launches you forward - great for escaping the fox. Boing!
public class BouncePad : MonoBehaviour
{
    private float lastBounce = -10f;
    private Transform cap;
    private Vector3 capScale;

    public static BouncePad Create(Vector3 position, Transform parent)
    {
        var root = new GameObject("Giant Mushroom");
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        var pad = root.AddComponent<BouncePad>();

        GameObject prefab = Resources.Load<GameObject>("Nature/Mushroom_Common");
        GameObject model;
        if (prefab != null)
        {
            model = Instantiate(prefab, root.transform);
            foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>()) Stylizer.ToToon(r);
            model.transform.localScale *= 3.2f;
        }
        else
        {
            model = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(model.GetComponent<Collider>());
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = new Vector3(1.6f, 0.6f, 1.6f);
            model.transform.localPosition = Vector3.up * 0.6f;
            model.GetComponent<Renderer>().sharedMaterial = SnakeHazard.Toon(new Color(0.9f, 0.2f, 0.15f), 0.5f);
        }
        pad.cap = model.transform;
        pad.capScale = model.transform.localScale;

        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.3f;
        trigger.center = new Vector3(0f, 0.6f, 0f);
        return pad;
    }

    void Update()
    {
        // A little squash and stretch after a bounce.
        float since = Time.time - lastBounce;
        float squash = since < 0.4f ? 1f + Mathf.Sin(since / 0.4f * Mathf.PI * 3f) * 0.18f * (1f - since / 0.4f) : 1f;
        float side = 1f / Mathf.Sqrt(squash);
        cap.localScale = Vector3.Scale(capScale, new Vector3(side, squash, side));
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || Time.time - lastBounce < 0.8f) return;
        PlayerMovement frog = other.GetComponentInParent<PlayerMovement>();
        if (frog == null || !frog.enabled) return;
        lastBounce = Time.time;
        frog.Bounce(0.38f);
        AudioDirector.Play("Bounce", 0.8f);
        FrogEffects.Say("Boing!");
    }
}
