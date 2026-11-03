using UnityEngine;

// Helper: when this object goes, its root goes too (used by the golden fly).
public class DestroyRootWithMe : MonoBehaviour
{
    public GameObject root;
    void OnDestroy()
    {
        if (root != null) Destroy(root);
    }
}
