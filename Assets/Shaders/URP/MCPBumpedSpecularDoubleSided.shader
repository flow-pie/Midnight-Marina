Shader "MCP/URP/Bumped Specular DoubleSided"
{
    Properties
    {
        _Color ("Main Color", Color) = (1, 1, 1, 1)
        _SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 1)
        _Shininess ("Shininess", Range(0.03, 1)) = 0.078125
        _MainTex ("Base (RGB) Gloss (A)", 2D) = "white" {}
        _BumpMap ("Normalmap", 2D) = "bump" {}
        [HideInInspector] _BumpScale ("Bump Scale", Range(0.0, 2.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }
        LOD 400

        Pass
        {
            Name "MCPBumpedSpecularDoubleSided"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _SpecColor;
                float4 _MainTex_ST;
                float  _Shininess;
                float  _BumpScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 tangentWS   : TEXCOORD2;
                float  tangentW    : TEXCOORD5;
                float2 uv          : TEXCOORD3;
                float  fogCoord    : TEXCOORD4;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.tangentWS = TransformObjectToWorldDir(IN.tangentOS.xyz);
                OUT.tangentW = IN.tangentOS.w;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.fogCoord = ComputeFogFactor(positions.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half3 albedo = tex.rgb * _Color.rgb;

                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv), _BumpScale);
                float3 bitangent = SafeNormalize(cross(IN.normalWS, IN.tangentWS) * IN.tangentW);
                float3 normalWS = SafeNormalize(
                    IN.tangentWS * normalTS.x + bitangent * normalTS.y + IN.normalWS * normalTS.z);

                float3 viewDirWS = SafeNormalize(GetWorldSpaceViewDir(IN.positionWS));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                half attenuation = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 diffuse = mainLight.color * (attenuation * ndotl * albedo);

                half3 halfDir = SafeNormalize(mainLight.direction + viewDirWS);
                half ndoth = saturate(dot(normalWS, halfDir));
                half specularPower = exp2(_Shininess * 11.0);
                half3 specular = mainLight.color * (attenuation * pow(ndoth, specularPower) * _SpecColor.rgb * _Shininess);

                half3 ambient = SampleSH(normalWS) * albedo;

                half3 color = MixFog(diffuse + specular + ambient, IN.fogCoord);
                return half4(color, tex.a * _Color.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
