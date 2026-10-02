Shader "MCP/URP/Tree Leaves Optimized"
{
    Properties
    {
        _MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
        _BumpSpecMap ("Normal (RG) Spec (B)", 2D) = "white" {}
        _ShadowTex ("Self Shadow (A)", 2D) = "white" {}
        _TranslucencyMap ("Translucency (A)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _TranslucencyColor ("Translucency Color", Color) = (0.73, 0.85, 0.41, 1)
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.35
        _ShadowOffsetScale ("Shadow Offset Scale", Float) = 1
        _ShadowStrength ("Self Shadow Strength", Range(0.0, 1.0)) = 0.8
        _TranslucencyViewDependency ("Translucency View Dependency", Range(0.0, 1.0)) = 0.7
        _SquashAmount ("Squash Amount", Range(0.0, 2.0)) = 1
        _Translucency ("Translucency Strength", Range(0.0, 2.0)) = 0.7
        _Wrap ("Diffuse Wrap", Range(0.0, 1.0)) = 0.5
        _Sheen ("Leaf Sheen", Range(0.0, 1.0)) = 0.15
        _AmbientBoost ("Ambient Boost", Range(0.0, 2.0)) = 0.25
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
            Name "MCPTreeLeavesOptimized"
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

            TEXTURE2D(_BumpSpecMap);
            SAMPLER(sampler_BumpSpecMap);

            TEXTURE2D(_ShadowTex);
            SAMPLER(sampler_ShadowTex);

            TEXTURE2D(_TranslucencyMap);
            SAMPLER(sampler_TranslucencyMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _ShadowTex_ST;
                float4 _BumpSpecMap_ST;
                float4 _TranslucencyMap_ST;
                float4 _Color;
                float4 _TranslucencyColor;
                float  _Cutoff;
                float  _ShadowOffsetScale;
                float  _ShadowStrength;
                float  _TranslucencyViewDependency;
                float  _SquashAmount;
                float  _Translucency;
                float  _Wrap;
                float  _Sheen;
                float  _AmbientBoost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float4 color      : COLOR;
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
                OUT.color = IN.color;
                OUT.fogCoord = ComputeFogFactor(positions.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 albedoTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half  alpha = albedoTex.a * IN.color.a * _Color.a;

                clip(alpha - _Cutoff);

                half3 albedo = albedoTex.rgb * IN.color.rgb * _Color.rgb;

                float3 viewDirWS = SafeNormalize(GetWorldSpaceViewDir(IN.positionWS));

                // Foliage cards are two sided. Flip the normal toward the viewer so
                // back faces are lit instead of shading to black, which is what made
                // the canopy read as dark patches. SV_IsFrontFace is not usable as a
                // varying on every platform, so the view direction is used instead.
                float3 normalWS = SafeNormalize(IN.normalWS);
                normalWS *= sign(dot(normalWS, viewDirWS));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                half attenuation = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half ndotl = saturate(dot(normalWS, mainLight.direction));

                // Wrapped diffuse: thin leaves scatter light around the terminator
                // instead of terminating hard.
                half wrap = saturate((dot(normalWS, mainLight.direction) + _Wrap) / (1.0 + _Wrap));
                half3 diffuse = mainLight.color * (attenuation * wrap * albedo);

                // Transmission: light coming through the leaf from behind.
                // _TranslucencyViewDependency widens the falloff, so a low value
                // gives a tight glow and a high value spreads it across the canopy.
                half backLight = saturate(dot(viewDirWS, -mainLight.direction));
                half transmit = pow(backLight, lerp(1.0, 8.0, _TranslucencyViewDependency)) * _Translucency;
                half3 transmission = mainLight.color *
                    (attenuation * transmit * _TranslucencyColor.rgb * albedo);

                half3 ambient = SampleSH(normalWS) * albedo * (1.0 + _AmbientBoost);

                // Broad waxy highlight; a tight specular makes leaves look plastic.
                float3 halfDir = SafeNormalize(mainLight.direction + viewDirWS);
                half sheen = pow(saturate(dot(normalWS, halfDir)), 12.0) * _Sheen;
                half3 specular = mainLight.color * (attenuation * sheen * _TranslucencyColor.rgb);

                // _ShadowOffsetScale biases the shadow lookup along the light direction, which
                // keeps self-shadowing from banding on flat cards.
                float2 shadowUV = IN.uv + _ShadowOffsetScale * 0.0015 * float2(1.0, 0.0);
                half4 shadowTex = SAMPLE_TEXTURE2D(_ShadowTex, sampler_ShadowTex, shadowUV);
                half3 color = (diffuse + ambient + specular) * lerp(1.0h, shadowTex.a, _ShadowStrength)
                            + transmission;

                color = MixFog(color, IN.fogCoord);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
