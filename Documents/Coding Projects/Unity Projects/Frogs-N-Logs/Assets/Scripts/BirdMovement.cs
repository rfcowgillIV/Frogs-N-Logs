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
	// Nov 2026: the bird's speed depends on the difficulty (Easy is slower, Hard is faster).
	// Hazards and the shop's laser can also slow it down for a while (1 = normal).
	public static float SpeedMultiplier = 1f;
	private float baseSpeed;
	private float baseAcceleration;

	void Start () {
		birdAgent = GetComponent<UnityEngine.AI.NavMeshAgent> ();
		birdAnimator = GetComponent<Animator> ();
		baseSpeed = birdAgent.speed;
		baseAcceleration = birdAgent.acceleration;
	}

	// Speed for this frame: difficulty x any temporary effect.
	private void ApplySpeed () {
		float m = DifficultyRules.BirdSpeedMultiplier (GameSettings.Difficulty) * SpeedMultiplier;
		birdAgent.speed = baseSpeed * m;
		birdAgent.acceleration = baseAcceleration * Mathf.Max (1f, m);
		birdAgent.isStopped = m <= 0.01f;
	}
	
	// Update is called once per frame
	void Update () {

		ApplySpeed ();

		//Set the birds destination
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
