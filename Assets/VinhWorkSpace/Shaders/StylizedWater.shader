Shader "Custom/StylizedWater"
{
    Properties
    {
        [Header(CaribbeanTropicalColors)]
        _ShallowColor("Shallow Color (Ngoc lam trong vat)", Color) = (0.35, 0.90, 0.92, 1.0)
        _DeepColor("Deep Color (Xanh ngoc nhiet doi)", Color) = (0.08, 0.65, 0.80, 1.0)
        _DepthDistance("Depth Distance (Do sau chuyen mau)", Range(0.5, 10.0)) = 3.5
        _WaterClarity("Water Clarity (Do trong suot)", Range(0.0, 1.0)) = 0.85

        [Header(IslandLagoon)]
        _IslandCenter("Island Center XZ", Vector) = (0, 0, 0, 0)
        _IslandRadius("Island Lagoon Radius", Float) = 2.5

        [Header(IslandShadows)]
        _ShadowColor("Shadow Color (Bong do tren nuoc)", Color) = (0.04, 0.32, 0.44, 1.0)
        _ShadowStrength("Shadow Darkness", Range(0.0, 1.0)) = 0.60

        [Header(ShorelineFoam)]
        _FoamColor("Foam Color (Bot chan dao)", Color) = (1.0, 1.0, 1.0, 0.90)
        _FoamDistance("Foam Width", Range(0.01, 0.4)) = 0.10

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

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthDistance;
                float _WaterClarity;

                float4 _IslandCenter;
                float _IslandRadius;

                half4 _ShadowColor;
                float _ShadowStrength;

                half4 _FoamColor;
                float _FoamDistance;

                half4 _HorizonColor;
                float _FogStart;
                float _FogEnd;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                // Mặt biển tĩnh, phẳng, siêu nhẹ (không tính toán chuyển động sóng)
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);

                output.positionWS = posWS;
                output.positionCS = TransformWorldToHClip(posWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.screenPos = ComputeScreenPos(output.positionCS);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.shadowCoord = GetShadowCoord(vertexInput);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfaceDepth = input.screenPos.w;
                float waterDepth = max(0.0, sceneDepth - surfaceDepth);

                // 1. HIỆU ỨNG TRONG SUỐT TĨNH (Zero distortion - hình ảnh đứng yên 100%, siêu tối ưu)
                // Lấy trực tiếp màu sắc vật thể ngập nước mà không qua khúc xạ làm rung lắc hình ảnh
                float3 underwaterScene = SampleSceneColor(screenUV);

                // Phân biệt có chân đảo/đá bên dưới hay là khoảng trống ngoài khơi
                bool hasSubmergedFloor = (waterDepth < 30.0);

                half3 baseWaterColor;
                if (hasSubmergedFloor)
                {
                    // Vùng chân đảo: Nhìn xuyên thấu khối đá chìm cố định, rõ ràng và trong vắt
                    float depthFactor = saturate(waterDepth / max(0.2, _DepthDistance));
                    
                    half3 shallowWaterTint = underwaterScene * lerp(_ShallowColor.rgb * 1.25, half3(1.0, 1.0, 1.0), 0.15);
                    half3 deepWaterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthFactor);
                    
                    float clarity = (1.0 - smoothstep(0.0, 1.0, depthFactor)) * _WaterClarity;
                    baseWaterColor = lerp(deepWaterColor, shallowWaterTint, clarity);
                }
                else
                {
                    // Vùng biển sâu: Chuyển màu thềm ngọc lam mượt mà ra đại dương
                    float distFromIsland = max(0.0, length(input.positionWS.xz - _IslandCenter.xz) - _IslandRadius);
                    float shelfFactor = saturate(distFromIsland / max(0.5, _DepthDistance));
                    baseWaterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, smoothstep(0.0, 1.0, shelfFactor));
                }

                half4 finalColor = half4(baseWaterColor, 1.0);

                // 2. VIỀN BỌT NƯỚC TĨNH NHẸ NHÀNG SÁT CHÂN ĐẢO
                // Chỉ xuất hiện sát đường tiếp xúc chân đá (< 0.1m)
                if (hasSubmergedFloor && waterDepth < _FoamDistance)
                {
                    float foam = saturate(1.0 - (waterDepth / max(0.005, _FoamDistance)));
                    foam = smoothstep(0.3, 0.85, foam);
                    finalColor.rgb = lerp(finalColor.rgb, _FoamColor.rgb, foam * _FoamColor.a);
                }

                // 3. BÓNG ĐỔ THỰC TẾ TỪ CÁC KHỐI ĐẢO
                float4 shadowCoord = input.shadowCoord;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif

                Light mainLight = GetMainLight(shadowCoord);
                half shadow = mainLight.shadowAttenuation;

                // Nhuộm bóng đổ xanh mát dịu tự nhiên
                half3 shadowedWater = lerp(finalColor.rgb, _ShadowColor.rgb, _ShadowStrength);
                finalColor.rgb = lerp(shadowedWater, finalColor.rgb, shadow);

                // 4. HÒA TAN MƯỢT MÀ VÀO CHÂN TRỜI XA (Fog xa)
                float distToCamera = length(GetCameraPositionWS() - input.positionWS);
                float fogFactor = saturate((distToCamera - _FogStart) / max(0.01, _FogEnd - _FogStart));
                fogFactor = smoothstep(0.0, 1.0, fogFactor);

                finalColor.rgb = lerp(finalColor.rgb, _HorizonColor.rgb, fogFactor);

                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
