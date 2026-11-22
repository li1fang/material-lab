Shader "Material Destruction/Workbench"
{
    Properties { _Tint ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct A { float3 p:POSITION; float3 n:NORMAL; half4 c:COLOR; };
            struct V { float4 p:SV_POSITION; half3 n:TEXCOORD0; half3 c:COLOR; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END
            V vert(A v)
            {
                V o;
                o.p = TransformObjectToHClip(v.p);
                o.n = TransformObjectToWorldNormal(v.n);
                o.c = v.c.rgb * _Tint.rgb;
                return o;
            }
            half4 frag(V v, bool front:SV_IsFrontFace):SV_Target
            {
                half3 n=normalize(v.n)*(front ? 1 : -1);
                half light=.70+.34*saturate(dot(n,normalize(half3(-.45,.74,-.53))));
                half3 col=v.c*light;
                #ifndef UNITY_COLORSPACE_GAMMA
                col=SRGBToLinear(col);
                #endif
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
