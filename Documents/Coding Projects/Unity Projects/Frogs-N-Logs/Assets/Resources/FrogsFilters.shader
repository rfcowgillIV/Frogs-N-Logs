// FrogsFilters.shader (Nov 2026, rebuilt for URP): the looks in Pause > Graphics > Filter.
// Drawn by FrogsFilterFeature as one full-screen pass after everything else.
//   0 B&W   1 Night   2 Cartoon   3 Colour-blind friendly   4 Vintage   5 Retro
Shader "Hidden/FrogsFilters"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        float luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }
        float3 src(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb; }
        ENDHLSL

        Pass { Name "BlackAndWhite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag (Varyings i) : SV_Target { float g = saturate((luma(src(i.texcoord)) - 0.5) * 1.15 + 0.5); return half4(g, g, g, 1); }
            ENDHLSL }

        Pass { Name "Night"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag (Varyings i) : SV_Target
            {
                float l = luma(src(i.texcoord));
                float3 moon = lerp(float3(0.02, 0.04, 0.10), float3(0.55, 0.70, 1.0), pow(l, 0.9));
                float2 d = i.texcoord - 0.5;
                return half4(moon * saturate(1.15 - dot(d, d) * 2.2), 1);
            }
            ENDHLSL }

        Pass { Name "Cartoon"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag (Varyings i) : SV_Target
            {
                float2 t = (1.0 / max(_ScreenParams.xy, 1)) * 1.5;
                float3 c = src(i.texcoord);
                float lx = luma(src(i.texcoord + float2(t.x, 0))) - luma(src(i.texcoord - float2(t.x, 0)));
                float ly = luma(src(i.texcoord + float2(0, t.y))) - luma(src(i.texcoord - float2(0, t.y)));
                float edge = saturate(1 - smoothstep(0.12, 0.3, sqrt(lx * lx + ly * ly)));
                float3 bands = floor(c * 5 + 0.5) / 5;
                bands = lerp(luma(c).xxx, bands, 1.35);
                return half4(saturate(bands) * edge, 1);
            }
            ENDHLSL }

        Pass { Name "ColorBlindFriendly"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag (Varyings i) : SV_Target
            {
                float3 c = src(i.texcoord);
                float3x3 sim = float3x3(0.625, 0.375, 0.0, 0.700, 0.300, 0.0, 0.000, 0.300, 0.700);
                float3 err = c - mul(sim, c);
                float3 fix = float3(0, err.r * 0.7 + err.g, err.r * 0.7 + err.b);
                return half4(saturate(c + fix), 1);
            }
            ENDHLSL }

        Pass { Name "Vintage"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag (Varyings i) : SV_Target
            {
                float3 c = src(i.texcoord);
                float3 sepia = float3(dot(c, float3(0.393, 0.769, 0.189)), dot(c, float3(0.349, 0.686, 0.168)), dot(c, float3(0.272, 0.534, 0.131)));
                float2 d = i.texcoord - 0.5;
                return half4(lerp(sepia, float3(0.95, 0.9, 0.8), 0.08) * saturate(1.1 - dot(d, d) * 1.6), 1);
            }
            ENDHLSL }

        Pass { Name "Retro"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag (Varyings i) : SV_Target
            {
                float rows = 240.0;
                float cols = rows * (_ScreenParams.x / max(_ScreenParams.y, 1));
                float2 uv = (floor(i.texcoord * float2(cols, rows)) + 0.5) / float2(cols, rows);
                float3 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).rgb;
                return half4(floor(c * 6 + 0.5) / 6, 1);
            }
            ENDHLSL }
    }
    Fallback Off
}
