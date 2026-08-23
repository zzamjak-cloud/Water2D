# Water2D

버텍스 기반 2D 물 시뮬레이션 Unity 패키지입니다. 모바일 게임 최적화를 최우선으로 설계했습니다.

| 컴포넌트 | 대상 | 렌더 | 메뉴 |
|----------|------|------|------|
| `Water2D` | 월드 오브젝트 | MeshFilter + MeshRenderer | `Add Component > CAT > Effects > 2D Water` |
| `UIWater2D` | UI Canvas 자식 | CanvasRenderer (`MaskableGraphic`) | `Add Component > CAT > UI > UI Water 2D` |

두 컴포넌트는 시뮬레이션 코어(`WaterSurface`)를 공유하므로 거동과 수치 의미가 동일합니다.

- **지속 출렁임(Ambient Wave)**: 충돌이 없어도 표면이 계속 출렁입니다. 강도·빈도·랜덤성을 수치로 조절합니다.
- **물리 기능 opt-in**: 충돌·부력·포인터 입력은 기본 OFF. 모두 꺼져 있으면 콜라이더까지 비활성되어 물리 비용이 0 입니다.
- **스프링 슬립**: 파동은 이벤트로만 깨어나고 표면이 멈추면 자동으로 잠들어 연산·정점 업로드를 생략합니다.
- **마스크 대응**: `UIWater2D` 는 `Mask`(스텐실) · `RectMask2D` · `CanvasGroup` 알파에 UGUI 표준 방식으로 대응합니다.

## 요구 사항

- Unity 6000.0 (Unity 6) 이상
- **URP 17.0.4 이상** — 월드용 셰이더가 URP 셰이더 라이브러리를 인클루드합니다
- uGUI (`com.unity.ugui`) — `UIWater2D` 용

## 설치

```bash
openupm add com.zzamjak.water2d
```

또는 Package Manager 의 `Install package from git URL...` 에 아래를 입력합니다.

```
https://github.com/zzamjak-cloud/Water2D.git?path=/Packages/com.zzamjak.water2d#v1.0.0
```

## 빠른 사용법

1. 빈 GameObject 에 `Water2D` 를 추가합니다 (UI 라면 Canvas 자식에 `UIWater2D`).
   → 패키지 동봉 기본 머티리얼이 자동 할당됩니다 (프로젝트에 파일이 생기지 않습니다).
   개체별로 수치를 다르게 하려면 인스펙터의 `복제` 로 전용 머티리얼을 만듭니다 (저장 위치 선택).
2. `크기`(월드) 또는 `RectTransform` 크기(UI) 를 조절합니다.
3. `지속 출렁임` 섹션에서 진폭·파장·진행 속도를 맞춥니다.
4. 인스펙터 하단 `▶ 60초 플레이` 로 Play 없이 확인합니다.

```csharp
// 파동 주입 (localX: 월드는 로컬 X, UI는 RectTransform 로컬 X)
water.Splash(0f, -0.3f);
water.SplashArea(0.5f, -0.4f, 0.8f);   // 폭을 가진 파동

// 런타임 토글
water.AmbientEnabled = true;
water.InteractionEnabled = true;       // Water2D
water.PointerInteractionEnabled = true; // UIWater2D

// 수면 높이 샘플링 (연출 오브젝트 정렬용)
float y = water.SampleSurfaceHeight(0f);
```

## 성능 요약 (실측, 데스크톱 기준)

| 상태 | 프레임 비용 |
|------|-------------|
| 완전 유휴 (파형 OFF · 스프링 슬립) | 0.01µs |
| 월드, 표면 24점, 파형 ON | 약 5~6µs |
| UI, 표면 34점, 파형 ON | 약 21µs (메시 재생성 포함) |

- 매 프레임 경로 GC 할당 0 B
- GPU 픽셀 비용은 물이 덮는 화면 면적에 비례하며, 대부분이 코스틱입니다
  (스프라이트 대비 전 기능 OFF 1.04x / 왜곡+거품 1.33x / 기본 3.23x / +질감 3.61x)
- 텍스처 페치는 기본 0회 (질감 사용 시 1회)

## 주의 사항

- 기능 토글은 `shader_feature` 입니다. 런타임에 `EnableKeyword()` 로 켜려면 해당 조합을 쓰는
  머티리얼이 빌드에 포함되어 있어야 합니다 (없으면 조용히 무시됩니다).
- 질감 텍스처는 `질감 텍스처 사용` 토글이 켜져 있어야 반영됩니다. 인스펙터가 이 상태를 감지해 경고합니다.
- 타일링을 1보다 크게 쓰려면 텍스처 Wrap Mode 가 `Repeat` 여야 합니다 (Sprite 임포트 기본은 `Clamp`).
- `Foam Thickness` 는 UV 기준이라 Depth·Rect 높이에 비례해 두꺼워집니다.

자세한 문서는 [레포지토리 README](https://github.com/zzamjak-cloud/Water2D) 를,
버전별 변경 사항은 [CHANGELOG](CHANGELOG.md) 를 참고하세요.

## 라이선스

GNU General Public License v3.0 only (`GPL-3.0-only`) — [LICENSE.md](LICENSE.md). 저작권 및 저작자 고지는 [NOTICE.md](NOTICE.md) 를 참고하세요.

원저작권자는 zzamjak입니다. 재배포본, 수정본, 파생물은 원저작권 고지를 유지하고 GPLv3 조건에 따라 소스 공개 및 동일 라이선스 배포 의무를 따라야 합니다. 독점/폐쇄 소스 소프트웨어로 재배포하는 것은 허용되지 않습니다.
