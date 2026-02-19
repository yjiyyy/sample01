// WeaponTrail_SoftAlpha
// 부드러운 알파만 사용. Vertex Color Alpha로 길이 방향 페이드.

Shader "Custom/WeaponTrail_SoftAlpha"
{
    Properties
    {
        _Color ("Trail Tint", Color) = (1, 1, 1, 0.9)
        _MainTex ("Gradient Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SoftAlpha"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float vertexAlpha = i.color.a;
                if (vertexAlpha < 0.001) discard;
                half texAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                float finalAlpha = vertexAlpha * texAlpha * _Color.a;
                if (finalAlpha < 0.001) discard;
                return half4(_Color.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }

    // Built-in fallback
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;

            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };

            v2f vert(appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            half4 frag(v2f i) : SV_Target {
                float vertexAlpha = i.color.a;
                if (vertexAlpha < 0.001) discard;
                half texAlpha = tex2D(_MainTex, i.uv).a;
                float finalAlpha = vertexAlpha * texAlpha * _Color.a;
                if (finalAlpha < 0.001) discard;
                return half4(_Color.rgb, finalAlpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
