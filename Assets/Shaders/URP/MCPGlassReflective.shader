Shader "MCP/URP/Glass Reflective"
{
    Properties
    {
        _SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 1)
        _Shininess ("Shininess", Range(0.01, 1)) = 0.078125
        _ReflectColor ("Reflection Color", Color) = (1, 1, 1, 0.5)
        _Cube ("Reflection Cubemap", Cube) = "black" { TexGen CubeReflect }
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
        LOD 300

        Pass
        {
            Name "MCPGlassReflective"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURECUBE(_Cube);
            SAMPLER(sampler_Cube);

            CBUFFER_START(UnityPerMaterial)
                float4 _SpecColor;
                float4 _ReflectColor;
                float _Shininess;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  fogCoord   : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.fogCoord = ComputeFogFactor(positions.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = SafeNormalize(GetWorldSpaceViewDir(IN.positionWS));
                float3 reflectDir = reflect(-viewDirWS, normalWS);

                half4 reflection = SAMPLE_TEXTURECUBE_LOD(_Cube, sampler_Cube, reflectDir, 0);
                half3 emission = reflection.rgb * _ReflectColor.rgb;
                half  alpha = saturate(reflection.a * _ReflectColor.a);

                half3 color = MixFog(emission, IN.fogCoord);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
