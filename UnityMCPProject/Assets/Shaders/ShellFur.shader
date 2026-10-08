Shader "DSH/Shell Fur"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.16, 0.09, 0.04, 1)
        _TipColor ("Tip Color", Color) = (0.90, 0.68, 0.36, 1)
        _FurLength ("Fur Length", Range(0.0, 1.0)) = 0.15
        _ShellCount ("Shell Count", Range(1, 31)) = 20
        _Density ("Strand Density", Range(0.0, 1.0)) = 0.5
        _StrandScale ("Strand Scale", Range(2.0, 300.0)) = 60.0
        _Gravity ("Gravity Droop", Range(0.0, 2.0)) = 0.45
        _BaseOcclusion ("Base Occlusion", Range(0.0, 1.0)) = 0.65
        _NormalJitter ("Normal Jitter", Range(0.0, 1.0)) = 0.4
        _RimBoost ("Tip Rim", Range(0.0, 1.0)) = 0.15
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 300

        Pass
        {
            Name "FUR_FORWARD"
            Tags { "LightMode" = "ForwardBase" }

            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            fixed4 _BaseColor;
            fixed4 _TipColor;
            float _FurLength;
            float _ShellCount;
            float _Density;
            float _StrandScale;
            float _Gravity;
            float _BaseOcclusion;
            float _NormalJitter;
            float _RimBoost;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2g
            {
                float4 objPos    : TEXCOORD0;
                float3 objNormal : TEXCOORD1;
            };

            struct g2f
            {
                float4 pos         : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos    : TEXCOORD1;
                float  shell       : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            float hash13(float3 p3)
            {
                p3 = frac(p3 * 0.1031);
                p3 += dot(p3, p3.zyx + 31.32);
                return frac((p3.x + p3.y) * p3.z);
            }

            float vnoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = hash13(i + float3(0, 0, 0));
                float n100 = hash13(i + float3(1, 0, 0));
                float n010 = hash13(i + float3(0, 1, 0));
                float n110 = hash13(i + float3(1, 1, 0));
                float n001 = hash13(i + float3(0, 0, 1));
                float n101 = hash13(i + float3(1, 0, 1));
                float n011 = hash13(i + float3(0, 1, 1));
                float n111 = hash13(i + float3(1, 1, 1));

                return lerp(
                    lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                    lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y),
                    f.z);
            }

            // 3 octaves; renormalised so the result spans roughly 0..1 around a mean of 0.5
            float fbm(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                [unroll]
                for (int k = 0; k < 3; k++)
                {
                    v += a * vnoise(p);
                    p *= 2.03;
                    a *= 0.5;
                }
                return v * 1.142857; // 1 / 0.875
            }

            v2g vert(appdata v)
            {
                v2g o;
                o.objPos = v.vertex;
                o.objNormal = v.normal;
                return o;
            }

            // Emit one copy of the triangle per fur shell, pushed out along the normal.
            // D3D caps (maxvertexcount * scalar components of the output struct) at 1024.
            // g2f is 11 scalars, so 93 vertices is the ceiling: 31 shells * 3 vertices.
            [maxvertexcount(93)]
            void geom(triangle v2g input[3], inout TriangleStream<g2f> stream)
            {
                float shells = clamp(floor(_ShellCount), 1.0, 31.0);
                float last = max(1.0, shells - 1.0);

                for (float s = 0.0; s < shells; s += 1.0)
                {
                    float f = s / last; // 0 at the skin, 1 at the tips

                    g2f o[3];
                    for (int j = 0; j < 3; j++)
                    {
                        float4 wp = mul(unity_ObjectToWorld, float4(input[j].objPos.xyz, 1.0));
                        float3 wn = UnityObjectToWorldNormal(input[j].objNormal);

                        float3 offset = normalize(wn) * (f * _FurLength);
                        offset.y -= f * f * _Gravity * _FurLength; // droop

                        wp.xyz += offset;

                        o[j].pos = mul(UNITY_MATRIX_VP, wp);
                        o[j].worldPos = wp.xyz;
                        o[j].worldNormal = normalize(wn);
                        o[j].shell = f;
                        UNITY_TRANSFER_FOG(o[j], o[j].pos);
                    }

                    stream.Append(o[0]);
                    stream.Append(o[1]);
                    stream.Append(o[2]);
                    stream.RestartStrip();
                }
            }

            fixed4 frag(g2f i) : SV_Target
            {
                // Per-pixel strand mask: shells thin out toward the tip so the
                // silhouette breaks up into fur instead of smooth layers.
                float strand = fbm(i.worldPos * _StrandScale);
                float threshold = i.shell * _Density;
                clip(strand - threshold);

                // Jitter the shading normal per strand so the fuzz reads as soft.
                float3 cell = floor(i.worldPos * _StrandScale);
                float3 jitter = float3(
                    hash13(cell),
                    hash13(cell + 19.19),
                    hash13(cell + 53.53)) * 2.0 - 1.0;
                float3 n = normalize(i.worldNormal + jitter * _NormalJitter);

                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float ndl = saturate(dot(n, lightDir));
                float3 ambient = ShadeSH9(float4(n, 1.0));

                // Deeper shells sit in their own shadow.
                float ao = lerp(1.0 - _BaseOcclusion, 1.0, i.shell);
                float3 albedo = lerp(_BaseColor.rgb, _TipColor.rgb, i.shell);

                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float rim = pow(1.0 - saturate(dot(n, viewDir)), 2.0);

                float3 col = albedo * (_LightColor0.rgb * ndl * ao + ambient * ao);
                col += _TipColor.rgb * rim * _RimBoost * i.shell;

                UNITY_APPLY_FOG(i.fogCoord, col);
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }

    Fallback "Diffuse"
}
