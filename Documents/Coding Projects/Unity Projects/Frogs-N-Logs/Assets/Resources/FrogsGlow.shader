// FrogsGlow.shader (added Nov 2026): the glowing look for power-ups, the lily-pad shield
// bubble, the laser and the snake's eyes. Unlit (cheap on phones), with a bright rim and
// a gentle pulse so pickups catch your eye. In Resources/ so the web build includes it.
Shader "Frogs/Glow"
{
    Properties
    {
        _Color ("Color", Color) = (0.5, 1, 0.5, 1)
        _Rim ("Rim brightness", Range(0, 3)) = 1.2
        _Pulse ("Pulse amount", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Rim;
            float _Pulse;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float rim : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 viewDir = normalize(ObjSpaceViewDir(v.vertex));
                o.rim = 1.0 - saturate(dot(viewDir, normalize(v.normal)));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float pulse = 1.0 + _Pulse * sin(_Time.y * 4.0);
                float3 c = _Color.rgb * pulse + _Color.rgb * _Rim * i.rim * i.rim;
                return fixed4(c, saturate(_Color.a + i.rim * 0.35 * _Color.a));
            }
            ENDCG
        }
    }
    Fallback Off
}
