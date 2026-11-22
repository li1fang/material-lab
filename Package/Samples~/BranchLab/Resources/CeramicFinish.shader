Shader "Material Destruction/Celadon"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct A {float3 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct V {float4 p:SV_POSITION;float3 pos:TEXCOORD0;half3 n:TEXCOORD1;float2 uv:TEXCOORD2;half3 c:COLOR;};
            V vert(A a)
            {
                V o;
                o.pos=TransformObjectToWorld(a.p);
                o.p=TransformWorldToHClip(o.pos);
                o.n=TransformObjectToWorldNormal(a.n);
                o.uv=a.uv;o.c=a.color.rgb;
                return o;
            }
            half4 frag(V v):SV_Target
            {
                half3 n=normalize(v.n);
                half3 toEye=GetWorldSpaceNormalizeViewDir(v.pos);
                half3 key=normalize(half3(-.48,.82,-.30));
                half3 halfDir=normalize(key+toEye);
                half clay=step(v.c.g,v.c.r);
                float2 grain=floor(v.uv*670);
                half noise=frac(sin(dot(grain,float2(12.981,78.233)))*43758.545);
                half base=.70+.34*saturate(dot(n,key));
                half speck=lerp(1,.83,step(.974,noise));
                half3 col=v.c*base*lerp(speck, .82+.18*noise,clay);
                half edge=pow(1-saturate(dot(n,toEye)),4);
                col+=(1-clay)*half3(.94,.99,1)*(.24*pow(saturate(dot(n,halfDir)),42)+.10*edge);
                #ifndef UNITY_COLORSPACE_GAMMA
                col=SRGBToLinear(col);
                #endif
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
