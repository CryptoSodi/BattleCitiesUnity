Shader "BattleCities/Cartoon Clouds"
{
    Properties
    {
        _Tint("Cloud light",Color)=(1,1,1,.2)
        _Shadow("Ground shadow",Float)=0
        _SunDirection("Sun direction",Vector)=(0,1,1,0)
        _StageBounds("Stage bounds",Vector)=(0,-13,13,0)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
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
                half4 _Tint;float4 _StageBounds,_SunDirection;float _Shadow;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
            Varyings Vert(Attributes v){Varyings o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.world=p.positionWS;o.uv=v.uv;return o;}
            half4 Frag(Varyings i):SV_Target
            {
                float2 p=i.uv*2-1;
                const float3 lobes[6]={float3(-.53,-.05,.34),float3(-.27,.16,.44),float3(.12,.15,.48),float3(.48,.00,.35),float3(.23,-.25,.33),float3(-.19,-.25,.34)};
                float distance=2;float3 volumeNormal=float3(0,.001,0);
                [unroll] for(int j=0;j<6;j++)
                {
                    float2 q=p-lobes[j].xy;float d=length(q)-lobes[j].z;
                    distance=min(distance,d);
                    float h=sqrt(saturate(1-dot(q,q)/(lobes[j].z*lobes[j].z)));
                    volumeNormal+=float3(q.x*.5,h,q.y*.5)*h*h;
                }
                float softness=lerp(.035,.22,_Shadow);half alpha=1-smoothstep(-softness,softness,distance);
                half shade=.65+.35*saturate(dot(normalize(volumeNormal),normalize(_SunDirection.xyz)));
                half3 color=_Tint.rgb*lerp(shade,1,_Shadow);
                if(_Shadow>.5){float2 edge=min(i.world.xz-_StageBounds.xy,_StageBounds.zw-i.world.xz);alpha*=saturate(min(edge.x,edge.y)*8);}
                return half4(color,alpha*_Tint.a);
            }
            ENDHLSL
        }
    }
}
