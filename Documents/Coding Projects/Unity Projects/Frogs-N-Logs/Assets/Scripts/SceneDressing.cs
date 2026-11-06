using System.Collections.Generic;
using UnityEngine;

// SceneDressing (added Nov 2026): plants the new cartoon scenery around the pond.
//
// Models: Quaternius "Stylized Nature MegaKit" (CC0) - trees, pines, bushes, flowers,
// clover, mushrooms and rocks, with their textures resized to 512 px for the web.
//
//  - A ring of trees and pines just outside the pond's walls
//  - Bushes and flower clumps along the walls
//  - Small flowers, clover and mushrooms dotted around the meadow (no collisions, so they
//    never get in the frog's way)
//  - The level's old grey rocks get swapped for the new cartoon rocks (same size and spot)
//
// The layout uses a fixed random seed, so the pond looks the same every visit. Low graphics
// shows about half the small plants.
public class SceneDressing : MonoBehaviour
{
    private static SceneDressing instance;
    private readonly List<GameObject> optional = new List<GameObject>();

    // Which texture each imported material uses (from the kit's files).
    private static readonly Dictionary<string, string> TextureFor = new Dictionary<string, string>
    {
        { "Bark_NormalTree", "Bark_NormalTree" },
        { "Bark_TwistedTree", "Bark_TwistedTree" },
        { "Bark_DeadTree", "Bark_DeadTree" },
        { "Leaves_NormalTree", "Leaves_NormalTree_C" },
        { "Leaves_TwistedTree", "Leaves_TwistedTree_C" },
        { "Leaves_Pine", "Leaf_Pine_C" },
        { "Leaves", "Leaves" },
        { "Flowers", "Flowers" },
        { "Mushrooms", "Mushrooms" },
        { "Rocks", "Rocks_Diffuse" },
        { "PathRocks", "PathRocks_Diffuse" },
        { "Grass", "Grass" },
    };

    private static readonly string[] Trees = { "CommonTree_1", "CommonTree_2", "CommonTree_3", "Pine_1", "Pine_2" };
    private static readonly string[] EdgePlants = { "Bush_Common", "Bush_Common_Flowers", "Flower_3_Group", "Flower_4_Group", "Plant_1", "Rock_Medium_1", "Rock_Medium_2" };
    private static readonly string[] Meadow = { "Clover_1", "Flower_3_Group", "Flower_4_Group", "Mushroom_Common", "Plant_7", "Pebble_Round_1", "Grass_Common_Short" };
    private static readonly string[] Rocks = { "Rock_Medium_1", "Rock_Medium_2", "Rock_Medium_3" };
    // Nov 2026: the pond grows each level (PlayArea.cs), and each new ring has its own look.
    private static readonly string[] SwampTrees = { "TwistedTree_1", "TwistedTree_2", "TwistedTree_3" };
    private static readonly string[] SwampPlants = { "Grass_Common_Tall", "Grass_Common_Tall", "Fern_1", "Mushroom_Common", "Bush_Common" };
    private static readonly string[] NightTrees = { "DeadTree_1", "DeadTree_3" };
    private static readonly string[] NightPlants = { "Fern_1", "Mushroom_Laetiporus", "Mushroom_Laetiporus", "Rock_Medium_3", "Grass_Common_Tall" };

    private readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
    private System.Random random;

