using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlyMovement : MonoBehaviour {

	[SerializeField]
	private Transform center;


	private float flySpeed;
	private FlyKind kind;

	// Use this for initialization
	void Start () {
		flySpeed = Random.Range (300f, 700f);
	}
	
	// Update is called once per frame
	void Update () {
		// Nov 2026: dragonflies zip around faster (FlyKind.cs).
		if (kind == null) kind = GetComponentInParent<FlyKind> ();
		float scale = kind != null ? kind.SpeedScale : 1f;
		transform.RotateAround (center.position, Vector3.up, flySpeed * scale * Time.deltaTime);
	}
}
