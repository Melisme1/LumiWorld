Shader "LumiWorld/UI/CardRarityShimmer"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        [Header(Cozy Satin Sheen)]
        _ShimmerColor ("Shimmer Color", Color) = (1, 0.97, 0.86, 1)
        _ShimmerSpeed ("Sweep Speed", Float) = 0.23
        _ShimmerWidth ("Sweep Width", Float) = 0.13
        _ShimmerIntensity ("Sweep Intensity", Range(0, 1.5)) = 0.48

        [Header(Diagonal Corner Leaf Glint)]
        _GlintColor ("Corner Glint Color", Color) = (1, 0.95, 0.72, 1)
        _GlintSpeed ("Glint Speed", Float) = 1.85
        _GlintIntensity ("Glint Intensity", Range(0, 1.5)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            fixed4 _ShimmerColor;
            float _ShimmerSpeed;
            float _ShimmerWidth;
            float _ShimmerIntensity;

            fixed4 _GlintColor;
            float _GlintSpeed;
            float _GlintIntensity;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            float EvalStarGlint(float2 uv, float2 center, float phase)
            {
                float2 d = (uv - center) * float2(1.0, 1.46); // aspect-correct for 2:3 card
                float r = length(d);
                float core = exp(-r * r * 170.0);
                // Soft 4-point star rays (subtle cozy sparkle)
                float rayH = exp(-abs(d.y) * 46.0) * exp(-abs(d.x) * 14.0);
                float rayV = exp(-abs(d.x) * 46.0) * exp(-abs(d.y) * 14.0);
                float star = saturate(core * 0.85 + (rayH + rayV) * 0.45);
                float pulse = 0.5 + 0.5 * sin(_Time.y * _GlintSpeed + phase);
                pulse = pulse * pulse * pulse; // calm bloom-and-rest curve
                return star * pulse;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 tex = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                half4 col = tex * IN.color;

                // Mask shimmer strictly to visible frame/leaf pixels so center artwork is never veiled
                float frameMask = smoothstep(0.15, 0.85, tex.a);

                // 1. Diagonal cozy satin sheen sweep (with breathing pause between sweeps)
                float diag = IN.texcoord.x * 0.72 + (1.0 - IN.texcoord.y) * 0.68;
                float cycle = frac(_Time.y * _ShimmerSpeed);
                float sweepPos = lerp(-0.45, 1.95, cycle);
                float invW = 1.0 / max(0.025, _ShimmerWidth);
                float bandMain = exp(-pow((diag - sweepPos) * invW, 2.0));
                float bandTrail = exp(-pow((diag - (sweepPos - 0.13)) * (invW * 1.6), 2.0)) * 0.32;
                float sheen = (bandMain + bandTrail) * _ShimmerIntensity;

                // 2. Soft corner-leaf glints at Top-Left and Bottom-Right finials
                float glintTL = EvalStarGlint(IN.texcoord, float2(0.075, 0.935), 0.0);
                float glintBR = EvalStarGlint(IN.texcoord, float2(0.925, 0.065), 2.4);
                float glint = (glintTL + glintBR) * _GlintIntensity;

                col.rgb += (_ShimmerColor.rgb * sheen + _GlintColor.rgb * glint) * frameMask;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
