Shader "DSH/Dreamy Skybox"
{
    Properties
    {
        [Header(Gradient)]
        _ZenithColor ("Zenith Color (violet)", Color) = (0.10, 0.07, 0.30, 1)
        _MidColor ("Mid Sky Color (teal)", Color) = (0.10, 0.42, 0.55, 1)
        _HorizonColor ("Horizon Color (pink)", Color) = (0.85, 0.42, 0.55, 1)
        _GroundColor ("Below Horizon Color", Color) = (0.04, 0.05, 0.12, 1)
        _SkyBrightness ("Sky Brightness", Range(0.0, 2.0)) = 1.0

        [Header(Dreamy Aurora)]
        _Saturation ("Rainbow Saturation", Range(0.0, 1.0)) = 0.65
        _HueShift ("Hue Offset", Range(0.0, 1.0)) = 0.55
        _HueFlow ("Hue Flow Speed", Range(-1.0, 1.0)) = 0.035
        _HueSpreadY ("Hue Spread (vertical)", Range(0.0, 4.0)) = 1.2
        _HueSpreadX ("Hue Spread (horizontal)", Range(0.0, 4.0)) = 0.5
        _CloudScale ("Nebula Scale", Range(0.5, 12.0)) = 3.2
        _BandScale ("Aurora Band Scale", Range(0.5, 16.0)) = 4.5
        _BandThreshold ("Aurora Coverage", Range(0.0, 1.0)) = 0.62
        _FlowSpeed ("Drift Speed", Range(0.0, 0.6)) = 0.02
        _NebulaStrength ("Nebula Strength", Range(0.0, 2.0)) = 0.45
        _AuroraStrength ("Aurora Strength", Range(0.0, 2.0)) = 0.65
        _HorizonGlow ("Horizon Glow", Range(0.0, 2.0)) = 0.35

        [Header(Stars)]
        _StarDensity ("Star Density", Range(20.0, 400.0)) = 150.0
        _StarThreshold ("Star Threshold", Range(0.9, 0.9999)) = 0.9955
        _StarBrightness ("Star Brightness", Range(0.0, 3.0)) = 1.1
        _TwinkleSpeed ("Twinkle Speed", Range(0.0, 8.0)) = 2.5
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            fixed4 _ZenithColor;
            fixed4 _MidColor;
            fixed4 _HorizonColor;
            fixed4 _GroundColor;
            float _SkyBrightness;

            float _Saturation;
            float _HueShift;
            float _HueFlow;
            float _HueSpreadY;
            float _HueSpreadX;
            float _CloudScale;
            float _BandScale;
            float _BandThreshold;
            float _FlowSpeed;
            float _NebulaStrength;
            float _AuroraStrength;
            float _HorizonGlow;

            float _StarDensity;
            float _StarThreshold;
            float _StarBrightness;
            float _TwinkleSpeed;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
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

            float fbm(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    v += a * vnoise(p);
                    p *= 2.03;
                    a *= 0.5;
                }
                return v * 1.0666667; // 1 / 0.9375
            }

            float3 hsv2rgb(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz; // the skybox mesh carries directions
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float h = dir.y;

                // ---- three-stop gradient: violet zenith -> teal mid -> pink horizon.
                //      Three separate hue families are what makes it read as dreamy
                //      rather than a single flat wash.
                float t = saturate(h);
                float3 sky = lerp(_HorizonColor.rgb, _MidColor.rgb, smoothstep(0.0, 0.42, t));
                sky = lerp(sky, _ZenithColor.rgb, smoothstep(0.32, 1.0, t));
                float3 col = lerp(_GroundColor.rgb, sky * _SkyBrightness, smoothstep(-0.18, 0.06, h));

                // Only the sky half carries aurora / stars.
                float above = smoothstep(-0.02, 0.22, h);

                // ---- drifting nebula ----
                float3 np = dir * _CloudScale;
                np.x += _Time.y * _FlowSpeed;
                np.z += _Time.y * _FlowSpeed * 0.7;
                float nebula = saturate(fbm(np) * 1.15 - 0.15);

                // ---- aurora ribbons: noise stretched vertically makes bands run
                //      horizontally across the sky ----
                float3 bp = float3(dir.x * _BandScale + _Time.y * _FlowSpeed * 2.0,
                                   h * _BandScale * 0.22,
                                   dir.z * _BandScale + _Time.y * _FlowSpeed * 1.1);
                float band = smoothstep(_BandThreshold, 0.97, fbm(bp));

                // Same hue recipe as DSH/Rainbow Fur, so sky and cube share a palette.
                float hue = frac(_HueShift
                                 + _Time.y * _HueFlow
                                 + h * _HueSpreadY
                                 + dir.x * _HueSpreadX
                                 + nebula * 0.40
                                 + band * 0.18);
                float3 rainbow = hsv2rgb(float3(hue, _Saturation, 1.0));

                col += rainbow * nebula * _NebulaStrength * above;
                col += rainbow * band * _AuroraStrength * above;

                // soft glow hugging the horizon line
                col += _HorizonColor.rgb * _HorizonGlow * exp(-abs(h) * 9.0);

                // ---- stars, twinkling ----
                float3 sp = floor(dir * _StarDensity);
                float sr = hash13(sp);
                float star = smoothstep(_StarThreshold, 1.0, sr) * above;
                float twinkle = 0.55 + 0.45 * sin(_Time.y * _TwinkleSpeed + sr * 90.0);
                float3 starTint = hsv2rgb(float3(frac(sr * 7.13 + _HueShift), 0.35, 1.0));
                col += starTint * star * twinkle * _StarBrightness;

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
