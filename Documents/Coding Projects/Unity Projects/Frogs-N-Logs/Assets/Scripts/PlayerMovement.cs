using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// PlayerMovement: steers the frog.
//
// LAG FIX (Nov 2026): in the 2017 version the frog had no speed of its own. It only moved
// when its hop animation pushed it forward ("root motion"), so pressing a key meant:
// wait for the hop to start -> lunge -> pause -> lunge. Letting go was worse: the
// animator was set to finish the whole hop before stopping, so the frog kept going.
//
// Now the frog moves at a steady speed the moment you press a key (reaching full speed
// in about a tenth of a second), and stops as soon as you let go. The hop animation
// still plays on top so it looks the same - it just doesn't steer anymore.
// The animator's Hop -> Idle transition no longer waits for the hop to finish, either.
//
// No more hop-scotch delays. Just hop.
public class PlayerMovement : MonoBehaviour {

	private Animator playerAnimator;
	private Vector3 movement;
	private float turningSpeed = 20f;
	private Rigidbody playerRigidbody;

	[SerializeField]
	private RandomSoundPlayer playerFootsteps;

	// Metres per second at full speed. Tuned to roughly match the old hop's average pace.
	[SerializeField]
	private float hopSpeed = 4.5f;

	// How quickly the frog gets up to speed / stops (metres per second, per second).
	[SerializeField]
	private float acceleration = 45f;

	// Power-ups and shop upgrades multiply this (1 = normal speed).
	public static float SpeedMultiplier = 1f;

	// For the automated play tests: pretend a direction is being held.
	public static Vector3? TestMoveOverride;

	// For the automated play tests: the old 2017 root-motion movement, to compare speeds.
	public static bool ClassicRootMotion;

	public float CurrentSpeed { get; private set; }
	private Vector3 velocity;

	void Start () {
		//Gather the components from the Player GameObject
		playerAnimator = GetComponent<Animator> ();
		playerRigidbody = GetComponent<Rigidbody> ();
	}

	void OnDisable () {
		// Stop dead when the game pauses controls (title screen, game over).
		if (playerRigidbody != null && !playerRigidbody.isKinematic) {
			playerRigidbody.linearVelocity = new Vector3 (0f, playerRigidbody.linearVelocity.y, 0f);
		}
		CurrentSpeed = 0f;
		velocity = Vector3.zero;
		if (playerAnimator != null) {
			playerAnimator.SetFloat ("Speed", 0f);
		}
		if (playerFootsteps != null) {
			playerFootsteps.enabled = false;
		}
	}

	// Update is called once per frame
	void Update () {

		if (TestMoveOverride.HasValue) {
			movement = TestMoveOverride.Value;
		} else {
			// Keyboard (changeable in Pause > Controls; arrow keys always work too).
			movement = KeyBindings.KeyboardMove ();

			// Touch & mouse: if no key is pressed, hop toward the finger/mouse while it's held
			// (see PointerInput.cs). Keyboard always wins if both are used.
			if (movement == Vector3.zero) {
				movement = PointerInput.WorldDirectionTowardPointer (Camera.main, transform.position);
			}
		}

		// Diagonals shouldn't be faster than straight lines.
		if (movement.sqrMagnitude > 1f) {
			movement.Normalize ();
		}
	}

	// Unity calls this instead of applying the hop animation's own movement, because this
	// script now does the moving. (Only the 2017 comparison mode uses the animation's push.)
	void OnAnimatorMove () {
		if (ClassicRootMotion && playerAnimator != null && playerRigidbody != null) {
			playerRigidbody.MovePosition (playerRigidbody.position + playerAnimator.deltaPosition);
		}
	}

	void FixedUpdate () {
		bool moving = movement != Vector3.zero;

		if (!ClassicRootMotion) {
			// Ease toward the target speed so starts and stops feel snappy but not robotic.
			// The frog is moved directly (like the old hop did) rather than pushed with physics
			// forces: its body sits slightly in the ground, and ground friction would otherwise
			// hold it in place.
			Vector3 target = movement * hopSpeed * SpeedMultiplier;
			velocity = Vector3.MoveTowards (velocity, target, acceleration * Time.fixedDeltaTime);
			if (velocity != Vector3.zero) {
				playerRigidbody.MovePosition (playerRigidbody.position + velocity * Time.fixedDeltaTime);
			}
			// No leftover sliding from bumps.
			playerRigidbody.linearVelocity = new Vector3 (0f, playerRigidbody.linearVelocity.y, 0f);
			CurrentSpeed = velocity.magnitude;
		}

		//if the player is steering...
		if (moving) {

			//...then turn toward the movement direction...
			Quaternion targetRotation = Quaternion.LookRotation (movement, Vector3.up);
			Quaternion newRotation = Quaternion.Lerp (playerRigidbody.rotation, targetRotation, turningSpeed * Time.fixedDeltaTime);
			playerRigidbody.MoveRotation (newRotation);

			//... play the hop animation (a little faster when the frog is boosted)...
			playerAnimator.SetFloat ("Speed", 3f);
			playerAnimator.speed = Mathf.Clamp (SpeedMultiplier, 1f, 2f);

			//... and play footstep sounds.
			playerFootsteps.enabled = true;

		} else {
			//Otherwise, don't play the hop animation or footsteps.
			playerAnimator.SetFloat ("Speed", 0f);
			playerAnimator.speed = 1f;
			playerFootsteps.enabled = false;
		}
	}
}
