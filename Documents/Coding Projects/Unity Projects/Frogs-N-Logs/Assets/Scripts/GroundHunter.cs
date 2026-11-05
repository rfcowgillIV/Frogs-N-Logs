using UnityEngine;
using UnityEngine.AI;

// GroundHunter (added Nov 2026): Autumn Woods' fox. While the bird hunts from above, the fox
// trots after you on the ground - so there are two predators to watch. It's a bit slower
// than the bird. Your helper buddy, the laser and the lily-pad shield all work on it, and it
// loses track of you when you hide in the reeds.
// Model: Quaternius "Animal Pack Vol. 2" wolf, recoloured fox-orange (CC0).
public class GroundHunter : MonoBehaviour
{
    public static GroundHunter Active { get; private set; }

    private NavMeshAgent agent;
    private Transform frog;
    private float stunnedUntil;
    private Animation anim;
    private string runClip, idleClip, playing;
    private Vector3 lastSeen;
    private float wanderPick;

    public bool Stunned { get { return Time.time < stunnedUntil; } }

    public static GroundHunter Create(Vector3 position)
    {
        var go = new GameObject("Fox");
        go.tag = "Enemy";
        go.transform.position = position;
        var hunter = go.AddComponent<GroundHunter>();
        hunter.agent = go.AddComponent<NavMeshAgent>();
        hunter.agent.radius = 0.4f;
        hunter.agent.height = 0.8f;
        hunter.agent.acceleration = 20f;
        hunter.agent.angularSpeed = 540f;
        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        var trigger = go.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.55f;
        trigger.center = new Vector3(0f, 0.4f, 0f);

        GameObject prefab = Resources.Load<GameObject>("Characters/Wolf");
        if (prefab != null)
        {
            GameObject model = Instantiate(prefab, go.transform);
            model.name = "Fox (model)";
            foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                Stylizer.ToToon(r);
                block.SetColor("_Recolor", new Color(1f, 0.42f, 0.08f, 0.9f));
                r.SetPropertyBlock(block);
            }
            HelperBuddy.FitModel(model, go.transform, 1.25f);
            hunter.anim = model.GetComponentInChildren<Animation>();
            if (hunter.anim != null)
            {
                foreach (AnimationState s in hunter.anim)
                {
                    s.wrapMode = WrapMode.Loop;
                    string n = s.name.ToLowerInvariant();
                    if (n.EndsWith("gallop") || n.EndsWith("run")) hunter.runClip = s.name;
                    else if (hunter.runClip == null && (n.EndsWith("walk") || n.EndsWith("walking"))) hunter.runClip = s.name;
                    if (n.EndsWith("idle")) hunter.idleClip = s.name;
                }
            }
        }
        Active = hunter;
        FrogEffects.Say("A fox is on the prowl!");
        return hunter;
    }

    void OnDestroy()
    {
        if (Active == this) Active = null;
    }

    public void Stun(float seconds)
    {
        stunnedUntil = Time.time + seconds;
    }

    void Update()
    {
        if (frog == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p == null) return;
            frog = p.transform;
        }
        if (agent == null || !agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (agent != null && NavMesh.SamplePosition(transform.position, out hit, 6f, NavMesh.AllAreas)) agent.Warp(hit.position);
            return;
        }
        GameState game = GameState.Current;
        bool playing = game != null && game.IsPlaying && frog.gameObject.activeInHierarchy;
        float difficulty = DifficultyRules.BirdSpeedMultiplier(GameSettings.Difficulty);
        agent.speed = 3.6f * difficulty * (Cheats.SlowBird ? 0.5f : 1f);
        agent.isStopped = !playing || Stunned;

        if (!HidingSpot.FrogHidden) lastSeen = frog.position;
        if (HidingSpot.FrogHidden)
        {
            // Sniff around where it last saw you.
            if (Time.time > wanderPick)
            {
                wanderPick = Time.time + 1.5f;
                agent.SetDestination(lastSeen + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f)));
            }
        }
        else
        {
            agent.SetDestination(frog.position);
        }

        if (anim != null)
        {
            string clip = agent.velocity.sqrMagnitude > 0.3f ? runClip : idleClip;
            if (clip != null && clip != this.playing) { anim.CrossFade(clip, 0.15f); this.playing = clip; }
        }
    }
}
