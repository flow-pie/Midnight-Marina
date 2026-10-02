Shader "MCP/URP/Reflective Transparent Diffuse"
{
    Properties
    {
        _Color ("Main Color", Color) = (1, 1, 1, 1)
        _ReflectColor ("Reflection Color", Color) = (1, 1, 1, 1)
        _MainTex ("Base Texture (RGB)", 2D) = "white" {}
        _Cube ("Reflection Cubemap", Cube) = "_Skybox" { TexGen CubeReflect }
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }
        LOD 200

        Pass
        {
            Name "MCPReflectiveTransparentDiffuse"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURECUBE(_Cube);
            SAMPLER(sampler_Cube);

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _ReflectColor;
                float4 _MainTex_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float  fogCoord   : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.fogCoord = ComputeFogFactor(positions.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 baseColor = tex * _Color;

                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = SafeNormalize(GetWorldSpaceViewDir(IN.positionWS));
                float3 reflectDir = reflect(-viewDirWS, normalWS);

                half4 reflection = SAMPLE_TEXTURECUBE_LOD(_Cube, sampler_Cube, reflectDir, 0);
                reflection *= tex.a;

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                half attenuation = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 diffuse = mainLight.color * (attenuation * ndotl * baseColor.rgb);
                half3 ambient = SampleSH(normalWS) * baseColor.rgb;
                half3 emission = reflection.rgb * _ReflectColor.rgb;

                half3 color = MixFog(diffuse + ambient + emission, IN.fogCoord);
                return half4(color, baseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
