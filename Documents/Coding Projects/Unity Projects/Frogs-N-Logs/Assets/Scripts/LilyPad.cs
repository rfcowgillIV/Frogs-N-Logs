using UnityEngine;

// LilyPad (added Nov 2026): Lily Pad Lake's floating pads. Wading through the flooded lake
// slows the frog down; standing on a pad doesn't. They bob gently, and some have a flower.
public class LilyPad : MonoBehaviour
{
    private float phase;
    private float baseY;

    public static LilyPad Create(Vector3 position, float size, bool flower, Transform parent)
    {
        var root = new GameObject("Lily Pad");
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(pad.GetComponent<Collider>());
        pad.name = "Pad";
        pad.transform.SetParent(root.transform, false);
        pad.transform.localScale = new Vector3(1.7f * size, 0.025f, 1.7f * size);
        pad.GetComponent<Renderer>().sharedMaterial = SnakeHazard.Toon(new Color(0.28f, 0.62f, 0.2f), 0.25f);

        // The little notch cut into a lily pad.
        GameObject notch = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(notch.GetComponent<Collider>());
        notch.transform.SetParent(root.transform, false);
        notch.transform.localScale = new Vector3(0.12f * size, 0.04f, 0.85f * size);
        notch.transform.localPosition = new Vector3(0f, 0.005f, 0.42f * size);
        notch.GetComponent<Renderer>().sharedMaterial = SnakeHazard.Toon(new Color(0.18f, 0.42f, 0.14f), 0.1f);

        if (flower)
        {
            GameObject bloom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(bloom.GetComponent<Collider>());
            bloom.transform.SetParent(root.transform, false);
            bloom.transform.localScale = new Vector3(0.35f, 0.22f, 0.35f);
            bloom.transform.localPosition = new Vector3(0.25f * size, 0.08f, -0.2f * size);
            bloom.GetComponent<Renderer>().sharedMaterial = SnakeHazard.Toon(new Color(1f, 0.6f, 0.8f), 0.5f);
        }

        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.95f * size;
        trigger.center = new Vector3(0f, 0.4f, 0f);
        var lily = root.AddComponent<LilyPad>();
        lily.baseY = position.y;
        lily.phase = Random.Range(0f, 6.28f);
        return lily;
    }

    void Update()
    {
        Vector3 p = transform.position;
        p.y = baseY + Mathf.Sin(Time.time * 1.3f + phase) * 0.025f;
        transform.position = p;
    }

    void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player")) Biome.LastOnPad = Time.time;
    }
}
