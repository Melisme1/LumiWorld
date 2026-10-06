Shader "Custom/AnimalRimGlow"
{
    Properties
    {
        [HDR] _RimColor ("Rim Glow Color", Color) = (1.5, 1.25, 0.6, 1.0)
        _RimPower ("Rim Sharpness Power", Range(0.5, 6.0)) = 2.2
        _RimIntensity ("Rim Glow Intensity", Range(0.5, 5.0)) = 2.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent+200" 
            "RenderPipeline"="UniversalPipeline" 
        }

        Blend One One  // Cộng sáng Additive: chỉ làm rực sáng viền, giữ nguyên 100% texture gốc bên dưới
        ZWrite Off
        ZTest LEqual
        Cull Back

        Pass
        {
            Name "AnimalRimPass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _RimColor;
                float _RimPower;
                float _RimIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                // Hiệu ứng viền sáng Fresnel định hình đường viền mép ngoài
                float NdotV = 1.0 - saturate(dot(normalWS, viewDirWS));
                float rim = pow(NdotV, _RimPower) * _RimIntensity;

                // Màu viền rực rỡ bám quanh đường cong lưng, sừng và bờ vai
                half3 glowColor = _RimColor.rgb * rim;

                return half4(glowColor, 1.0);
            }
            ENDHLSL
        }
    }
}
