Shader "AI RTS/Bay Brick Paint"
{
    Properties
    {
        _BaseMap ("Brick face albedo", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _TileScale ("Tiles per model unit", Float) = 2
        _BrickOrigin ("Original mesh centre in the wall", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "BayBrickForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _BrickOrigin;
            float _TileScale;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                // Map in original model units, so a loose piece keeps its glaze and grain.
                float3 at = (input.positionOS + _BrickOrigin.xyz) * _TileScale;
                float3 n = abs(input.normalOS);
                if (n.z >= n.x && n.z >= n.y) o.uv = at.xy;
                else if (n.x > n.y) o.uv = at.zy;
                else o.uv = at.xz;
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                Light key = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 normal = normalize(input.normalWS);
                half direct = saturate(dot(normal, key.direction));
                half sky = normal.y * .5h + .5h;
                half lighting = .56h + .16h * sky + .28h * direct * lerp(.3h, 1.h, key.shadowAttenuation);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                return half4(albedo * _BaseColor.rgb * lighting, 1.h);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
