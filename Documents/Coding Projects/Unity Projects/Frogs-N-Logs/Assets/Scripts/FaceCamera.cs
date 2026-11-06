using UnityEngine;

// Keeps a flat glow facing the camera.
public class FaceCamera : MonoBehaviour
{
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
    }
}
