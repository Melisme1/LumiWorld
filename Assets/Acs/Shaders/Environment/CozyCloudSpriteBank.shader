Shader "LumiWorld/Environment/CozyCloudSpriteBank"
{
    Properties
    {
        _MainTex ("Cloud Puff Atlas (2x2 RGBA32)", 2D) = "white" {}
        _TopColor ("Sunlit Cloud Top", Color) = (0.996, 0.990, 0.966, 1.0)
        _ShadowColor ("Cloud Belly Pastel", Color) = (0.68, 0.83, 0.94, 1.0)
        _RimColor ("Warm Sun Rim", Color) = (1.0, 0.98, 0.91, 1.0)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.24
        _BillboardTilt ("Camera Tilt Blend (0=Flat, 1=Full Billboard)", Range(0, 1)) = 0.64
        _DetailScale ("Procedural Fluffy Edge Scale", Float) = 0.36
        _EdgeFluffStrength ("Fluffy Edge Wisp Strength", Range(0, 0.6)) = 0.32
        _InnerDetailStrength ("Inner Cotton Shading Strength", Range(0, 0.4)) = 0.18
        _Opacity ("Overall Opacity", Range(0, 1)) = 0.95
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+40"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "CozyCloudSpriteBankForward"
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
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uvAtlas    : TEXCOORD0;
                float2 uvLocal    : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
                float4 tintAlpha  : TEXCOORD3;
                float3 worldPos   : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _TopColor;
                half4 _ShadowColor;
                half4 _RimColor;
                float _RimStrength;
                float _BillboardTilt;
                float _DetailScale;
                float _EdgeFluffStrength;
                float _InnerDetailStrength;
                float _Opacity;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _TileRect)
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

            // Screen-resolution cotton detail FBM: eliminates pixel/compression staircasing at any close-up zoom!
            float CloudDetailFBM(float2 p)
            {
                float sum = 0.0;
                float amp = 0.55;
                float2x2 rot = float2x2(0.8, -0.6, 0.6, 0.8);
                for (int i = 0; i < 3; i++)
                {
                    sum += amp * ValueNoise(p);
                    p = mul(rot, p * 2.07) + float2(13.1, 29.7);
                    amp *= 0.48;
                }
                return sum;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 centerWS = float3(UNITY_MATRIX_M[0].w, UNITY_MATRIX_M[1].w, UNITY_MATRIX_M[2].w);
                float scaleX = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                float scaleY = length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y));

                float3 camRight = normalize(float3(UNITY_MATRIX_V[0].x, 0.0, UNITY_MATRIX_V[0].z));
                float3 camUp = normalize(float3(UNITY_MATRIX_V[1].x, UNITY_MATRIX_V[1].y, UNITY_MATRIX_V[1].z));
                float3 flatAway = normalize(cross(camRight, float3(0.0, 1.0, 0.0)));
                float3 puffUp = normalize(lerp(flatAway, camUp, _BillboardTilt));

                float3 worldPos = centerWS
                    + camRight * (input.positionOS.x * scaleX)
                    + puffUp * (input.positionOS.y * scaleY);

                output.positionCS = TransformWorldToHClip(worldPos);

                float4 tile = UNITY_ACCESS_INSTANCED_PROP(Props, _TileRect);
                float2 safeUv = lerp(float2(0.02, 0.02), float2(0.98, 0.98), input.uv);
                if (tile.x < 0.0)
                {
                    safeUv.x = 1.0 - safeUv.x;
                    tile.x = -tile.x;
                }
                output.uvAtlas = safeUv * tile.xy + tile.zw;
                output.uvLocal = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.tintAlpha = UNITY_ACCESS_INSTANCED_PROP(Props, _TintAlpha);
                output.worldPos = worldPos;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uvAtlas);
                if (tex.a <= 0.003) discard;

                // Resolution-independent cotton edge wisps + gentle internal billow variation
                float2 wCoord = input.worldPos.xz * _DetailScale;
                float edgeNoise = CloudDetailFBM(wCoord + float2(_Time.y * 0.09, -_Time.y * 0.05));
                float innerNoise = CloudDetailFBM(wCoord * 0.45 - float2(_Time.y * 0.04, _Time.y * 0.06) + float2(5.3, 11.1));

                // Modulate ONLY the soft boundary transition zone (zero holes in core, zero clipping outside quad)
                float edgeZone = 4.0 * tex.a * (1.0 - tex.a);
                float density = saturate(tex.a + (edgeNoise - 0.5) * _EdgeFluffStrength * edgeZone);

                float bottomSoft = smoothstep(0.02, 0.22, input.uvLocal.y);
                float alpha = smoothstep(0.06, 0.88, density) * lerp(0.52, 1.0, bottomSoft) * _Opacity * input.tintAlpha.a;
                if (alpha <= 0.004) discard;

                float lum = dot(tex.rgb, half3(0.299, 0.587, 0.114));
                float verticalGrad = smoothstep(0.06, 0.84, input.uvLocal.y);
                float shade = saturate(lum * 0.64 + verticalGrad * 0.34 + (innerNoise - 0.5) * _InnerDetailStrength - 0.04);

                // Gently lift outer wispy fringe toward sunlit cream so overlapping clouds melt cleanly
                float edgeLift = (1.0 - smoothstep(0.22, 0.88, density)) * 0.34;
                shade = saturate(shade + edgeLift);

                half3 col = lerp(_ShadowColor.rgb, _TopColor.rgb, smoothstep(0.14, 0.90, shade));
                col *= input.tintAlpha.rgb;

                float rim = smoothstep(0.50, 0.95, input.uvLocal.y) * smoothstep(0.18, 0.78, density);
                col = lerp(col, _RimColor.rgb, saturate(rim * _RimStrength));

                col = MixFog(col, input.fogFactor);
                return half4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
