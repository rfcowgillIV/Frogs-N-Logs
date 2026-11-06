using UnityEngine;

// LeafPile (added Nov 2026): Autumn Woods' piles of fallen leaves. Hop through one to
// scatter it; more than half of them hide a bonus fly (+2). They pile up again later.
public class LeafPile : MonoBehaviour
{
    private bool hasFly;
    private float scatteredAt = -100f;
    private Transform visual;
    private Vector3 fullScale;

    public static LeafPile Create(Vector3 position, Transform parent)
    {
        var root = new GameObject("Leaf Pile");
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        var pile = root.AddComponent<LeafPile>();

        var heap = new GameObject("Leaves");
        heap.transform.SetParent(root.transform, false);
        Color[] colours = { new Color(0.95f, 0.45f, 0.1f), new Color(0.85f, 0.25f, 0.1f), new Color(0.98f, 0.72f, 0.2f), new Color(0.6f, 0.35f, 0.15f) };
        for (int i = 0; i < 6; i++)
        {
            GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(blob.GetComponent<Collider>());
            blob.transform.SetParent(heap.transform, false);
            float a = i / 6f * Mathf.PI * 2f;
            blob.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.45f, 0.12f, Mathf.Sin(a) * 0.45f) * (i == 0 ? 0f : 1f);
            blob.transform.localScale = new Vector3(Random.Range(0.8f, 1.2f), Random.Range(0.35f, 0.55f), Random.Range(0.8f, 1.2f));
            blob.GetComponent<Renderer>().sharedMaterial = SnakeHazard.Toon(colours[i % colours.Length], 0.1f);
        }
        pile.visual = heap.transform;
        pile.fullScale = heap.transform.localScale;

        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.1f;
        trigger.center = new Vector3(0f, 0.5f, 0f);
        pile.hasFly = Random.value < 0.6f;
        return pile;
    }

    void Update()
    {
        // Scatter (shrink away), then pile back up after a while.
        float since = Time.time - scatteredAt;
        float k = since < 0.35f ? 1f - since / 0.35f : since > 14f ? Mathf.Clamp01((since - 14f) / 1.5f) : 0f;
        visual.localScale = fullScale * Mathf.Max(0.001f, k);
        if (since > 15.5f && since < 16f) hasFly = Random.value < 0.6f;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || Time.time - scatteredAt < 15.5f) return;
        GameState game = GameState.Current;
        if (game == null || !game.IsPlaying) return;
        scatteredAt = Time.time;
        AudioDirector.Play("Rustle", 0.7f);
        if (hasFly)
        {
            hasFly = false;
            ScoreCounter.score += 2 * Shop.PointsMultiplier;
            FrogEffects.Say("A fly was hiding in the leaves! +2");
            AudioDirector.Play("FlyEat", 0.6f);
        }
    }
}
