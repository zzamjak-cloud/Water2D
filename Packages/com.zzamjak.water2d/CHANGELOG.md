# Changelog

이 프로젝트의 주요 변경 사항을 기록합니다.

포맷은 [Keep a Changelog](https://keepachangelog.com/ko/1.1.0/)를 따르며,
버전은 [Semantic Versioning](https://semver.org/lang/ko/)을 따릅니다.

## [1.0.1] - 2026-08-23

### Changed

- 라이선스를 MIT에서 GNU General Public License v3.0 only (`GPL-3.0-only`)로 변경했습니다.
- 원저작권자 `zzamjak` 고지와 GPLv3 기반 소스 공개/동일 라이선스 배포 의무를 명확히 문서화했습니다.

## [1.0.0] - 2026-08-23

`Assets/Plugins/CAT/Water2D` 로 관리하던 2D 물 시뮬레이션 스크립트를 독립 UPM 패키지로 분리한 최초 릴리스입니다.

### Added

**시뮬레이션 코어 (`WaterSurface`)**

- 스프링 파동: Hooke's Law + damping + 좌·우 이웃 전파(2-pass), 고정 스텝 1/60s 로 프레임레이트 독립
- 이벤트 기반 슬립: 충돌 · `Splash()` · 랜덤 임펄스로만 깨어나고 표면이 정지하면 자동 슬립
  (슬립 중에는 스텝 연산과 정점 업로드를 모두 생략 — 유휴 프레임 비용 0.01µs 실측)
- 지속 출렁임(Ambient Wave): 사인 옥타브 중첩 + value noise 랜덤 성분을 정점마다 **해석적으로** 평가.
  스프링과 별개로 가산되므로 감쇠에 먹히지 않고 인스펙터 진폭이 그대로 표현됨
- 임의 위치 샘플링은 Catmull-Rom 보간(C1 연속)으로 정점 사이도 곡선으로 반환
- 자체 xorshift 난수 사용으로 `UnityEngine.Random` 전역 상태를 오염시키지 않음
- 매 프레임 경로에서 할당 0 B (1000 프레임 실측)

**월드용 `Water2D`** (`Add Component > CAT > Effects > 2D Water`)

- MeshFilter + MeshRenderer 기반 quad strip. 컴포넌트 추가 시 패키지 동봉 기본 머티리얼 자동 할당
- 물리 기능 opt-in: `Interaction Enabled` / `Buoyancy Enabled` 가 모두 OFF 면 `BoxCollider2D` 를 비활성해
  트리거 콜백·물리 브로드페이즈 비용을 제거
- 충돌 상호작용: 진입 `Rigidbody2D` 의 속도·질량 기반 impulse 자동 주입
- 부력: 잠긴 `Rigidbody2D` 에 잠김 깊이 × 질량 비례 부력 + 수중 선형·각속도 드래그
- 표면 라인(LineRenderer) 옵션. 프로퍼티 재설정을 dirty 기반으로 처리해 매 프레임 낭비 제거
- Sorting Layer / Order in Layer 지원 (SpriteRenderer 와 동일한 정렬 축)
- 공개 API: `Splash` / `SplashArea` / `ResetSurface` / `SampleSurfaceHeight` / `OnSplash` UnityEvent

**UI Canvas 용 `UIWater2D`** (`Add Component > CAT > UI > UI Water 2D`)

- `MaskableGraphic` 파생 → Canvas 배칭 · `Mask`(스텐실) · `RectMask2D`(`_ClipRect`) · `CanvasGroup` 알파 자동 대응
- `OnPopulateMesh` 로 RectTransform 을 채우는 quad strip 직접 생성. 모든 길이 단위는 px
- 포인터 상호작용 opt-in: 클릭 · 터치 · 드래그(간격 제한) 로 파동 주입
- 셰이더가 `_Time` 기반으로 애니메이션하므로 매 프레임 머티리얼 프로퍼티를 갱신하지 않음
  → `StencilMaterial` 캐시 · 마스크 체인의 stale 값 문제를 원천 차단
- 표면 라인 대신 셰이더 Foam 을 사용해 드로우콜 추가 없음

**셰이더**

- `CAT/Effects/2D Water` (URP: Universal2D + UniversalForward 2패스)
- `CAT/UI/Water2D` (UGUI 규약: Stencil 블록 + `UNITY_UI_CLIP_RECT`)
- 효과 본체를 `CAT_UIWater2D_Body.cginc` 로 분리해 공유
- 기능: 깊이 그라디언트 · 절차적 코스틱 · 굴절 왜곡 · 수면 거품 · 질감 텍스처 · 경계 페이드
- 질감 텍스처 합성 모드 (`0` = 곱셈 / `1` = 오버레이, 기본 오버레이)
- 모바일 최적화: 텍스처 페치 기본 0회(질감 사용 시 1회), 기능별 `shader_feature_local_fragment` 로
  미사용 연산 컴파일 제거, `if` 분기 없음, `half` 우선, `#pragma target 2.0`, GrabPass 미사용,
  SRP Batcher 호환 CBUFFER, 그림자·라이트 프로브·모션 벡터 자동 off

**에디터**

- 접기(폴딩) + 구분선 기반 커스텀 인스펙터. 접힌 헤더에 ON/OFF·값 요약 표시, 상태는 `EditorPrefs` 유지
- 머티리얼(셰이더) 수치를 컴포넌트 인스펙터 안에 기능별 섹션으로 통합 (머티리얼 창을 오갈 필요 없음)
- 비활성 기능의 하위 옵션 자동 숨김
- `▶ 60초 플레이` 에디터 프리뷰 — 커스텀 에디터가 `EditorApplication.update` 루프를 소유하고
  매 틱 뷰를 리페인트하므로 재생이 균일함 (남은 시간 진행바 + 즉시 정지)
- 실측 기반 성능·빌드 주의사항 HelpBox (GPU 비용 배수, shader_feature 스트리핑, 마스크 대응, Foam 두께 특성 등)
- 함정 감지 + 1클릭 수정: 텍스처가 있는데 `질감 텍스처 사용` 토글이 꺼진 상태, 타일링 > 1 인데 Wrap Mode 가 Clamp 인 상태
- Scene 기즈모 (bounds · 표면 라인 · 포인트 도트)

### 패키지 분리 시 변경

- 셰이더를 `Runtime/Resources/Water2D/` 로 이동 — 런타임 `Shader.Find` 경로가 빌드에 항상 포함되도록 보장
- 머티리얼 정책을 UPM 관행에 맞춰 변경 (기존 방식은 스크립트 경로 기준이라 읽기 전용 패키지 폴더를 가리켜 실패)
  - **기본 머티리얼을 패키지에 동봉** (`Runtime/Resources/Water2D/*.mat`) — 컴포넌트를 추가해도
    프로젝트에 에셋이 생기지 않는다. URP · UIParticle · UIEffect 등이 쓰는 방식
  - **전용 머티리얼 복제 시 저장 위치를 사용자에게 질의** (`EditorUtility.SaveFilePanelInProject`).
    마지막 저장 폴더를 기억하고, 없으면 현재 씬 폴더를 제안한다. Timeline · ShaderGraph 등이 쓰는 방식
  - 동봉 머티리얼이 셰이더를 참조하므로 셰이더 빌드 포함도 함께 보장된다
- 런타임 / 에디터 어셈블리 분리 (`CAT.Water2D` / `CAT.Water2D.Editor`) — 에디터 코드가 플레이어 빌드에 포함되지 않음
- 테스트 씬을 `Samples~/TestScene` 으로 이동 (컴파일 대상에서 제외, Package Manager 에서 선택 임포트)
- 스크립트 GUID 는 분리 이전과 **동일하게 유지** — 기존 프리팹 · 씬 참조가 그대로 살아남음

### 분리 이전 개발 이력 (참고)

패키지로 분리되기 전 `Assets/Plugins/CAT/Water2D` 에서 진행한 반복 개선 내역입니다. 위 1.0.0 에 모두 포함되어 있습니다.

| 내부 버전 | 내용 |
|-----------|------|
| v1.0 | 버텍스 기반 스프링 물 시뮬레이션, Rigidbody2D 트리거 상호작용, `Splash` API, 부력, Scene 기즈모 |
| v1.1 | 지속 출렁임(Ambient Wave), 전용 물 셰이더 `CAT/Effects/2D Water`, 머티리얼 자동 생성, `SplashArea` API |
| v1.2 | 물리 기능 opt-in 전환, 스프링 이벤트 기반 슬립, 표면 라인 dirty 기반 갱신, 주의사항 HelpBox |
| v1.3 | `UIWater2D` 추가, 시뮬 코어 `WaterSurface` 분리, UI 셰이더 `CAT/UI/Water2D`, 테스트 씬 |
| v1.4 | 표면 해상도 단일 노브 정리, 질감 합성 모드 추가, 인스펙터 폴딩·구분선, 60초 프리뷰 |
