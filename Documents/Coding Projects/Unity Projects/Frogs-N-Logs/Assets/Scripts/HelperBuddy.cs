using UnityEngine;

// HelperBuddy (added Nov 2026): a friend who follows the frog and chases predators away.
//
// When the bird (or a snake) gets close to the frog, the buddy dashes at it. On contact the
// predator is knocked back and left dizzy, then the buddy trots back to the frog and needs
// a few seconds to get its breath back before it can chase again.
//
//   Biscuit the dog     (free)  dizzy for 3 s
//   Marmalade the cat   (shop)  fastest dash
//   Shadow the wolf     (shop)  dizzy for 4.5 s
//   Buddy training      (shop)  recovers faster (12 -> 9 -> 7 -> 5 s)
//   Guard zone          (shop)  guards a bigger area (7 -> 9 -> 11 -> 13 m)
//
// Turn it off in Pause > Extras > Helper buddy. Models: Quaternius "Animal Pack Vol. 2" (CC0).
// Who's a good boy? Biscuit is.
public class HelperBuddy : MonoBehaviour
{
    public static HelperBuddy Current { get; private set; }
    public static int ChasesThisRound { get; private set; }

    public enum Mode { Follow, Chase, Return }
    public Mode State { get; private set; }
    public float ReadyAt { get; private set; }
    public bool Ready { get { return Time.time >= ReadyAt; } }
    public string HelperName { get; private set; }

    private Transform frog;
    private Transform target;
    private Transform visual;
    private Animation anim;
    private string walkClip, idleClip, playing;
    private float dashSpeed, stunSeconds;
    private Vector3 lastPosition;
    private Vector3 followSide = new Vector3(-1.6f, 0f, -1.2f);

    // ------------------------------------------------------------ setup

