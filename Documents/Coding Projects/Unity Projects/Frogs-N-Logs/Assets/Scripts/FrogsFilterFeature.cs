using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

// FrogsFilterFeature (Nov 2026, for URP): draws the filter chosen in Pause > Graphics > Filter
// (Retro, B&W, Night, Cartoon, Colour-blind friendly, Vintage) as one full-screen pass.
// It's added to the game's URP renderer by Editor/FrogsURPSetup.cs. With "None" selected it
// adds nothing at all, so it's free when unused.
public class FrogsFilterFeature : ScriptableRendererFeature
{
    // True once URP has loaded this feature (checked by the play tests).
    public static bool IsInstalled { get; private set; }
    // Frame number of the last time a filter was actually drawn.
    public static int LastDrawnFrame { get; private set; } = -1;

    private class FilterPass : ScriptableRenderPass
    {
        public Material material;
        public int passIndex;

        public FilterPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer || material == null) return;

            TextureHandle source = resources.activeColorTexture;
            TextureDesc desc = renderGraph.GetTextureDesc(source);
            desc.name = "_FrogsFilter";
            desc.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(desc);

            var blit = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, passIndex);
            renderGraph.AddBlitPass(blit, "Frogs Filter");
            resources.cameraColor = destination;
            LastDrawnFrame = Time.frameCount;
        }
    }

    private FilterPass pass;
    private Material material;

    public override void Create()
    {
        pass = new FilterPass();
        IsInstalled = true;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        int index = ScreenFilter.PassFor(GameSettings.Filter);
        if (index < 0) return;
        CameraType type = renderingData.cameraData.cameraType;
        if (type != CameraType.Game) return;

        if (material == null)
        {
            Shader shader = Shader.Find("Hidden/FrogsFilters");
            if (shader == null) shader = Resources.Load<Shader>("FrogsFilters");
            if (shader == null || !shader.isSupported) return;
            material = CoreUtils.CreateEngineMaterial(shader);
        }
        pass.material = material;
        pass.passIndex = index;
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
    }
}

// Names and shader passes for each filter.
public static class ScreenFilter
{
    public static int PassFor(ScreenFilterMode m)
    {
        switch (m)
        {
            case ScreenFilterMode.BlackAndWhite: return 0;
            case ScreenFilterMode.Night: return 1;
            case ScreenFilterMode.Cartoon: return 2;
            case ScreenFilterMode.ColorBlindFriendly: return 3;
            case ScreenFilterMode.Vintage: return 4;
            case ScreenFilterMode.Retro: return 5;
            default: return -1;
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
