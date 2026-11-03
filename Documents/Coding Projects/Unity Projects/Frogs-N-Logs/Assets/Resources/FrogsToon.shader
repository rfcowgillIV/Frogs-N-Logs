// FrogsToon.shader (added Nov 2026): a simple lit colour for things built in code (the snake,
// logs, hats). Lit by the sun and ambient light, with fog, so it matches the pond at any time
// of day. Very cheap. In Resources/ so the web build always includes it.
Shader "Frogs/Toon"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Shine ("Shine", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            fixed4 _Color;
            float _Shine;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float diffuse = saturate(dot(n, l));
                diffuse = smoothstep(0.0, 0.6, diffuse) * 0.8 + 0.2;   // soft toon bands
                float3 h = normalize(l + i.viewDir);
                float spec = pow(saturate(dot(n, h)), 32) * _Shine;
                float3 ambient = ShadeSH9(float4(n, 1));
                float3 c = _Color.rgb * (_LightColor0.rgb * diffuse + ambient) + _LightColor0.rgb * spec;
                fixed4 col = fixed4(c, 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
