Shader "Custom/StylizedGradientSkybox"
{
    Properties
    {
        [Header(Sky Gradient)]
        _TopColor("Top Color (Zenith)", Color) = (0.35, 0.65, 0.95, 1.0)
        _HorizonColor("Horizon Color", Color) = (0.85, 0.92, 0.98, 1.0)
        _BottomColor("Bottom Color (Ground/Nadir)", Color) = (0.55, 0.75, 0.85, 1.0)
        
        [Header(Gradient Control)]
        _HorizonOffset("Horizon Offset", Range(-1.0, 1.0)) = 0.0
        _ExponentTop("Top Exponent", Range(0.1, 10.0)) = 1.5
        _ExponentBottom("Bottom Exponent", Range(0.1, 10.0)) = 2.0

        [Header(Sun)]
        [Toggle] _EnableSun("Enable Sun Disc", Float) = 1.0
        _SunColor("Sun Color", Color) = (1.0, 0.95, 0.8, 1.0)
        _SunSize("Sun Size", Range(0.01, 0.5)) = 0.08
        _SunSoftness("Sun Softness", Range(0.001, 0.2)) = 0.02
    }

    SubShader
    {
        Tags 
        { 
            "Queue"="Background" 
            "RenderType"="Background" 
            "PreviewType"="Skybox"
            "RenderPipeline"="UniversalPipeline"
        }
        
        Cull Off
        ZWrite Off

        Pass
        {
            Name "SkyboxPass"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ENABLESUN_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirWS  : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half4 _SunColor;
                float _HorizonOffset;
                float _ExponentTop;
                float _ExponentBottom;
                float _SunSize;
                float _SunSoftness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.viewDirWS = normalize(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.viewDirWS);
                float y = dir.y - _HorizonOffset;

                // 3-color gradient interpolation
                half3 skyColor;
                if (y >= 0.0)
                {
                    float factor = pow(saturate(y), _ExponentTop);
                    skyColor = lerp(_HorizonColor.rgb, _TopColor.rgb, factor);
                }
                else
                {
                    float factor = pow(saturate(-y), _ExponentBottom);
                    skyColor = lerp(_HorizonColor.rgb, _BottomColor.rgb, factor);
                }

                #if defined(_ENABLESUN_ON)
                // Main light / sun direction
                Light mainLight = GetMainLight();
                float3 sunDir = normalize(mainLight.direction);
                
                // Dot product for sun disc
                float sunDot = dot(dir, sunDir);
                float sunThreshold = 1.0 - (_SunSize * _SunSize);
                float sunDisc = smoothstep(sunThreshold - _SunSoftness, sunThreshold, sunDot);
                
                skyColor = lerp(skyColor, _SunColor.rgb, sunDisc * _SunColor.a);
                #endif

                return half4(skyColor, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
