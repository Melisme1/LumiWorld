Shader "Custom/StylizedRiverWater"
{
    Properties
    {
        [Header(WaterColors)]
        _ShallowColor("Shallow Color (Ngoc lam trong vat)", Color) = (0.35, 0.90, 0.92, 0.85)
        _DeepColor("Deep Color (Xanh ngoc nhiet doi)", Color) = (0.10, 0.65, 0.80, 0.92)
        _FoamColor("Foam Color (Bot trang ven bo)", Color) = (1.0, 1.0, 1.0, 0.95)

        [Header(FlowAnimation)]
        _FlowSpeed("Flow Speed (Toc do chay)", Float) = 0.50
        _FlowTiling("Flow Tiling (Mat do song doc)", Float) = 2.5
        _FoamEdgeWidth("Bank Foam Width (Do rong bot bo)", Range(0.01, 0.40)) = 0.16
        _NoiseTex("Flow Noise Texture", 2D) = "white" {}

        [Header(Lighting)]
        _SunSheen("Sun Glint Sheen (Anh nang lap lanh)", Range(0.0, 1.0)) = 0.45
        _SunGloss("Sun Glint Gloss", Range(10.0, 256.0)) = 128.0
    }

    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent+50" 
            "RenderType"="Transparent" 
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "RiverForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float2 uv           : TEXCOORD2;
                float4 color        : COLOR;
            };

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                float _FlowSpeed;
                float _FlowTiling;
                float _FoamEdgeWidth;
                float _SunSheen;
                float _SunGloss;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Diễn hoạt dao động gợn sóng nhẹ nhàng (Subtle vertical wave bobbing)
                float wave = sin(_Time.y * 3.5 + input.uv.y * 8.0) * 0.0025;
                input.positionOS.y += wave;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.uv = input.uv;
                output.color = input.color;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. TÍNH TOÁN 2 TẦNG DÒNG CHẢY CUỘN SÓNG (Dual-layer UV scrolling along river spline)
                float time = _Time.y * _FlowSpeed;
                float2 uv1 = float2(input.uv.x * 2.0, input.uv.y * _FlowTiling - time);
                float2 uv2 = float2(input.uv.x * 2.8 + 0.35, input.uv.y * _FlowTiling * 1.35 - time * 1.25);

                half4 n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv1 * 0.25);
                half4 n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv2 * 0.25);

                // Gợn sóng giao thoa tự nhiên không lặp lại
                half wavePattern = (n1.r * 0.6 + n2.g * 0.4);
                half flowRipples = saturate(pow(wavePattern * 1.8, 2.0));

                // 2. BỌT NƯỚC VEN BỜ ĐÁ (Bank Foam) & BỌT DÒNG CHẢY (Current Foam streaks)
                half bankDist = abs(input.uv.x - 0.5) * 2.0; // 0 ở tâm lòng suối, 1 ở mép gờ đá 2 bên bờ
                half bankFoam = smoothstep(1.0 - _FoamEdgeWidth, 1.0, bankDist);

                half flowFoam = smoothstep(0.68, 0.95, flowRipples) * (1.0 - bankDist * 0.3);

                // 3. PHỐI MÀU NƯỚC NHIỆT ĐỚI TRONG VẮT
                half3 waterColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(1.0 - bankDist * 0.7));

                // Điểm xuyết vệt sáng ngọc bích trên đỉnh gợn sóng
                waterColor += half3(0.12, 0.25, 0.28) * flowRipples;

                // Hòa trộn bọt trắng ven bờ đá và bọt xoáy
                half totalFoam = saturate(bankFoam * 0.85 + flowFoam * 0.65);
                waterColor = lerp(waterColor, _FoamColor.rgb, totalFoam);

                // 4. ÁNH NẮNG LẤP LÁNH THEO DÒNG NƯỚC (Specular Sun Glint)
                Light mainLight = GetMainLight();
                half3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                
                // Độ dốc pháp tuyến động theo sóng
                half3 rippleNormal = normalize(input.normalWS + half3(sin(uv1.y * 6.0) * 0.15, 0.0, cos(uv2.y * 6.0) * 0.15));
                half3 halfDir = normalize(mainLight.direction + viewDirWS);
                half NdotH = saturate(dot(rippleNormal, halfDir));
                half sunSpecular = pow(NdotH, _SunGloss) * _SunSheen;

                waterColor += sunSpecular * mainLight.color;

                // 5. ĐỘ TRONG SUỐT (Alpha Transparency)
                half vertAlpha = (input.color.a > 0.01) ? input.color.a : 1.0;
                half baseAlpha = lerp(_DeepColor.a, _ShallowColor.a, saturate(1.0 - bankDist * 0.7));
                half alpha = lerp(baseAlpha, _FoamColor.a, totalFoam) * vertAlpha;

                return half4(waterColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
