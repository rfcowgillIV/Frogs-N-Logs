using UnityEngine;

// ScreenFitCamera (added Nov 2026): keeps the same amount of pond in view on every screen
// shape. On a phone held upright the view widens (and the camera lifts a little on very
// tall phones), so you can still see the bird coming from the sides.
[RequireComponent(typeof(Camera))]
public class ScreenFitCamera : MonoBehaviour
{
    private Camera cam;
    private float appliedAspect = -1f;

    void Awake()
    {
        cam = GetComponent<Camera>();
        Apply();
    }

    void LateUpdate()
    {
        Apply();
    }

    private void Apply()
    {
        float aspect = ScreenLayout.Aspect;
        if (Mathf.Abs(aspect - appliedAspect) < 0.001f) return;
        appliedAspect = aspect;
        float distance;
        cam.fieldOfView = ScreenLayout.CameraFov(aspect, out distance);
        FollowCamera.DistanceScale = distance;
    }
}
