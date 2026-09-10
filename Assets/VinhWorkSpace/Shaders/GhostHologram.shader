Shader "Custom/HexGhostHologram"
{
    Properties
    {
        [MainColor] _BaseColor("Tint Color", Color) = (0.2, 0.9, 0.4, 0.6)
        _FresnelPower("Fresnel Rim Power", Range(0.5, 5.0)) = 2.0
        _FresnelIntensity("Fresnel Intensity", Range(0.5, 3.0)) = 1.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent+1000" 
            "RenderPipeline"="UniversalPipeline" 
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Back

        Pass
        {
            Name "GhostPass"
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
                float4 _BaseColor;
                float _FresnelPower;
                float _FresnelIntensity;
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

                // Hiệu ứng viền sáng Fresnel định hình đường viền bên ngoài
                float NdotV = 1.0 - saturate(dot(normalWS, viewDirWS));
                float rim = pow(NdotV, _FresnelPower) * _FresnelIntensity;

                // Phủ màu ĐỀU trên toàn bộ mọi mặt, nổi bật trên mặt nước
                half4 finalColor;
                finalColor.rgb = _BaseColor.rgb * 1.15 + rim * half3(0.35, 0.45, 0.35);
                finalColor.a = saturate(_BaseColor.a * 0.95 + rim * 0.25);

                return finalColor;
            }
            ENDHLSL
        }
    }
}
