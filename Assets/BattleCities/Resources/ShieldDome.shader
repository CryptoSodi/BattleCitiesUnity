Shader "BattleCities/ShieldDome"
{
    Properties { _Pulse("Pulse",Float)=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Pulse;
            CBUFFER_END
            struct Input { float4 positionOS:POSITION;float3 normalOS:NORMAL; };
            struct Output { float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float height:TEXCOORD2; };
            Output vert(Input v)
            {
                Output o;o.world=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(v.normalOS);o.height=v.positionOS.y;return o;
            }
            half4 frag(Output i):SV_Target
            {
                clip(i.height);
                float rim=pow(1-saturate(dot(normalize(i.normal),normalize(GetWorldSpaceViewDir(i.world)))),2);
                float foot=1-smoothstep(0,.025,i.height);
                float glow=saturate(rim*.7+foot*.15);
                return half4(lerp(float3(.035,.28,.7),float3(.35,.9,1),glow),(.012+glow*.55)*_Pulse);
            }
            ENDHLSL
        }
    }
}

