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
            "Queue"="Transparent" 
            "RenderPipeline"="UniversalPipeline" 
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
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

                // 1. Tương phản bề mặt 3D (Mặt trên sáng rõ, các mặt cạnh bên tối nhẹ hơn giúp nổi bật hình khối từng mảnh lục giác)
                float faceShading = saturate(normalWS.y * 0.35 + 0.65);

                // 2. Hiệu ứng viền sáng Fresnel định hình đường viền bên ngoài
                float NdotV = 1.0 - saturate(dot(normalWS, viewDirWS));
                float rim = pow(NdotV, _FresnelPower) * _FresnelIntensity;

                float4 finalColor = _BaseColor;
                finalColor.rgb = _BaseColor.rgb * faceShading + rim * (_BaseColor.rgb + half3(0.15, 0.15, 0.15));
                finalColor.a = saturate(_BaseColor.a + rim * 0.35);

                return finalColor;
            }
            ENDHLSL
        }
    }
}
