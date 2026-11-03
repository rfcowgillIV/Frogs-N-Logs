using System;
using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameState : MonoBehaviour {

	private bool gameStarted = false;

	// BUG FIX (2026): new flag. After the frog died, gameStarted went back to
	// false, so pressing Space on the "Game Over!" screen called StartGame()
	// again - it hid the Game Over text and switched controls back on for a
	// frog that no longer existed, while the restart countdown kept running.
	// Once it's game over, it's game over (until the level reloads). Croak.
	private bool gameOver = false;
	[SerializeField]
	private Text gameStateText;
	[SerializeField]
	private GameObject player;
	[SerializeField]
	private BirdMovement birdMovement;
	[SerializeField]
	private FollowCamera followCamera;
	private float restartDelay = 3f;
	private float restartTimer;
	private PlayerMovement playerMovement;
	private PlayerHealth playerHealth;

	// ---- Nov 2026: hooks for the title screen, pause menu and high scores (FrogsUI.cs) ----

	// The GameState in the currently loaded level.
	public static GameState Current { get; private set; }

	// Fired when a round starts / when the frog is caught (with the final score).
	public static event Action RoundStarted;
	public static event Action<int, bool> RoundEnded; // (score, isNewBest)

	// Set by the menus. While a menu is open, Space doesn't start the game underneath it.
	public static bool MenuOpen;

	public bool IsPlaying { get { return gameStarted && !gameOver; } }
	public bool IsOver { get { return gameOver; } }
	public bool HasStarted { get { return gameStarted || gameOver; } }

	void Awake () {
		Current = this;
		MenuOpen = false;
		// Temporary effects (power-ups, stuns) never carry over into a new round.
		PlayerMovement.SpeedMultiplier = 1f;
		BirdMovement.SpeedMultiplier = 1f;
	}

	void OnDestroy () {
		if (Current == this) {
			Current = null;
		}
	}

	// Use this for initialization
	void Start () {
		// 2026: the cursor stays visible now, because the mouse can steer the frog.
		// (It used to be hidden for the keyboard-only version.)
		Cursor.visible = true;

		// Nov 2026: the title screen (FrogsUI) now explains the controls, so this text
		// stays hidden until it's needed for "Game Over!".
		if (gameStateText != null) {
			gameStateText.text = "Tap Play or press Space";
			gameStateText.color = Color.clear;
		}

		playerMovement = player.GetComponent<PlayerMovement> ();
		playerHealth = player.GetComponent<PlayerHealth> ();

		// prevent the player from moving at the start of the game
		playerMovement.enabled = false;

		//prevent the bird from moving at the the start of the game
		birdMovement.enabled = false;

		//prevent the follow camera from moving to its game position
		followCamera.enabled = false;
	}

	// Update is called once per frame
	void Update () {
		//if the game has not started and the player presses Space/Enter...
		// (Nov 2026: tapping now goes through the title screen's Play button instead, so a tap
		// on the difficulty buttons doesn't start the game by accident.)
		if (gameStarted == false && gameOver == false && !MenuOpen && KeyBindings.StartPressed ()) {
			//... then start the game.
			StartGame();
		}
		//if the player is no longer alive....
		if(playerHealth.alive == false) {
			//... end the game. (BUG FIX 2026: only once - this used to run every
			//single frame, re-setting the text and hiding the player 60x a second.)
			if (gameOver == false) {
				EndGame();
			}

			//... increment a timer to count up to restarting...
			restartTimer = restartTimer + Time.deltaTime;

			//... and if it reaches the restart delay...
			if (restartTimer >= restartDelay) {


				//... then reload the currently loaded scene.
				SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

		}
	}
}

		// Called by the title screen's Play button (and by Space/Enter).
		public void RequestStart() {
			if (!gameStarted && !gameOver) {
				StartGame();
			}
		}

		private void StartGame() {

			//Set the game state
			gameStarted = true;


			//Remove the start text
			gameStateText.color = Color.clear;


			//Allow the player to move
			playerMovement.enabled = true;


			//Allow the bird to move
			birdMovement.enabled = true;


			//Allow the camera to move
			followCamera.enabled = true;

			if (RoundStarted != null) {
				RoundStarted ();
			}
		}

		private void EndGame() {

			//Set the game state
			gameStarted = false;
			gameOver = true;

			//Show the game over text
			gameStateText.color = Color.white;
			gameStateText.text = "Game Over!";

			//Remove the player from the game
			player.SetActive (false);

			// Nov 2026: remember the best score for this difficulty.
			int score = ScoreCounter.score;
			bool newBest = GameSettings.SubmitScore (GameSettings.Difficulty, score);
			if (RoundEnded != null) {
				RoundEnded (score, newBest);
			}
		}
}
