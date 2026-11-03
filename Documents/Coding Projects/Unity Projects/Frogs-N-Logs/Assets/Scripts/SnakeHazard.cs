using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// SnakeHazard (added Nov 2026): a snake that wakes up in the Swamp.
//
// It slithers around the pond on the same paths as the bird. When the frog comes within
// about 11 metres it gives chase, but it's slower than the bird, so you can out-hop it.
// Touch its head and it's game over (unless you have a lily-pad shield).
//
// It's built from simple spheres in code: a head with eyes, and a tail that follows the
// path the head took, so it really slithers.
//
// Hiss-terical.
public class SnakeHazard : MonoBehaviour
{
    private const int Segments = 12;
    private const float SegmentSpacing = 0.38f;
    private const float ChaseRange = 11f;

    private NavMeshAgent agent;
    private readonly List<Transform> body = new List<Transform>();
    private readonly List<Vector3> trail = new List<Vector3>();
    private Transform frog;
    private float stunnedUntil;
    private float wanderTimer;
    private float baseSpeed;

    public bool IsStunned { get { return Time.time < stunnedUntil; } }

    public void Stun(float seconds)
    {
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
    }

    public static SnakeHazard Create(Vector3 position)
    {
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(position, out hit, 6f, NavMesh.AllAreas)) return null;

