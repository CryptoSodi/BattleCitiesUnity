Shader "BattleCities/UI/TVBackdropBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _BlurRadius ("Blur Radius (Screen Pixels)", Range(0, 16)) = 9.9
        _FogStrength ("White Fog Strength", Range(0, 1)) = 0.4
        _GlassStrength ("Glass Reflection Strength", Range(0, 1)) = 0
        _Refraction ("Edge Refraction", Range(0, 20)) = 0
        _Dispersion ("Edge Color Separation", Range(0, 1)) = 0
        _Saturation ("Glass Saturation", Range(0, 2)) = 1
        _GlassRect ("Glass Local Bounds", Vector) = (0, 0, 1000, 700)
        _GlassUVRect ("Glass Texture Bounds", Vector) = (0, 0, 1, 1)
        _CornerRadius ("Corner Radius", Float) = 16
        _BorderOpacity ("White Border Opacity", Range(0, 1)) = 0.3
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "TV Blur"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _BlurRadius;
            float _FogStrength;
            float _GlassStrength;
            float _Refraction;
            float _Dispersion;
            float _Saturation;
            float4 _GlassRect;
            float4 _GlassUVRect;
            float _CornerRadius;
            float _BorderOpacity;

            v2f vert(appdata input)
            {
                v2f output;
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                // Screen derivatives keep the frost consistent at different
                // resolutions instead of measuring blur in source-art texels.
                float2 delta = float2(length(float2(ddx(input.texcoord.x), ddy(input.texcoord.x))),
                    length(float2(ddx(input.texcoord.y), ddy(input.texcoord.y)))) * (_BlurRadius / 3.0);
                float2 uv = input.texcoord;
                // UVs survive Canvas batching and fullscreen scale changes;
                // incoming vertex positions need not remain graphic-local.
                float2 glassUV = (input.texcoord - _GlassUVRect.xy) / max(_GlassUVRect.zw, .00001);
                float2 panelUV = saturate(glassUV);
                float2 edgeDistance = min(panelUV, 1 - panelUV);
                float2 edgeNormal = (1 - smoothstep(0, .09, edgeDistance)) * sign(.5 - panelUV);
                uv += edgeNormal * _MainTex_TexelSize.xy * _Refraction;
                // Closely spaced Gaussian samples avoid the separated copies of
                // background edges produced by the old sparse nine-tap kernel.
                half4 color = 0;
                float3 totalWeight = 0;
                float alphaWeight = 0;
                [loop] for (int y = -6; y <= 6; y++)
                {
                    [loop] for (int x = -6; x <= 6; x++)
                    {
                        float weight = exp(-(x * x + y * y) / 18.0);
                        float2 separation = edgeNormal * _Dispersion;
                        float2 redTap = float2(x, y) - separation;
                        float2 blueTap = float2(x, y) + separation;
                        float3 weights = float3(exp(-dot(redTap, redTap) / 18.0), weight, exp(-dot(blueTap, blueTap) / 18.0));
                        half4 sampleColor = tex2D(_MainTex, saturate(uv + float2(x, y) * delta));
                        color.rgb += sampleColor.rgb * weights;
                        color.a += sampleColor.a * weight;
                        totalWeight += weights;
                        alphaWeight += weight;
                    }
                }
                color = (half4(color.rgb / totalWeight, color.a / alphaWeight) + _TextureSampleAdd) * input.color;
                half luminance = dot(color.rgb, half3(.2126, .7152, .0722));
                color.rgb = lerp(luminance.xxx, color.rgb, _Saturation);
                color.rgb = lerp(color.rgb, half3(1, 1, 1), saturate(_FogStrength));
                // A continuous sheen, without diagonal streaks or faceted bands.
                float reflection = smoothstep(.15, 1, panelUV.y) * _GlassStrength * .24;
                color.rgb = lerp(color.rgb, half3(.78, .90, 1), _GlassStrength * .18);
                color.rgb = lerp(color.rgb, half3(.92, .97, 1), reflection);
                float2 halfSize = _GlassRect.zw * .5;
                float radius = min(_CornerRadius, min(halfSize.x, halfSize.y));
                float2 q = abs((glassUV - .5) * _GlassRect.zw) - halfSize + radius;
                float distanceToEdge = length(max(q, 0)) + min(max(q.x, q.y), 0) - radius;
                float antialias = max(fwidth(distanceToEdge), .001);
                float border = 1 - smoothstep(.5, 1.5, -distanceToEdge);
                color.rgb = lerp(color.rgb, half3(1, 1, 1), border * _BorderOpacity);
                color.a *= 1 - smoothstep(-antialias, 0, distanceToEdge);
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
