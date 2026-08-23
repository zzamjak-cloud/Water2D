// UIWater2D 셰이더 공통 본체.
// 메인 셰이더(CAT/UI/Water2D)와 SoftMaskLight Hidden 변형이 이 파일을 공유한다.
//
// UV 규약 (UIWater2D 메시): u = 좌→우(0~1), v = 하단 0 → 수면 1.
// 모바일 우선: 텍스처 샘플 최대 1회, 나머지는 전부 절차적(ALU). 기능별 shader_feature 로 제거.
#ifndef CAT_UIWATER2D_BODY_INCLUDED
#define CAT_UIWATER2D_BODY_INCLUDED

sampler2D _MainTex;
float4 _MainTex_ST;

half4 _ShallowColor;
half4 _DeepColor;
half _GradientPower;

half4 _TexTint;
half _TexStrength;
half _TexBlendMode;
float4 _TexTiling;
float4 _TexScroll;

half4 _CausticsColor;
half _CausticsStrength;
half _CausticsScale;
half _CausticsSpeed;
half _CausticsSharpness;
half _CausticsDepthBias;

half _DistortStrength;
half _DistortScale;
half _DistortSpeed;

half4 _FoamColor;
half _FoamThickness;
half _FoamSoftness;

half _BottomFade;
half _EdgeFade;

/// 굴절 왜곡된 패턴 UV. 씬 텍스처(GrabPass) 미사용.
inline float2 CAT_UIWater_PatternUV(float2 uv, float t)
{
    float2 patternUV = uv;
#if defined(_CAT_DISTORT)
    float2 wobble;
    wobble.x = sin(uv.y * _DistortScale + t * _DistortSpeed);
    wobble.y = cos(uv.x * _DistortScale * 0.9 - t * _DistortSpeed * 1.1);
    patternUV += wobble * _DistortStrength;
#endif
    return patternUV;
}

/// 물속 색상 계산. 반환 알파는 스트레이트 알파.
inline half4 CAT_UIWater_Color(float2 uv, float t)
{
    float2 patternUV = CAT_UIWater_PatternUV(uv, t);

    // 깊이 그라디언트: v=1(수면) → Shallow, v=0(하단) → Deep
    half depthT = pow(saturate(uv.y), _GradientPower);
    half4 col = lerp(_DeepColor, _ShallowColor, depthT);

    // 질감 텍스처 (샘플 1회, 스크롤 포함)
#if defined(_CAT_TEXTURE)
    float2 texUV = patternUV * _TexTiling.xy + _TexScroll.xy * t;
    half4 tex = tex2D(_MainTex, texUV);
    // 합성 모드: 0 = 곱셈(명암 있는 불투명 텍스처용), 1 = 오버레이(알파 있는 패턴·데칼용)
    half3 mulBlend = col.rgb * tex.rgb * _TexTint.rgb;
    half3 ovlBlend = tex.rgb * _TexTint.rgb;
    half3 src = lerp(mulBlend, ovlBlend, _TexBlendMode);
    col.rgb = lerp(col.rgb, src, _TexStrength * tex.a);
#endif

    // 코스틱(물결 무늬): sin 교차 패턴. 노이즈 텍스처 없이 표현.
#if defined(_CAT_CAUSTICS)
    float2 cp = patternUV * _CausticsScale;
    float ct = t * _CausticsSpeed;
    half c = sin(cp.x + sin(cp.y * 1.7 + ct)) * sin(cp.y - sin(cp.x * 1.3 - ct * 0.8));
    c = saturate(c * 0.5 + 0.5);
    c = pow(c, _CausticsSharpness);
    half causticMask = lerp(1.0, depthT, _CausticsDepthBias);
    col.rgb += _CausticsColor.rgb * (c * _CausticsStrength * causticMask * _CausticsColor.a);
#endif

    // 수면 거품 라인
#if defined(_CAT_FOAM)
    half distFromSurface = 1.0 - uv.y;
    half foam = 1.0 - smoothstep(_FoamThickness, _FoamThickness + _FoamSoftness, distFromSurface);
    foam *= _FoamColor.a;
    col.rgb = lerp(col.rgb, _FoamColor.rgb, foam);
    col.a = max(col.a, foam);
#endif

    // 경계 페이드 (분기 없이 처리: 값이 0 이면 max(1e-4) 로 smoothstep 이 즉시 1)
    half bottomEdge = max(1e-4, _BottomFade);
    half sideEdge = max(1e-4, _EdgeFade);
    half fade = smoothstep(0.0, bottomEdge, uv.y)
              * smoothstep(0.0, sideEdge, uv.x)
              * smoothstep(0.0, sideEdge, 1.0 - uv.x);

    col.a = saturate(col.a * fade);
    return col;
}

#endif // CAT_UIWATER2D_BODY_INCLUDED
