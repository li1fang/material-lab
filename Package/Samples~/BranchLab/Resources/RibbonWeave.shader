Shader "Material Destruction/Woven cotton"
{
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
            struct A {float3 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};
            struct V {float4 p:SV_POSITION;half3 n:TEXCOORD0;float2 uv:TEXCOORD1;};
            V vert(A a)
            {
                V o;o.p=TransformObjectToHClip(a.p);
                o.n=TransformObjectToWorldNormal(a.n);o.uv=a.uv;return o;
            }
            half4 frag(V v):SV_Target
            {
                float edge=min(v.uv.y,1-v.uv.y);
                float stripe=abs(edge-.128);
                float aa=max(fwidth(v.uv.y),.0004);
                half3 dye=lerp(half3(.16,.30,.40),half3(.89,.83,.65),1-smoothstep(.030-aa,.030+aa,stripe));
                dye=lerp(dye,half3(.80,.45,.25),1-smoothstep(.008-aa,.008+aa,abs(edge-.056)));
                dye=lerp(dye,half3(.54,.65,.65),1-smoothstep(.007-aa,.007+aa,abs(edge-.199)));
                float2 q=v.uv*float2(238,61);
                float2 fade=1-smoothstep(.32,1.0,fwidth(q));
                float warp=cos(6.283*q.y)*fade.y;
                float weft=cos(6.283*q.x)*fade.x;
                float twill=sin(2.094*(q.x+q.y))*min(fade.x,fade.y);
                float ink=.93+.105*warp+.082*weft+.10*twill;
                float3 n=normalize(v.n);if(n.z>0)n=-n;
                float light=.79+.32*saturate(dot(n,normalize(float3(-.29,.59,-.75))));
                half3 c=dye*(ink*light);
                #ifndef UNITY_COLORSPACE_GAMMA
                c=SRGBToLinear(c);
                #endif
                return half4(c,1);
            }
            ENDHLSL
        }
    }
}
