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
		if (other.CompareTag ("Enemy")) {
			Catch (other);
		}
	}

	// Nov 2026: also used by attacks that aren't touching (the pike, the Great Owl's dive).
	// attacker may be null.
	public void Catch (Collider attacker) {
		if (!alive) {
			return;
		}
		// A lily-pad shield, a dodge hop (or the moment right after either) saves the frog.
		if (FrogEffects.TryAbsorbHit (attacker)) {
			return;
		}
		alive = false;

		//create the pickup particles
		GameObject particles = Instantiate(pickupPrefab, transform.position, Quaternion.identity);
		AudioManager.Register (particles);
	}
}
