Shader "LumiWorld/WetIslandGround"
{
    Properties
    {
        [Header(Top and Side Surface)]
        _TopTex("Top Ground Texture (Seamless)", 2D) = "white" {}
        _TopTiling("Top Texture Tiling", Float) = 0.55
        _TopTint("Top Color Tint", Color) = (1.0, 1.0, 1.0, 1.0)
        _SideColor("Side Wall Color", Color) = (0.58, 0.41, 0.25, 1.0)
        _SideBottomColor("Side Wall Bottom Shadow", Color) = (0.36, 0.24, 0.14, 1.0)
        _BevelEdge1("Bevel Blend Min NormalY", Range(0.0, 1.0)) = 0.45
        _BevelEdge2("Bevel Blend Max NormalY", Range(0.0, 1.0)) = 0.86
        _SeamCreaseTint("Dry Hex Seam Darkening", Range(0.0, 0.5)) = 0.22
        _BevelRimHighlight("Top Bevel Sun Rim", Range(0.0, 0.3)) = 0.08

        [Header(Water Mask Shore and Natural Sand Bed)]
        _WaterMask("Water Slope Mask (R)", 2D) = "black" {}
        _ShoreStart("Shore Transition Start (Mask)", Range(0.0, 0.6)) = 0.04
        _WaterThreshold("Waterline Mask Value", Range(0.1, 0.8)) = 0.34
        _ShoreColor("Dry Shore Sand Color", Color) = (0.93, 0.78, 0.44, 1.0)
        _BedColor("Submerged Shallow Sand Bed", Color) = (0.88, 0.73, 0.42, 1.0)
        _BedMidColor("Submerged Mid Sand Bed", Color) = (0.76, 0.64, 0.38, 1.0)
        _BedDeepColor("Submerged Deep Basin Bed", Color) = (0.52, 0.50, 0.36, 1.0)
        _ShoreGrainStrength("Sand Pebble and Grain Relief", Range(0.0, 0.20)) = 0.065
        _WetnessDarken("Waterline Wet Sand Darkening", Range(0.0, 0.35)) = 0.11

        [Header(Underwater Caustics Texture)]
        _CausticsTex("Caustics Texture (RGB)", 2D) = "black" {}
        _BedCausticColor("Underwater Caustic Tint", Color) = (1.0, 0.96, 0.84, 1.0)
        _BedCausticStrength("Underwater Caustic Strength", Range(0.0, 1.0)) = 0.32
        _BedCausticScale("Underwater Caustic Scale", Float) = 0.65
        _BedCausticSpeed("Underwater Caustic Speed", Float) = 0.08

        [Header(Stylized Painterly Lighting)]
        _DiffuseWrap("Soft Diffuse Wrap", Range(0.0, 0.6)) = 0.28
        _ShadowStrength("Shadow Darkness", Range(0.0, 1.0)) = 0.42
        _WetGloss("Wet Shore Specular", Range(0.0, 0.6)) = 0.22
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uvTop      : TEXCOORD0;
                float2 uvMask     : TEXCOORD1;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uvTop       : TEXCOORD2;
                float2 uvMask      : TEXCOORD3;
                float4 color       : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
                float  fogFactor   : TEXCOORD6;
            };

            TEXTURE2D(_TopTex);
            SAMPLER(sampler_TopTex);
            TEXTURE2D(_WaterMask);
            SAMPLER(sampler_WaterMask);
            TEXTURE2D(_CausticsTex);
            SAMPLER(sampler_CausticsTex);

            CBUFFER_START(UnityPerMaterial)
                float _TopTiling;
                half4 _TopTint;
                half4 _SideColor;
                half4 _SideBottomColor;
                float _BevelEdge1;
                float _BevelEdge2;
                float _SeamCreaseTint;
                float _BevelRimHighlight;

                float _ShoreStart;
                float _WaterThreshold;
                half4 _ShoreColor;
                half4 _BedColor;
                half4 _BedMidColor;
                half4 _BedDeepColor;
                float _ShoreGrainStrength;
                float _WetnessDarken;

                half4 _BedCausticColor;
                float _BedCausticStrength;
                float _BedCausticScale;
                float _BedCausticSpeed;

                float _DiffuseWrap;
                float _ShadowStrength;
                float _WetGloss;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Smooth value noise for natural sand ripples & small pebble spots on the shore/lakebed
            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = posInputs.positionCS;
                o.positionWS = posInputs.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uvTop = input.uvTop;
                o.uvMask = input.uvMask;
                o.color = input.color;
                o.shadowCoord = GetShadowCoord(posInputs);
                o.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 N = normalize((half3)i.normalWS);

                // 1. Seamless planar top ground texture
                float2 groundUV = i.uvTop * _TopTiling;
                half3 topAlbedo = SAMPLE_TEXTURE2D(_TopTex, sampler_TopTex, groundUV).rgb * _TopTint.rgb;

                // 2. Water slope mask (R channel, linear)
                float maskVal = SAMPLE_TEXTURE2D(_WaterMask, sampler_WaterMask, i.uvMask).r;

                // Completely erase interior hex seams inside the shore and water basin!
                float dryMask = 1.0 - smoothstep(_ShoreStart * 0.4, _WaterThreshold * 0.80, maskVal);
                half seamCrease = saturate((half)(i.color.r * dryMask)) * (half)_SeamCreaseTint;
                topAlbedo = lerp(topAlbedo, topAlbedo * half3(0.58h, 0.52h, 0.44h), seamCrease);

                half bevelRim = saturate((half)(i.color.r * 0.5 + (1.0 - N.y) * 2.0)) * (half)(dryMask * _BevelRimHighlight);
                topAlbedo += half3(0.95h, 0.92h, 0.72h) * bevelRim;

                // 3. Natural Sand / Pebble Bed (Uber-Stylized-Water style: keep the bed warm & tactile so clear water shows real bottom!)
                float nSand1 = ValueNoise(i.uvTop * 14.0) - 0.5;
                float nSand2 = ValueNoise(i.uvTop * 38.0) - 0.5;
                // Small warm sand pebbles scattered organically along the shore and shallow bed
                float pebbleCell = ValueNoise(i.uvTop * 22.0);
                float pebbleSpot = smoothstep(0.76, 0.84, pebbleCell) * 0.08 - smoothstep(0.84, 0.92, pebbleCell) * 0.05;
                half3 sandDetail = (half)((nSand1 * 0.6 + nSand2 * 0.4) * _ShoreGrainStrength + pebbleSpot);

                float shoreT = smoothstep(_ShoreStart, _WaterThreshold - 0.02, maskVal);
                // Wet sand darkening band right at the waterline (like wet beach sand in Uber-Stylized-Water)
                float wetBand = smoothstep(max(_ShoreStart, _WaterThreshold - 0.07), _WaterThreshold + 0.02, maskVal)
                              * (1.0 - smoothstep(_WaterThreshold + 0.12, _WaterThreshold + 0.45, maskVal));

                float underwaterT = saturate((maskVal - _WaterThreshold) / max(0.08, 1.0 - _WaterThreshold));
                float midBedT = smoothstep(0.0, 0.55, underwaterT);
                float deepBedT = smoothstep(0.45, 0.98, underwaterT);

                half3 shoreBase = _ShoreColor.rgb + sandDetail;
                half3 submergedBed = lerp(_BedColor.rgb + sandDetail, _BedMidColor.rgb + sandDetail * 0.6h, (half)midBedT);
                submergedBed = lerp(submergedBed, _BedDeepColor.rgb, (half)deepBedT);

                // Project real texture Caustics (Water_WaterCaustics.png) onto the submerged sand bed!
                float submergeMask = smoothstep(_WaterThreshold - 0.01, _WaterThreshold + 0.05, maskVal);
                if (submergeMask > 0.001 && _BedCausticStrength > 0.001)
                {
                    float t = _Time.y * _BedCausticSpeed;
                    float2 cUV1 = i.uvTop * _BedCausticScale + float2(t, t * 0.6);
                    float2 cUV2 = i.uvTop * (_BedCausticScale * 1.27) + float2(-t * 0.8 + 0.37, -t * 0.5 + 0.71);
                    half3 cTex1 = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, cUV1).rgb;
                    half3 cTex2 = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, cUV2).rgb;
                    half causticVal = min(cTex1.r, cTex2.r) * 2.2h;

                    float depthFade = (1.0 - smoothstep(0.55, 0.98, underwaterT));
                    submergedBed += _BedCausticColor.rgb * causticVal * (half)(submergeMask * depthFade * _BedCausticStrength);
                }

                float intoWaterT = smoothstep(_WaterThreshold - 0.03, _WaterThreshold + 0.06, maskVal);
                half3 combinedBasin = lerp(shoreBase, submergedBed, (half)intoWaterT);
                combinedBasin *= (1.0h - (half)(wetBand * _WetnessDarken));

                half3 topSurfaceColor = lerp(topAlbedo, combinedBasin, (half)shoreT);

                // 4. Outer wall vs top cap blend
                half wallMask = saturate((half)i.color.g);
                half normalTopMask = smoothstep((half)_BevelEdge1, (half)_BevelEdge2, N.y);
                half isTop = saturate(normalTopMask * (1.0h - wallMask * 0.92h));

                half heightGrad = saturate((half)i.color.b);
                half3 sideAlbedo = lerp(_SideBottomColor.rgb, _SideColor.rgb, heightGrad);
                half strata = (half)(sin(i.positionWS.y * 18.0 + i.uvTop.x * 4.0) * 0.025);
                sideAlbedo += strata;

                half3 albedo = lerp(sideAlbedo, topSurfaceColor, isTop);

                // 5. Painterly Sunlight + Warm Sky Ambient
                float4 shadowCoord = i.shadowCoord;
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                #endif
                Light mainLight = GetMainLight(shadowCoord);

                half3 lightDir = normalize((half3)mainLight.direction + half3(0.001h, 0.001h, 0.001h));
                half lightLum = dot((half3)mainLight.color, half3(0.299h, 0.587h, 0.114h));
                half3 sunColor = lightLum > 0.05h ? (half3)mainLight.color : half3(1.02h, 0.96h, 0.86h);
                if (lightLum <= 0.05h)
                {
                    lightDir = normalize(half3(-0.45h, 0.78h, -0.42h));
                }

                half NdotL = dot(N, lightDir);
                half wrapped = saturate((NdotL + (half)_DiffuseWrap) / (1.0h + (half)_DiffuseWrap));
                half shadowFactor = lerp(1.0h - (half)_ShadowStrength, 1.0h, (half)mainLight.shadowAttenuation);

                half3 shAmbient = SampleSH(N);
                half3 stylizedSkyAmbient = lerp(half3(0.28h, 0.25h, 0.22h), half3(0.42h, 0.44h, 0.48h), saturate(N.y * 0.5h + 0.5h));
                half3 indirect = max(shAmbient, stylizedSkyAmbient);
                half3 direct = sunColor * wrapped * shadowFactor;

                // Wet shoreline specular sheen
                half wetSpecMask = (half)wetBand * isTop;
                half3 V = normalize((half3)(GetCameraPositionWS() - i.positionWS));
                half3 H = normalize(lightDir + V);
                half spec = pow(saturate(dot(N, H)), 48.0h) * (half)_WetGloss * wetSpecMask * (half)mainLight.shadowAttenuation;

                half3 finalRGB = albedo * (direct * 0.88h + indirect * 0.42h) + spec * sunColor;
                finalRGB = MixFog(finalRGB, i.fogFactor);

                return half4(finalRGB, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }

            half4 ShadowPassFragment(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings DepthVert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half DepthFrag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthNormVert
            #pragma fragment DepthNormFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings DepthNormVert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 DepthNormFrag(Varyings i) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(i.normalWS), 0.0h);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
