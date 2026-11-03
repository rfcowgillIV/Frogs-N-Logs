using UnityEngine;

// ScreenFilter (added Nov 2026): the "Filter" choice in Pause > Graphics.
//
// Retro          - chunky pixels and fewer colours, like an old console
// Black & White  - classic film look
// Night          - moonlit blue with dark edges
// Cartoon        - flat colour bands with outlines
// Colour-blind friendly - makes red/green differences easier to tell apart
// Vintage        - warm sepia photo look
//
// Added to the main camera automatically. When the filter is "None" this component
// switches itself off, so it costs nothing. Filter-tastic.
[RequireComponent(typeof(Camera))]
public class ScreenFilter : MonoBehaviour
{
    private static Material material;
    private ScreenFilterMode mode;

    public static void AttachTo(Camera cam)
    {
        if (cam == null) return;
        ScreenFilter filter = cam.GetComponent<ScreenFilter>();
        if (filter == null)
        {
            filter = cam.gameObject.AddComponent<ScreenFilter>();
        }
        filter.Refresh();
    }

    void Awake()
    {
        // Subscribed for the component's whole life, so it can switch itself back on.
        GameSettings.Changed += Refresh;
    }

    void OnDestroy()
    {
        GameSettings.Changed -= Refresh;
    }

    private void Refresh()
    {
        if (this == null) return;
        mode = GameSettings.Filter;
        if (material == null && mode != ScreenFilterMode.None)
        {
            Shader shader = Resources.Load<Shader>("FrogsFilters");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("[Filter] Filters shader isn't available on this device");
                enabled = false;
                return;
            }
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        enabled = mode != ScreenFilterMode.None;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (material == null || mode == ScreenFilterMode.None)
        {
            Graphics.Blit(source, destination);
            return;
        }

        switch (mode)
        {
            case ScreenFilterMode.Retro:
            {
                // Draw at ~240 pixels tall, then blow it back up with hard pixel edges.
                int h = 240;
                int w = Mathf.Max(1, Mathf.RoundToInt(h * (float)source.width / source.height));
                RenderTexture small = RenderTexture.GetTemporary(w, h, 0, source.format);
                small.filterMode = FilterMode.Point;
                material.SetFloat("_Levels", 6f);
                Graphics.Blit(source, small, material, 5);
                Graphics.Blit(small, destination);
                RenderTexture.ReleaseTemporary(small);
                break;
            }
            case ScreenFilterMode.BlackAndWhite: Graphics.Blit(source, destination, material, 0); break;
            case ScreenFilterMode.Night: Graphics.Blit(source, destination, material, 1); break;
            case ScreenFilterMode.Cartoon: Graphics.Blit(source, destination, material, 2); break;
            case ScreenFilterMode.ColorBlindFriendly: Graphics.Blit(source, destination, material, 3); break;
            case ScreenFilterMode.Vintage: Graphics.Blit(source, destination, material, 4); break;
            default: Graphics.Blit(source, destination); break;
        }
    }

    public static string Label(ScreenFilterMode m)
    {
        switch (m)
        {
            case ScreenFilterMode.Retro: return "Retro";
            case ScreenFilterMode.BlackAndWhite: return "B & W";
            case ScreenFilterMode.Night: return "Night";
            case ScreenFilterMode.Cartoon: return "Cartoon";
            case ScreenFilterMode.ColorBlindFriendly: return "Colour-blind";
            case ScreenFilterMode.Vintage: return "Vintage";
            default: return "None";
        }
    }
}
