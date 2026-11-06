using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// FlySpawner: keeps the pond stocked with flies.
//
// Nov 2026:
//  - How many flies there are depends on the difficulty (Easy has more, Hard has fewer).
//  - Flies are now REUSED instead of being destroyed and re-created every time one is
//    eaten ("object pooling"). Creating objects mid-game causes tiny stutters, especially
//    in a web browser, so an eaten fly just hides and pops back up somewhere new.
//    Reduce, reuse, re-fly-cle.
public class FlySpawner : MonoBehaviour {

	[SerializeField]
	private GameObject flyPrefab;

	[SerializeField]
	private int totalyFlyMinimum = 12;

	private float spawnArea = 25f;

	// Number of flies currently out and about.
	public static int totalFlies;

	// Flies that are out right now (used by the shop's fly magnet).
	public static readonly List<GameObject> ActiveFlies = new List<GameObject> ();

	private static FlySpawner instance;
	private readonly Stack<GameObject> sleepingFlies = new Stack<GameObject> ();

	// The fly model, shared with the golden-fly power-up.
	public GameObject FlyPrefab {
		get { return flyPrefab; }
	}

	public int FlyMinimum {
		get {
			int n = DifficultyRules.FlyMinimum (GameSettings.Difficulty, totalyFlyMinimum);
			// A bigger pond gets a few more flies; the Fly frenzy cheat doubles them.
			n += Mathf.RoundToInt ((PlayArea.HalfSize - 30f) / 3f);
			n = Mathf.RoundToInt (n * Weather.FlyFactor); // rainstorms bring out more flies
			return Cheats.FlyFrenzy ? n * 2 : n;
		}
	}

	void Awake () {
		instance = this;
		ActiveFlies.Clear ();
	}

	void OnDestroy () {
		if (instance == this) {
			instance = null;
		}
	}

	// Use this for initialization
	void Start () {
		totalFlies = 0;
	}
	
	// Update is called once per frame
	void Update () {

		//while the total number of flies is less than the minimum...
		int minimum = FlyMinimum;
		while (totalFlies < minimum) {

			//... then increment the total number of flies...
			totalFlies++;

			//... and put a fly somewhere random.
			Spawn (RandomPosition ());
		}
	}

	public Vector3 RandomPosition () {
		// Nov 2026: the pond grows each level (PlayArea), and the flies spread out with it.
		float area = Mathf.Max (spawnArea, PlayArea.Inner (5f));
		float positionX = Random.Range(-area, area);
		float positionZ = Random.Range(-area, area);
		return new Vector3 (positionX, 2f, positionZ);
	}

	private void Spawn (Vector3 position) {
		GameObject fly;
		if (sleepingFlies.Count > 0) {
			// Wake up a fly that was eaten earlier.
			fly = sleepingFlies.Pop ();
			fly.transform.SetPositionAndRotation (position, Quaternion.identity);
			fly.SetActive (true);
		} else {
			// First time: create a new fly.
			fly = Instantiate (flyPrefab, position, Quaternion.identity);
			AudioManager.Register (fly);
		}
		ActiveFlies.Add (fly);
		// Nov 2026: dragonflies, fireflies and bees in the later levels.
		FlyKind.Assign (fly);
	}

	// Called by FlyPickUp when a fly is eaten.
	public static void Recycle (GameObject fly) {
		totalFlies--;
		ActiveFlies.Remove (fly);
		if (instance != null) {
			fly.SetActive (false);
			instance.sleepingFlies.Push (fly);
		} else {
			Destroy (fly);
		}
	}
}
