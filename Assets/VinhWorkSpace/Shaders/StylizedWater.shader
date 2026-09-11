Shader "Custom/StylizedWater"
{
    Properties
    {
        [Header(CaribbeanTropicalColors)]
        _ShallowColor("Shallow Color (Ngoc lam trong vat)", Color) = (0.35, 0.90, 0.92, 1.0)
        _DeepColor("Deep Color (Xanh ngoc nhiet doi)", Color) = (0.08, 0.65, 0.80, 1.0)
        _DepthDistance("Depth Distance (Do sau chuyen mau)", Range(0.5, 10.0)) = 3.5
        _WaterClarity("Water Clarity (Do trong suot)", Range(0.0, 1.0)) = 0.85

        [Header(IslandShadows)]
        _ShadowColor("Shadow Color (Bong do tren nuoc)", Color) = (0.04, 0.32, 0.44, 1.0)
        _ShadowStrength("Shadow Darkness", Range(0.0, 1.0)) = 0.60

        [Header(ShorelineFoam)]
        _FoamColor("Foam Color (Bot chan dao)", Color) = (1.0, 1.0, 1.0, 0.90)
        _FoamDistance("Foam Width", Range(0.01, 0.4)) = 0.10

        [Header(ElongatedOceanCurrents)]
        _NoiseTex("Ocean Flow Noise (R: Base, G: Detail)", 2D) = "white" {}
        _CurrentColor("Current Color (Mau dai dong chay)", Color) = (0.45, 0.92, 0.96, 0.30)
        _CurrentScale("Current Scale (Mat do dai)", Float) = 0.25
        _CurrentStretch("Current Stretch (Do keo dai dai)", Range(3.0, 20.0)) = 10.0
        _CurrentWidth("Current Width (Do mong cua dai)", Range(0.01, 0.25)) = 0.06
        _CurrentSpeed("Drift Speed (Toc do troi)", Float) = 0.10
        _CurrentIntensity("Current Opacity (Do ro nhe cua dai)", Range(0.0, 1.0)) = 0.28
        _WaveRibbonIntensity("Wave Crest (Gon song tren dai)", Range(0.0, 1.0)) = 0.15
        _SunSheen("Sun Glint Sheen", Range(0.0, 1.0)) = 0.25
        _SunGloss("Sun Glint Gloss", Range(10.0, 256.0)) = 96.0

        [Header(InfiniteHorizonBlend)]
        _HorizonColor("Horizon Sky Color", Color) = (0.68, 0.86, 0.98, 1.0)
        _FogStart("Fog Start Distance", Float) = 80.0
        _FogEnd("Fog End Distance", Float) = 220.0
    }

    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "RenderType"="Transparent" 
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

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
                float4 shadowCoord  : TEXCOORD3;
            };

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthDistance;
                float _WaterClarity;

                half4 _ShadowColor;
                float _ShadowStrength;

                half4 _FoamColor;
                float _FoamDistance;

                half4 _CurrentColor;
                float _CurrentScale;
                float _CurrentStretch;
                float _CurrentWidth;
                float _CurrentSpeed;
                float _CurrentIntensity;
                float _WaveRibbonIntensity;
                float _SunSheen;
                float _SunGloss;

                half4 _HorizonColor;
                float _FogStart;
                float _FogEnd;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);

                output.positionWS = posWS;
                output.positionCS = TransformWorldToHClip(posWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.screenPos = ComputeScreenPos(output.positionCS);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.shadowCoord = GetShadowCoord(vertexInput);

                return output;
            }

            // Tính toán các dải dòng chảy mỏng nhẹ, thanh thoát + gợn sóng trôi bằng Texture Sampler phần cứng (Siêu tối ưu)
            void CalculateOceanCurrents(float2 wsXZ, float time, out half currentMask, out half ribbonCrest, out half2 normalSlope)
            {
                // Hướng dòng chảy hải lưu (vector chuẩn hóa)
                half2 flowDir = half2(0.9248, 0.3804);
                half2 crossDir = half2(-flowDir.y, flowDir.x);

                // Kéo dãn mạnh tọa độ dọc theo dòng chảy
                float stretchRatio = max(1.0, _CurrentStretch);
                float u = (dot(wsXZ, flowDir) - time * _CurrentSpeed * 3.0) * (_CurrentScale / stretchRatio);
                float v = dot(wsXZ, crossDir) * _CurrentScale;

                // Domain warping uốn lượn dải dòng chảy mềm mại như dải lụa
                float warp = sin(u * 2.5 + time * 0.15) * 0.6;
                v += warp;

                // Đọc 2 tầng nhiễu đồng thời trong 1 lần sample texture phần cứng!
                // Chia 8.0 để đồng bộ chu kỳ lặp ô lưới của texture, tạo đường nét mượt mà liên tục không có cạnh cắt
                float2 noiseUV = float2(u, v) * 0.125;
                half4 noiseSample = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV);
                half n1 = noiseSample.r;
                half n2 = noiseSample.g;
                half streamPattern = n1 * 0.65 + n2 * 0.35;

                // Tạo dải mỏng nhẹ thanh thoát theo đường đồng mức (contour band)
                half distToBand = abs(streamPattern - 0.52);
                half width = max(0.005, (half)_CurrentWidth);
                half rawMask = smoothstep(width, 0.0, distToBand);
                // Vuốt mềm cạnh dải để nhẹ nhàng hòa tan vào làn nước
                currentMask = rawMask * rawMask;

                // Gợn sóng mảnh uốn lượn theo dải dòng chảy
                half ribbonWave = sin(v * 6.0 + sin(u * 3.0) * 1.5 + time * _CurrentSpeed * 5.0);
                ribbonCrest = smoothstep(0.60, 0.95, ribbonWave) * currentMask;

                // Độ dốc bề mặt cho ánh sáng lấp lánh (Sun Glint)
                half dV = cos(v * 6.0 + time * _CurrentSpeed * 5.0) * 6.0 * (half)_CurrentScale;
                normalSlope = crossDir * dV * ribbonCrest;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfaceDepth = input.screenPos.w;
                float waterDepth = max(0.0, sceneDepth - surfaceDepth);

                // 1. MÀU NƯỚC TỰ NHIÊN THEO ĐỘ SÂU
                bool hasSubmergedFloor = (waterDepth < 30.0);
                half3 baseWaterColor = _DeepColor.rgb;

                // Tối ưu hóa: Chỉ đọc Opaque Texture khi thực sự ở vùng nước nông nhìn thấy đáy!
                if (hasSubmergedFloor && waterDepth < _DepthDistance)
                {
                    // Vùng nông ven lục địa/đảo: Trong vắt nhìn rõ đá chìm
                    float3 underwaterScene = SampleSceneColor(screenUV);
                    float depthFactor = saturate(waterDepth / max(0.2, _DepthDistance));
                    half3 shallowWaterTint = (half3)underwaterScene * lerp(_ShallowColor.rgb * 1.25, half3(1.0, 1.0, 1.0), 0.15);
                    half3 depthGradColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthFactor);
                    
                    half clarity = (1.0 - smoothstep(0.0, 1.0, depthFactor)) * (half)_WaterClarity;
                    baseWaterColor = lerp(depthGradColor, shallowWaterTint, clarity);
                }

                half4 finalColor = half4(baseWaterColor, 1.0);

                // 2. TÍNH TOÁN CÁC DẢI HẢI LƯU MỎNG NHẸ TRÔI ÊM Ả (Texture Sampler Hardware)
                half currentMask;
                half ribbonCrest;
                half2 normalSlope;
                CalculateOceanCurrents(input.positionWS.xz, _Time.y, currentMask, ribbonCrest, normalSlope);

                // Áp dụng dải dòng chảy mỏng nhẹ thanh thoát
                finalColor.rgb = lerp(finalColor.rgb, _CurrentColor.rgb, currentMask * (half)_CurrentIntensity * _CurrentColor.a);

                // Áp dụng vệt gợn sóng mảnh nhẹ nhàng
                finalColor.rgb = lerp(finalColor.rgb, half3(0.92, 0.98, 1.0), ribbonCrest * (half)_WaveRibbonIntensity);

                // 3. VIỀN BỌT NƯỚC NHẸ NHÀNG SÁT CHÂN ĐẢO
                if (hasSubmergedFloor && waterDepth < _FoamDistance)
                {
                    half foam = saturate(1.0 - (waterDepth / max(0.005, _FoamDistance)));
                    foam = smoothstep(0.3, 0.85, foam);
                    finalColor.rgb = lerp(finalColor.rgb, _FoamColor.rgb, foam * _FoamColor.a);
                }

                // 4. BÓNG ĐỔ VÀ ÁNH NẮNG LẤP LÁNH (Sun Glint & Shadow)
                float4 shadowCoord = input.shadowCoord;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif

                Light mainLight = GetMainLight(shadowCoord);
                half shadow = mainLight.shadowAttenuation;

                // Nhuộm bóng đổ xanh mát dịu
                half3 shadowedWater = lerp(finalColor.rgb, _ShadowColor.rgb, (half)_ShadowStrength);
                finalColor.rgb = lerp(shadowedWater, finalColor.rgb, shadow);

                // Ánh kim phản chiếu mặt trời lấp lánh nhẹ theo sườn gợn
                half3 waveNormalWS = normalize(half3(-normalSlope.x * 0.25, 1.0, -normalSlope.y * 0.25));
                half3 viewDirWS = (half3)normalize(GetCameraPositionWS() - input.positionWS);
                half3 halfDir = normalize((half3)mainLight.direction + viewDirWS);
                half NdotH = saturate(dot(waveNormalWS, halfDir));
                half sunSpecular = pow(NdotH, (half)_SunGloss) * (half)_SunSheen * shadow;
                finalColor.rgb += sunSpecular * (half3)mainLight.color;

                // 5. HÒA TAN MƯỢT MÀ VÀO CHÂN TRỜI XA (Horizon Fog)
                float distToCamera = length(GetCameraPositionWS() - input.positionWS);
                half fogFactor = saturate((distToCamera - _FogStart) / max(0.01, _FogEnd - _FogStart));
                fogFactor = smoothstep(0.0, 1.0, fogFactor);

                finalColor.rgb = lerp(finalColor.rgb, _HorizonColor.rgb, fogFactor);

                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
