Shader "DSH/Neon"
{
    Properties
    {
        _Color ("Color", Color) = (0.20, 0.80, 1.0, 1)
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 2.5
        _RimStrength ("Rim Strength", Range(0.0, 2.0)) = 0.55
        _Emission ("Emission", Range(0.0, 3.0)) = 1.0
        _PulseSpeed ("Pulse Speed", Range(0.0, 8.0)) = 0.0
        _PulseDepth ("Pulse Depth", Range(0.0, 1.0)) = 0.15
        _VerticalFade ("Vertical Fade", Range(0.0, 4.0)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _RimColor;
            float _RimPower;
            float _RimStrength;
            float _Emission;
            float _PulseSpeed;
            float _PulseDepth;
            float _VerticalFade;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 objectPos : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.objectPos = v.vertex;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);

                float rim = pow(1.0 - saturate(dot(normal, viewDir)), _RimPower);
                float pulse = 1.0 - _PulseDepth + _PulseDepth * sin(_Time.y * _PulseSpeed);

                // Optional darkening along the object's own Y, used by tall obstacles so
                // they read as solid rather than flat.
                float fade = _VerticalFade > 0.0
                    ? lerp(1.0, saturate(0.35 + 0.65 * (i.objectPos.y + 0.5)), _VerticalFade)
                    : 1.0;

                float3 color = _Color.rgb * _Emission * pulse * fade
                             + _RimColor.rgb * rim * _RimStrength;

                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }

    Fallback "Unlit/Color"
}