    void Start()
    {
        instance = this;
        random = new System.Random(20171128); // the day the original game's Unity license was activated
        var root = new GameObject("Scenery (Stylized Nature MegaKit)").transform;
        root.SetParent(transform, false);

        // A forest edge all the way round, just past the biggest (Night) walls at +-47.
        float outer = PlayArea.StageHalfSize[PlayArea.StageHalfSize.Length - 1];
        for (int i = 0; i < 76; i++)
        {
            Place(root, Pick(Trees), InBand(outer + 0.8f, outer + 2.8f), Range(0.85f, 1.3f), i % 3 == 2);
        }
        // The Swamp ring (opens at level 2): twisted trees you hop around, reeds and ferns.
        float pond = PlayArea.StageHalfSize[0], swamp = PlayArea.StageHalfSize[1];
        for (int i = 0; i < 16; i++) Solid(Place(root, Pick(SwampTrees), InBand(pond + 2f, swamp - 2f), Range(0.8f, 1.1f), false));
        for (int i = 0; i < 44; i++) Place(root, Pick(SwampPlants), InBand(pond + 0.5f, swamp - 0.5f), Range(0.6f, 1f), i % 2 == 1);
        // The Night ring (opens at level 3): bare trees, ferns and big mushrooms.
        for (int i = 0; i < 14; i++) Solid(Place(root, Pick(NightTrees), InBand(swamp + 2f, outer - 2f), Range(0.8f, 1.15f), false));
        for (int i = 0; i < 40; i++) Place(root, Pick(NightPlants), InBand(swamp + 0.5f, outer - 0.5f), Range(0.6f, 1f), i % 2 == 1);

        // Nov 2026: clumps of tall reeds to hide in (HidingSpot.cs).
        for (int i = 0; i < 5; i++) HidingSpot.Create(Grounded(InBand(9f, pond - 4f)), root);
        for (int i = 0; i < 3; i++) HidingSpot.Create(Grounded(InBand(pond + 2f, swamp - 3f)), root);
        for (int i = 0; i < 3; i++) HidingSpot.Create(Grounded(InBand(swamp + 2f, outer - 3f)), root);
        // Bushes, flowers and rocks along the inside of the walls.
        for (int i = 0; i < 36; i++)
        {
            float side = Range(26.5f, 29f) * (random.Next(2) == 0 ? -1f : 1f);
            float along = Range(-28f, 28f);
            Vector3 p = random.Next(2) == 0 ? new Vector3(side, 0f, along) : new Vector3(along, 0f, side);
            Place(root, Pick(EdgePlants), p, Range(0.7f, 1.1f), i % 2 == 1);
        }
        // Little things in the meadow.
        for (int i = 0; i < 60; i++)
        {
            Vector3 p = new Vector3(Range(-25f, 25f), 0f, Range(-25f, 25f));
            if (p.magnitude < 3f) continue; // keep the frog's starting spot clear
            Place(root, Pick(Meadow), p, Range(0.35f, 0.6f), i % 2 == 1);
        }

        ReplaceOldRocks();
        ApplyTier(GraphicsManager.ActiveTier);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public static void ApplyTier(GraphicsTier tier)
    {
        if (instance == null) return;
        foreach (GameObject go in instance.optional)
        {
            if (go != null) go.SetActive(tier != GraphicsTier.Low);
        }
    }

    private float Range(float a, float b) { return a + (float)random.NextDouble() * (b - a); }

    private static Vector3 Grounded(Vector3 p)
    {
        p.y = PowerUpSpawner.GroundHeight(p);
        return p;
    }

    // A random spot in the square ring between "inner" and "outer" metres from the middle.
    private Vector3 InBand(float inner, float outer)
    {
        float depth = Range(inner, outer) * (random.Next(2) == 0 ? -1f : 1f);
        float along = Range(-outer, outer);
        return random.Next(2) == 0 ? new Vector3(depth, 0f, along) : new Vector3(along, 0f, depth);
    }

    // Give a tree a trunk you can't hop through (the bird's map walks around it too).
    private static void Solid(GameObject tree)
    {
        if (tree == null) return;
        var trunk = tree.AddComponent<CapsuleCollider>();
        float s = Mathf.Max(tree.transform.lossyScale.x, 0.01f);
        trunk.radius = 0.55f / s;
        trunk.height = 4f / s;
        trunk.center = new Vector3(0f, 2f / s, 0f);
    }
    private string Pick(string[] list) { return list[random.Next(list.Length)]; }

    private GameObject Model(string name)
    {
        GameObject m;
        if (!models.TryGetValue(name, out m))
        {
            m = Resources.Load<GameObject>("Nature/" + name);
            models[name] = m;
            if (m == null) Debug.LogWarning("[Scenery] Missing model " + name);
        }
        return m;
    }

    private GameObject Place(Transform root, string name, Vector3 position, float scale, bool isOptional)
    {
        GameObject prefab = Model(name);
        if (prefab == null) return null;
        position.y = PowerUpSpawner.GroundHeight(position) - 0.05f;
        GameObject go = Instantiate(prefab, position, Quaternion.Euler(0f, Range(0f, 360f), 0f), root);
        go.transform.localScale *= scale;
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) Dress(r);
        if (isOptional) optional.Add(go);
        return go;
    }

