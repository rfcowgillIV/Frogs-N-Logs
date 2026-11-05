using UnityEngine;

// BossOwl (added Nov 2026): the grand finale. When you reach the Winter Freeze's fly goal,
// the Great Owl swoops in. For 30 seconds it circles overhead and dives at you: a red ring
// marks where it will strike, so hop (or dodge-hop) out of the ring before it fills up.
// The dives get quicker as time goes on. Survive, and the owl gives up and flies away -
// and the credits roll. (The regular bird keeps its distance while the Great Owl hunts.)
public class BossOwl : MonoBehaviour
{
    public static BossOwl Active { get; private set; }
    public const float Duration = 30f;

    public float SecondsLeft { get { return Mathf.Max(0f, endAt - Time.time); } }
    public int Dives { get; private set; }

    private float endAt, nextDive;
    private Transform frog, model;
    private StrikeZone currentDive;
    private float angle;
    private bool leaving;
    private float leftAt;

    void Awake()
    {
        LevelManager.BossStarted += Begin;
    }

    void OnDestroy()
    {
        LevelManager.BossStarted -= Begin;
        if (Active == this) Active = null;
    }

    private void Begin()
    {
        if (Active != null) return;
        Active = this;
        GameObject p = GameObject.FindWithTag("Player");
        frog = p != null ? p.transform : null;
        endAt = Time.time + Duration;
        nextDive = Time.time + 2.5f;
        Dives = 0;
        leaving = false;

        // The Great Owl: the eagle model, huge and snowy white.
        GameObject prefab = Resources.Load<GameObject>("Characters/Eagle");
        if (prefab != null)
        {
            GameObject owl = Instantiate(prefab);
            owl.name = "The Great Owl";
            foreach (Collider c in owl.GetComponentsInChildren<Collider>()) Destroy(c);
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in owl.GetComponentsInChildren<Renderer>(true))
            {
                Stylizer.ToToon(r);
                block.SetColor("_Recolor", new Color(0.96f, 0.97f, 1f, 0.9f));
                r.SetPropertyBlock(block);
            }
            owl.transform.localScale *= 4.5f;
            Animation anim = owl.GetComponentInChildren<Animation>();
            if (anim != null)
            {
                foreach (AnimationState s in anim)
                {
                    s.wrapMode = WrapMode.Loop;
                    if (s.name.ToLowerInvariant().EndsWith("flying")) anim.Play(s.name);
                }
            }
            model = owl.transform;
            model.position = (frog != null ? frog.position : Vector3.zero) + new Vector3(0f, 14f, -20f);
        }

        // The regular bird backs off while the Great Owl hunts.
        BirdMovement.Stun(Duration + 2f);
        if (AudioDirector.Instance != null) AudioDirector.Instance.PlayMusic("VolatileReaction");
        FrogEffects.Say("The Great Owl! Survive " + Mathf.RoundToInt(Duration) + " seconds!");
    }

    void Update()
    {
        if (Active != this) return;
        GameState game = GameState.Current;
        if (game == null || !game.IsPlaying || frog == null || !frog.gameObject.activeInHierarchy) return;

        if (leaving)
        {
            if (model != null) model.position += new Vector3(0f, 6f, 14f) * Time.deltaTime;
            if (Time.time - leftAt > 3f) { if (model != null) Destroy(model.gameObject); Active = null; }
            return;
        }

        // Circle overhead, diving down towards each strike.
        angle += Time.deltaTime * 0.6f;
        Vector3 circle = frog.position + new Vector3(Mathf.Cos(angle) * 11f, 12f, Mathf.Sin(angle) * 11f);
        if (model != null)
        {
            Vector3 goal = circle;
            if (currentDive != null)
            {
                goal = Vector3.Lerp(circle, currentDive.transform.position + Vector3.up * 1.5f, Mathf.SmoothStep(0f, 1f, DiveProgress()));
            }
            Vector3 move = goal - model.position;
            model.position = Vector3.Lerp(model.position, goal, 4f * Time.deltaTime);
            move.y = 0f;
            if (move.sqrMagnitude > 0.01f) model.rotation = Quaternion.Slerp(model.rotation, Quaternion.LookRotation(move.normalized), 5f * Time.deltaTime);
        }

        if (Time.time >= endAt)
        {
            Survive();
            return;
        }

        if (Time.time >= nextDive && (currentDive == null || currentDive.Struck))
        {
            // Aim a little ahead of where the frog is going.
            Vector3 lead = frog.position;
            PlayerMovement move = frog.GetComponent<PlayerMovement>();
            if (move != null) lead += frog.forward * move.CurrentSpeed * 0.5f;
            lead.y = PowerUpSpawner.GroundHeight(lead);
            float progress = 1f - SecondsLeft / Duration; // 0 -> 1 over the fight
            float warn = Mathf.Lerp(1.4f, 0.95f, progress);
            currentDive = StrikeZone.Create(lead, 2.6f, warn, StrikeZone.Kind.Owl);
            Dives++;
            nextDive = Time.time + warn + Mathf.Lerp(1.6f, 0.7f, progress);
        }
    }

    private float DiveProgress()
    {
        if (currentDive == null) return 0f;
        return currentDive.Struck ? 1f : 0.5f;
    }

    // You made it: the owl gives up. (Also used by the play tests.)
    public void Survive()
    {
        if (leaving) return;
        leaving = true;
        leftAt = Time.time;
        BirdMovement.Stun(0.1f);
        FrogEffects.Say("You outlasted the Great Owl!");
        AudioDirector.Play("Win", 0.8f);
        if (LevelManager.Instance != null) LevelManager.Instance.FinishGame();
    }
}
