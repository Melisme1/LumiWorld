Shader "LumiWorld/Environment/CozyWorldCloudSea"
{
    Properties
    {
        _TopColor ("Sunlit Cloud Top", Color) = (0.988, 0.978, 0.948, 1.0)
        _ShadowColor ("Cloud Belly Shadow", Color) = (0.68, 0.83, 0.94, 1.0)
        _RimColor ("Warm Sun Rim", Color) = (1.0, 0.96, 0.85, 1.0)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.22

        _NoiseScale ("Cloud Scale", Float) = 0.068
        _DriftSpeed ("Drift Speed (XZ)", Vector) = (0.14, 0.05, 0, 0)
        _BillowHeight ("Billow Height", Range(0, 3)) = 1.15

        _EdgeSoftness ("Edge Softness", Range(0.08, 0.65)) = 0.30
        _EdgeNoiseStrength ("Edge Scallop Strength", Range(0, 0.65)) = 0.46
        _OverallOpacity ("Overall Opacity", Range(0, 1)) = 0.98
        _WorldFadeStart ("World Fade Start", Float) = 155.0
        _WorldFadeEnd ("World Fade End", Float) = 200.0

        _ClearZoneCount ("Clear Zone Count", Float) = 0.0
        _ClearZone0 ("Clear Zone 0", Vector) = (0, 0, 0, 0)
        _ClearZone1 ("Clear Zone 1", Vector) = (0, 0, 0, 0)
        _ClearZone2 ("Clear Zone 2", Vector) = (0, 0, 0, 0)
        _ClearZone3 ("Clear Zone 3", Vector) = (0, 0, 0, 0)
        _ClearZone4 ("Clear Zone 4", Vector) = (0, 0, 0, 0)
        _ClearZone5 ("Clear Zone 5", Vector) = (0, 0, 0, 0)
        _ClearZone6 ("Clear Zone 6", Vector) = (0, 0, 0, 0)
        _ClearZone7 ("Clear Zone 7", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "CozyWorldCloudSeaForward"
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
                float _EdgeSoftness;
                float _EdgeNoiseStrength;
                float _OverallOpacity;
                float _WorldFadeStart;
                float _WorldFadeEnd;
                float _ClearZoneCount;
                float4 _ClearZone0;
                float4 _ClearZone1;
                float4 _ClearZone2;
                float4 _ClearZone3;
                float4 _ClearZone4;
                float4 _ClearZone5;
                float4 _ClearZone6;
                float4 _ClearZone7;
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
                        float h = saturate(0.5 + 0.5 * (minD - d) / 0.38);
                        minD = lerp(minD, d, h) - 0.38 * h * (1.0 - h);
                    }
                }
                return saturate(1.0 - minD * 1.12);
            }

            float BillowField(float2 worldXZ, float t)
            {
                float2 drift = _DriftSpeed.xy * t;
                float2 p1 = worldXZ * (_NoiseScale * 1.25) + drift;
                float2 p2 = worldXZ * (_NoiseScale * 2.15) - drift * 0.60 + float2(7.1, 3.4);

                float pillow = CellularBillow(p1);
                float detail = CloudFBM(p2);
                return saturate(pillow * 0.65 + detail * 0.45 - 0.08);
            }

            float CombineZone(float coverage, float4 zone, float2 xz, float edgeSignal)
            {
                float openT = saturate(zone.w);
                if (openT <= 0.001 || zone.z <= 0.01) return coverage;

                float d = length(xz - zone.xy);
                float targetR = zone.z * openT;

                // Cap shoreline scallop amplitude cleanly so large island boundaries stay crisp and never encroach on outer hexes
                float scallopRadius = min(12.0, max(6.5, zone.z * 0.42));
                float perturb = (edgeSignal - 0.46) * _EdgeNoiseStrength * scallopRadius;
                float softBand = max(2.8, zone.z * _EdgeSoftness * (0.45 + 0.55 * openT));

                float innerEdge = max(0.0, targetR - softBand * 0.48);
                float outerEdge = max(innerEdge + 0.15, targetR + softBand * 0.85);
                float zoneCov = smoothstep(innerEdge, outerEdge, d + perturb);

                float k = 0.14;
                float h = saturate(0.5 + 0.5 * (coverage - zoneCov) / k);
                return saturate(lerp(coverage, zoneCov, h) - k * h * (1.0 - h));
            }

            float EvaluateClearMask(float2 xz, float edgeSignal)
            {
                float cov = 1.0;
                int count = (int)_ClearZoneCount;
                if (count > 0) cov = CombineZone(cov, _ClearZone0, xz, edgeSignal);
                if (count > 1) cov = CombineZone(cov, _ClearZone1, xz, edgeSignal);
                if (count > 2) cov = CombineZone(cov, _ClearZone2, xz, edgeSignal);
                if (count > 3) cov = CombineZone(cov, _ClearZone3, xz, edgeSignal);
                if (count > 4) cov = CombineZone(cov, _ClearZone4, xz, edgeSignal);
                if (count > 5) cov = CombineZone(cov, _ClearZone5, xz, edgeSignal);
                if (count > 6) cov = CombineZone(cov, _ClearZone6, xz, edgeSignal);
                if (count > 7) cov = CombineZone(cov, _ClearZone7, xz, edgeSignal);
                return cov;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posOS = input.positionOS.xyz;
                float3 preWorld = TransformObjectToWorld(posOS);
                float billow = BillowField(preWorld.xz, _Time.y);
                float cov = EvaluateClearMask(preWorld.xz, billow);

                posOS.y += (billow * _BillowHeight - (1.0 - cov) * 0.55) * cov;

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

                const float eps = 0.55;
                float hC = BillowField(xz, t);
                float hX = BillowField(xz + float2(eps, 0.0), t);
                float hZ = BillowField(xz + float2(0.0, eps), t);

                float slopeScale = 1.45 * (0.35 + 0.65 * _BillowHeight);
                float3 procNormal = normalize(float3((hC - hX) * slopeScale / eps, 1.0, (hC - hZ) * slopeScale / eps));
                float3 geomNormal = normalize(input.normalWS);
                float3 N = normalize(lerp(geomNormal, procNormal, 0.80));

                Light mainLight = GetMainLight();
                float3 V = GetWorldSpaceNormalizeViewDir(input.worldPos);
                float ndl = saturate(dot(N, mainLight.direction) * 0.56 + 0.48);

                float shadeFactor = smoothstep(0.18, 0.88, saturate(ndl * 0.54 + hC * 0.56));
                half3 col = lerp(_ShadowColor.rgb, _TopColor.rgb, shadeFactor);

                float fresnel = 1.0 - saturate(dot(V, N));
                float rim = pow(fresnel, 2.2) * saturate(0.35 + 0.65 * hC);
                col = lerp(col, _RimColor.rgb, saturate(rim * _RimStrength));

                float lobeMacro = CellularBillow(xz * (_NoiseScale * 0.85) - _DriftSpeed.xy * (t * 0.40) + float2(4.2, 8.9));
                float lobeFbm = CloudFBM(xz * (_NoiseScale * 1.40) + _DriftSpeed.xy * (t * 0.30));
                float edgeSignal = lerp(lobeFbm, lobeMacro, 0.68);

                float cov = EvaluateClearMask(xz, edgeSignal);

                float distLocal = length(input.localXZ);
                float fadeEnd = max(_WorldFadeStart + 1.0, _WorldFadeEnd);
                float outerWorldFade = 1.0 - smoothstep(_WorldFadeStart, fadeEnd, distLocal);

                float alpha = saturate(cov * outerWorldFade * _OverallOpacity);

                col = MixFog(col, input.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
