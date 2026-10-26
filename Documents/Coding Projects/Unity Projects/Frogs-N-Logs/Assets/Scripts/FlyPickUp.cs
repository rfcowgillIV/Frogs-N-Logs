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

	void OnTriggerEnter(Collider other) {

		if (eaten) {
			return;
		}

		//If the colider other is tagged with "player"...
		if (other.CompareTag ("Player")) {
			eaten = true;

			//... add the pickup particles....
			Instantiate(pickupPrefab, transform.position, Quaternion.identity);

			//...decrement the total number of flies...
			FlySpawner.totalFlies--;

			//...update the score
			ScoreCounter.score++;

			Destroy (gameObject);
		}
	}
}
