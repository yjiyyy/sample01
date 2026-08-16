Shader "Custom/Lobby/HorizonMist"
{
    Properties
    {
        [MainColor] _Color("Mist Color", Color) = (0.62, 0.64, 0.68, 0.85)
        _FadeStartZ("Fade Start Z", Float) = 0.2
        _FadeEndZ("Fade End Z", Float) = 7
        _Power("Falloff", Range(0.4, 4)) = 0.75
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Mist"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _FadeStartZ;
                float _FadeEndZ;
                float _Power;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float startZ = _FadeStartZ;
                float endZ = max(_FadeEndZ, startZ + 0.01);
                float fade = saturate((input.positionWS.z - startZ) / (endZ - startZ));
                fade = fade * fade * (3.0 - 2.0 * fade);
                return half4(_Color.rgb, _Color.a * (half)fade);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
