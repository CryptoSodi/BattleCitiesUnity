Shader "BattleCities/Water Banks URP"
{
    Properties
    {
        _BaseMap("Surrounding ground",2D)="white"{}
        _BaseColor("Ground tint",Color)=(1,1,1,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;half4 color:COLOR;half fog:TEXCOORD3;};
            Varyings Vert(Attributes v)
            {
                Varyings o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                o.uv=TRANSFORM_TEX(v.uv,_BaseMap);o.color=v.color;o.fog=ComputeFogFactor(p.positionCS.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
                // Exposed soil below the narrow grassy lip, darkened where the water meets the bank.
                half soil=smoothstep(.37,.92,i.color.a);
                half grain=.94+.06*sin(i.positionWS.x*61+i.positionWS.z*43);
                base=lerp(base,half3(.28,.205,.125)*grain,soil*.85)*i.color.rgb;
                half3 normal=normalize(i.normalWS);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 lighting=SampleSH(normal)+sun.color*saturate(dot(normal,sun.direction))*sun.shadowAttenuation;
                return half4(MixFog(base*max(lighting,half3(.10,.12,.16)),i.fog),1);
            }
            ENDHLSL
        }
    }
}
