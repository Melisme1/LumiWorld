Shader "LumiWorld/WetIslandWater"
{
    Properties
    {
        [Header(Water Mask and Shoreline Feather)]
        _WaterMask("Water Slope Mask (R)", 2D) = "white" {}
        _WaterThreshold("Waterline Mask Value", Range(0.1, 0.8)) = 0.34
        _EdgeFeather("Shoreline Alpha Feather", Range(0.005, 0.08)) = 0.026

        [Header(Normal Map and Optical Refraction)]
        _NormalMap("Water Normal Map", 2D) = "bump" {}
        _NormalScale("Normal Map Tiling", Float) = 0.48
        _NormalStrength("Wave Normal Strength", Range(0.05, 1.5)) = 0.42
        _RefractionStrength("Screen Refraction Distortion", Range(0.0, 0.08)) = 0.022

        [Header(Uber Stylized Crystal Clear Depth Palette)]
        _ShallowColor("Shallow Water Tint (RGBA)", Color) = (0.45, 0.86, 0.94, 0.14)
        _MidColor("Mid Depth Tint (RGBA)", Color) = (0.24, 0.68, 0.90, 0.46)
        _DeepColor("Deep Core Color (RGBA)", Color) = (0.14, 0.42, 0.78, 0.74)
        _DepthCurve("Depth Absorption Curve", Range(0.4, 3.0)) = 1.35

        [Header(Sky and Cloud Fresnel Reflection)]
        _FresnelColor("Sky Reflection Color (RGB)", Color) = (0.46, 0.76, 0.98, 1.0)
        _CloudColor("Reflected Cloud Tint (RGB)", Color) = (0.94, 0.96, 1.0, 1.0)
        _FresnelPower("Fresnel Falloff Power", Range(1.0, 6.0)) = 2.6
        _FresnelStrength("Sky Reflection Strength", Range(0.0, 1.0)) = 0.52
        _CloudReflectionStrength("Cloud Reflection Strength", Range(0.0, 0.6)) = 0.22

        [Header(Texture Caustics and Intersection Edge Foam)]
        _CausticsTex("Water Caustics Texture (RGB)", 2D) = "black" {}
        _CausticStrength("Underwater Caustic Strength", Range(0.0, 1.0)) = 0.38
        _CausticScale("Caustic Tiling", Float) = 0.62
        _NoiseTex("Foam Breakup Noise", 2D) = "white" {}
        _FoamColor("Edge Intersection Foam (RGBA)", Color) = (0.98, 0.99, 1.0, 0.82)
        _FoamBandWidth("Shore Edge Foam Width", Range(0.005, 0.12)) = 0.028
        _ObjectFoamDistance("Rock Intersection Foam Depth", Range(0.02, 0.5)) = 0.16
        _FlowParams("Flow (X:DirX, Y:DirZ, Z:Scale, W:Speed)", Vector) = (0.78, -0.62, 0.48, 0.08)
        _StreamRibbonStrength("Subtle Stream Current", Range(0.0, 0.5)) = 0.0

        [Header(Uber Stylized Glittering Sun Sparkles)]
        _ShadowColor("Shadowed Water Tint", Color) = (0.12, 0.28, 0.52, 1.0)
        _ShadowStrength("Shadow Influence", Range(0.0, 1.0)) = 0.32
        _SunSheen("Sun Sparkle HDR Intensity", Range(0.0, 4.0)) = 2.2
        _SunGloss("Sun Sparkle Sharpness", Range(32.0, 512.0)) = 220.0
        _SparkleThreshold("Sparkle Cutoff Threshold", Range(0.85, 0.995)) = 0.955
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+10"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "WetIslandWaterForward"
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
                float2 uvTop      : TEXCOORD0;
                float2 uvMask     : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uvTop       : TEXCOORD2;
                float2 uvMask      : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                float  fogFactor   : TEXCOORD5;
                float4 screenPos   : TEXCOORD6;
            };

            TEXTURE2D(_WaterMask);
            SAMPLER(sampler_WaterMask);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_CausticsTex);
            SAMPLER(sampler_CausticsTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float _WaterThreshold;
                float _EdgeFeather;

                float _NormalScale;
                float _NormalStrength;
                float _RefractionStrength;

                half4 _ShallowColor;
                half4 _MidColor;
                half4 _DeepColor;
                float _DepthCurve;

                half4 _FresnelColor;
                half4 _CloudColor;
                float _FresnelPower;
                float _FresnelStrength;
                float _CloudReflectionStrength;

                float _CausticStrength;
                float _CausticScale;
                half4 _FoamColor;
                float _FoamBandWidth;
                float _ObjectFoamDistance;
                float4 _FlowParams;
                float _StreamRibbonStrength;

                half4 _ShadowColor;
                float _ShadowStrength;
                float _SunSheen;
                float _SunGloss;
                float _SparkleThreshold;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 posOS = input.positionOS.xyz;

                // Subtle gentle vertical wave breathing
                float wave = sin(_Time.y * 1.8 + posOS.x * 2.4 + posOS.z * 2.0) * 0.0025;
                posOS.y += wave;

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                o.positionCS = posInputs.positionCS;
                o.positionWS = posInputs.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uvTop = input.uvTop;
                o.uvMask = input.uvMask;
                o.shadowCoord = GetShadowCoord(posInputs);
                o.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                o.screenPos = ComputeScreenPos(posInputs.positionCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 flowDir = normalize(_FlowParams.xy + float2(1e-5, 0.0));
                float2 perpDir = float2(-flowDir.y, flowDir.x);
                float flowSpeed = _FlowParams.w;
                float t = _Time.y * flowSpeed;

                // 1. Dual counter-scrolling Water_Normal.png for smooth organic ripples & sparkles (no white lines!)
                float2 baseUV = float2(dot(i.positionWS.xz, flowDir), dot(i.positionWS.xz, perpDir)) * _NormalScale;
                float2 nUV1 = baseUV + float2(-t * 1.1, t * 0.45);
                float2 nUV2 = baseUV * 1.37 + float2(-t * 0.65 + 0.41, -t * 0.85 + 0.23);
                float2 nUV3 = i.positionWS.xz * (_NormalScale * 2.6) + float2(t * 0.9, -t * 1.1);

                half3 nTex1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, nUV1));
                half3 nTex2 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, nUV2));
                half3 nTex3 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, nUV3));

                half2 rippleXY = (nTex1.xy + nTex2.xy) * (0.5h * (half)_NormalStrength);
                half3 N = normalize(half3(rippleXY.x, 1.0h, rippleXY.y));

                // 2. Sample WaterMask with slight ripple distortion at the shoreline
                float2 maskUVDistort = i.uvMask + rippleXY * 0.004;
                float maskVal = SAMPLE_TEXTURE2D(_WaterMask, sampler_WaterMask, maskUVDistort).r;

                float shoreFeather = smoothstep(_WaterThreshold - _EdgeFeather, _WaterThreshold + _EdgeFeather, maskVal);
                clip(shoreFeather - 0.002);

                float rawMaskDepth = saturate((maskVal - _WaterThreshold) / max(0.08, 1.0 - _WaterThreshold));

                // 3. Real URP Screen-Space Depth & Refraction (_CameraDepthTexture & _CameraOpaqueTexture)
                float2 screenUV = i.screenPos.xy / max(1e-5, i.screenPos.w);
                float surfaceEyeDepth = LinearEyeDepth(i.positionCS.z, _ZBufferParams);

                float2 refractOffset = rippleXY * _RefractionStrength * saturate(rawMaskDepth * 2.5);
                float2 refractedUV = saturate(screenUV + refractOffset);

                float rawSceneDepth = SampleSceneDepth(refractedUV);
                float refractedEyeDepth = LinearEyeDepth(rawSceneDepth, _ZBufferParams);
                float depthDiff = refractedEyeDepth - surfaceEyeDepth;

                // Reject foreground objects above water
                if (depthDiff < 0.0)
                {
                    refractedUV = screenUV;
                    rawSceneDepth = SampleSceneDepth(screenUV);
                    refractedEyeDepth = LinearEyeDepth(rawSceneDepth, _ZBufferParams);
                    depthDiff = max(0.0, refractedEyeDepth - surfaceEyeDepth);
                }

                // Sample actual refracted sand/rock bed underneath the water!
                half3 refractedBedRGB = SampleSceneColor(refractedUV);

                // 4. Main Light & Shadows (calculated early so caustics and water tint react to real shadows!)
                float4 shadowCoord = i.shadowCoord;
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                #endif
                Light mainLight = GetMainLight(shadowCoord);

                half3 lightDir = normalize((half3)mainLight.direction + half3(0.001h, 0.001h, 0.001h));
                half lightLum = dot((half3)mainLight.color, half3(0.299h, 0.587h, 0.114h));
                half3 sunColor = lightLum > 0.05h ? (half3)mainLight.color : half3(1.04h, 0.98h, 0.88h);
                if (lightLum <= 0.05h)
                {
                    lightDir = normalize(half3(-0.45h, 0.78h, -0.42h));
                }
                half shadowAtten = (half)mainLight.shadowAttenuation;

                // 5. Texture Caustics (Water_WaterCaustics.png) refracted onto the underwater bed
                float2 causticUV1 = (i.positionWS.xz + rippleXY * 0.35) * _CausticScale + float2(t * 0.45, t * 0.25);
                float2 causticUV2 = (i.positionWS.xz - rippleXY * 0.28) * (_CausticScale * 1.28) + float2(-t * 0.38 + 0.33, -t * 0.42 + 0.67);
                half3 cSample1 = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, causticUV1).rgb;
                half3 cSample2 = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, causticUV2).rgb;
                half causticTexVal = min(cSample1.r, cSample2.r) * 2.4h;

                float causticDepthEnvelope = smoothstep(0.02, 0.18, rawMaskDepth) * (1.0 - smoothstep(0.55, 0.96, rawMaskDepth));
                refractedBedRGB += sunColor * causticTexVal * (half)(causticDepthEnvelope * _CausticStrength) * shadowAtten;

                // 6. Beer-Lambert Crystal-Clear Depth Absorption (Uber-Stylized-Water style)
                // Combine mask basin depth with real vertical depth so shallow rim is 90%+ clear golden sand!
                float combinedDepth = saturate(lerp(rawMaskDepth, saturate(depthDiff * 1.4), 0.35));
                float depthT = pow(combinedDepth, _DepthCurve);

                half3 tintRGB;
                half tintOpacity;
                if (depthT < 0.45)
                {
                    half k = (half)(depthT / 0.45);
                    tintRGB = lerp(_ShallowColor.rgb, _MidColor.rgb, k);
                    tintOpacity = lerp(_ShallowColor.a, _MidColor.a, k);
                }
                else
                {
                    half k = (half)((depthT - 0.45) / 0.55);
                    tintRGB = lerp(_MidColor.rgb, _DeepColor.rgb, k);
                    tintOpacity = lerp(_MidColor.a, _DeepColor.a, k);
                }

                // Darken water tint inside cast shadows (like the pillar shadows in Uber-Stylized-Water!)
                half3 shadowedTint = lerp(tintRGB, _ShadowColor.rgb, (half)_ShadowStrength);
                tintRGB = lerp(shadowedTint, tintRGB, shadowAtten);

                // Blend refracted sandy bottom with crystal-clear water depth tint
                half3 waterRGB = lerp(refractedBedRGB, tintRGB, tintOpacity);

                // 7. Sky & Painterly Cloud Fresnel Reflection (Grazing angle sky mirror)
                half3 V = normalize((half3)(GetCameraPositionWS() - i.positionWS));
                half NdotV = saturate(dot(N, V));
                half fresnel = pow(1.0h - NdotV, (half)_FresnelPower);

                // Soft reflected clouds in reflection vector direction
                float3 reflDir = reflect(-V, N);
                float2 skyUV = reflDir.xz / max(0.25, reflDir.y + 0.35) * 0.65 + float2(_Time.y * 0.015, 0.0);
                float cloudNoise = ValueNoise(skyUV * 3.2) * 0.65 + ValueNoise(skyUV * 6.8) * 0.35;
                half cloudMask = (half)smoothstep(0.52, 0.78, cloudNoise) * (half)_CloudReflectionStrength;

                half3 skyReflRGB = lerp(_FresnelColor.rgb, _CloudColor.rgb, cloudMask);
                half fresnelFactor = saturate(fresnel * (half)_FresnelStrength * (half)smoothstep(0.04, 0.35, rawMaskDepth));
                waterRGB = lerp(waterRGB, skyReflRGB * lerp(0.75h, 1.05h, shadowAtten), fresnelFactor);

                // 8. Uber-Stylized-Water Glittering Sun Sparkles (Faceted normal-crest glints, ZERO white wave lines!)
                half3 H = normalize(lightDir + V);
                // High-frequency sparkle normal combining 3 scrolling normal octaves
                half2 sparkleXY = (nTex1.xy * 0.9h + nTex2.xy * 0.9h + nTex3.xy * 1.1h);
                half3 N_sparkle = normalize(half3(sparkleXY.x, 1.0h, sparkleXY.y));
                half NdotH = saturate(dot(N_sparkle, H));

                // Primary stepped sun glints (crisp round dots like Uber-Stylized-Water)
                half glintDots = smoothstep((half)_SparkleThreshold, (half)min(0.999, _SparkleThreshold + 0.018), NdotH);
                // Secondary warm specular bloom halo around glints
                half specHalo = pow(saturate(dot(N, H)), (half)_SunGloss) * 0.45h;

                half sparkleEnvelope = (half)smoothstep(0.03, 0.14, rawMaskDepth) * shadowAtten;
                half3 sunSparkles = (glintDots * (half)_SunSheen + specHalo * (half)(_SunSheen * 0.4)) * sunColor * sparkleEnvelope;
                waterRGB += sunSparkles;

                // 9. Single Ultra-Thin Broken Intersection Foam (Shoreline + Submerged 3D Rocks/Pillars!)
                float foamNoise = ValueNoise(i.positionWS.xz * 7.5 + rippleXY * 2.5 + float2(_Time.y * 0.4, -_Time.y * 0.3));
                float shoreEdgeDist = max(0.0, maskVal - _WaterThreshold);
                float normShoreDist = shoreEdgeDist / max(0.004, _FoamBandWidth);
                float shoreFoam = (1.0 - smoothstep(0.0, 0.65, normShoreDist)) * smoothstep(0.32, 0.62, foamNoise + 0.18);

                // Depth intersection foam around any 3D rock/pillar/plant standing in the water!
                float objFoam = 0.0;
                if (rawMaskDepth > 0.08 && depthDiff > 0.001 && depthDiff < _ObjectFoamDistance)
                {
                    float normObj = depthDiff / max(0.01, _ObjectFoamDistance);
                    objFoam = (1.0 - smoothstep(0.0, 0.75, normObj)) * smoothstep(0.30, 0.60, foamNoise + 0.22);
                }

                half foamMask = (half)saturate(max(shoreFoam, objFoam) * _FoamColor.a);
                waterRGB = lerp(waterRGB, _FoamColor.rgb * lerp(0.8h, 1.05h, shadowAtten), foamMask);

                waterRGB = MixFog(waterRGB, i.fogFactor);

                // Since we composite over SampleSceneColor(refractedUV), output full alpha inside the water body
                // with a silky smooth feather right at the dry sand edge!
                return half4(waterRGB, (half)shoreFeather);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
