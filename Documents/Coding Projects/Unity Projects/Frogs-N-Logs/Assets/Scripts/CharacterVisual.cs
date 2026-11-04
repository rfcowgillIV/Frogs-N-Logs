using UnityEngine;
using UnityEngine.AI;

// CharacterVisual (added Nov 2026): puts the new cartoon models on the frog and the bird.
//
// The game logic (movement, physics, colliders, AI) stays exactly as it was on the original
// objects; this just hides the 2017 model and shows the new one in its place, sized to match,
// and plays its animations: "walk" / "idle" for the frog (by methodical pixel, CC0) and
// "Flying" / "Idle" for the eagle (Quaternius, CC0).
//
// If a model ever fails to load, the original 2017 look simply stays visible.
public class CharacterVisual : MonoBehaviour
{
    public string modelName;
    public string moveClip;
    public string idleClip;
    public float yaw;           // turn the model to face the way the object moves
    public float sizeFactor = 1f;
    public float hover;          // lift above the ground (the eagle flies low)

    public Transform Visual { get; private set; }
    private Animation anim;
    private string playing;
    private Rigidbody body;
    private NavMeshAgent agent;
    private PlayerMovement frogMovement;
    private Vector3 baseLocalPosition;
    private Vector3 lastPosition;
    private float smoothedSpeed;

    public static CharacterVisual Attach(GameObject host, string model, string move, string idle, float yaw, float size, float hover)
    {
        var v = host.GetComponent<CharacterVisual>();
        if (v == null) v = host.AddComponent<CharacterVisual>();
        v.modelName = model;
        v.moveClip = move;
        v.idleClip = idle;
        v.yaw = yaw;
        v.sizeFactor = size;
        v.hover = hover;
        v.Build();
        return v;
    }

    private static bool BoundsOf(Transform root, Transform exclude, out Bounds bounds)
    {
        bounds = new Bounds(root.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is ParticleSystemRenderer) continue;
            if (exclude != null && r.transform.IsChildOf(exclude)) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }

    private void Build()
    {
        if (Visual != null) return;
        GameObject prefab = Resources.Load<GameObject>("Characters/" + modelName);
        if (prefab == null)
        {
            Debug.LogWarning("[Characters] Model " + modelName + " not found; keeping the original look");
            return;
        }

        Bounds old;
        bool hadOld = BoundsOf(transform, null, out old);

        GameObject instance = Instantiate(prefab, transform);
        instance.name = modelName + " (new model)";
        Visual = instance.transform;
        Visual.localPosition = Vector3.zero;
        Visual.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Visual.localScale = Vector3.one;
        foreach (Collider c in instance.GetComponentsInChildren<Collider>()) Destroy(c);

        // Size the new model to match the old one (times sizeFactor).
        Bounds fresh;
        if (BoundsOf(Visual, null, out fresh) && hadOld)
        {
            float oldSize = Mathf.Max(old.size.x, old.size.z);
            float newSize = Mathf.Max(fresh.size.x, fresh.size.z);
            if (newSize > 0.0001f) Visual.localScale = Vector3.one * (oldSize / newSize * sizeFactor) / Mathf.Max(transform.lossyScale.x, 0.0001f);
            BoundsOf(Visual, null, out fresh);
            // Stand on the same ground the old model stood on.
            Vector3 shift = new Vector3(old.center.x - fresh.center.x, old.min.y - fresh.min.y + hover, old.center.z - fresh.center.z);
            Visual.position += shift;
        }
        baseLocalPosition = Visual.localPosition;

        // Hide the 2017 model (its game logic keeps running untouched).
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (!r.transform.IsChildOf(Visual) && !(r is ParticleSystemRenderer)) r.enabled = false;
        }
        Animator oldAnimator = GetComponent<Animator>();
        if (oldAnimator != null) oldAnimator.cullingMode = AnimatorCullingMode.CullCompletely;

        foreach (Renderer r in Visual.GetComponentsInChildren<Renderer>(true))
        {
            Stylizer.ToToon(r);
            if (r is SkinnedMeshRenderer) ((SkinnedMeshRenderer)r).updateWhenOffscreen = true;
        }

        anim = Visual.GetComponentInChildren<Animation>();
        if (anim != null)
        {
            foreach (AnimationState s in anim) s.wrapMode = WrapMode.Loop;
            anim.playAutomatically = false;
        }
        body = GetComponent<Rigidbody>();
        agent = GetComponent<NavMeshAgent>();
        frogMovement = GetComponent<PlayerMovement>();
        lastPosition = transform.position;
        Play(idleClip, 1f);
        Debug.Log("[Characters] " + modelName + " model in place (scale " + Visual.localScale.x.ToString("0.000") + ")");
    }

    private string FindClip(string key)
    {
        if (anim == null || string.IsNullOrEmpty(key)) return null;
        string lower = key.ToLowerInvariant();
        foreach (AnimationState s in anim)
        {
            string n = s.name.ToLowerInvariant();
            if (n == lower || n.EndsWith("|" + lower)) return s.name;
        }
        return null;
    }

    private void Play(string key, float speed)
    {
        string clip = FindClip(key);
        if (clip == null) return;
        if (playing != clip)
        {
            anim.CrossFade(clip, 0.15f);
            playing = clip;
        }
        anim[clip].speed = speed;
    }

    void LateUpdate()
    {
        if (Visual == null) return;

        float speed;
        if (frogMovement != null && frogMovement.enabled) speed = frogMovement.CurrentSpeed;
        else if (agent != null && agent.enabled && agent.isOnNavMesh) speed = agent.velocity.magnitude;
        else
        {
            speed = Time.deltaTime > 0f ? Vector3.Distance(lastPosition, transform.position) / Time.deltaTime : 0f;
        }
        lastPosition = transform.position;
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-12f * Time.deltaTime));

        bool moving = smoothedSpeed > 0.3f;
        if (moving) Play(moveClip, Mathf.Clamp(smoothedSpeed / 3.5f, 0.8f, 2f));
        else Play(idleClip, 1f);

        // The eagle bobs gently while it flies.
        if (hover > 0f)
        {
            float bob = moving ? Mathf.Sin(Time.time * 4f) * 0.12f : -hover * 0.8f;
            Visual.localPosition = Vector3.Lerp(Visual.localPosition, baseLocalPosition + Vector3.up * bob / Mathf.Max(transform.lossyScale.y, 0.0001f), 0.2f);
        }
    }
}
