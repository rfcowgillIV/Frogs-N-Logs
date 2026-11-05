using UnityEngine;

// Keeps rain and snow falling around wherever the camera is looking (added Nov 2026).
public class FollowAbove : MonoBehaviour
{
    public Transform target;

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 p = target.position;
        transform.position = new Vector3(p.x, p.y + 6f, p.z + 8f);
    }
}
