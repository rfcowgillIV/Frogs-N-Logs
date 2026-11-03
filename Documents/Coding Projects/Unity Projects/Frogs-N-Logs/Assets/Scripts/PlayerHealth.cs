using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth : MonoBehaviour {

	public bool alive;
	[SerializeField]
	private GameObject pickupPrefab;


	// Use this for initialization
	void Start () {
		alive = true;
	}

	void OnTriggerEnter(Collider other) {
		if (other.CompareTag ("Enemy") && alive == true) {
			// Nov 2026: a lily-pad shield (or the moment right after it pops) saves the frog.
			if (FrogEffects.TryAbsorbHit (other)) {
				return;
			}
			alive = false;

			//create the pickup particles
			GameObject particles = Instantiate(pickupPrefab, transform.position, Quaternion.identity);
			AudioManager.Register (particles);

		}
	}
}
