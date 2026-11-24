Shader "LumiWorld/UI/Score Tutorial Hole"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0,0,0,0.78)
        _HoleCenter ("Hole Center (Normalized)", Vector) = (0.5,0.5,0,0)
        _HoleSize ("Hole Size (Normalized)", Vector) = (0.2,0.2,0,0)
        _Softness ("Edge Softness", Range(0,0.02)) = 0.003
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

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _HoleCenter;
            float4 _HoleSize;
            float _Softness;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 delta = abs(input.uv - _HoleCenter.xy);
                float2 halfSize = _HoleSize.xy * 0.5;
                float outsideDistance = max(delta.x - halfSize.x, delta.y - halfSize.y);
                float mask = smoothstep(-_Softness, _Softness, outsideDistance);
                fixed4 color = tex2D(_MainTex, input.uv) * input.color;
                color.a *= mask;
                return color;
            }
            ENDCG
        }
    }
}
