Shader "Custom/EnemyBodySkinTint"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _SkinMask("Skin Mask (Baked)", 2D) = "black" {}
        _SourceSkinColor("Source Skin Color", Color) = (0.9411765, 0.8196079, 0.6980392, 1)
        _SkinTintColor("Skin Tint Color", Color) = (0.9411765, 0.8196079, 0.6980392, 1)
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        _Metallic("Metallic", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            // URP Lit(Forward+)과 동일한 조명·그림자 키워드.
            // _FORWARD_PLUS가 없으면 Forward+에서 주광 attenuation이 깨져 피부가 플랫해 보임.
            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_SkinMask);
            SAMPLER(sampler_SkinMask);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _SourceSkinColor;
                half4 _SkinTintColor;
                half _BumpScale;
                half _Smoothness;
                half _Metallic;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half3 viewDirWS : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
                half fogFactor : TEXCOORD6;
            };

            // 텍스처 명암(밝기)는 유지하고 피부색(채도/색상)만 바꿉니다.
            half3 ApplySkinTintPreserveLuminance(half3 albedo, float2 uv)
            {
                half mask = SAMPLE_TEXTURE2D(_SkinMask, sampler_SkinMask, uv).r;
                if (mask <= 1e-4h)
                    return albedo;

                const half3 lumWeights = half3(0.2126390059h, 0.7151686788h, 0.0721923154h);
                half lum = dot(albedo, lumWeights);
                half srcLum = dot(_SourceSkinColor.rgb, lumWeights);
                half3 tinted = _SkinTintColor.rgb * (lum / max(srcLum, 1e-4h));
                return lerp(albedo, tinted, mask);
            }

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.normalWS = normalInput.normalWS;
                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.shadowCoord = GetShadowCoord(vertexInput);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                albedoAlpha.rgb = ApplySkinTintPreserveLuminance(albedoAlpha.rgb, input.uv);

                half3 normalTS = half3(0.0h, 0.0h, 1.0h);
                #if defined(_NORMALMAP)
                normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                #endif

                float3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                float3 normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
                normalWS = NormalizeNormalPerPixel(normalWS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedoAlpha.rgb;
                surfaceData.alpha = albedoAlpha.a;
                surfaceData.metallic = _Metallic;
                surfaceData.specular = half3(0.0h, 0.0h, 0.0h);
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = normalTS;
                surfaceData.emission = half3(0.0h, 0.0h, 0.0h);
                surfaceData.occlusion = 1.0h;
                surfaceData.clearCoatMask = 0.0h;
                surfaceData.clearCoatSmoothness = 0.0h;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = SafeNormalize(input.viewDirWS);
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                inputData.shadowCoord = input.shadowCoord;
                #else
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = half3(0.0h, 0.0h, 0.0h);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
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
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
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
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
