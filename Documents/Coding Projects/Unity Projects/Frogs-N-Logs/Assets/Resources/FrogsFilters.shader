// FrogsFilters.shader (added Nov 2026): the looks in Pause > Graphics > Filter.
// One full-screen pass per filter. Kept deliberately cheap so they run fine on phones.
// It's in Resources/ so the web build always includes it.
Shader "Hidden/FrogsFilters"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }

    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    float _Levels;

    float luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // 0: Black & white (with a little contrast so it doesn't look flat)
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            fixed4 frag (v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
                float g = saturate((luma(c) - 0.5) * 1.15 + 0.5);
                return fixed4(g, g, g, 1);
            }
            ENDCG
        }

        // 1: Night (moonlit blue, darker edges)
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            fixed4 frag (v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
                float l = luma(c);
                float3 moon = lerp(float3(0.02, 0.04, 0.10), float3(0.55, 0.70, 1.0), pow(l, 0.9));
                float2 d = i.uv - 0.5;
                float vignette = saturate(1.15 - dot(d, d) * 2.2);
                return fixed4(moon * vignette, 1);
            }
            ENDCG
        }

        // 2: Cartoon (flat colour bands + dark outlines)
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 t = _MainTex_TexelSize.xy * 1.5;
                float3 c = tex2D(_MainTex, i.uv).rgb;
                float l  = luma(c);
                float lx = luma(tex2D(_MainTex, i.uv + float2(t.x, 0)).rgb) - luma(tex2D(_MainTex, i.uv - float2(t.x, 0)).rgb);
                float ly = luma(tex2D(_MainTex, i.uv + float2(0, t.y)).rgb) - luma(tex2D(_MainTex, i.uv - float2(0, t.y)).rgb);
                float edge = saturate(1 - smoothstep(0.12, 0.3, sqrt(lx * lx + ly * ly)));
                float3 bands = floor(c * 5 + 0.5) / 5;
                bands = lerp(float3(l, l, l), bands, 1.35); // a bit more saturated
                return fixed4(saturate(bands) * edge, 1);
            }
            ENDCG
        }

        // 3: Colour-blind friendly (re-maps red/green differences into blue/yellow,
        //    "daltonize" style for deuteranopia, the most common type)
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            fixed4 frag (v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
                // simulate what a deuteranope sees
                float3x3 sim = float3x3(0.625, 0.375, 0.0,
                                        0.700, 0.300, 0.0,
                                        0.000, 0.300, 0.700);
                float3 seen = mul(sim, c);
                float3 err = c - seen;
                // shift the lost information into channels they can see
                float3 fix = float3(0, err.r * 0.7 + err.g, err.r * 0.7 + err.b);
                return fixed4(saturate(c + fix), 1);
            }
            ENDCG
        }

        // 4: Vintage (warm sepia, soft fade)
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            fixed4 frag (v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
                float3 sepia = float3(dot(c, float3(0.393, 0.769, 0.189)),
                                      dot(c, float3(0.349, 0.686, 0.168)),
                                      dot(c, float3(0.272, 0.534, 0.131)));
                float2 d = i.uv - 0.5;
                float vignette = saturate(1.1 - dot(d, d) * 1.6);
                return fixed4(lerp(sepia, float3(0.95, 0.9, 0.8), 0.08) * vignette, 1);
            }
            ENDCG
        }

        // 5: Retro colour steps (used with the low-resolution "Retro" pixel look)
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            fixed4 frag (v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
                float levels = max(_Levels, 2);
                return fixed4(floor(c * levels + 0.5) / levels, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