    // Cartoon materials with the kit's textures (leaves get cut-out edges and two sides).
    // Nov 2026: the leaf and grass materials, for Biome.cs (autumn colours, snow).
    public static readonly HashSet<Material> Leaves = new HashSet<Material>();
    public static readonly HashSet<Material> Plants = new HashSet<Material>();

    // For plants placed by other scripts (hiding spots): same cartoon look as the scenery.
    public static void DressPlant(Renderer r) { Dress(r); }

    private static void Dress(Renderer r)
    {
        Material[] mats = r.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
        {
            string matName = mats[i] != null ? mats[i].name.Replace(" (Instance)", "") : "";
            string texName = null;
            foreach (KeyValuePair<string, string> pair in TextureFor)
            {
                if (matName.StartsWith(pair.Key)) { texName = pair.Value; break; }
            }
            Texture2D tex = texName != null ? Resources.Load<Texture2D>("Nature/Textures/" + texName) : null;
            string lower = matName.ToLowerInvariant();
            bool leafy = lower.Contains("lea") || lower.Contains("grass") || lower.Contains("flower");
            if (tex == null)
            {
                mats[i] = Stylizer.ToonFrom(mats[i]);
                continue;
            }
            mats[i] = Stylizer.Toon(tex, Color.white, leafy ? 0.45f : 0f, leafy, leafy ? 0.05f : 0.15f);
            // Remember leaves and grass so later levels can turn them autumn-orange or snowy.
            if (lower.Contains("lea")) Leaves.Add(mats[i]);
            else if (leafy) Plants.Add(mats[i]);
        }
        r.sharedMaterials = mats;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    // Swap the level's 2017 rocks for the new cartoon rocks (keeping their collisions).
    private void ReplaceOldRocks()
    {
        int swapped = 0;
        foreach (MeshRenderer old in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!old.gameObject.name.StartsWith("Rock") || old.transform.IsChildOf(transform)) continue;
            GameObject prefab = Model(Rocks[swapped % Rocks.Length]);
            if (prefab == null) continue;
            Bounds b = old.bounds;
            GameObject rock = Instantiate(prefab, b.center, Quaternion.Euler(0f, Range(0f, 360f), 0f), old.transform);
            foreach (Collider c in rock.GetComponentsInChildren<Collider>()) Destroy(c);
            Bounds nb = new Bounds(rock.transform.position, Vector3.zero);
            bool first = true;
            foreach (Renderer r in rock.GetComponentsInChildren<Renderer>())
            {
                Dress(r);
                if (first) { nb = r.bounds; first = false; } else nb.Encapsulate(r.bounds);
            }
            float s = Mathf.Max(b.size.x, b.size.z) / Mathf.Max(Mathf.Max(nb.size.x, nb.size.z), 0.01f);
            rock.transform.localScale *= s;
            // Sit the new rock's bottom where the old rock's bottom was.
            rock.transform.position += new Vector3(0f, b.min.y - b.center.y - (nb.min.y - b.center.y) * s, 0f);
            old.enabled = false;
            swapped++;
        }
        if (swapped > 0) Debug.Log("[Scenery] Swapped " + swapped + " rocks for cartoon rocks");
    }
}
