using System.Collections.Generic;
using UnityEngine;

// HazardSpawner (added Nov 2026): brings in each level's dangers.
//   The Swamp: a snake wakes up and two logs at a time drift across the pond
//   Night:     the snake stays, three logs at a time, and the bird gets faster (LevelManager)
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
            return level >= 3 ? 3 : level == 2 ? 2 : 0;
        }
    }

    void Update()
    {
        GameState game = GameState.Current;
        if (game == null || !game.IsPlaying) return;
        int level = LevelManager.Instance != null ? LevelManager.Instance.Level : 1;

        if (level >= 2 && snake == null)
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

    private static bool FarSpot(float minDistance, out Vector3 spot)
    {
        GameObject frog = GameObject.FindWithTag("Player");
        for (int i = 0; i < 25; i++)
        {
            Vector3 p = new Vector3(Random.Range(-24f, 24f), 0f, Random.Range(-24f, 24f));
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
