// FrogsToonWater.shader (Nov 2026): cartoon pond water for URP. Replaces the 2017 Standard
// Assets water, which drew the whole scene two extra times per frame for reflections.
// This is one cheap pass: a deep-to-shallow colour gradient, drifting ripple bands, little
// sparkles and soft waves - no extra cameras at all.
Shader "Frogs/ToonWater"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.45, 0.78, 0.80, 0.38)
        _DeepColor ("Deep", Color) = (0.20, 0.50, 0.62, 0.52)
        _RippleColor ("Ripples", Color) = (0.88, 0.97, 1, 0.9)
        _RippleScale ("Ripple size", Float) = 1.1
        _Speed ("Speed", Float) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ToonWater"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _RippleColor;
                float _RippleScale;
                float _Speed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half fog : TEXCOORD1; };

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }

            Varyings vert (Attributes i)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                ws.y += 0.03 * sin(ws.x * 0.7 + _Time.y * _Speed * 2.0) * cos(ws.z * 0.6 + _Time.y * _Speed * 1.7);
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 p = i.positionWS.xz * _RippleScale;
                float t = _Time.y * _Speed;
                // big soft colour pools
                float pools = sin(p.x * 0.6 + t * 0.4) * sin(p.y * 0.5 - t * 0.3) * 0.5 + 0.5;
                half4 col = lerp(_DeepColor, _ShallowColor, pools);
                // crisp cartoon ripple bands
                float bands = sin((p.x + sin(p.y * 1.3 + t) * 0.6) * 3.0 + t * 2.0);
                // Nov 2026: keep the ripple lines smooth on any screen size. fwidth = how much
                // "bands" changes across one pixel; widen the edge by that much, and fade the
                // lines out when they'd be thinner than a pixel (small phones, far away).
                float fw = fwidth(bands);
                float ripple = smoothstep(0.975 - fw, 0.995 + fw, bands) * saturate(1.2 - fw * 3.0);
                // twinkling sparkles
                float2 cell = floor(p * 3.0);
                float sparkle = step(0.985, hash21(cell)) * (0.5 + 0.5 * sin(t * 6.0 + hash21(cell + 3.1) * 6.28));
                // Sparkle cells smaller than ~2 pixels just flicker, so fade them out.
                float cellPixels = 1.0 / max(length(fwidth(p * 3.0)), 1e-4);
                sparkle *= saturate((cellPixels - 1.5) / 2.5);
                half glint = saturate(ripple * 0.3 + sparkle * 0.7);
                col.rgb = lerp(col.rgb, _RippleColor.rgb, glint);
                col.a = saturate(col.a + glint * 0.3);
                Light l = GetMainLight();
                col.rgb *= lerp(0.6, 1.1, saturate(l.direction.y)) * l.color;
                col.rgb = MixFog(col.rgb, i.fog);
                return col;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
