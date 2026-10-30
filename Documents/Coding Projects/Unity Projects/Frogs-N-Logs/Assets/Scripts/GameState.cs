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


	// Use this for initialization
	void Start () {
		Cursor.visible = false;

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
		//if the game has not started and the player presses the space bar...
		if (gameStarted == false && gameOver == false && Input.GetKeyUp (KeyCode.Space)) {
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
		}
}