        var root = new GameObject("Snake");
        root.transform.position = hit.position;
        var snake = root.AddComponent<SnakeHazard>();
        snake.Build();
        return snake;
    }

    private void Build()
    {
        Color green = new Color(0.32f, 0.55f, 0.18f, 1f);
        Color belly = new Color(0.78f, 0.82f, 0.35f, 1f);

        // Head: the part that bites. Tagged Enemy so the frog's existing "caught" logic works.
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Snake Head";
        head.tag = "Enemy";
        head.transform.SetParent(transform, false);
        head.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        head.transform.localScale = new Vector3(0.62f, 0.45f, 0.8f);
        head.GetComponent<Renderer>().sharedMaterial = Solid(green);
        var headCollider = head.GetComponent<SphereCollider>();
        headCollider.isTrigger = true;
        headCollider.radius = 0.75f;
        var rb = head.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Eyes (glowing yellow) and a tongue.
        foreach (float side in new[] { -1f, 1f })
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(eye.GetComponent<Collider>());
            eye.transform.SetParent(head.transform, false);
            eye.transform.localPosition = new Vector3(0.3f * side, 0.25f, 0.3f);
            eye.transform.localScale = Vector3.one * 0.22f;
            eye.GetComponent<Renderer>().sharedMaterial = PowerUpSpawner.Glow(new Color(1f, 0.9f, 0.2f, 1f), 0.6f);
        }
        var tongue = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(tongue.GetComponent<Collider>());
        tongue.transform.SetParent(head.transform, false);
        tongue.transform.localPosition = new Vector3(0f, -0.1f, 0.62f);
        tongue.transform.localScale = new Vector3(0.08f, 0.04f, 0.35f);
        tongue.GetComponent<Renderer>().sharedMaterial = Solid(new Color(0.85f, 0.15f, 0.2f));

        // Body: spheres that get smaller toward the tail. No colliders (only the head bites),
        // and they're not children of the head, so they can follow its trail freely.
        for (int i = 0; i < Segments; i++)
        {
            var seg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(seg.GetComponent<Collider>());
            seg.name = "Snake Body " + (i + 1);
            float size = Mathf.Lerp(0.55f, 0.18f, i / (float)(Segments - 1));
            seg.transform.localScale = new Vector3(size, size * 0.75f, size);
            seg.transform.position = transform.position - transform.forward * SegmentSpacing * (i + 1) + Vector3.up * 0.3f;
            seg.GetComponent<Renderer>().sharedMaterial = Solid(i % 3 == 1 ? belly : green);
            seg.transform.SetParent(transform.parent, true);
            body.Add(seg.transform);
        }

        agent = gameObject.AddComponent<NavMeshAgent>();
        float difficulty = GameSettings.Difficulty == Difficulty.Easy ? 0.8f : GameSettings.Difficulty == Difficulty.Hard ? 1.2f : 1f;
        baseSpeed = 2.8f * difficulty;
        agent.speed = baseSpeed;
        agent.acceleration = 10f;
        agent.angularSpeed = 300f;
        agent.radius = 0.35f;
        agent.height = 0.6f;
        agent.stoppingDistance = 0f;
        agent.autoBraking = false;
    }

    private static Material toonShared;

    // A simple lit material (Resources/FrogsToon.shader), shared with logs and hats.
    public static Material Toon(Color color, float shine)
    {
        if (toonShared == null)
        {
            Shader shader = Resources.Load<Shader>("FrogsToon");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            toonShared = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        var m = new Material(toonShared) { hideFlags = HideFlags.HideAndDontSave };
        m.SetColor("_Color", color);
        if (m.HasProperty("_Shine")) m.SetFloat("_Shine", shine);
        return m;
    }

    private static Material Solid(Color color)
    {
        return Toon(color, 0.45f);
    }

    void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) frog = player.transform;
        trail.Add(transform.position);
    }

    void OnDestroy()
    {
        foreach (Transform seg in body)
        {
            if (seg != null) Destroy(seg.gameObject);
        }
    }

    void Update()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        GameState game = GameState.Current;
        bool playing = game != null && game.IsPlaying;
        agent.isStopped = !playing || IsStunned;
        if (IsStunned)
        {
            transform.Rotate(0f, 400f * Time.deltaTime, 0f);
        }

        if (playing && !IsStunned)
        {
            bool chasing = frog != null && frog.gameObject.activeInHierarchy
                           && Vector3.Distance(frog.position, transform.position) < ChaseRange;
            if (chasing)
            {
                agent.speed = baseSpeed * 1.15f;
                agent.SetDestination(frog.position);
            }
            else
            {
                agent.speed = baseSpeed;
                wanderTimer -= Time.deltaTime;
                if (wanderTimer <= 0f || agent.remainingDistance < 1f)
                {
                    wanderTimer = Random.Range(4f, 8f);
                    Vector3 target = new Vector3(Random.Range(-24f, 24f), transform.position.y, Random.Range(-24f, 24f));
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(target, out hit, 5f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
                }
            }
        }

        UpdateBody();
    }

    private void UpdateBody()
    {
        // Record the head's path every few centimetres...
        Vector3 headPos = transform.position + Vector3.up * 0.3f;
        if (trail.Count == 0 || Vector3.Distance(trail[0], headPos) > 0.06f)
        {
            trail.Insert(0, headPos);
            int max = Mathf.CeilToInt(Segments * SegmentSpacing / 0.06f) + 10;
            if (trail.Count > max) trail.RemoveRange(max, trail.Count - max);
        }

        // ...and place each body segment along it, with a gentle side-to-side wiggle.
        float wiggleTime = Time.time * 6f;
        for (int i = 0; i < body.Count; i++)
        {
            float distance = SegmentSpacing * (i + 1);
            Vector3 p = PointAlongTrail(distance);
            Vector3 next = PointAlongTrail(distance - 0.1f);
            Vector3 dir = next - p;
            dir.y = 0f;
            Vector3 side = dir.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, dir.normalized) : Vector3.right;
            p += side * Mathf.Sin(wiggleTime - i * 0.7f) * 0.07f;
            p.y = PowerUpSpawner.GroundHeight(p) + body[i].localScale.y * 0.5f;
            body[i].position = p;
        }
    }

    private Vector3 PointAlongTrail(float distance)
    {
        if (trail.Count == 0) return transform.position;
        float walked = 0f;
        for (int i = 1; i < trail.Count; i++)
        {
            float step = Vector3.Distance(trail[i - 1], trail[i]);
            if (walked + step >= distance)
            {
                return Vector3.Lerp(trail[i - 1], trail[i], (distance - walked) / Mathf.Max(step, 0.0001f));
            }
            walked += step;
        }
        return trail[trail.Count - 1];
    }
}
