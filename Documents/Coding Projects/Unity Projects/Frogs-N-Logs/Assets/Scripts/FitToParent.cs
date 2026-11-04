using UnityEngine;

// FitToParent (added Nov 2026): shrinks a menu card just enough to fit on screen (with a
// small margin), so the title screen, pause menu and credits always fit - on a phone held
// sideways or upright, a tablet or a big monitor. It never makes anything bigger.
[RequireComponent(typeof(RectTransform))]
public class FitToParent : MonoBehaviour
{
    public float margin = 16f;

    void LateUpdate()
    {
        var rt = (RectTransform)transform;
        var parent = rt.parent as RectTransform;
        if (parent == null) return;
        Vector2 have = parent.rect.size - new Vector2(margin * 2f, margin * 2f);
        Vector2 need = rt.rect.size;
        if (need.x <= 0f || need.y <= 0f || have.x <= 0f || have.y <= 0f) return;
        float s = Mathf.Min(1f, have.x / need.x, have.y / need.y);
        if (Mathf.Abs(rt.localScale.x - s) > 0.001f) rt.localScale = new Vector3(s, s, 1f);
    }
}
