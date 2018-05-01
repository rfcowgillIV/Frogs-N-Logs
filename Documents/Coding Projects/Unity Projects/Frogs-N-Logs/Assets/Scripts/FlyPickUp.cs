using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlyPickUp : MonoBehaviour {

	[SerializeField]
	private GameObject pickupPrefab;


	void OnTriggerEnter(Collider other) {


		//If the colider other is tagged with "player"...
		if (other.CompareTag ("Player")) {

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
