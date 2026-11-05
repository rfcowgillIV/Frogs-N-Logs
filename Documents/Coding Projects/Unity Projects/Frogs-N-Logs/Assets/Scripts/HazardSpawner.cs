using System.Collections.Generic;
using UnityEngine;

// HazardSpawner (added Nov 2026): brings in each level's dangers.
//   The Swamp:          a snake wakes up and two logs at a time drift across the pond
//   Night:              the snake stays, three logs at a time, and the bird gets faster
//   Lily Pad Lake:      no snake (too wet), three logs, and a pike lunging from the water
//   Autumn Woods:       the snake is back, two logs, and a fox hunting on the ground
//   Winter Freeze:      snakes hibernate; one log, thin ice (Biome.cs) and the Great Owl
public class HazardSpawner : MonoBehaviour
{
    private SnakeHazard snake;
    private readonly List<DriftingLog> logs = new List<DriftingLog>();
    private Material bark;
    private float nextLog;

    void Start()
    {
        // Borrow the bark texture from one of the pond's decorative logs.
        foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (r.gameObject.name.StartsWith("Wooden Log") && r.sharedMaterial != null)
            {
                bark = r.sharedMaterial;
                break;
            }
        }
    }

    private int LogsWanted
    {
        get
        {
            int level = LevelManager.Instance != null ? LevelManager.Instance.Level : 1;
            switch (level)
            {
                case 2: return 2;
                case 3: return 3;
                case 4: return 3;
                case 5: return 2;
                case 6: return 1;
                default: return 0;
            }
        }
    }

    void Update()
    {
        GameState game = GameState.Current;
        if (game == null || !game.IsPlaying) return;
        int level = LevelManager.Instance != null ? LevelManager.Instance.Level : 1;

        bool snakeLevel = level == 2 || level == 3 || level == 5;
        if (!snakeLevel && snake != null)
        {
            Destroy(snake.gameObject); // too wet or too cold for snakes
            snake = null;
        }

        // Lily Pad Lake: a pike lunges out of the water near the frog every few seconds.
        if (level == 4 && Time.time >= nextPike && StrikeZone.Active == 0)
        {
            nextPike = Time.time + Random.Range(4.5f, 8f);
            Vector3 spot;
            if (WaterSpotNearFrog(out spot)) StrikeZone.Create(spot, 1.8f, 1.3f, StrikeZone.Kind.Pike);
        }

        // Autumn Woods: a fox.
        if (level == 5 && GroundHunter.Active == null)
        {
            Vector3 spot;
            if (FarSpot(18f, out spot)) GroundHunter.Create(spot);
        }
        else if (level != 5 && GroundHunter.Active != null)
        {
            Destroy(GroundHunter.Active.gameObject);
        }

        if (snakeLevel && snake == null)
        {
            Vector3 spot;
            if (FarSpot(15f, out spot))
            {
                snake = SnakeHazard.Create(spot);
                if (snake != null) FrogEffects.Say("A snake! Watch out!");
            }
        }

        logs.RemoveAll(l => l == null);
        if (logs.Count < LogsWanted && Time.time >= nextLog)
        {
            logs.Add(DriftingLog.Create(bark));
            nextLog = Time.time + Random.Range(3f, 7f);
        }
    }

    private float nextPike;

    // Somewhere in the water within a few metres of the frog (the pike's lunge).
    private static bool WaterSpotNearFrog(out Vector3 spot)
    {
        GameObject frog = GameObject.FindWithTag("Player");
        spot = Vector3.zero;
        if (frog == null) return false;
        for (int i = 0; i < 30; i++)
        {
            Vector2 offset = Random.insideUnitCircle * 5f;
            Vector3 p = frog.transform.position + new Vector3(offset.x, 0f, offset.y);
            float ground = PowerUpSpawner.GroundHeight(p);
            if (ground < Biome.WaterLevel - 0.1f)
            {
                spot = new Vector3(p.x, Biome.WaterLevel, p.z);
                return true;
            }
        }
        return false;
    }

    private static bool FarSpot(float minDistance, out Vector3 spot)
    {
        GameObject frog = GameObject.FindWithTag("Player");
        for (int i = 0; i < 25; i++)
        {
            Vector3 p = PlayArea.RandomPoint(6f);
            p.y = PowerUpSpawner.GroundHeight(p);
            if (frog == null || Vector3.Distance(frog.transform.position, p) >= minDistance)
            {
                spot = p;
                return true;
            }
        }
        spot = Vector3.zero;
        return false;
    }
}
