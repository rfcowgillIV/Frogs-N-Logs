using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BirdMovement : MonoBehaviour {

	[SerializeField]
	private Transform target;
	private UnityEngine.AI.NavMeshAgent birdAgent;
	private Animator birdAnimator;

	[SerializeField]
	private RandomSoundPlayer birdFootsteps;

	// Use this for initialization
	void Start () {
		birdAgent = GetComponent<UnityEngine.AI.NavMeshAgent> ();
		birdAnimator = GetComponent<Animator> ();
	}
	
	// Update is called once per frame
	void Update () {

		//Set the birds destiantion
		birdAgent.SetDestination(target.position);

		//Measure the magnitude of the NavMeshAgent's Velocity
		float speed = birdAgent.velocity.magnitude;

		//pass the velocity to the animator compnent 
		birdAnimator.SetFloat ("Speed", speed);


		if (speed > 0f) {
			birdFootsteps.enabled = true;

		} else {
			birdFootsteps.enabled = false;
		}
	}
}
