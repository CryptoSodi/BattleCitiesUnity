Shader "BattleCities/Cartoon Terrain Surfaces"
{
    Properties
    {
        _RippleMap("Cell distance field",2D)="gray"{}
        _Kind("Lava / Mud / Sand / Ice / Grease",Float)=0
        _FlowTime("Simulation clock",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_RippleMap);SAMPLER(sampler_RippleMap);
            CBUFFER_START(UnityPerMaterial)
                float _Kind,_FlowTime;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float shore:TEXCOORD1;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float2 uv:TEXCOORD1;float shore:TEXCOORD2;half fog:TEXCOORD3;};
            Varyings Vert(Attributes v)
            {
                Varyings o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.uv=v.uv;o.shore=v.shore;
                o.fog=ComputeFogFactor(p.positionCS.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float time=_FlowTime;
                float2 p=i.uv;
                float2 flow=float2(sin(p.y*2+time*.24),cos(p.x*1.7-time*.2))*.018;
                half4 cells=SAMPLE_TEXTURE2D(_RippleMap,sampler_RippleMap,p*.30+flow+float2(time*.002,-time*.003));
                float edge=cells.r*.5;
                float aa=max(fwidth(edge),.002);
                half3 color,emission=0;
                if(_Kind<.5)
                {
                    // Slow dark crust islands, molten orange seams and hot yellow junctions.
                    half seam=1-smoothstep(.020-aa,.055+aa,edge);
                    half core=1-smoothstep(.009,.022,edge);
                    half pulse=.90+.10*sin(time*1.1+p.x*1.4+p.y*.7);
                    color=lerp(half3(.095,.012,.008),half3(.35,.048,.013),cells.b*.7+cells.g*.22);
                    emission=lerp(half3(1.7,.12,.006),half3(2.5,.95,.06),core)*seam*pulse;
                    color+=half3(.28,.018,.003)*exp2(-edge*18);
                }
                else if(_Kind<1.5)
                {
                    // Opaque muddy water: broad olive-brown currents with restrained cream foam.
                    float waves=sin(p.x*3+p.y*4+sin(p.y*2-time*.18)*1.5+time*.3);
                    color=lerp(half3(.12,.095,.035),half3(.34,.26,.10),.5+waves*.18+cells.b*.14);
                    float current=smoothstep(.78,.91,waves)-smoothstep(.96,1,waves);
                    color+=half3(.095,.075,.032)*current;
                    float2 eddy=frac(p*.7+float2(time*.012,.27))-.5;
                    float ripple=1-smoothstep(.009,.024,abs(length(eddy)-.17));
                    color+=half3(.10,.087,.043)*ripple*smoothstep(-.3,.25,eddy.y)*.65;
                }
                else if(_Kind<2.5)
                {
                    // Soft irregular contour bands suggest a yielding sand bed, not dry cracked soil.
                    float bands=sin(p.x*5+p.y*2.8+sin(p.y*3+time*.12)*1.8+sin(p.x*2-time*.15));
                    float ridge=smoothstep(.66,.86,bands)-smoothstep(.90,1,bands);
                    color=lerp(half3(.43,.245,.065),half3(.77,.56,.24),.54+bands*.12+cells.b*.17);
                    color+=ridge*half3(.085,.063,.028);
                    float2 b=frac(p*.87+float2(.23,.47))-.5;
                    float ringRadius=.03+frac(time*.12+floor(p.x*.87)*.31+floor(p.y*.87)*.21)*.12;
                    float bubble=(1-smoothstep(.009,.020,abs(length(b)-ringRadius)))*.14;
                    color-=bubble*half3(.19,.14,.065);
                }
                else if(_Kind<3.5)
                {
                    // Joined ice sheets retain still cracks and long pale reflections.
                    half4 frozen=SAMPLE_TEXTURE2D(_RippleMap,sampler_RippleMap,p*.18);
                    float crack=1-smoothstep(.006,.014,frozen.r*.5);
                    float glint=pow(saturate(sin(p.x*1.8+p.y*3.6)),18);
                    color=lerp(half3(.13,.42,.59),half3(.48,.79,.87),frozen.b*.45+frozen.g*.35);
                    color+=glint*half3(.20,.25,.25)+crack*half3(.20,.29,.29);
                    color=lerp(half3(.70,.88,.90),color,smoothstep(.012,.055,i.shore));
                }
                else
                {
                    // A dark oily slick with broad violet/teal sheen, distinct from water and ice.
                    float sheen=sin(p.x*3+p.y*2+sin(p.y*2-time*.08)*2);
                    color=lerp(half3(.022,.025,.031),half3(.057,.045,.075),.5+.5*sheen);
                    color+=pow(saturate(sheen),12)*half3(.025,.095,.085);
                }
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 ambient=SampleSH(half3(0,1,0));
                half3 lighting=ambient*.8+sun.color*(.4+.5*saturate(sun.direction.y))*lerp(.62,1,sun.shadowAttenuation);
                half brightness=max(dot(lighting,half3(.2126,.7152,.0722)),.22);
                half shore=lerp(.52,1,smoothstep(.035,.19,i.shore));
                half alpha=_Kind>3.5?smoothstep(.012,.15,i.shore):1;
                return half4(MixFog((color*brightness+emission)*shore,i.fog),alpha);
            }
            ENDHLSL
        }
    }
}
