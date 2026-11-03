using UnityEngine;

// GameDirector (added Nov 2026): ties the round together each frame.
//  - frog speed = shop upgrades x speed-boost power-up
//  - shows a see-through green bubble around the frog while a lily-pad shield is up
// Lives next to the menus (FrogsUI) and the level, hazard and power-up spawners.
public class GameDirector : MonoBehaviour
{
    private GameObject bubble;
    private Transform frog;

    void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) frog = player.transform;
    }

    void Update()
    {
        PlayerMovement.SpeedMultiplier = Shop.SpeedMultiplier * (FrogEffects.Boosted ? FrogEffects.BoostMultiplier : 1f);
        UpdateBubble();
    }

    private void UpdateBubble()
    {
        bool show = frog != null && frog.gameObject.activeInHierarchy
                    && (FrogEffects.ShieldActive || Time.time < FrogEffects.InvulnerableUntil);
        if (show && bubble == null)
        {
            bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bubble.name = "Shield Bubble";
            Destroy(bubble.GetComponent<Collider>());
            bubble.GetComponent<Renderer>().sharedMaterial = PowerUpSpawner.Glow(new Color(0.4f, 1f, 0.5f, 0.18f), 1.8f);
            bubble.transform.localScale = Vector3.one * 2.2f;
        }
        if (bubble != null)
        {
            bubble.SetActive(show);
            if (show)
            {
                bubble.transform.position = frog.position + Vector3.up * 0.4f;
                // Flicker during the short safe time after the shield pops.
                if (!FrogEffects.ShieldActive)
                {
                    bubble.SetActive(Mathf.Repeat(Time.time * 8f, 1f) > 0.4f);
                }
            }
        }
    }
}
