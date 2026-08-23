// UI Canvas 용 2D 물 셰이더. UGUI 규약(스텐실 마스크 / RectMask2D 클리핑) 준수.
// 효과 본체는 CAT_UIWater2D_Body.cginc 에 있으며 SoftMaskLight 변형과 공유한다.
Shader "CAT/UI/Water2D"
{
    Properties
    {
        // UGUI 배칭 호환: CanvasRenderer 가 Graphic.mainTexture 를 주입한다 (질감 텍스처로 사용)
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Color)]
        _ShallowColor ("Shallow Color (수면)", Color) = (0.45, 0.85, 1.0, 0.65)
        _DeepColor ("Deep Color (심층)", Color) = (0.03, 0.25, 0.5, 0.9)
        _GradientPower ("Gradient Power", Range(0.1, 6)) = 1.4

        [Header(Texture)]
        [Toggle(_CAT_TEXTURE)] _TextureEnabled ("질감 텍스처 사용", Float) = 0
        _TexTint ("질감 색조", Color) = (1, 1, 1, 1)
        _TexStrength ("질감 세기", Range(0, 1)) = 0.35
        _TexBlendMode ("질감 합성 (0=곱셈, 1=오버레이)", Range(0, 1)) = 1
        _TexTiling ("질감 타일링 (XY)", Vector) = (2, 1, 0, 0)
        _TexScroll ("질감 스크롤 (XY/초)", Vector) = (0.06, 0.01, 0, 0)

        [Header(Caustics)]
        [Toggle(_CAT_CAUSTICS)] _CausticsEnabled ("물결 무늬(코스틱) 사용", Float) = 1
        _CausticsColor ("코스틱 색", Color) = (0.7, 0.95, 1.0, 1)
        _CausticsStrength ("코스틱 세기", Range(0, 2)) = 0.55
        _CausticsScale ("코스틱 밀도", Range(0.5, 40)) = 12
        _CausticsSpeed ("코스틱 속도", Range(0, 5)) = 0.6
        _CausticsSharpness ("코스틱 선명도", Range(1, 16)) = 4
        _CausticsDepthBias ("깊이 감쇠 (0=균일, 1=수면집중)", Range(0, 1)) = 0.5

        [Header(Distortion)]
        [Toggle(_CAT_DISTORT)] _DistortEnabled ("굴절 왜곡 사용", Float) = 1
        _DistortStrength ("왜곡 세기", Range(0, 0.3)) = 0.03
        _DistortScale ("왜곡 밀도", Range(0.5, 30)) = 6
        _DistortSpeed ("왜곡 속도", Range(0, 5)) = 0.8

        [Header(Foam)]
        [Toggle(_CAT_FOAM)] _FoamEnabled ("수면 거품 사용", Float) = 1
        _FoamColor ("거품 색", Color) = (1, 1, 1, 0.9)
        _FoamThickness ("거품 두께 (0~1 UV)", Range(0, 0.5)) = 0.03
        _FoamSoftness ("거품 경계 부드러움", Range(0.001, 0.5)) = 0.02

        [Header(Depth Fade)]
        _BottomFade ("하단 페이드", Range(0, 1)) = 0
        _EdgeFade ("좌우 페이드", Range(0, 0.5)) = 0

        // UI 스텐실/마스크 설정
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15

        // RectMask2D: 기본값을 넓게 두어 미주입 시 전체가 사라지지 않게 함
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
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

            #pragma shader_feature_local_fragment _CAT_TEXTURE
            #pragma shader_feature_local_fragment _CAT_CAUSTICS
            #pragma shader_feature_local_fragment _CAT_DISTORT
            #pragma shader_feature_local_fragment _CAT_FOAM

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "CAT_UIWater2D_Body.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                half4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                half4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color;
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                half4 col = CAT_UIWater_Color(IN.texcoord, _Time.y);
                col *= IN.color; // Graphic 색상 · CanvasGroup 알파

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
