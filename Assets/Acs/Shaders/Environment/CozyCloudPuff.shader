Shader "LumiWorld/Environment/CozyCloudPuff"
{
    Properties
    {
        _TopColor ("Sunlit Cloud Top", Color) = (0.990, 0.980, 0.952, 1.0)
        _ShadowColor ("Cloud Belly Shadow", Color) = (0.62, 0.79, 0.93, 1.0)
        _RimColor ("Warm Sun Rim", Color) = (1.0, 0.96, 0.86, 1.0)
        _RimPower ("Rim Power", Range(0.5, 6.0)) = 2.2
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.28

        _WobbleSpeed ("Wobble Speed", Float) = 0.45
        _WobbleAmp ("Wobble Amplitude", Range(0, 0.3)) = 0.04
        _NoiseScale ("Wobble Scale", Float) = 0.45

        _SubsurfaceWrap ("Soft Belly Wrap", Range(0.1, 0.9)) = 0.52
        _EdgeFadeStart ("Soft Silhouette Fade Start", Range(0.0, 0.6)) = 0.08
        _EdgeFadeEnd ("Soft Silhouette Fade End", Range(0.2, 0.95)) = 0.62
        _EdgeNoiseStrength ("Cotton Silhouette Noise", Range(0, 0.5)) = 0.24
        _OverallOpacity ("Overall Opacity", Range(0, 1)) = 0.92
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+35"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Back

        Pass
        {
            Name "CozyCloudPuffForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
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
                float  localPosY  : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
                float4 tintAlpha  : TEXCOORD4;
                float3 worldPos   : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _ShadowColor;
                half4 _RimColor;
                float _RimPower;
                float _RimStrength;
                float _WobbleSpeed;
                float _WobbleAmp;
                float _NoiseScale;
                float _SubsurfaceWrap;
                float _EdgeFadeStart;
                float _EdgeFadeEnd;
                float _EdgeNoiseStrength;
                float _OverallOpacity;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _TintAlpha)
            UNITY_INSTANCING_BUFFER_END(Props)

            float Hash21(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float PuffFBM(float2 p)
            {
                return ValueNoise(p) * 0.65 + ValueNoise(p * 2.13 + float2(7.3, 19.1)) * 0.35;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posOS = input.positionOS.xyz;
                float3 worldPre = TransformObjectToWorld(posOS);
                float phase = _Time.y * _WobbleSpeed;
                float wave = sin(worldPre.x * _NoiseScale + phase) * cos(worldPre.z * _NoiseScale - phase * 0.8);
                posOS += input.normalOS * (wave * _WobbleAmp);

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.normalWS = normInputs.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                output.localPosY = input.positionOS.y;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                output.worldPos = posInputs.positionWS;

                float4 ta = UNITY_ACCESS_INSTANCED_PROP(Props, _TintAlpha);
                if (dot(ta, ta) < 0.0001) ta = float4(1.0, 1.0, 1.0, 1.0);
                output.tintAlpha = ta;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 N = normalize(input.normalWS);
                float3 V = normalize(input.viewDirWS);

                Light mainLight = GetMainLight();
                float ndotl = dot(N, mainLight.direction);
                float wrap = saturate((ndotl + _SubsurfaceWrap) / (1.0 + _SubsurfaceWrap));
                float lobeShade = pow(saturate(wrap), 1.35);
                float bellyAO = smoothstep(-0.35, 0.55, input.localPosY);
                float verticalGrad = saturate(N.y * 0.42 + 0.50);

                float detail = PuffFBM(input.worldPos.xz * 0.28 + float2(_Time.y * 0.06, -_Time.y * 0.04));
                float shade = saturate(lobeShade * 0.56 + bellyAO * 0.24 + verticalGrad * 0.20 + (detail - 0.5) * 0.14);
                half3 col = lerp(_ShadowColor.rgb, _TopColor.rgb, shade) * input.tintAlpha.rgb;

                float ndotv = saturate(dot(V, N));
                float fresnel = pow(1.0 - ndotv, _RimPower) * saturate(ndotl * 0.5 + 0.5);
                col = lerp(col, _RimColor.rgb, saturate(fresnel * _RimStrength));

                // Soft cottony Fresnel silhouette fade modulated by resolution-independent noise
                float fadeEnd = max(_EdgeFadeStart + 0.05, _EdgeFadeEnd);
                float rimPerturbed = ndotv + (detail - 0.5) * _EdgeNoiseStrength;
                float rimAlpha = smoothstep(_EdgeFadeStart, fadeEnd, rimPerturbed);
                float bottomFade = smoothstep(-0.52, -0.02, input.localPosY);
                float alpha = saturate(rimAlpha * bottomFade * _OverallOpacity * input.tintAlpha.a);
                if (alpha <= 0.004) discard;

                col = MixFog(col, input.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
