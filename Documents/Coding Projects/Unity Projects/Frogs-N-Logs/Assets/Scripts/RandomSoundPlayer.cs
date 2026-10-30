using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RandomSoundPlayer : MonoBehaviour {

	private AudioSource audioSource;

	[SerializeField]
	private List<AudioClip> soundClips = new List<AudioClip> ();

	[SerializeField]
	private float soundTimerDelay = 3f;

	private float soundTimer;

	// Use this for initialization
	void Start () {
		audioSource = GetComponent<AudioSource> ();
	}

	//Update is called once per frame
	void Update () {

		//Increment a timer to count to restarting.
		soundTimer = soundTimer + Time.deltaTime;

		//If the timer reaches the delay...
		if (soundTimer >= soundTimerDelay) {

			//...reset the timer...
			soundTimer = 0f;

			// BUG FIX (2026): if no sound clips were assigned in the Inspector (or
			// there's no AudioSource), the old code threw an "index out of range"
			// error every 3 seconds. Now it just stays quiet. Sound decision.
			if (audioSource == null || soundClips.Count == 0) {
				return;
			}

			//...choose a random sound...
			AudioClip randomSound = soundClips [Random.Range (0, soundClips.Count)];

			//...and play the sound.
			audioSource.PlayOneShot (randomSound);

		}

	}
}