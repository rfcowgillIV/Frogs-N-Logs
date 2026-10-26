using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowCamera : MonoBehaviour {


	[SerializeField]
	private Transform player;
	[SerializeField]
	private Vector3 offset;
	private float cameraFollowSpeed = 5f;

	
	// BUG FIX (2026): LateUpdate instead of Update. LateUpdate runs after the
	// frog has moved this frame, so the camera follows smoothly instead of
	// lagging one frame behind and jittering. Lights, camera, smooth action!
	void LateUpdate () {
		Vector3 newPosition = player.position + offset;

		transform.position = Vector3.Lerp (transform.position, newPosition, cameraFollowSpeed * Time.deltaTime);
	}
}
