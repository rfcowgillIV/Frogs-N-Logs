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

		if (eaten) {
			return;
		}

		//If the colider other is tagged with "player"...
		if (other.CompareTag ("Player")) {
			eaten = true;

			//... add the pickup particles....
			GameObject particles = Instantiate(pickupPrefab, transform.position, Quaternion.identity);
			AudioManager.Register (particles);

			//...update the score
			ScoreCounter.score++;

			//...and send the fly back to the spawner to reuse later
			// (this also lowers the count of flies that are out).
			FlySpawner.Recycle (gameObject);
		}
	}
}
