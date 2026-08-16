Shader "Custom/Lobby/Backdrop"
{
    Properties
    {
        [MainColor] _BaseColor("Wall Color", Color) = (0.54, 0.56, 0.60, 1)
        _HorizonColor("Horizon Color", Color) = (0.62, 0.64, 0.68, 1)
        _TintColor("Image Tint", Color) = (0.54, 0.56, 0.60, 1)

        _TexA("Image A", 2D) = "white" {}
        _TexB("Image B", 2D) = "white" {}
        _TexAAspect("Image A Aspect", Float) = 1.777
        _TexBAspect("Image B Aspect", Float) = 1.777
        _WallAspect("Wall Aspect", Float) = 2.667
        _ScrollA("Scroll A", Range(0, 1)) = 0
        _ScrollB("Scroll B", Range(0, 1)) = 0
        _Fade("Crossfade", Range(0, 1)) = 0
        _Zoom("Zoom", Range(1, 2)) = 1.2
        _ImageOpacity("Image Opacity", Range(0, 1)) = 0.28
        _Saturation("Saturation", Range(0, 1)) = 0.12
        _Contrast("Contrast", Range(0.2, 1.5)) = 0.7
        _Horizon("Horizon Height", Range(0.05, 0.8)) = 0.34
        _HorizonStrength("Horizon Strength", Range(0, 1)) = 0.9
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
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_TexA);
            TEXTURE2D(_TexB);
            SamplerState sampler_linear_repeat;

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _HorizonColor;
                half4 _TintColor;
                float _TexAAspect;
                float _TexBAspect;
                float _WallAspect;
                float _ScrollA;
                float _ScrollB;
                float _Fade;
                float _Zoom;
                float _ImageOpacity;
                float _Saturation;
                float _Contrast;
                float _Horizon;
                float _HorizonStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            float2 TileUV(float2 uv, float texAspect, float wallAspect, float zoom, float scroll)
            {
                float aspect = max(texAspect, 0.01);
                float wall = max(wallAspect, 0.01);
                float z = max(zoom, 1.0);

                // 세로에 맞추고 가로는 타일링. 늘어나지 않습니다.
                float2 tiled;
                tiled.x = uv.x * (wall / aspect) * z + scroll;
                tiled.y = (uv.y - 0.5) * z + 0.5;
                tiled.x = frac(tiled.x);
                tiled.y = saturate(tiled.y);
                return tiled;
            }

            half3 FilterImage(half3 rgb)
            {
                half luma = dot(rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half3 grey = luma.xxx;
                half3 sat = lerp(grey, rgb, (half)_Saturation);
                sat = saturate((sat - 0.5h) * (half)_Contrast + 0.5h);
                return sat * _TintColor.rgb;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 color = _BaseColor.rgb;

                if (_ImageOpacity > 0.001h)
                {
                    float2 uvA = TileUV(input.uv, _TexAAspect, _WallAspect, _Zoom, _ScrollA);
                    half3 imgA = FilterImage(SAMPLE_TEXTURE2D(_TexA, sampler_linear_repeat, uvA).rgb);

                    half3 img = imgA;
                    if (_Fade > 0.001h)
                    {
                        float2 uvB = TileUV(input.uv, _TexBAspect, _WallAspect, _Zoom, _ScrollB);
                        half3 imgB = FilterImage(SAMPLE_TEXTURE2D(_TexB, sampler_linear_repeat, uvB).rgb);
                        img = lerp(imgA, imgB, (half)_Fade);
                    }

                    color = lerp(color, img, (half)_ImageOpacity);
                }

                float horizon = 1.0 - smoothstep(0.0, max(_Horizon, 0.001), input.uv.y);
                horizon *= horizon;
                color = lerp(color, _HorizonColor.rgb, (half)(horizon * _HorizonStrength));

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half DepthFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
