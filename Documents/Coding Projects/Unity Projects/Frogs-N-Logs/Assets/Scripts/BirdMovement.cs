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

	// Knocked dizzy (lily-pad shield, laser) until this time.
	public static float StunnedUntil;
	public static bool IsStunned { get { return Time.time < StunnedUntil; } }

	public static void Stun (float seconds) {
		StunnedUntil = Mathf.Max (StunnedUntil, Time.time + seconds);
	}
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
		float m = DifficultyRules.BirdSpeedMultiplier (GameSettings.Difficulty) * SpeedMultiplier
		          * (Cheats.SlowBird ? 0.5f : 1f);
		birdAgent.speed = baseSpeed * m;
		birdAgent.acceleration = baseAcceleration * Mathf.Max (1f, m);
		birdAgent.isStopped = m <= 0.01f || IsStunned;
		if (IsStunned) {
			// Seeing stars: wobble on the spot.
			transform.Rotate (0f, 360f * Time.deltaTime, 0f);
		}
	}
	
	// Update is called once per frame
	private Vector3 lastSeen;
	private float nextSearch;

	void Update () {

		ApplySpeed ();

		//Set the birds destination
		// Nov 2026: if the frog is hiding in the reeds, search around where it was last seen.
		if (!HidingSpot.FrogHidden) {
			lastSeen = target.position;
			birdAgent.SetDestination (target.position);
		} else if (Time.time > nextSearch) {
			nextSearch = Time.time + 1.5f;
			birdAgent.SetDestination (lastSeen + new Vector3 (Random.Range (-7f, 7f), 0f, Random.Range (-7f, 7f)));
		}

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
