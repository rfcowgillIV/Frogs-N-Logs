using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

// PlayArea (added Nov 2026): the pond grows as you go.
//
//   The Pond   60 x 60 m   (the original 2017 walls)
//   The Swamp  78 x 78 m   twisted trees and tall reeds around the new edge
//   Night      94 x 94 m   dead trees, ferns and glowing mushrooms in the outer ring
//
// The level's invisible walls slide outward when a new level starts, and everything that
// picks a random spot (flies, power-ups, snakes, logs) uses the current size. The bird's
// walking map (NavMesh) is built once when the level loads, covering the whole final area,
// so the bird can follow you anywhere straight away.
public class PlayArea : MonoBehaviour
{
    public static readonly float[] StageHalfSize = { 30f, 39f, 47f };

    // Half the width of the area you can hop around in right now (walls are at +-HalfSize).
    public static float HalfSize { get; private set; } = 30f;
    public static int Stage { get; private set; } = 1;
    public static event Action Changed;

    // A random-spawn range that stays clear of the walls.
    public static float Inner(float margin) { return Mathf.Max(5f, HalfSize - margin); }

    public static Vector3 RandomPoint(float margin)
    {
        float r = Inner(margin);
        return new Vector3(UnityEngine.Random.Range(-r, r), 0f, UnityEngine.Random.Range(-r, r));
    }

    public static bool BuildingDone { get; private set; }

    private BoxCollider[] walls = new BoxCollider[0];
    private Vector3[] wallDirections = new Vector3[0];
    private float from = 30f, to = 30f, blend = 1f;

    void Awake()
    {
        HalfSize = StageHalfSize[0];
        Stage = 1;
        BuildingDone = false;
        LevelManager.LevelChanged += OnLevelChanged;
    }

    void OnDestroy()
    {
        LevelManager.LevelChanged -= OnLevelChanged;
    }

    IEnumerator Start()
    {
        FindWalls();
        // Let the scenery plant its trees first, so the bird walks around them.
        yield return null;
        yield return null;
        BuildNavMesh();
    }

    private void FindWalls()
    {
        var list = new System.Collections.Generic.List<BoxCollider>();
        var dirs = new System.Collections.Generic.List<Vector3>();
        foreach (BoxCollider box in FindObjectsByType<BoxCollider>(FindObjectsSortMode.None))
        {
            if (!box.gameObject.name.StartsWith("Wall")) continue;
            Vector3 p = box.transform.position;
            // Which way this wall faces out from the middle (+x, -x, +z or -z).
            Vector3 dir = Mathf.Abs(p.x) > Mathf.Abs(p.z) ? new Vector3(Mathf.Sign(p.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(p.z));
            list.Add(box);
            dirs.Add(dir);
            // Don't let walls shape the bird's map (they move).
            var modifier = box.gameObject.GetComponent<NavMeshModifier>();
            if (modifier == null) modifier = box.gameObject.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
        }
        walls = list.ToArray();
        wallDirections = dirs.ToArray();
        ApplyWalls(HalfSize);
    }

    private void BuildNavMesh()
    {
        try
        {
            var surfaceGo = new GameObject("Bird Walking Map (NavMesh)");
            surfaceGo.transform.SetParent(transform, false);
            var surface = surfaceGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(0f, 5f, 0f);
            surface.size = new Vector3(StageHalfSize[StageHalfSize.Length - 1] * 2f + 4f, 30f, StageHalfSize[StageHalfSize.Length - 1] * 2f + 4f);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.agentTypeID = 0;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.25f;
            surface.layerMask = ~0;

            // Things that move (frog, bird, flies, power-ups, hazards) aren't part of the ground.
            IgnoreMoving<PlayerHealth>();
            IgnoreMoving<BirdMovement>();
            IgnoreMoving<FlyPickUp>();
            IgnoreMoving<PowerUpPickup>();
            IgnoreMoving<SnakeHazard>();
            IgnoreMoving<DriftingLog>();
            IgnoreMoving<HelperBuddy>();
            IgnoreMoving<HidingSpot>();

            // Swap the 2017 baked map for the new, bigger one.
            NavMesh.RemoveAllNavMeshData();
            surface.BuildNavMesh();

            // Put anyone walking back on the new map.
            foreach (NavMeshAgent agent in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(agent.transform.position, out hit, 5f, NavMesh.AllAreas)) agent.Warp(hit.position);
            }
            Debug.Log("[PlayArea] Walking map built for " + surface.size.x + " m");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlayArea] Couldn't build the bigger walking map: " + e.Message);
        }
        BuildingDone = true;
    }

    private static void IgnoreMoving<T>() where T : Component
    {
        foreach (T c in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var m = c.GetComponent<NavMeshModifier>();
            if (m == null) m = c.gameObject.AddComponent<NavMeshModifier>();
            m.ignoreFromBuild = true;
            m.applyToChildren = true;
        }
    }

    private void OnLevelChanged(int level, string name)
    {
        int stage = Mathf.Clamp(level, 1, StageHalfSize.Length);
        if (stage <= Stage) return;
        Stage = stage;
        from = HalfSize;
        to = StageHalfSize[stage - 1];
        blend = 0f;
        FrogEffects.Say("The pond grows! More room to hop");
    }

    void Update()
    {
        if (blend >= 1f) return;
        blend = Mathf.Min(1f, blend + Time.deltaTime / 2.5f);
        HalfSize = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, blend));
        ApplyWalls(HalfSize);
        if (blend >= 1f && Changed != null) Changed();
    }

    // For the play tests: jump straight to a stage.
    public void SetStage(int stage)
    {
        Stage = Mathf.Clamp(stage, 1, StageHalfSize.Length);
        HalfSize = to = from = StageHalfSize[Stage - 1];
        blend = 1f;
        ApplyWalls(HalfSize);
        if (Changed != null) Changed();
    }

    private void ApplyWalls(float half)
    {
        for (int i = 0; i < walls.Length; i++)
        {
            BoxCollider wall = walls[i];
            if (wall == null) continue;
            Vector3 dir = wallDirections[i];
            Vector3 p = wall.transform.position;
            if (dir.x != 0f) p.x = dir.x * half; else p.z = dir.z * half;
            wall.transform.position = p; // the walls are 100 m long, so the corners stay closed
        }
    }
}
