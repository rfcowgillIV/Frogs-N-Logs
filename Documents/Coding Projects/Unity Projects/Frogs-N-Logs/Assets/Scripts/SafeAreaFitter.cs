using UnityEngine;

// SafeAreaFitter (added Nov 2026): keeps the menus inside the part of the screen that isn't
// covered by a phone's notch, camera hole, rounded corners or home bar. The 3D game still
// fills the whole screen; only the buttons and text move in.
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    private Rect applied;
    private Vector2Int appliedSize;

    void Update()
    {
        Rect safe = Screen.safeArea;
        var size = new Vector2Int(Screen.width, Screen.height);
        if (safe == applied && size == appliedSize) return;
        applied = safe;
        appliedSize = size;
        if (size.x <= 0 || size.y <= 0) return;

        var rt = (RectTransform)transform;
        rt.anchorMin = new Vector2(safe.xMin / size.x, safe.yMin / size.y);
        rt.anchorMax = new Vector2(safe.xMax / size.x, safe.yMax / size.y);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
