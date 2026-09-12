Shader "Custom/StylizedCloud"
{
    Properties
    {
        _BaseColor("Cloud Color", Color) = (0.98, 0.99, 1.0, 0.95)
        _ShadowColor("Cloud Shadow Tint", Color) = (0.75, 0.82, 0.92, 1.0)
        _RimColor("Sun Rim Light", Color) = (1.0, 0.95, 0.85, 1.0)
        _RimPower("Rim Power", Range(0.5, 6.0)) = 2.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "CloudForwardPass"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadowColor;
                half4 _RimColor;
                float _RimPower;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.normalWS = normInputs.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalWS, mainLight.direction) * 0.5 + 0.5); // Soft half-lambert

                // Soft lighting gradient from bottom shadow to lit top
                half3 col = lerp(_ShadowColor.rgb, _BaseColor.rgb, NdotL);

                // Subtle rim lighting
                float fresnel = 1.0 - saturate(dot(viewDirWS, normalWS));
                float rim = pow(fresnel, _RimPower);
                col += _RimColor.rgb * rim * 0.3;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
