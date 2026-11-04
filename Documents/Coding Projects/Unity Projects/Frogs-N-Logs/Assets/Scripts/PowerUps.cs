using System;
using System.Collections.Generic;
using UnityEngine;

// PowerUps (added Nov 2026): goodies that appear around the pond every so often.
//
//   Golden fly       - rare and quick; worth 5 flies. Gone in 8 seconds, so hop to it!
//   Speed boost      - glowing blue orb: 60% faster for 6 seconds
//   Lily-pad shield  - a green lily pad: the next time the bird or snake catches you, the
//                      shield pops instead, the attacker is knocked dizzy for a moment and
//                      you get a second and a half to escape.
//
// They never appear right next to the frog (so standing still doesn't hand you freebies)
// and disappear after a while if nobody grabs them.
//
// Power to the pond!
public enum PowerUpType { GoldenFly, SpeedBoost, Shield }

// The frog's temporary and permanent bonuses, reset every round.
public static class FrogEffects
{
    public static event Action<string> Announce; // short message for the HUD ("Speed boost!")

    public static float BoostUntil;
    public static bool ShieldActive;
    public static float InvulnerableUntil;

    public const float BoostMultiplier = 1.6f;
    public const float BoostSeconds = 6f;

    public static bool Boosted { get { return Time.time < BoostUntil; } }
    public static float BoostSecondsLeft { get { return Mathf.Max(0f, BoostUntil - Time.time); } }

    public static void ResetRound()
    {
        BoostUntil = 0f;
        ShieldActive = false;
        InvulnerableUntil = 0f;
    }

    public static void Say(string message)
    {
        if (Announce != null) Announce(message);
    }

    public static void Collect(PowerUpType type)
    {
        AudioDirector.Play("PowerUp", 0.7f);
        switch (type)
        {
            case PowerUpType.GoldenFly:
                ScoreCounter.score += 5 * Shop.PointsMultiplier;
                Achievements.Unlock("golden");
                Say("Golden fly! +" + (5 * Shop.PointsMultiplier));
                break;
            case PowerUpType.SpeedBoost:
                BoostUntil = Time.time + BoostSeconds;
                Say("Speed boost!");
                break;
            case PowerUpType.Shield:
                ShieldActive = true;
                Say("Lily-pad shield!");
                break;
        }
    }

    // Called by PlayerHealth when something tagged Enemy touches the frog.
    // Returns true if the hit was blocked (shield or the short safe time after it popped).
    public static bool TryAbsorbHit(Collider attacker)
    {
        if (Time.time < InvulnerableUntil) return true;
        if (!ShieldActive) return false;

        ShieldActive = false;
        InvulnerableUntil = Time.time + 1.5f;
        StunAttacker(attacker, 2.5f);
        Say("Shield popped!");
        AudioDirector.Play("ShieldPop", 0.8f);
        return true;
    }

    public static void StunAttacker(Collider attacker, float seconds)
    {
        if (attacker == null) return;
        if (attacker.GetComponentInParent<BirdMovement>() != null)
        {
            BirdMovement.Stun(seconds);
            Achievements.CountStun();
        }
        SnakeHazard snake = attacker.GetComponentInParent<SnakeHazard>();
        if (snake != null) snake.Stun(seconds);
    }
}
