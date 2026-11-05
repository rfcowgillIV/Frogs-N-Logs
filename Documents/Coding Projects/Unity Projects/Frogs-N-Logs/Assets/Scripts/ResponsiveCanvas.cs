using UnityEngine;
using UnityEngine.UI;

// ResponsiveCanvas (added Nov 2026): switches the menu scaling between landscape and
// portrait as the screen rotates, so text stays a sensible size either way up.
[RequireComponent(typeof(CanvasScaler))]
public class ResponsiveCanvas : MonoBehaviour
{
    private CanvasScaler scaler;
    private int applied = -1;

    void Awake()
    {
        scaler = GetComponent<CanvasScaler>();
        Apply();
    }

    void Update()
    {
        Apply();
    }

    // Tells the menus to re-arrange (e.g. stack the title screen's boxes in portrait).
    public static event System.Action OrientationChanged;

    public void Apply()
    {
        int portrait = ScreenLayout.IsPortrait ? 1 : 0;
        if (portrait == applied) return;
        bool first = applied < 0;
        applied = portrait;
        if (!first && OrientationChanged != null) OrientationChanged();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        if (portrait == 1)
        {
            // Phone held upright: lay out for a 720-wide screen, scaled by the width.
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.matchWidthOrHeight = 0f;
        }
        else
        {
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
        }
    }
}
