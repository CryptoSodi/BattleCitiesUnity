Shader "BattleCities/Weather Particles"
{
    Properties {_Splash("Splash mode",Float)=0}
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Splash;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float2 data:TEXCOORD1;half4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float2 data:TEXCOORD1;half4 color:COLOR;};
            Varyings Vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;o.data=v.data;o.color=v.color;return o;}
            half4 Frag(Varyings i):SV_Target
            {
                float2 p=i.uv*2-1;half alpha;
                if(_Splash<.5)alpha=(1-smoothstep(.12,1,abs(p.x)))*pow(saturate(1-i.uv.y),.6);
                else
                {
                    float radius=length(p);float aa=max(fwidth(radius),.04);
                    half ring=1-smoothstep(.055,.055+aa,abs(radius-.73));float angle=atan2(p.y,p.x);
                    half beads=pow(saturate(cos(angle*4+i.data.y*2)),5)*ring;alpha=lerp(beads,ring,i.data.x);
                }
                return half4(i.color.rgb,alpha*i.color.a);
            }
            ENDHLSL
        }
    }
}
