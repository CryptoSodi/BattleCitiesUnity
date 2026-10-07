Shader "BattleCities/Forest Water URP"
{
    Properties
    {
        _BaseMap("Forest Pack Water",2D)="white"{}
        _RippleMap("Cartoon ripple pattern",2D)="black"{}
        _DeepColor("Deep water",Color)=(.003,.25,.78,1)
        _ShallowColor("Shallow water",Color)=(.005,.90,1,1)
        [HDR] _FoamColor("White caustics",Color)=(1.45,1.85,2,1)
        _FlowTime("Flow clock",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10"}
        Pass
        {
            Name "ForestWater"
            Tags {"LightMode"="UniversalForward"}
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            TEXTURE2D(_RippleMap);SAMPLER(sampler_RippleMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _DeepColor,_ShallowColor,_FoamColor;
                float _FlowTime;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float shore:TEXCOORD1;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float2 uv:TEXCOORD1;float shore:TEXCOORD2;half fog:TEXCOORD3;};
            Varyings Vert(Attributes v)
            {
                Varyings o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.uv=v.uv;o.shore=v.shore;o.fog=ComputeFogFactor(p.positionCS.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                // Compact closed cells drift and breathe gently instead of stretching into long streaks.
                float2 uv=i.uv*.52+float2(_FlowTime*.006,-_FlowTime*.009);
                uv+=float2(sin(i.uv.y*2.1+_FlowTime*.65),cos(i.uv.x*1.8-_FlowTime*.55))*.006;
                half4 cells=SAMPLE_TEXTURE2D(_RippleMap,sampler_RippleMap,uv);
                half wave=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv*.42+float2(_FlowTime*.012,_FlowTime*.018)).r;
                float edge=cells.r*.5;
                float aa=max(fwidth(edge)*.65,.002);
                half causticLine=1-smoothstep(.020-aa,.020+aa,edge);
                half glow=exp2(-edge*23);
                float shore=i.shore;
                half3 water=lerp(_DeepColor.rgb,_ShallowColor.rgb,saturate(.12+cells.g*.63+cells.b*.13+wave*.05));
                water+=half3(.005,.32,.42)*(glow+cells.a*.3);
                half shimmer=.91+.09*sin(i.uv.x*3+i.uv.y*2-_FlowTime*.8);
                water=lerp(water,_FoamColor.rgb*shimmer,causticLine);
                // Retain the approved recessed banks, with a narrow shadow under their lip.
                water*=lerp(.48,1,smoothstep(.065,.21,shore));
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 ambient=SampleSH(half3(0,1,0));
                half3 light=ambient*.8+sun.color*(.4+.5*saturate(sun.direction.y))*lerp(.62,1,sun.shadowAttenuation);
                // Stylized water retains its blue/cyan hue under warm daylight, but still dims at night.
                half brightness=max(dot(light,half3(.2126,.7152,.0722)),.22);
                half3 color=water*brightness;
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
