using UnityEngine;

// KeyBindings (added Nov 2026): keyboard controls players can change in Pause > Controls.
//
// Each action has one changeable key (WASD + P by default). The arrow keys always work
// as well, so nobody can lock themselves out by remapping. Start is always Space/Enter
// and Escape always pauses for the same reason.
//
// Have it your way: frogs don't judge your keyboard layout.
public static class KeyBindings
{
    public const string Up = "up";
    public const string Down = "down";
    public const string Left = "left";
    public const string Right = "right";
    public const string Pause = "pause";

    public static readonly string[] Actions = { Up, Down, Left, Right, Pause };

    public static KeyCode DefaultFor(string action)
    {
        switch (action)
        {
            case Up: return KeyCode.W;
            case Down: return KeyCode.S;
            case Left: return KeyCode.A;
            case Right: return KeyCode.D;
            default: return KeyCode.P;
        }
    }

    public static string Label(string action)
    {
        switch (action)
        {
            case Up: return "Move up";
            case Down: return "Move down";
            case Left: return "Move left";
            case Right: return "Move right";
            default: return "Pause";
        }
    }

    public static KeyCode Get(string action)
    {
        return GameSettings.GetKey(action, DefaultFor(action));
    }

    // Keys that can't be used for a custom binding (they already have fixed jobs).
    public static bool IsReserved(KeyCode key)
    {
        return key == KeyCode.Escape || key == KeyCode.Space || key == KeyCode.Return || key == KeyCode.KeypadEnter
            || key == KeyCode.UpArrow || key == KeyCode.DownArrow || key == KeyCode.LeftArrow || key == KeyCode.RightArrow
            || (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6) || key == KeyCode.None;
    }

    // Assign a key; if another action already used it, that action swaps to our old key.
    public static void Set(string action, KeyCode key)
    {
        KeyCode previous = Get(action);
        foreach (string other in Actions)
        {
            if (other != action && Get(other) == key)
            {
                GameSettings.SetKey(other, previous);
            }
        }
        GameSettings.SetKey(action, key);
    }

    public static void ResetAll()
    {
        foreach (string action in Actions)
        {
            GameSettings.ClearKey(action);
        }
    }

    // Movement on the ground plane from the keyboard: x = right, z = up the screen.
    public static Vector3 KeyboardMove()
    {
        float x = 0f;
        float z = 0f;
        if (Input.GetKey(Get(Right)) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
        if (Input.GetKey(Get(Left)) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
        if (Input.GetKey(Get(Up)) || Input.GetKey(KeyCode.UpArrow)) z += 1f;
        if (Input.GetKey(Get(Down)) || Input.GetKey(KeyCode.DownArrow)) z -= 1f;
        return new Vector3(x, 0f, z);
    }

    public static bool StartPressed()
    {
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    public static bool PausePressed()
    {
        return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(Get(Pause));
    }

    public static string KeyName(KeyCode key)
    {
        string name = key.ToString();
        if (name.StartsWith("Alpha")) return name.Substring(5);
        if (name.StartsWith("Keypad")) return "Num " + name.Substring(6);
        return name;
    }
}
