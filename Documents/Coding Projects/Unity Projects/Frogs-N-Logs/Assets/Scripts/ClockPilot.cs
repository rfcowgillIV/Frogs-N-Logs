using UnityEngine;

// ClockPilot (added Nov 2026): plays the game by itself while clock mode is on.
//
// The frog hops toward the nearest fly (skipping bees), flicks its tongue when one is in
// reach, keeps away from the bird and other hunters, and stays inside the pond. If there's
// nothing to chase it wanders. If anything gets too close it dodge-hops, and the bird gets
// knocked dizzy on contact (ClockMode can't lose; see FrogEffects.TryAbsorbHit).
//
// Every half minute it checks the real clock and switches to the world that suits the time
// of day and season (ClockMode.LevelFor), so the pond turns to dusk and night on its own.
public class ClockPilot : MonoBehaviour
{
    public static ClockPilot Instance { get; private set; }

    private Transform frog;
    private Transform bird;
    private Vector3 wanderTarget;
    private float wanderUntil;
    private float nextLevelCheck;
    private Vector3 lastPos;
    private float stuckSince = -1f;

    void Awake()
    {
        Instance = this;
        PlayerMovement.AutoMove = Vector3.zero;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        PlayerMovement.AutoMove = Vector3.zero;
    }

    void Update()
    {
        if (!ClockMode.Active)
        {
            PlayerMovement.AutoMove = Vector3.zero;
            return;
        }
        GameState game = GameState.Current;
        if (game == null || !game.IsPlaying) return;

        if (frog == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p == null) return;
            frog = p.transform;
        }
        if (bird == null)
        {
            BirdMovement b = FindFirstObjectByType<BirdMovement>();
            if (b != null) bird = b.transform;
        }

        FollowTheRealTime();
        PlayerMovement.AutoMove = Steer();
    }

    // ------------------------------------------------------------ time of day

    public void FollowTheRealTime(bool now = false)
    {
        if (!now && Time.time < nextLevelCheck) return;
        nextLevelCheck = Time.time + 30f;
        LevelManager levels = LevelManager.Instance;
        if (levels == null) return;
        int want = ClockMode.LevelFor(ClockMode.Now);
        if (levels.Level != want) levels.GoToLevel(want);
    }

    // ------------------------------------------------------------ steering

    private Vector3 Steer()
    {
        Vector3 pos = Flat(frog.position);
        Vector3 desire = Vector3.zero;

        // 1. Go for the nearest fly (bees sting, so leave them be).
        Vector3? fly = NearestFly(pos);
        if (fly.HasValue)
        {
            Vector3 to = fly.Value - pos;
            desire = to.normalized;
            if (to.magnitude < FrogTongue.Range * 0.9f && FrogTongue.Instance != null) FrogTongue.Instance.Flick();
        }
        else
        {
            if (Time.time > wanderUntil || (wanderTarget - pos).magnitude < 2f)
            {
                wanderTarget = Flat(PlayArea.RandomPoint(8f));
                wanderUntil = Time.time + 8f;
            }
            desire = (wanderTarget - pos).normalized * 0.7f;
        }

        // 2. Keep away from the bird and anything else that bites.
        Vector3 away = Vector3.zero;
        float closest = 99f;
        if (bird != null && !BirdMovement.IsStunned)
        {
            Vector3 d = pos - Flat(bird.position);
            float dist = d.magnitude;
            closest = dist;
            if (dist < 9f && dist > 0.01f) away += d.normalized * (9f - dist) / 9f * 2.2f;
        }
        foreach (GameObject e in GameObject.FindGameObjectsWithTag("Enemy"))
        {
            Vector3 d = pos - Flat(e.transform.position);
            float dist = d.magnitude;
            closest = Mathf.Min(closest, dist);
            if (dist < 5f && dist > 0.01f) away += d.normalized * (5f - dist) / 5f * 1.6f;
        }
        desire += away;

        // A quick dodge hop when something is right on top of us.
        if (closest < 2.5f && PlayerMovement.Instance != null && Time.time >= PlayerMovement.DodgeReadyAt)
        {
            PlayerMovement.Instance.Dodge();
        }

        // 3. Stay inside the pond.
        float edge = PlayArea.Inner(4f);
        if (Mathf.Abs(pos.x) > edge) desire.x -= Mathf.Sign(pos.x) * 1.5f;
        if (Mathf.Abs(pos.z) > edge) desire.z -= Mathf.Sign(pos.z) * 1.5f;

        // 4. Not getting anywhere (a log or a tree in the way)? Wander off somewhere else.
        if (desire.sqrMagnitude > 0.01f && (pos - lastPos).magnitude < 0.02f)
        {
            if (stuckSince < 0f) stuckSince = Time.time;
            if (Time.time - stuckSince > 1.2f)
            {
                wanderTarget = Flat(PlayArea.RandomPoint(8f));
                wanderUntil = Time.time + 4f;
                stuckSince = -1f;
                return (wanderTarget - pos).normalized;
            }
        }
        else stuckSince = -1f;
        lastPos = pos;

        if (desire.sqrMagnitude > 1f) desire.Normalize();
        return desire;
    }

    private static Vector3? NearestFly(Vector3 pos)
    {
        Vector3? best = null;
        float bestD = float.MaxValue;
        foreach (GameObject fly in FlySpawner.ActiveFlies)
        {
            if (fly == null || !fly.activeInHierarchy) continue;
            FlyKind kind = fly.GetComponent<FlyKind>();
            if (kind != null && kind.Kind == FlyKind.Type.Bee) continue;
            Vector3 p = Flat(fly.transform.position);
            float d = (p - pos).sqrMagnitude;
            if (d < bestD) { bestD = d; best = p; }
        }
        return best;
    }

    private static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }
}
