Shader "SbScene/UI/Additive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SbSceneUvRect ("SbScene UV Rect", Vector) = (0,0,1,1)
        _SbSceneFlip ("SbScene Flip", Vector) = (0,0,0,0)
        _SbSceneUvMode ("SbScene UV Mode", Float) = 0
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
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

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
        BlendOp Add, Max
        Blend SrcAlpha One, One One
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _MainTex_ST;
            float4 _ClipRect;
            float4 _SbSceneUvRect;
            float4 _SbSceneFlip;
            float _SbSceneUvMode;

            float2 SbSceneTransformUv(float2 uv)
            {
                float2 minUv = _SbSceneUvRect.xy;
                float2 maxUv = _SbSceneUvRect.zw;
                float2 sizeUv = max(abs(maxUv - minUv), float2(0.000001, 0.000001));
                float2 p = saturate((uv - minUv) / sizeUv);

                float left = _SbSceneFlip.x > 0.5 ? 1.0 : 0.0;
                float right = _SbSceneFlip.x > 0.5 ? 0.0 : 1.0;
                float top = _SbSceneFlip.y > 0.5 ? 1.0 : 0.0;
                float bottom = _SbSceneFlip.y > 0.5 ? 0.0 : 1.0;

                float2 topLeft = float2(left, top);
                float2 bottomLeft = float2(left, bottom);
                float2 topRight = float2(right, top);
                float2 bottomRight = float2(right, bottom);
                float mode = floor(_SbSceneUvMode + 0.5);
                if (mode > 0.5 && mode < 1.5)
                {
                    topLeft = float2(right, top);
                    bottomLeft = float2(left, top);
                    topRight = float2(right, bottom);
                    bottomRight = float2(left, bottom);
                }
                else if (mode > 1.5 && mode < 2.5)
                {
                    topLeft = float2(right, bottom);
                    bottomLeft = float2(right, top);
                    topRight = float2(left, bottom);
                    bottomRight = float2(left, top);
                }
                else if (mode > 2.5 && mode < 3.5)
                {
                    topLeft = float2(left, bottom);
                    bottomLeft = float2(right, bottom);
                    topRight = float2(left, top);
                    bottomRight = float2(right, top);
                }

                float2 topUv = lerp(topLeft, topRight, p.x);
                float2 bottomUv = lerp(bottomLeft, bottomRight, p.x);
                float2 transformed = lerp(topUv, bottomUv, p.y);
                return minUv + transformed * sizeUv;
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = SbSceneTransformUv(TRANSFORM_TEX(v.texcoord, _MainTex));
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, i.texcoord) * i.color;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
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