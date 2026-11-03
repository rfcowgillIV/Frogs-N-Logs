using System;
using System.Collections.Generic;
using UnityEngine;

// PowerUpPickup (added Nov 2026) - see PowerUps.cs for what each power-up does.
// One pickup lying (or buzzing) around the pond.
public class PowerUpPickup : MonoBehaviour
{
    public PowerUpType type;
    public float lifetime = 14f;

    private float age;
    private bool taken;
    private Vector3 basePosition;
    private Renderer[] renderers;

    void Start()
    {
        basePosition = transform.position;
        renderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        age += Time.deltaTime;
        if (type != PowerUpType.GoldenFly)
        {
            // Bob and spin so it reads as "grab me".
            transform.position = basePosition + Vector3.up * (0.15f * Mathf.Sin(age * 3f));
            transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        }

        // Blink for the last two seconds, then vanish.
        float left = lifetime - age;
        if (left < 2f && renderers != null)
        {
            bool visible = Mathf.Repeat(left * 6f, 1f) > 0.35f;
            foreach (Renderer r in renderers) if (r != null) r.enabled = visible;
        }
        if (left <= 0f) Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (taken || !other.CompareTag("Player")) return;
        taken = true;
        FrogEffects.Collect(type);
        Destroy(gameObject);
    }
}
