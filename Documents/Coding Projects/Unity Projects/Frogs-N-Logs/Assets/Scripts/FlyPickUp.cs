using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlyPickUp : MonoBehaviour {

	[SerializeField]
	private GameObject pickupPrefab;


	// BUG FIX (2026): Destroy() doesn't happen until the end of the frame, so if
	// the frog touched a fly with two colliders at once, the fly was counted
	// twice (double score, and the spawner lost track of how many flies exist).
	// This flag makes sure each fly is only eaten once. One fly, one bite.
	private bool eaten = false;

	// Nov 2026: flies are reused (see FlySpawner), so a "new" fly must forget it was eaten.
	void OnEnable () {
		eaten = false;
	}

	void OnTriggerEnter(Collider other) {
		//If the colider other is tagged with "player"...
		if (other.CompareTag ("Player")) {
			Eat ();
		}
	}

	// Nov 2026: also used by the tongue flick (FrogTongue.cs). Returns true if it was eaten.
	public bool Eat () {

		if (eaten || !gameObject.activeInHierarchy) {
			return false;
		}

		// Nov 2026: no free points before the round starts (a fly can spawn right on the frog).
		if (GameState.Current != null && !GameState.Current.IsPlaying) {
			return false;
		}

		eaten = true;

		//... add the pickup particles....
		GameObject particles = Instantiate(pickupPrefab, transform.position, Quaternion.identity);
		AudioManager.Register (particles);

		//...update the score (2 per fly with the shop's "Double flies"; dragonflies are worth 3,
		// bees sting - see FlyKind.cs)
		FlyKind kind = GetComponentInParent<FlyKind> ();
		if (kind != null && kind.Kind == FlyKind.Type.Bee) {
			kind.Sting ();
		} else {
			int points = kind != null ? kind.Points : 1;
			ScoreCounter.score += points * Shop.PointsMultiplier;
			AudioDirector.Play ("FlyEat", 0.6f);
		}

		//...and send the fly back to the spawner to reuse later
		// (this also lowers the count of flies that are out).
		FlySpawner.Recycle (kind != null ? kind.gameObject : gameObject);
		return true;
	}
}
