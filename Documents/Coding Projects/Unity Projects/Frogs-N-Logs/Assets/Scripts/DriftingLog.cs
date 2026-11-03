using UnityEngine;
using UnityEngine.AI;

// DriftingLog (added Nov 2026): logs that roll slowly across the swamp.
//
// They shove the frog out of the way (and can push you right into trouble), but the bird
// has to go AROUND them, so a log between you and the bird buys you precious seconds.
// Frogs-N-LOGS finally lives up to its name.
public class DriftingLog : MonoBehaviour
{
    private Vector3 direction;
    private float speed;
    private float travelled;
    private float distance;
    private Rigidbody body;
    private Transform visual;

    public static DriftingLog Create(Material bark)
    {
        // Start just inside one wall and roll to the opposite one.
        bool alongX = Random.value < 0.5f;
        float lane = Random.Range(-22f, 22f);
        float sign = Random.value < 0.5f ? 1f : -1f;
        Vector3 dir = alongX ? new Vector3(sign, 0f, 0f) : new Vector3(0f, 0f, sign);
        Vector3 start = alongX ? new Vector3(-27f * sign, 0f, lane) : new Vector3(lane, 0f, -27f * sign);

        var root = new GameObject("Drifting Log");
        start.y = PowerUpSpawner.GroundHeight(start) + 0.5f;
        root.transform.position = start;
        // Lie the log across its direction of travel.
        root.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

        var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        log.name = "Log";
        log.transform.SetParent(root.transform, false);
        log.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // cylinder axis -> sideways
        log.transform.localScale = new Vector3(0.9f, 2.2f, 0.9f);    // 4.4 m long, 0.9 m thick
        Destroy(log.GetComponent<Collider>());
        var renderer = log.GetComponent<Renderer>();
        renderer.sharedMaterial = bark != null ? bark : SnakeHazard.Toon(new Color(0.45f, 0.30f, 0.17f), 0.1f);

        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.direction = 0; // x axis
        capsule.height = 4.4f;
        capsule.radius = 0.45f;

        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // The bird (and snake) path around it.
        var obstacle = root.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box; // boxes follow the log's rotation
        obstacle.size = new Vector3(4.4f, 1f, 1f);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;

        var drift = root.AddComponent<DriftingLog>();
        drift.direction = dir;
        drift.speed = Random.Range(1.3f, 2.2f);
        drift.distance = 54f;
        drift.body = rb;
        drift.visual = log.transform;
        return drift;
    }

    void FixedUpdate()
    {
        GameState game = GameState.Current;
        if (game == null || !game.IsPlaying) return;

        float step = speed * Time.fixedDeltaTime;
        travelled += step;
        Vector3 next = body.position + direction * step;
        next.y = PowerUpSpawner.GroundHeight(next) + 0.5f;
        body.MovePosition(next);

        // Roll as it goes (purely visual).
        visual.Rotate(Vector3.up, -step / 0.45f * Mathf.Rad2Deg, Space.Self);

        if (travelled >= distance) Destroy(gameObject);
    }
}