    public static void Refresh()
    {
        bool want = GameSettings.HelperOn && GameState.Current != null;
        string id = Shop.Owns(Shop.EquippedHelper) ? Shop.EquippedHelper : "helper.dog";
        if (Current != null && (!want || Current.HelperName != NameFor(id)))
        {
            Destroy(Current.gameObject);
            Current = null;
        }
        if (want && Current == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) return;
            var go = new GameObject("Helper Buddy");
            Current = go.AddComponent<HelperBuddy>();
            Current.Setup(id, player.transform);
        }
    }

    private static string NameFor(string id)
    {
        Shop.Item item = Shop.Find(id);
        return item != null ? item.name : "Biscuit the dog";
    }

    private void Setup(string id, Transform frogTransform)
    {
        frog = frogTransform;
        HelperName = NameFor(id);
        string model = id == "helper.cat" ? "Cat" : id == "helper.wolf" ? "Wolf" : "Dog";
        // Nov 2026: a bit bigger, so they read clearly next to the frog.
        float length = id == "helper.cat" ? 1.4f : id == "helper.wolf" ? 2.2f : 1.8f;
        dashSpeed = id == "helper.cat" ? 13f : 11f;
        stunSeconds = id == "helper.wolf" ? 4.5f : 3f;

        transform.position = frog.position + followSide;
        lastPosition = transform.position;

        GameObject prefab = Resources.Load<GameObject>("Characters/" + model);
        if (prefab == null)
        {
            // Fallback: a little ball, so the buddy still works if the model is missing.
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(ball.GetComponent<Collider>());
            ball.transform.SetParent(transform, false);
            ball.transform.localScale = Vector3.one * 0.7f;
            ball.transform.localPosition = Vector3.up * 0.35f;
            ball.GetComponent<Renderer>().sharedMaterial = SnakeHazard.Toon(new Color(0.85f, 0.6f, 0.3f), 0.4f);
            visual = ball.transform;
            return;
        }

        GameObject instance = Instantiate(prefab, transform);
        instance.name = model + " (helper model)";
        visual = instance.transform;
        foreach (Collider c in instance.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
        {
            Stylizer.ToToon(r);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            if (r is SkinnedMeshRenderer) ((SkinnedMeshRenderer)r).updateWhenOffscreen = true;
        }

        FitModel(instance, transform, length);

        anim = instance.GetComponentInChildren<Animation>();
        if (anim != null)
        {
            foreach (AnimationState s in anim)
            {
                s.wrapMode = WrapMode.Loop;
                string n = s.name.ToLowerInvariant();
                if (n.EndsWith("walking") || n.EndsWith("walk")) walkClip = s.name;
                if (n.EndsWith("idle")) idleClip = s.name;
            }
            anim.playAutomatically = false;
        }
        Play(idleClip, 1f);
        Debug.Log("[Helper] " + HelperName + " is here to help");
    }

    // Turn an animal model to face +Z (front legs ahead of back legs), scale it to the given
    // length and stand it on its host's position. Also used for the fox (GroundHunter.cs).
    public static void FitModel(GameObject instance, Transform host, float length)
    {
        Transform visual = instance.transform;
        Vector3 front, back;
        if (Average(visual, "IKFront", out front) && Average(visual, "IKBack", out back))
        {
            Vector3 d = front - back;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) visual.localRotation = Quaternion.FromToRotation(d.normalized, Vector3.forward) * visual.localRotation;
        }
        Bounds b = BoundsOf(instance);
        float have = Mathf.Max(b.size.x, b.size.z);
        if (have > 0.0001f) visual.localScale *= length / have;
        b = BoundsOf(instance);
        visual.position += new Vector3(host.position.x - b.center.x, host.position.y - b.min.y, host.position.z - b.center.z);
    }

    // Average position of the bones whose names start with prefix (e.g. the front feet).
    private static bool Average(Transform root, string prefix, out Vector3 average)
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith(prefix)) continue;
            sum += t.position;
            n++;
        }
        average = n > 0 ? sum / n : Vector3.zero;
        return n > 0;
    }

    private static Bounds BoundsOf(GameObject go)
    {
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        bool first = true;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    void Awake()
    {
        ChasesThisRound = 0;
    }

    public static void ResetRound() { ChasesThisRound = 0; }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    // ------------------------------------------------------------ behaviour

    void Update()
    {
        if (frog == null) { Destroy(gameObject); return; }
        GameState game = GameState.Current;
        bool playing = game != null && game.IsPlaying && frog.gameObject.activeInHierarchy;
        float dt = Time.deltaTime;

        if (playing && State == Mode.Follow && Ready)
        {
            target = ClosestThreat();
            if (target != null) State = Mode.Chase;
        }

        Vector3 goal;
        float speed;
        if (State == Mode.Chase && target != null && target.gameObject.activeInHierarchy)
        {
            goal = target.position;
            speed = dashSpeed;
            if (Flat(goal - transform.position).magnitude < 1.6f) Scare(target); // (clears target)
            // Give up if it gets away.
            else if (Flat(target.position - frog.position).magnitude > Shop.HelperGuardRadius * 2.5f) State = Mode.Return;
        }
        else
        {
            if (State == Mode.Chase) State = Mode.Return;
            // Trot along beside the frog.
            goal = frog.position + frog.rotation * followSide;
            float d = Flat(goal - transform.position).magnitude;
            speed = Mathf.Clamp(d * 2.2f, 0f, 9f);
            if (State == Mode.Return && d < 1.5f) State = Mode.Follow;
        }

        Vector3 pos = transform.position;
        Vector3 step = Vector3.MoveTowards(Flat(pos), Flat(goal), speed * dt);
        // Stay inside the pond.
        float half = PlayArea.HalfSize - 0.8f;
        step.x = Mathf.Clamp(step.x, -half, half);
        step.z = Mathf.Clamp(step.z, -half, half);
        step.y = PowerUpSpawner.GroundHeight(step);
        transform.position = step;

        Vector3 moved = Flat(step - lastPosition);
        float moving = moved.magnitude / Mathf.Max(dt, 0.0001f);
        if (moved.sqrMagnitude > 0.0004f)
        {
            Quaternion look = Quaternion.LookRotation(moved.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 12f * dt);
        }
        else if (State == Mode.Follow)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, frog.rotation, 3f * dt);
        }
        lastPosition = step;

        if (moving > 0.4f) Play(walkClip, Mathf.Clamp(moving / 3f, 0.7f, 2.5f));
        else Play(idleClip, 1f);
    }

    private Transform ClosestThreat()
    {
        float radius = Shop.HelperGuardRadius;
        Transform best = null;
        float bestD = radius;
        BirdMovement bird = FindFirstObjectByType<BirdMovement>();
        if (bird != null && !BirdMovement.IsStunned)
        {
            float d = Flat(bird.transform.position - frog.position).magnitude;
            if (d < bestD) { bestD = d; best = bird.transform; }
        }
        foreach (SnakeHazard snake in FindObjectsByType<SnakeHazard>(FindObjectsSortMode.None))
        {
            float d = Flat(snake.transform.position - frog.position).magnitude;
            if (d < bestD) { bestD = d; best = snake.transform; }
        }
        GroundHunter fox = GroundHunter.Active;
        if (fox != null && !fox.Stunned)
        {
            float d = Flat(fox.transform.position - frog.position).magnitude;
            if (d < bestD) { bestD = d; best = fox.transform; }
        }
        return best;
    }

    private void Scare(Transform predator)
    {
        Vector3 away = Flat(predator.position - frog.position);
        away = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;

        BirdMovement bird = predator.GetComponent<BirdMovement>();
        if (bird != null)
        {
            BirdMovement.Stun(stunSeconds);
            var agent = predator.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh) agent.Move(away * 4f); // knocked back
        }
        SnakeHazard snake = predator.GetComponent<SnakeHazard>();
        if (snake != null) snake.Stun(stunSeconds);
        GroundHunter fox = predator.GetComponent<GroundHunter>();
        if (fox != null)
        {
            fox.Stun(stunSeconds);
            var foxAgent = predator.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (foxAgent != null && foxAgent.isOnNavMesh) foxAgent.Move(away * 4f);
        }

        ChasesThisRound++;
        int total = PlayerPrefs.GetInt("FNL.buddyChases", 0) + 1;
        PlayerPrefs.SetInt("FNL.buddyChases", total);
        if (total >= 5) Achievements.Unlock("buddy");
        if (ChasesThisRound == 1) FrogEffects.Say(HelperName.Split(' ')[0] + " chased it off!");
        AudioDirector.Play("ShieldPop", 0.7f);

        State = Mode.Return;
        target = null;
        ReadyAt = Time.time + Shop.HelperCooldown;
    }

    private void Play(string clip, float speed)
    {
        if (anim == null || string.IsNullOrEmpty(clip)) return;
        if (playing != clip)
        {
            anim.CrossFade(clip, 0.15f);
            playing = clip;
        }
        AnimationState s = anim[clip];
        if (s != null) s.speed = speed;
    }

    private static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }
}
