using UnityEngine;

// PointerInput (added 2026): touch and mouse controls, so Frogs-N-Logs can be played on an
// iPad/phone browser or with a mouse, not just the keyboard.
//
//   Start the game:  tap the screen, click, or press Space.
//   Move the frog:   hold your finger (or the mouse button) down; the frog hops toward it.
//                    Keyboard arrows / WASD still work exactly as before.
//
// Unity turns touches into mouse events by default (Input.simulateMouseWithTouches), so the
// same mouse checks below cover fingers too. One input to rule them all - a real tap dance.
public static class PointerInput
{
    // Ignore the pointer when it's this close to the frog (as a fraction of screen height),
    // so the frog doesn't spin in place when your finger is right on top of it.
    public const float DeadZoneFraction = 0.05f;

    // True while a finger is on the screen or the left mouse button is held.
    public static bool IsHeld
    {
        get { return Input.GetMouseButton(0); }
    }

    // True on the frame a tap/click is released.
    public static bool WasTapped
    {
        get { return Input.GetMouseButtonUp(0); }
    }

    // Direction (on the ground) from the frog toward the finger/mouse, or zero if the pointer
    // isn't held or is inside the dead zone. Uses the camera's orientation, so "up the screen"
    // always means "away from the camera", whatever angle the camera is at.
    public static Vector3 WorldDirectionTowardPointer(Camera cam, Vector3 frogWorldPosition)
    {
        if (cam == null || !IsHeld)
        {
            return Vector3.zero;
        }
        Vector3 frogOnScreen = cam.WorldToScreenPoint(frogWorldPosition);
        return ScreenToGroundDirection(
            new Vector2(frogOnScreen.x, frogOnScreen.y),
            Input.mousePosition,
            Screen.height * DeadZoneFraction,
            cam.transform.right,
            cam.transform.forward);
    }

    // The math, kept separate (and free of Input/Screen) so the automated tests can check it.
    public static Vector3 ScreenToGroundDirection(Vector2 originOnScreen, Vector2 pointerOnScreen,
                                                  float deadZonePixels, Vector3 cameraRight, Vector3 cameraForward)
    {
        Vector2 delta = pointerOnScreen - originOnScreen;
        if (delta.magnitude < deadZonePixels)
        {
            return Vector3.zero;
        }
        delta.Normalize();

        // Flatten the camera's axes onto the ground so a tilted camera still maps cleanly.
        Vector3 right = new Vector3(cameraRight.x, 0f, cameraRight.z).normalized;
        Vector3 forward = new Vector3(cameraForward.x, 0f, cameraForward.z);
        if (forward.sqrMagnitude < 0.0001f)
        {
            // Camera looking straight down: "up the screen" is the camera's up vector.
            forward = Vector3.Cross(right, Vector3.up);
        }
        forward.Normalize();

        Vector3 direction = right * delta.x + forward * delta.y;
        return direction.normalized;
    }
}
