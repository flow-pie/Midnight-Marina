Shader "MCP/URP/Tree Bark"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _BumpSpecMap ("Normal (RG) Spec (B)", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _TranslucencyMap ("Translucency (A)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 1)
        _BumpScale ("Bump Scale", Range(0.0, 2.0)) = 1.0
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.333
        _SquashAmount ("Squash Amount", Range(0.0, 2.0)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }
        LOD 400

        Pass
        {
            Name "MCPTreeBark"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_BumpSpecMap);
            SAMPLER(sampler_BumpSpecMap);

            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            TEXTURE2D(_TranslucencyMap);
            SAMPLER(sampler_TranslucencyMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BumpSpecMap_ST;
                float4 _BumpMap_ST;
                float4 _TranslucencyMap_ST;
                float4 _Color;
                float4 _SpecColor;
                float  _BumpScale;
                float  _Cutoff;
                float  _SquashAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float3 tangentWS    : TEXCOORD2;
                float  tangentW     : TEXCOORD5;
                float2 uv           : TEXCOORD3;
                float2 uv2          : TEXCOORD4;
                float4 color        : COLOR;
                float  fogCoord     : TEXCOORD6;
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
                OUT.uv2 = TRANSFORM_TEX(IN.uv, _BumpSpecMap);
                OUT.color = IN.color;
                OUT.fogCoord = ComputeFogFactor(positions.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 albedoTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 tint = IN.color * _Color;
                half3 albedo = albedoTex.rgb * tint.rgb;

                // The pack stores a combined normal+specular map; only the
                // tangential normal is used when no dedicated normal map is set.
                half4 specTex = SAMPLE_TEXTURE2D(_BumpSpecMap, sampler_BumpSpecMap, IN.uv2);

                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv), _BumpScale);
                float3 bitangent = SafeNormalize(cross(IN.normalWS, IN.tangentWS) * IN.tangentW);
                float3 normalWS = SafeNormalize(
                    IN.tangentWS * normalTS.x + bitangent * normalTS.y + IN.normalWS * normalTS.z);

                clip(albedoTex.a - _Cutoff);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                half attenuation = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half ndotl = saturate(dot(normalWS, mainLight.direction));

                half3 diffuse = mainLight.color * (attenuation * ndotl * albedo);
                half3 ambient = SampleSH(normalWS) * albedo;

                float3 viewDirWS = SafeNormalize(GetWorldSpaceViewDir(IN.positionWS));
                half3 halfDir = SafeNormalize(mainLight.direction + viewDirWS);
                half ndoth = saturate(dot(normalWS, halfDir));
                half3 specular = mainLight.color *
                    (attenuation * pow(ndoth, exp2(6.0)) * specTex.b * _SpecColor.rgb);

                half3 color = MixFog(diffuse + ambient + specular, IN.fogCoord);
                return half4(color, albedoTex.a * tint.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
