Shader "Custom/StylizedWater"
{
    Properties
    {
        [Header(CrystalClearCaribbeanColors)]
        _ShallowColor("Shallow Color (Ngoc lam trong vat)", Color) = (0.38, 0.92, 0.95, 0.45)
        _DeepColor("Deep Color (Xanh ngoc sang tuoi)", Color) = (0.10, 0.70, 0.84, 0.85)
        _DepthDistance("Depth Distance (Do sau chuyen mau)", Range(0.5, 10.0)) = 4.0
        _WaterClarity("Water Clarity (Do trong suot)", Range(0.0, 1.0)) = 0.88

        [Header(NormalMapWaves)]
        _NormalMap("Water Normal Texture", 2D) = "bump" {}
        _WaveScale("Wave Tiling", Float) = 0.09
        _WaveSpeed("Wave Drift Speed", Float) = 0.35
        _WaveStrength("Wave Ripple Strength", Range(0.0, 1.5)) = 0.45

        [Header(ShorelineFoam)]
        _FoamColor("Shore Foam Color (Bot chan dao)", Color) = (1.0, 1.0, 1.0, 0.92)
        _FoamDistance("Shore Foam Width", Range(0.01, 0.5)) = 0.14

        [Header(IslandShadowsAndLighting)]
        _ShadowColor("Shadow Color (Bong do tren nuoc)", Color) = (0.05, 0.38, 0.50, 1.0)
        _ShadowStrength("Shadow Darkness", Range(0.0, 1.0)) = 0.40
        _HorizonColor("Horizon Sky Color", Color) = (0.68, 0.88, 0.98, 1.0)
        _ReflectionStrength("Sky Gloss Reflection", Range(0.0, 1.0)) = 0.22
        _FresnelPower("Reflection Falloff", Range(1.0, 8.0)) = 4.5
        _SunSheen("Sun Sparkle Sheen", Range(0.0, 2.0)) = 0.35
        _SunGloss("Sun Sparkle Sharpness", Range(10.0, 512.0)) = 160.0

        [Header(InfiniteHorizonBlend)]
        _FogStart("Fog Start Distance", Float) = 70.0
        _FogEnd("Fog End Distance", Float) = 190.0
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
            Name "WaterForward"
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
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float4 screenPos    : TEXCOORD2;
                float  fogFactor    : TEXCOORD3;
            };

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthDistance;
                float _WaterClarity;

                float _WaveScale;
                float _WaveSpeed;
                float _WaveStrength;

                half4 _FoamColor;
                float _FoamDistance;

                half4 _ShadowColor;
                float _ShadowStrength;
                half4 _HorizonColor;
                float _ReflectionStrength;
                float _FresnelPower;
                float _SunSheen;
                float _SunGloss;

                float _FogStart;
                float _FogEnd;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);

                // Giữ mặt phẳng hình học phẳng hoàn hảo (không uốn vertex tạo gân lưới)
                output.positionWS = posWS;
                output.positionCS = TransformWorldToHClip(posWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / max(1e-5, input.screenPos.w);
                float rawSceneDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawSceneDepth, _ZBufferParams);
                float waterEyeDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                float waterDepth = max(0.0, sceneEyeDepth - waterEyeDepth);
                bool hasSubmergedFloor = (waterDepth < 35.0);

                // 1. TÍNH TOÁN 2 TẦNG SÓNG LĂN TĂN CHUYỂN ĐỘNG (Dual Layer Normal Ripples)
                float t = _Time.y * _WaveSpeed;
                float2 uv1 = input.positionWS.xz * _WaveScale + float2(t * 0.05, t * 0.035);
                float2 uv2 = input.positionWS.xz * (_WaveScale * 1.42) + float2(-t * 0.038, t * 0.058);

                half3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv1));
                half3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv2));
                half3 blendedNormalTS = normalize(half3((n1.xy + n2.xy) * (half)_WaveStrength, n1.z * n2.z));
                half3 waveNormalWS = normalize(half3(blendedNormalTS.x, 1.0h, blendedNormalTS.y));

                // 2. MÀU NƯỚC XANH TRONG TỰ NHIÊN (Crystal Clear Shallow Transparency)
                half3 baseWaterColor = _DeepColor.rgb;
                half alpha = _DeepColor.a;

                if (hasSubmergedFloor && waterDepth < _DepthDistance)
                {
                    // Vùng nước nông quanh đảo: Trong vắt nhìn rõ đá và đáy đảo chìm
                    // Khúc xạ nhẹ đáy nước theo sóng
                    float2 refractUV = screenUV + blendedNormalTS.xy * 0.012h * saturate(waterDepth / 1.5h);
                    float3 underwaterScene = SampleSceneColor(refractUV);

                    float depthFactor = saturate(waterDepth / max(0.2, _DepthDistance));
                    half3 shallowWaterTint = (half3)underwaterScene * lerp(_ShallowColor.rgb * 1.25h, half3(1.0h, 1.0h, 1.0h), 0.20h);
                    half3 depthGradColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthFactor);

                    half clarity = (1.0h - smoothstep(0.0h, 1.0h, depthFactor)) * (half)_WaterClarity;
                    baseWaterColor = lerp(depthGradColor, shallowWaterTint, clarity);
                    alpha = lerp(_ShallowColor.a, _DeepColor.a, depthFactor);
                }
                else
                {
                    // Ngoài xa: Màu xanh ngọc sáng trong, có ánh sáng tán xạ nhẹ trên đỉnh sóng
                    half waveCrest = saturate(blendedNormalTS.y * 0.5h + 0.5h);
                    baseWaterColor += half3(0.04h, 0.08h, 0.09h) * waveCrest;
                }

                half4 finalColor = half4(baseWaterColor, alpha);

                // 3. VIỀN BỌT SÓNG TRẮNG VEN CHÂN ĐẢO (Pulsing Tide Shoreline Foam)
                if (hasSubmergedFloor && waterDepth < (_FoamDistance * 1.5))
                {
                    float shorePulse = sin(_Time.y * 2.5 - waterDepth * 14.0) * 0.5 + 0.5;
                    float activeFoamDist = _FoamDistance * (0.8 + 0.3 * shorePulse);
                    if (waterDepth < activeFoamDist)
                    {
                        half foam = saturate(1.0 - (waterDepth / max(0.005, activeFoamDist)));
                        foam = smoothstep(0.25, 0.85, foam);
                        finalColor.rgb = lerp(finalColor.rgb, _FoamColor.rgb, foam * _FoamColor.a);
                        finalColor.a = max(finalColor.a, foam * _FoamColor.a);
                    }
                }

                // 4. BÓNG ĐỔ TỪ ĐẢO VÀ CÂY CỐI
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half shadow = (half)mainLight.shadowAttenuation;

                // Nhuộm bóng đổ xanh mát dịu thay vì đen đục
                half3 shadowedWater = lerp(finalColor.rgb, _ShadowColor.rgb, (half)_ShadowStrength);
                finalColor.rgb = lerp(shadowedWater, finalColor.rgb, shadow);

                // 5. ÁNH KIM MẶT TRỜI LẤP LÁNH THEO TỪNG GỢN SÓNG (Sun Specular Sparkle)
                half3 viewDirWS = normalize((half3)(GetCameraPositionWS() - input.positionWS));
                half3 halfDir = normalize((half3)mainLight.direction + viewDirWS);
                half NdotH = saturate(dot(waveNormalWS, halfDir));
                half sunSpecular = pow(NdotH, (half)_SunGloss) * (half)_SunSheen * shadow;
                finalColor.rgb += sunSpecular * (half3)mainLight.color;

                // 6. PHẢN CHIẾU BẦU TRỜI BÓNG BẨY DỊU NHẸ (Fresnel Sky Gloss)
                half fresnel = pow(1.0h - saturate(dot(waveNormalWS, viewDirWS)), (half)_FresnelPower);
                half3 skyRefl = lerp(_HorizonColor.rgb, half3(0.72h, 0.90h, 1.0h), 0.55h);
                finalColor.rgb = lerp(finalColor.rgb, skyRefl, fresnel * (half)_ReflectionStrength);

                // 7. HÒA TAN MƯỢT MÀ VÀO ĐƯỜNG CHÂN TRỜI (Horizon Fog)
                float distToCam = length(GetCameraPositionWS() - input.positionWS);
                half horizonFactor = saturate((distToCam - _FogStart) / max(0.01, _FogEnd - _FogStart));
                horizonFactor = smoothstep(0.0, 1.0, horizonFactor);
                finalColor.rgb = lerp(finalColor.rgb, _HorizonColor.rgb, horizonFactor);
                finalColor.a = lerp(finalColor.a, 1.0h, horizonFactor);

                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
