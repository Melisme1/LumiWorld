Shader "LumiWorld/Environment/CozyCloudBlanket"
{
    Properties
    {
        _TopColor ("Sunlit Cloud Top", Color) = (0.985, 0.976, 0.945, 1.0)
        _ShadowColor ("Cloud Belly Shadow", Color) = (0.58, 0.76, 0.91, 1.0)
        _RimColor ("Warm Sun Rim", Color) = (1.0, 0.95, 0.82, 1.0)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.28

        _NoiseScale ("Cloud Scale", Float) = 0.14
        _DriftSpeed ("Drift Speed (XZ)", Vector) = (0.14, 0.05, 0, 0)
        _BillowHeight ("Billow Height", Range(0, 3)) = 1.25

        _CoreRadius ("Solid Core Radius", Range(0.2, 0.9)) = 0.52
        _EdgeSoftness ("Edge Softness", Range(0.05, 0.5)) = 0.28
        _EdgeNoiseStrength ("Edge Scallop Strength", Range(0, 0.45)) = 0.34
        _OverallOpacity ("Overall Opacity", Range(0, 1)) = 1.0
        _Dissolve ("Unlock Dissolve", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "CozyCloudBlanketForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos   : TEXCOORD0;
                float2 localXZ    : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _ShadowColor;
                half4 _RimColor;
                float _RimStrength;
                float _NoiseScale;
                float4 _DriftSpeed;
                float _BillowHeight;
                float _CoreRadius;
                float _EdgeSoftness;
                float _EdgeNoiseStrength;
                float _OverallOpacity;
                float _Dissolve;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 Hash22(float2 p)
            {
                float n = Hash21(p);
                return float2(n, Hash21(p + n * 17.3 + 11.7));
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float CloudFBM(float2 p)
            {
                float sum = 0.0;
                float amp = 0.55;
                float2 shift = float2(19.7, 37.3);
                float2x2 rot = float2x2(0.8, -0.6, 0.6, 0.8);
                for (int i = 0; i < 3; i++)
                {
                    sum += amp * ValueNoise(p);
                    p = mul(rot, p * 2.03) + shift;
                    amp *= 0.48;
                }
                return sum;
            }

            // Smooth cellular cumulus dome height (creates distinct round pillow clouds & soft blue valleys)
            float CellularBillow(float2 p)
            {
                float2 ip = floor(p);
                float2 fp = frac(p);
                float minD = 1.6;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        float2 cell = ip + float2(dx, dy);
                        float2 rnd = Hash22(cell) * 0.68 + 0.16;
                        float2 diff = float2(dx, dy) + rnd - fp;
                        float d = length(diff);
                        // Smooth polynomial min for soft organic pillow domes
                        float h = saturate(0.5 + 0.5 * (minD - d) / 0.35);
                        minD = lerp(minD, d, h) - 0.35 * h * (1.0 - h);
                    }
                }
                return saturate(1.0 - minD * 1.15);
            }

            float BillowField(float2 worldXZ, float t)
            {
                float2 drift = _DriftSpeed.xy * t;
                float2 p1 = worldXZ * (_NoiseScale * 1.35) + drift;
                float2 p2 = worldXZ * (_NoiseScale * 2.25) - drift * 0.65 + float2(7.1, 3.4);

                float pillow = CellularBillow(p1);
                float detail = CloudFBM(p2);
                return saturate(pillow * 0.68 + detail * 0.42 - 0.08);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posOS = input.positionOS.xyz;
                float rNorm = length(posOS.xz);

                float3 preWorld = TransformObjectToWorld(posOS);
                float billow = BillowField(preWorld.xz, _Time.y);
                float edgeAtten = 1.0 - smoothstep(0.48, 0.98, rNorm);
                posOS.y += billow * _BillowHeight * edgeAtten * (1.0 - _Dissolve * 0.75);

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.worldPos = posInputs.positionWS;
                output.localXZ = input.positionOS.xz;
                output.normalWS = normInputs.normalWS;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float t = _Time.y;
                float2 xz = input.worldPos.xz;
                float rNorm = length(input.localXZ);

                // Finite-difference normal over cellular billow field for sculpted pillow shading
                const float eps = 0.42;
                float hC = BillowField(xz, t);
                float hX = BillowField(xz + float2(eps, 0.0), t);
                float hZ = BillowField(xz + float2(0.0, eps), t);

                float slopeScale = 1.55 * (0.35 + 0.65 * _BillowHeight);
                float3 procNormal = normalize(float3((hC - hX) * slopeScale / eps, 1.0, (hC - hZ) * slopeScale / eps));
                float3 geomNormal = normalize(input.normalWS);
                float3 N = normalize(lerp(geomNormal, procNormal, 0.82));

                Light mainLight = GetMainLight();
                float3 V = GetWorldSpaceNormalizeViewDir(input.worldPos);
                float ndl = saturate(dot(N, mainLight.direction) * 0.58 + 0.46);

                // Controlled contrast so shadow troughs stay pastel sky-blue and crests stay warm cream (never blown-out #FFF)
                float shadeFactor = smoothstep(0.18, 0.88, saturate(ndl * 0.56 + hC * 0.54));
                half3 col = lerp(_ShadowColor.rgb, _TopColor.rgb, shadeFactor);

                // Soft warm rim blend (capped inside 0..1 to preserve detail)
                float fresnel = 1.0 - saturate(dot(V, N));
                float rim = pow(fresnel, 2.2) * saturate(0.35 + 0.65 * hC);
                col = lerp(col, _RimColor.rgb, saturate(rim * _RimStrength));

                // Multi-lobe scalloped silhouette mimicking overlapping cloud puffs along shoreline
                float lobeMacro = CellularBillow(xz * (_NoiseScale * 0.95) - _DriftSpeed.xy * (t * 0.45) + float2(4.2, 8.9));
                float lobeFbm = CloudFBM(xz * (_NoiseScale * 1.45) + _DriftSpeed.xy * (t * 0.35));
                float edgeSignal = lerp(lobeFbm, lobeMacro, 0.65);

                float rPerturbed = rNorm - (edgeSignal - 0.42) * _EdgeNoiseStrength;
                float outerEnd = min(0.99, _CoreRadius + _EdgeSoftness);
                float outerMask = 1.0 - smoothstep(_CoreRadius, outerEnd, rPerturbed);

                // Solid opaque core hides 100% of underlying island when Dissolve == 0
                float alpha = saturate(outerMask * _OverallOpacity);

                // Center-outward radial unlock dissolve
                if (_Dissolve > 0.001)
                {
                    float dissolveRadius = _Dissolve * 1.15;
                    float centerClear = smoothstep(dissolveRadius - 0.22, dissolveRadius + 0.14, rNorm + edgeSignal * 0.28);
                    float fadeOut = 1.0 - smoothstep(0.70, 1.0, _Dissolve);
                    alpha *= centerClear * fadeOut;
                }

                col = MixFog(col, input.fogFactor);
                return half4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
