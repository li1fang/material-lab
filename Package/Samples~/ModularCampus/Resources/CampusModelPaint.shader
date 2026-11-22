Shader "Material Lab/Campus paint" {
Properties {
 _BaseColor("Paint",Color)=(1,1,1,1)
 _BaseMap("Paint texture",2D)="white"{}
}
SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
 Pass {
 Name "CampusForward"
 Tags {"LightMode"="UniversalForwardOnly"}
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 CBUFFER_START(UnityPerMaterial)
 half4 _BaseColor;
 CBUFFER_END
 struct Attributes {float4 positionOS:POSITION; float3 normalOS:NORMAL;};
 struct Varyings {float4 pos:SV_POSITION;float3 world:TEXCOORD0;half3 n:TEXCOORD1;};
 Varyings Vert(Attributes v){Varyings o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.pos=p.positionCS;o.world=p.positionWS;o.n=TransformObjectToWorldNormal(v.normalOS);return o;}
 half4 Frag(Varyings i):SV_Target{
  Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
  half3 n=normalize(i.n);
  half direct=saturate(dot(n,light.direction)), sky=n.y*.5h+.5h;
  half shade=.56h+.16h*sky+.28h*direct*lerp(.3h,1.h,light.shadowAttenuation);
  return half4(_BaseColor.rgb*shade,1);
 }
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 }
}
