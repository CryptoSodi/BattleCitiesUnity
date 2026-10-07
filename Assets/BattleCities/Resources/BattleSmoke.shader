Shader "BattleCities/Toon Blast Smoke"
{
    Properties
    {
        _DarkColor("Smoke shadow",Color)=(.17,.19,.23,1)
        _LightColor("Smoke light",Color)=(.62,.65,.70,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _DarkColor,_LightColor;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;half fog:TEXCOORD1;};
            Varyings Vert(Attributes v)
            {
                Varyings o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS;o.uv=v.uv;o.color=v.color;o.fog=ComputeFogFactor(p.positionCS.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p=i.uv*2-1;
                float d=length(p-float2(-.26,.10))-.47;
                d=min(d,length(p-float2(.18,.27))-.46);
                d=min(d,length(p-float2(.40,-.15))-.39);
                d=min(d,length(p-float2(-.04,-.28))-.52);
                d=min(d,length(p-float2(-.44,-.20))-.31);
                float billow=sin(p.x*11+p.y*6)*sin(p.y*9-p.x*4);
                d+=billow*.025;
                half alpha=(1-smoothstep(-.08,.10,d))*i.color.a;
                half shade=saturate(.55+p.y*.30-p.x*.15+billow*.065);
                half3 light=lerp(_DarkColor.rgb,_LightColor.rgb,shade)*i.color.rgb;
                Light sun=GetMainLight();light*=.3+.7*saturate(sun.color.g);
                return half4(MixFog(light,i.fog),alpha);
            }
            ENDHLSL
        }
    }
}
