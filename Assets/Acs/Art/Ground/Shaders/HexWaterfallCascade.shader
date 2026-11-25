Shader "LumiWorld/HexWaterfallCascade"
{
    Properties
    {
        [Header(Original Water Repo Textures)]
        _FoamTex("Fine Bubble Spray Mask (R)", 2D) = "black" {}
        _CausticsTex("Crisp Water Caustics (RGB)", 2D) = "black" {}
        _NormalMap("Water Normal Map", 2D) = "bump" {}
        _NoiseTex("Flow Breakup Noise", 2D) = "gray" {}

        [Header(Crystal Clear Refraction and Absorption)]
        _RefractionStrength("Screen Refraction Distortion", Range(0.0, 0.05)) = 0.018
        _NormalStrength("Liquid Ripple Strength", Range(0.05, 1.5)) = 0.74
        _WetRockDarken("Wet Stone Under-Water Darken", Range(0.5, 1.0)) = 0.80
        _WaterLightColor("Crystal Shallow Tint (RGBA)", Color) = (0.28, 0.88, 0.94, 0.24)
        _WaterDeepColor("Crystal Core Tint (RGBA)", Color) = (0.04, 0.48, 0.72, 0.46)
        _CausticStrength("Refracted Caustic Glow", Range(0.0, 2.0)) = 1.05

        [Header(Sky Fresnel and Sun Sparkles)]
        _FresnelColor("Sky Rim Reflection Color", Color) = (0.72, 0.96, 1.0, 1.0)
        _FresnelPower("Fresnel Sharpness", Range(1.0, 6.0)) = 2.4
        _FresnelStrength("Fresnel Reflection Strength", Range(0.0, 1.0)) = 0.54
        _SunSheen("Sun Sparkle Glint Intensity", Range(0.0, 4.0)) = 2.50
        _SunGloss("Specular Highlight Sharpness", Range(16.0, 256.0)) = 96.0
        _SparkleThreshold("Micro-Glint Cutoff", Range(0.80, 0.99)) = 0.910

        [Header(Silky Vertical Streams and Aerated Spray)]
        _FoamColor("Crisp Whitewater Color", Color) = (0.98, 0.995, 1.0, 0.95)
        _FlowSpeed("Downward Cascade Speed", Float) = 2.55
        _StreakScale("Vertical Filament Tiling (X, Y)", Vector) = (3.6, 0.42, 0, 0)
        _StreakStrength("Silky Vertical Streak Intensity", Range(0.0, 1.0)) = 0.72
        _SprayScale("Fine Bubble Spray Density", Float) = 7.5
        _FoamSharpness("Foam Lace Crispness", Range(0.02, 0.25)) = 0.08
        _RippleSpeed("Basin & Plunge Ripple Speed", Float) = 1.50

        [Header(Legacy Fallback and Mode)]
        _TopFoamBand("Legacy Top Crest Width", Range(0.05, 0.8)) = 0.34
        _BottomSplashBand("Legacy Bottom Splash Width", Range(0.05, 0.8)) = 0.38
        _EdgeFeather("Legacy Edge Softness", Range(0.02, 0.4)) = 0.16
        [Toggle] _UseMeshFlowUV1("Use Mesh UV1 Flow Attributes", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+10"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "HexWaterfallCascadeForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float4 flowData   : TEXCOORD1; // x=foamBoost, y=poolFlag, z=flowSpeed, w=edgeAlpha
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 positionOS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 tangentWS   : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float2 uv          : TEXCOORD5;
                float4 flowData    : TEXCOORD6;
                float4 screenPos   : TEXCOORD7;
                float4 shadowCoord : TEXCOORD8;
                float  fogFactor   : TEXCOORD9;
            };

            TEXTURE2D(_FoamTex);
            SAMPLER(sampler_FoamTex);
            TEXTURE2D(_CausticsTex);
            SAMPLER(sampler_CausticsTex);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _FoamTex_ST;
                float4 _CausticsTex_ST;
                float4 _NormalMap_ST;
                float4 _NoiseTex_ST;
                float _RefractionStrength;
                float _NormalStrength;
                float _WetRockDarken;
                half4 _WaterLightColor;
                half4 _WaterDeepColor;
                float _CausticStrength;
                half4 _FresnelColor;
                float _FresnelPower;
                float _FresnelStrength;
                float _SunSheen;
                float _SunGloss;
                float _SparkleThreshold;
                half4 _FoamColor;
                float _FlowSpeed;
                float4 _StreakScale;
                float _StreakStrength;
                float _SprayScale;
                float _FoamSharpness;
                float _RippleSpeed;
                float _TopFoamBand;
                float _BottomSplashBand;
                float _EdgeFeather;
                float _UseMeshFlowUV1;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posOS = IN.positionOS.xyz;

                // Lively rushing-water surface undulation (anchored cleanly at crevice & outer rock seams)
                float useUV1 = step(0.5, _UseMeshFlowUV1);
                float edgeAnchor = lerp(
                    sin(saturate(IN.uv.y / 2.2) * 3.14159265),
                    saturate(IN.flowData.w) * (1.0 - saturate(IN.flowData.y) * 0.78),
                    useUV1);
                // Positive +_Time.y * speed moves wave crests downward from high uv.y toward uv.y = 0
                float w1 = sin(IN.uv.y * 10.5 + _Time.y * 9.5 + IN.uv.x * 6.28318);
                float w2 = cos(IN.uv.y * 18.0 + _Time.y * 14.5 - IN.uv.x * 12.56636);
                posOS += IN.normalOS * ((w1 * 0.62 + w2 * 0.38) * 0.0095 * edgeAnchor);

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.positionOS = posOS;
                OUT.normalWS = normInputs.normalWS;
                OUT.tangentWS = normInputs.tangentWS;
                OUT.bitangentWS = normInputs.bitangentWS;
                OUT.uv = IN.uv;
                OUT.flowData = IN.flowData;
                OUT.screenPos = ComputeScreenPos(posInputs.positionCS);
                OUT.shadowCoord = GetShadowCoord(posInputs);
                OUT.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float useUV1 = step(0.5, _UseMeshFlowUV1);
                float u = saturate(IN.uv.x);
                float vArc = IN.uv.y;

                float meshFoam = saturate(IN.flowData.x);
                float poolFlag = saturate(IN.flowData.y) * useUV1;
                float speedMul = lerp(1.0, max(0.32, IN.flowData.z), useUV1);
                float meshAlpha = saturate(IN.flowData.w);

                // --- 1. Fast Multi-Speed Liquid Normals (UberStylizedWater Normal 1.png) ---
                float flowT = _Time.y * _FlowSpeed * speedMul;
                float rippleT = _Time.y * _RippleSpeed;

                // Fine vertically-elongated normals rushing downward at 3 parallax speeds!
                float2 nFallUV1 = float2(u * 4.2, vArc * 1.35 + flowT * 1.00);
                float2 nFallUV2 = float2(u * 6.8 + 0.37, vArc * 2.10 + flowT * 1.55);
                float2 nSparkleUV = float2(u * 9.5 - 0.19, vArc * 3.10 + flowT * 2.25);
                half3 nf1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, nFallUV1));
                half3 nf2 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, nFallUV2));
                half3 nf3 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, nSparkleUV));
                half2 fallRipple = (nf1.xy * 0.6h + nf2.xy * 0.4h) * (half)_NormalStrength;

                // Pool/basin concentric swirling normals
                float2 poolDelta = IN.uv - float2(0.5, 0.5);
                float poolRad = length(poolDelta) * 2.0;
                float poolAngle = atan2(poolDelta.y, poolDelta.x) / 6.2831853 + 0.5;
                float2 nPoolUV1 = IN.uv * 3.4 + float2(rippleT * 0.28, -rippleT * 0.36);
                float2 nPoolUV2 = IN.uv * 5.2 + float2(-rippleT * 0.34 + 0.43, rippleT * 0.24 + 0.17);
                half3 np1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, nPoolUV1));
                half3 np2 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, nPoolUV2));
                half2 poolRipple = (np1.xy + np2.xy) * (0.5h * (half)_NormalStrength);

                half2 rippleXY = lerp(fallRipple, poolRipple, (half)poolFlag);
                half2 sparkleXY = lerp((nf1.xy + nf2.xy * 1.1h + nf3.xy * 1.35h), (np1.xy + np2.xy + nf3.xy), (half)poolFlag);

                half3 T = normalize((half3)IN.tangentWS);
                half3 B = normalize((half3)IN.bitangentWS);
                half3 Ngeom = normalize((half3)IN.normalWS);
                half3 N = normalize(Ngeom + T * rippleXY.x + B * rippleXY.y);
                half3 Nsparkle = normalize(Ngeom + T * (sparkleXY.x * 0.62h) + B * (sparkleXY.y * 0.62h));

                // --- 2. Screen-Space Refraction of Wet Rock Behind Water (guarded against arch overhang!) ---
                float refractZone = lerp(1.0, smoothstep(0.03, 0.14, IN.positionOS.y), useUV1);

                float2 screenUV = IN.screenPos.xy / max(1e-5, IN.screenPos.w);
                float surfaceEyeDepth = LinearEyeDepth(IN.positionCS.z, _ZBufferParams);
                // Keep upward screen offset tightly clamped so overhanging dark cave arch never leaks into refracted water
                float2 refractOffset = float2(rippleXY.x, clamp(rippleXY.y * 0.35, -0.45, 0.20)) * (_RefractionStrength * (0.80 + 0.20 * (1.0 - poolFlag)));
                float2 refractedUV = saturate(screenUV + refractOffset);

                float rawSceneDepth = SampleSceneDepth(refractedUV);
                float sceneEyeDepth = LinearEyeDepth(rawSceneDepth, _ZBufferParams);
                if (sceneEyeDepth < surfaceEyeDepth - 0.015)
                {
                    refractedUV = screenUV;
                }
                half3 refractedSceneRGB = SampleSceneColor(refractedUV) * (half)_WetRockDarken;

                // --- 3. Crisp Dancing Caustics (UberStylizedWater Caustic 1.png) ---
                // On vertical falls: stretched vertically into fine luminous liquid light threads!
                // On pools: classic dancing caustic web across the flat wet stone!
                float2 cUV1 = lerp(
                    float2(u * 3.4 + rippleXY.x * 0.12, vArc * 0.55 + flowT * 0.95),
                    IN.uv * 3.0 + float2(rippleT * 0.24, -rippleT * 0.30) + rippleXY * 0.18,
                    poolFlag);
                float2 cUV2 = lerp(
                    float2(u * 5.2 - rippleXY.x * 0.10 + 0.37, vArc * 0.85 + flowT * 1.48 + 0.19),
                    IN.uv * 4.2 + float2(-rippleT * 0.28 + 0.41, rippleT * 0.21 + 0.63) - rippleXY * 0.15,
                    poolFlag);
                half c1 = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, cUV1).r;
                half c2 = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, cUV2).r;
                half causticWeb = saturate(max(c1, c2) * 0.82h + min(c1, c2) * 1.45h);

                // --- 4. URP Lighting, Shadows & Crystal-Aqua Water Absorption ---
                float4 shadowCoord = IN.shadowCoord;
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #endif
                Light mainLight = GetMainLight(shadowCoord);
                half3 lightDir = normalize((half3)mainLight.direction + half3(0.001h, 0.001h, 0.001h));
                half lightLum = dot((half3)mainLight.color, half3(0.299h, 0.587h, 0.114h));
                half3 sunColor = lightLum > 0.05h ? (half3)mainLight.color : half3(1.04h, 0.98h, 0.90h);
                half shadowAtten = lerp(0.58h, 1.0h, (half)mainLight.shadowAttenuation);
                half ndotl = saturate(dot(N, lightDir));

                refractedSceneRGB += sunColor * half3(0.72h, 0.96h, 1.0h) * causticWeb * (half)_CausticStrength * shadowAtten;

                half centerThickness = (half)(smoothstep(0.0, 0.30, u) * (1.0 - smoothstep(0.70, 1.0, u)));
                half thickness = lerp(centerThickness, 0.70h, (half)poolFlag);
                half4 waterTint = lerp(_WaterLightColor, _WaterDeepColor, thickness * 0.72h + causticWeb * 0.22h);
                half3 litTintRGB = waterTint.rgb * lerp(half3(0.78h, 0.90h, 0.97h), half3(1.14h, 1.10h, 1.04h), ndotl * shadowAtten) * sunColor;

                half3 glassWaterRGB = lerp(refractedSceneRGB, litTintRGB, waterTint.a);
                half3 waterRGB = lerp(litTintRGB, glassWaterRGB, (half)refractZone);

                // --- 5. Grazing Sky Fresnel Sheen & Lively Glittering Sun Sparkles ---
                half3 V = normalize((half3)(GetCameraPositionWS() - IN.positionWS));
                half NdotV = saturate(dot(N, V));
                half fresnel = pow(1.0h - NdotV, (half)_FresnelPower) * (half)_FresnelStrength;
                waterRGB = lerp(waterRGB, _FresnelColor.rgb * sunColor * lerp(0.84h, 1.08h, shadowAtten), saturate(fresnel));

                half3 H = normalize(lightDir + V);
                half NdotH = saturate(dot(Nsparkle, H));
                half glintDots = smoothstep((half)_SparkleThreshold, (half)min(0.998, _SparkleThreshold + 0.025), NdotH);
                half specSheen = pow(saturate(dot(N, H)), (half)_SunGloss) * 0.65h;
                waterRGB += (glintDots * (half)_SunSheen + specSheen * (half)(_SunSheen * 0.48)) * sunColor * shadowAtten;

                // --- 6. Silky Fine Vertical Water Filaments (Organic wispy Noise 7 + stretched Caustic 1, ZERO flat white slabs!) ---
                float2 nStreamUV1 = float2(u * _StreakScale.x + rippleXY.x * 0.08, vArc * _StreakScale.y + flowT * 1.15);
                float2 nStreamUV2 = float2(u * (_StreakScale.x * 1.65) + 0.43 - rippleXY.x * 0.06, vArc * (_StreakScale.y * 1.35) + flowT * 1.85);
                half ns1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, nStreamUV1).r;
                half ns2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, nStreamUV2).r;

                // Two-speed wispy vertical filaments (wide soft smoothstep -> translucent silky ribbons, never hard flat patches!)
                half wisp1 = smoothstep(0.32h, 0.88h, ns1);
                half wisp2 = smoothstep(0.38h, 0.90h, ns2);
                // Bright crisp liquid light threads from stretched Caustic 1 rushing down the falling sheet
                half vCausticThreads = smoothstep(0.28h, 0.82h, causticWeb) * 0.72h;
                half streamPulse = (half)(0.72 + 0.28 * sin(u * 22.0 + ns1 * 3.2 - flowT * 2.5));

                half silkyStreams = saturate(wisp1 * 0.68h + wisp2 * 0.58h + vCausticThreads * 0.52h) * streamPulse
                                  * (half)_StreakStrength * lerp(0.35h, 1.0h, centerThickness) * (1.0h - (half)poolFlag * 0.90h);

                // --- 7. Fine Aerated Bubble Spray & Sparkling Pool Lace (High-Frequency Micro-Bubbles ONLY!) ---
                // High frequency (_SprayScale ~ 7.5) broken up by wispy Noise 7 -> tiny sparkling droplets on falls, crisp lace on pools!
                float2 sprayFallUV1 = float2(u * _SprayScale + rippleXY.x * 0.14, vArc * (_SprayScale * 0.45) + flowT * 1.40);
                float2 sprayFallUV2 = float2(u * (_SprayScale * 1.45) + 0.37, vArc * (_SprayScale * 0.68) + flowT * 2.05);
                half sf1 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, sprayFallUV1).r;
                half sf2 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, sprayFallUV2).r;

                float2 sprayPoolUV1 = float2(poolAngle * 4.5 + rippleXY.x * 0.15, poolRad * 2.6 - rippleT * 1.05 + rippleXY.y * 0.15);
                float2 sprayPoolUV2 = IN.uv * 6.2 + float2(rippleT * 0.36, -rippleT * 0.44) + rippleXY * 0.18;
                half sp1 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, sprayPoolUV1).r;
                half sp2 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, sprayPoolUV2).r;
                half poolRingWave = (half)(0.5 + 0.5 * sin(poolRad * 20.0 - rippleT * 7.8 + ns1 * 3.0));

                // Airy misty spray on vertical lips vs crisp frothy bubble lace on middle basin & bottom plunge!
                half fallBubbles = saturate((sf1 * 0.52h + sf2 * 0.48h) * (0.45h + ns1 * 0.75h) + ns2 * 0.22h);
                half poolBubbles = saturate(sp1 * 0.55h + sp2 * 0.48h + poolRingWave * 0.28h);
                half fineLaceNoise = lerp(fallBubbles, poolBubbles, (half)poolFlag);

                half softW = (half)_FoamSharpness;
                half fallSoftW = lerp(softW * 2.1h, softW, (half)poolFlag);
                // On vertical fall (poolFlag=0), cap lip froth to delicate broken spray (never solid flat white curtain!)
                half effectiveFoam = (half)meshFoam * lerp(0.55h, 1.0h, (half)poolFlag);
                half foamCut = lerp(0.95h, 0.36h, effectiveFoam);
                half sprayLace = smoothstep(foamCut - fallSoftW, foamCut + fallSoftW, fineLaceNoise);

                // Sparkling aerated mist accent right at peak impact (middle pool splash & bottom plunge ONLY!)
                half impactSparkleFroth = (half)poolFlag * smoothstep(0.68h, 0.96h, (half)meshFoam * (0.70h + ns2 * 0.45h))
                                        * smoothstep(0.24h, 0.58h, poolBubbles + 0.16h);
                // Delicate shimmering concentric ripples across calm middle pool
                half basinRipples = (half)poolFlag * smoothstep(0.72h - softW, 0.72h + softW, sp1 * 0.65h + poolRingWave * 0.38h) * 0.58h;

                half uv1Whitewater = saturate(max(max(silkyStreams, sprayLace * 0.88h), max(impactSparkleFroth, basinRipples)));

                // Legacy UV0 fallback
                float vNormLegacy = saturate(vArc / 2.2);
                half topBand = (half)smoothstep(1.0 - _TopFoamBand * 0.72, 0.97, vNormLegacy);
                half botBand = (half)(1.0 - smoothstep(0.0, max(0.01, _BottomSplashBand), vNormLegacy));
                half legacyCut = lerp(0.92h, 0.38h, max(topBand, botBand));
                half legacyFoam = saturate(max(silkyStreams, smoothstep(legacyCut - softW, legacyCut + softW, fineLaceNoise)));

                half totalFoam = saturate(lerp(legacyFoam, uv1Whitewater, (half)useUV1) * _FoamColor.a);
                half3 foamLit = _FoamColor.rgb * lerp(half3(0.90h, 0.96h, 1.0h), half3(1.08h, 1.07h, 1.04h), ndotl * shadowAtten);

                half3 finalRGB = lerp(waterRGB, foamLit, totalFoam);
                finalRGB = MixFog(finalRGB, IN.fogFactor);

                // --- 8. Output Alpha ---
                half sideAlphaLegacy = (half)(smoothstep(0.0, _EdgeFeather, u) * (1.0 - smoothstep(1.0 - _EdgeFeather, 1.0, u)));
                half tipAlphaLegacy = (half)(smoothstep(0.0, 0.025, vNormLegacy) * (1.0 - smoothstep(0.96, 1.0, vNormLegacy)));
                half geomAlpha = lerp(sideAlphaLegacy * tipAlphaLegacy, (half)meshAlpha, (half)useUV1);

                half splashSurfaceAlpha = saturate(max(waterTint.a * 0.62h, totalFoam * 0.95h));
                half surfaceAlpha = lerp(splashSurfaceAlpha, 1.0h, (half)refractZone);
                half finalAlpha = saturate(geomAlpha * surfaceAlpha);

                clip(finalAlpha - 0.01h);
                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
