using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PickupParticlesDestruction : MonoBehaviour {

	// Use this for initialization
	void Start () {
		//Destroy the "pickup particles" after 5 seconds
		Destroy(gameObject, 5f);
	}
}