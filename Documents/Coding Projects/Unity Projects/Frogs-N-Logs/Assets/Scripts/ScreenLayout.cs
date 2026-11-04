using UnityEngine;

// ScreenLayout (added Nov 2026): one place that knows the screen's shape, so the menus and
// the camera can adapt to any phone, tablet or monitor, held either way up.
//
//   Landscape (wider than tall): the original layout.
//   Portrait  (taller than wide): menus are laid out for a narrow screen and shrink to fit,
//             and the camera widens so you still see as much of the pond side to side.
public static class ScreenLayout
{
    // Lets the play tests pretend to be a phone held upright.
    public static float? TestAspect;

    public static float Aspect
    {
        get
        {
            if (TestAspect.HasValue) return TestAspect.Value;
            return Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
        }
    }

    public static bool IsPortrait { get { return Aspect < 1f; } }

    // Camera: the 2017 game was designed for a 16:9 landscape screen with a 60 degree
    // (vertical) view. Keep at least that much of the pond visible across the SHORT side of
    // any screen, and keep the 16:9 side-to-side view on narrower landscape screens (iPads).
    public const float DesignFov = 60f;
    public const float DesignAspect = 16f / 9f;
    public const float MaxFov = 90f;

    // Returns the vertical field of view to use, and how much farther back the camera should
    // sit when the view would need to be wider than MaxFov (very tall phones held upright).
    public static float CameraFov(float aspect, out float distanceScale)
    {
        float tanHalf = Mathf.Tan(DesignFov * 0.5f * Mathf.Deg2Rad);
        float needTan;
        if (aspect >= 1f)
        {
            // Landscape: keep the designed side-to-side view (and never less than the design).
            needTan = Mathf.Max(tanHalf, tanHalf * DesignAspect / aspect);
        }
        else
        {
            // Portrait: the short side is now across, so give it the designed 60 degrees.
            needTan = tanHalf / aspect;
        }
        float maxTan = Mathf.Tan(MaxFov * 0.5f * Mathf.Deg2Rad);
        distanceScale = needTan > maxTan ? needTan / maxTan : 1f;
        return 2f * Mathf.Atan(Mathf.Min(needTan, maxTan)) * Mathf.Rad2Deg;
    }
}
