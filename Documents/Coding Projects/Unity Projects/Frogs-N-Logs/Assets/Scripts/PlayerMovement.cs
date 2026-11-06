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

	// ---- Nov 2026: dodge hop and slippery ice ----
	// Dodge hop (Shift / gamepad A / DODGE button): a quick 4 m leap. You can't be caught
	// while leaping. Recharges in a couple of seconds.
	public const float DodgeSpeed = 18f;
	public const float DodgeSeconds = 0.22f;
	public const float DodgeCooldown = 2.5f;
	public static float DodgeReadyAt;
	private static float dodgeUntil;
	private static Vector3 dodgeDirection;
	public static bool Dodging { get { return Time.time < dodgeUntil; } }

	// 1 = normal grip. Lower = slippery (Winter ice): the frog takes longer to speed up,
	// turn and stop, so it slides.
	public static float Grip = 1f;

	public static PlayerMovement Instance { get; private set; }

	void Awake () {
		Instance = this;
		DodgeReadyAt = 0f;
		dodgeUntil = 0f;
		Grip = 1f;
		StuckUntil = 0f;
	}

	// Giant mushrooms (Autumn Woods) launch the frog forward. Not limited by the dodge recharge.
	public void Bounce (float seconds = 0.35f) {
		Vector3 dir = movement.sqrMagnitude > 0.01f ? movement.normalized : transform.forward;
		dir.y = 0f;
		dodgeDirection = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
		dodgeUntil = Time.time + seconds;
		FrogEffects.InvulnerableUntil = Mathf.Max (FrogEffects.InvulnerableUntil, Time.time + seconds);
	}

	// Thin ice (Winter Freeze): stuck for a moment after falling through.
	public static float StuckUntil;
	public static bool Stuck { get { return Time.time < StuckUntil; } }

	// Leap in the direction you're steering (or the way you're facing). Returns true if it
	// happened (false while recharging).
	public bool Dodge () {
		if (!enabled || Time.time < DodgeReadyAt) return false;
		Vector3 dir = movement.sqrMagnitude > 0.01f ? movement.normalized : transform.forward;
		dir.y = 0f;
		if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
		dodgeDirection = dir.normalized;
		dodgeUntil = Time.time + DodgeSeconds;
		DodgeReadyAt = Time.time + DodgeCooldown;
		// Can't be caught mid-leap (and for a moment after landing).
		FrogEffects.InvulnerableUntil = Mathf.Max (FrogEffects.InvulnerableUntil, Time.time + DodgeSeconds + 0.15f);
		velocity = dodgeDirection * hopSpeed * SpeedMultiplier;
		AudioDirector.Play ("Dodge", 0.7f);
		return true;
	}

	void Start () {
		//Gather the components from the Player GameObject
		playerAnimator = GetComponent<Animator> ();
		playerRigidbody = GetComponent<Rigidbody> ();
		// Nov 2026: physics runs 50 times a second; interpolate so the frog glides smoothly
		// on 120 Hz+ screens instead of stepping.
		if (playerRigidbody != null) playerRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
	}

	void OnEnable () {
		// The Start / A press that began the round shouldn't also dodge.
		DodgeReadyAt = Mathf.Max (DodgeReadyAt, Time.time + 0.3f);
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

		// Stuck in thin ice: can't move for a moment.
		if (Stuck) {
			movement = Vector3.zero;
		}

		// Diagonals shouldn't be faster than straight lines.
		if (movement.sqrMagnitude > 1f) {
			movement.Normalize ();
		}

		if (KeyBindings.DodgePressed ()) {
			Dodge ();
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
			velocity = Vector3.MoveTowards (velocity, target, acceleration * Mathf.Clamp (Grip, 0.05f, 1f) * Time.fixedDeltaTime);
			if (Dodging) {
				playerRigidbody.MovePosition (playerRigidbody.position + dodgeDirection * DodgeSpeed * Time.fixedDeltaTime);
			} else if (velocity != Vector3.zero) {
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
