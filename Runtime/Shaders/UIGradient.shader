Shader "Control/UI/Gradient"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _GradientTex ("Gradient Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _ColorA ("Color A", Color) = (1, 1, 1, 1)
        _ColorB ("Color B", Color) = (0, 0, 0, 1)
        _GradientStart ("Gradient Start", Vector) = (0, 0.5, 0, 0)
        _GradientEnd ("Gradient End", Vector) = (1, 0.5, 0, 0)
        _GradientSquash ("Gradient Squash", Vector) = (0, 1.5, 0, 0)
        _GradientType ("Gradient Type", Float) = 0

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
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _GradientTex;
            fixed4 _TextureSampleAdd;
            fixed4 _Color;
            fixed4 _ColorA;
            fixed4 _ColorB;
            float4 _GradientStart;
            float4 _GradientEnd;
            float4 _GradientSquash;
            float4 _ClipRect;
            float _GradientType;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            float Cross2(float2 a, float2 b)
            {
                return a.x * b.y - a.y * b.x;
            }

            float2 GetGradientCoordinates(float2 uv)
            {
                float2 anchorA = _GradientStart.xy;
                float2 axisX = _GradientEnd.xy - anchorA;
                float2 axisY = _GradientSquash.xy - anchorA;
                float axisXLength = max(length(axisX), 0.000001);
                float determinant = Cross2(axisX, axisY);

                if (abs(determinant) <= 0.000001)
                {
                    axisY = float2(-axisX.y, axisX.x) / axisXLength;
                    axisY *= axisXLength;
                    determinant = Cross2(axisX, axisY);
                }

                float2 offset = uv - anchorA;
                return float2(Cross2(offset, axisY) / determinant, Cross2(axisX, offset) / determinant);
            }

            float EvaluateLinear(float2 uv)
            {
                return GetGradientCoordinates(uv).x;
            }

            float EvaluateRadial(float2 uv)
            {
                return length(GetGradientCoordinates(uv));
            }

            float EvaluateAngular(float2 uv)
            {
                float2 coords = GetGradientCoordinates(uv);
                if (dot(coords, coords) <= 0.000001)
                    return 0.0;

                return frac(atan2(coords.y, coords.x) / 6.28318530718);
            }

            float EvaluateDiamond(float2 uv)
            {
                float2 coords = GetGradientCoordinates(uv);
                return abs(coords.x) + abs(coords.y);
            }

            float EvaluateGradient(float2 uv)
            {
                if (_GradientType < 0.5)
                    return EvaluateLinear(uv);

                if (_GradientType < 1.5)
                    return EvaluateRadial(uv);

                if (_GradientType < 2.5)
                    return EvaluateAngular(uv);

                return EvaluateDiamond(uv);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float t = saturate(EvaluateGradient(IN.texcoord));
                fixed4 gradientColor = tex2D(_GradientTex, float2(t, 0.5));
                fixed4 textureColor = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                fixed4 color = gradientColor * textureColor * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
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
