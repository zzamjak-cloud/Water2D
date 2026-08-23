# Water2D

[![openupm](https://img.shields.io/npm/v/com.zzamjak.water2d?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.zzamjak.water2d/)
[![license](https://img.shields.io/badge/license-GPL--3.0--only-blue.svg)](Packages/com.zzamjak.water2d/LICENSE.md)

버텍스 기반 2D 물 시뮬레이션 Unity 패키지입니다. 모바일 게임 최적화를 최우선으로 설계했습니다.

이 레포지토리는 **개발용 Unity 프로젝트**이며, 패키지 본체는
[`Packages/com.zzamjak.water2d`](Packages/com.zzamjak.water2d) 에 임베디드되어 있습니다.
버전별 변경 사항은 [CHANGELOG](Packages/com.zzamjak.water2d/CHANGELOG.md) 를 참고하세요.

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
- **URP (Universal Render Pipeline) 17.0.4 이상** — 월드용 셰이더가 URP 셰이더 라이브러리를 인클루드합니다
- uGUI (`com.unity.ugui`) — `UIWater2D` 용. Unity 기본 내장 모듈입니다

> UI 물(`UIWater2D`)만 사용하는 경우에도 패키지가 URP 를 의존성으로 선언하므로 URP 가 함께 설치됩니다.

---

## 설치 방법

### 1. OpenUPM (권장)

```bash
openupm add com.zzamjak.water2d
```

또는 `Packages/manifest.json` 에 스코프 레지스트리를 직접 추가합니다.

```json
{
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": ["com.zzamjak"]
    }
  ],
  "dependencies": {
    "com.zzamjak.water2d": "1.0.3"
  }
}
```

### 2. Unity Package Manager (Git URL)

1. Unity 에디터에서 `Window > Package Manager` 를 엽니다.
2. 좌측 상단 `+` → `Install package from git URL...` 을 선택합니다.
3. 아래 URL을 입력하고 `Install` 을 누릅니다.

```
https://github.com/zzamjak-cloud/Water2D.git?path=/Packages/com.zzamjak.water2d#v1.0.3
```

`#v1.0.3` 을 생략하면 `main` 브랜치의 최신 상태를 가져옵니다.

### 3. manifest.json 직접 편집 (Git)

```json
{
  "dependencies": {
    "com.zzamjak.water2d": "https://github.com/zzamjak-cloud/Water2D.git?path=/Packages/com.zzamjak.water2d#v1.0.3"
  }
}
```

### 테스트 씬 가져오기

`Window > Package Manager > Water2D > Samples > Test Scene > Import` 를 누르면
`Assets/Samples/Water2D/1.0.0/Test Scene/` 에 테스트 씬과 성능 점검용 HUD 가 복사됩니다.

### OpenUPM 레지스트리 (등록 완료)

이 패키지는 OpenUPM 에 등록되어 있습니다.

- 패키지 페이지: https://openupm.com/packages/com.zzamjak.water2d/
- 등록 PR: [openupm/openupm#6830](https://github.com/openupm/openupm/pull/6830) (merged)
- 등록 메타데이터: [`openupm-package.yml`](openupm-package.yml) (제출본 사본)

**새 버전 배포 절차**

1. `Packages/com.zzamjak.water2d/package.json` 의 `version` 을 올립니다.
2. `CHANGELOG.md` 에 변경 사항을 기록합니다.
3. 커밋 후 `vX.Y.Z` 태그를 푸시하면 OpenUPM 이 자동으로 빌드·배포합니다.

```bash
git tag v1.1.0 && git push origin v1.1.0
```

스코프는 `com.zzamjak` 이므로 같은 스코프의 다른 패키지(CATSprite 등)와 스코프 레지스트리 설정을 공유합니다.

### 기존 프로젝트에서 이전하는 경우

이 패키지의 스크립트 GUID는 분리 이전(`Assets/Plugins/CAT/Water2D`)과 **동일하게 유지**되어 있습니다.
따라서 기존 프리팹·씬의 컴포넌트 참조가 그대로 살아남습니다.

1. 패키지를 먼저 설치합니다 (위 방법 중 하나).
2. 기존 `Assets/Plugins/CAT/Water2D` 폴더를 삭제합니다. (동일 클래스 중복 정의를 피하기 위해 삭제가 필요합니다)
3. 기본 머티리얼이 패키지 동봉본으로 바뀌었습니다. 기존에 쓰던 머티리얼을 계속 쓰려면
   인스펙터의 `Material` 필드에 그대로 지정해 두면 됩니다.

---

## 머티리얼 정책

| 상황 | 동작 |
|------|------|
| 컴포넌트 추가 | 패키지 동봉 공용 기본 머티리얼을 할당. **프로젝트에 파일이 생기지 않음** |
| 수치 조정 (공용) | 개발 중인 임베디드 패키지에서는 직접 편집 가능. OpenUPM·Git 설치본은 읽기 전용 |
| 개체별로 다르게 | 인스펙터 `복제` → **저장 위치를 직접 선택** (마지막 폴더 기억, 없으면 현재 씬 폴더 제안) |

Unity 패키지 생태계의 관행을 그대로 따랐습니다.

- 기본 리소스를 패키지에 동봉: URP(`Runtime/Materials/*.mat`), UIParticle, UIEffect
- 사용자 소유 에셋은 저장 위치 질의(`EditorUtility.SaveFilePanelInProject`): Timeline Signal, ShaderGraph, Tile Palette
- (고정 경로 자동 생성은 URP Global Settings·Input System 처럼 **프로젝트 전역 싱글톤 설정**에 쓰이는 방식이라
  개체별 머티리얼에는 맞지 않습니다)

동봉 머티리얼은 `Resources` 에 있으므로 빌드에 항상 포함되고, 이 머티리얼이 셰이더를 참조하므로
셰이더 빌드 포함도 함께 보장됩니다.

## 패키지 구조

```
Packages/com.zzamjak.water2d/
├── package.json
├── README.md · CHANGELOG.md · LICENSE.md
├── Runtime/
│   ├── CAT.Water2D.asmdef        # 런타임 어셈블리 (UnityEngine.UI 참조)
│   ├── Water2D.cs                # 월드용 컴포넌트
│   ├── UIWater2D.cs              # UI Canvas 용 컴포넌트
│   ├── WaterSurface.cs           # 시뮬 코어 (두 컴포넌트 공유)
│   ├── WaterPoint.cs             # 표면 포인트 구조체
│   └── Resources/Water2D/        # 셰이더 (런타임 Shader.Find 대비 빌드 포함 보장)
│       ├── CAT_Water2D.shader
│       ├── CAT_UIWater2D.shader
│       └── CAT_UIWater2D_Body.cginc
├── Editor/
│   ├── CAT.Water2D.Editor.asmdef # 에디터 전용 (Editor 플랫폼 한정 → 빌드 미포함)
│   ├── Water2DEditor.cs
│   ├── UIWater2DEditor.cs
│   ├── WaterInspectorUI.cs       # 폴딩·구분선 공용 UI
│   ├── WaterMaterialSections.cs  # 머티리얼 수치를 컴포넌트 인스펙터에 통합
│   └── WaterPreviewDriver.cs     # 60초 에디터 프리뷰 구동기
└── Samples~/TestScene/           # 컴파일 대상 제외, Package Manager 에서 선택 임포트
    ├── Water2D_Test.unity
    └── Water2DTestHud.cs
```

---

## 주요 기능

| 기능 | 설명 |
|------|------|
| 버텍스 웨이브 | 8~128개 포인트의 스프링 시뮬레이션 |
| 이웃 파동 전파 | `_spread` 로 제어되는 좌·우 전파 (2-pass) |
| Rigidbody2D 자동 감지 | Trigger 진입 시 impulse 자동 계산 |
| 공개 Splash API | `water.Splash(localX, force)` (`force` 는 표면 포인트 `Velocity` 에 가산. **음수**는 아래로 찍히는 파동, **양수**는 위로 솟는 파동) |
| 표면 높이 샘플링 | `water.SampleSurfaceHeight(localX)` — 부력·외부 연출용 |
| 표면 리셋 | `water.ResetSurface()` — 높이·속도를 평형으로 |
| 부력 (옵션) | `Buoyancy Enabled` 시 잠긴 `Rigidbody2D` 에 `FixedUpdate` 로 부력·수중 드래그 |
| UnityEvent 훅 | `OnSplash(Vector2 worldPos, float force)` |
| 지속 출렁임 | 진행 파형(사인 중첩 + Perlin 랜덤) + 주기적 랜덤 임펄스. 강도/빈도/랜덤성 수치 조절 |
| 스프링 시뮬 슬립 | 충돌·`Splash()`·랜덤 임펄스로만 깨어남. 표면 정지 시 자동 슬립(연산·정점 업로드 생략) |
| 물리 토글 | `Interaction Enabled` / `Buoyancy Enabled`. 둘 다 OFF 면 콜라이더까지 비활성 |
| 폭 있는 Splash | `water.SplashArea(localX, force, spread)` — 코사인 감쇠로 부드러운 파동 주입 |
| 물속 셰이더 | 깊이 그라디언트 · 코스틱 · 굴절 왜곡 · 수면 거품 · 질감 텍스처 (기능별 keyword 분리) |
| 머티리얼 자동 생성 | 컴포넌트 추가 시 `Water2D_Default.mat` 생성·할당. 인스펙터에서 수치 인라인 편집 |
| 에디터 프리뷰 | `ExecuteAlways` + 에디터에서만 `EditorApplication.update` (`_editorPreview` 가 켜진 경우) |
| Scene 기즈모 | bounds + 표면 라인 + 포인트 도트 |

## 인스펙터 프로퍼티

| 그룹 | 프로퍼티 | 기본값 | 설명 |
|------|----------|--------|------|
| 크기 | Width | 4 | 물 본체 가로 폭 |
| 크기 | Depth | 2 | 물 본체 세로 깊이 |
| 메시 | Point Count | 24 | 표면 포인트 수 (Range 8~128) |
| 스프링 | Spring Constant | 0.025 | 복원력 강도 (60fps 튜닝) |
| 스프링 | Damping | 0.025 | 감쇠 계수 |
| 스프링 | Spread | 0.25 | 좌·우 전파율 |
| 물리 | Interaction Enabled | **false** | 충돌 상호작용 on/off. OFF 면 콜라이더 비활성 |
| 상호작용 | Velocity Multiplier | 0.1 | 진입 Y속도 계수 |
| 상호작용 | Mass Multiplier | 0.05 | 질량 계수 |
| 상호작용 | Max Impulse | 5 | 단일 진입 impulse 상한 |
| 부력 | Buoyancy Enabled | false | 부력 시스템 on/off |
| 부력 | Buoyancy Force | 30 | 단위 잠김 깊이 × 질량당 힘 |
| 부력 | Linear Drag | 3 | 수중 선형 감쇠(초당) |
| 부력 | Angular Drag | 1 | 수중 각속도 감쇠(초당) |
| 지속 출렁임 | Ambient Enabled | **true** | 지속 출렁임 on/off (디스플레이 기본값) |
| 지속 출렁임 | 강도 배율 | 1 | 진폭·임펄스 세기 공통 배율 (Range 0~3) |
| 지속 출렁임 | 진폭 | 0.08 | 진행 파형 크기(로컬 단위) |
| 지속 출렁임 | 파장 | 3 | 작을수록 잔물결, 클수록 너울 |
| 지속 출렁임 | 진행 속도 | 0.6 | 로컬 단위/초. 음수면 반대 방향. 시간 빈도 = 속도/파장 |
| 지속 출렁임 | 옥타브 수 | 2 | 중첩 파형 개수 (Range 1~4) |
| 지속 출렁임 | 옥타브 진폭비 | 0.5 | 다음 옥타브 진폭 비율 (파장은 1/2) |
| 지속 출렁임 | 옥타브 속도비 | 1.6 | 다음 옥타브 속도 비율. 1 이 아니면 반복 주기가 길어짐 |
| 지속 출렁임 | 랜덤성 | 0.35 | 진폭 대비 Perlin 노이즈 비율 |
| 지속 출렁임 | 노이즈 밀도 / 속도 | 0.5 / 0.35 | 랜덤 성분의 공간 밀도·시간 변화 속도 |
| 지속 출렁임 | 시드 | 0 | 여러 물 오브젝트의 위상 분리 |
| 지속 출렁임 | 랜덤 임펄스 사용 | **false** | 주기적 파동 주입 on/off. ON 이면 스프링 시뮬이 계속 깨어 있음 |
| 지속 출렁임 | 간격 최소/최대 | 0.6 / 2 | 임펄스 발생 빈도(초) |
| 지속 출렁임 | 세기 최소/최대 | -0.05 / 0.05 | 임펄스 세기 범위 (음수=아래, 양수=위). 스텝당 속도 단위 |
| 지속 출렁임 | 퍼짐 폭 | 0.6 | 임펄스 영향 로컬 폭 (코사인 감쇠) |
| 렌더링 | Sorting Layer | Default | SpriteRenderer 와 공유되는 정렬 레이어 |
| 렌더링 | Order in Layer | 0 | 같은 레이어 내 앞뒤 정렬 |
| 렌더링 | 표면 라인 (표시/두께/색상) | true / 0.06 / white | LineRenderer 기반 수면 라인 |
| 머티리얼 | Material | 자동 생성 | 비어 있으면 `CAT/Effects/2D Water` 머티리얼 자동 할당 |
| 이벤트 | On Splash | - | `Water2DSplashEvent` (`UnityEvent<Vector2, float>`) |

커스텀 인스펙터(`Water2DEditor`)에서 **크기·메시·스프링 물리·상호작용·부력·렌더링·이벤트** 섹션으로 그룹 표시된다. (`[Header]` 는 사용하지 않음 — 중복 라벨 방지.)

### Sorting Layer / Order in Layer

`MeshRenderer`는 기본 인스펙터에서 Sorting Layer 옵션이 노출되지 않지만, Water2D 는 SpriteRenderer 와 **동일한 정렬 시스템**을 내부적으로 사용한다. 인스펙터의 `렌더링` 섹션에서 드롭다운·정수 필드로 설정하면 내부적으로 `MeshRenderer.sortingLayerID`, `MeshRenderer.sortingOrder` 에 반영된다.

**용례**:
- 어항 앞 유리 스프라이트 뒤에 물 배치 → `Order in Layer` 를 유리보다 낮게
- 물속 물고기 스프라이트를 물 앞에 → 물고기의 `Order in Layer` 를 물보다 높게
- 씬 전체 배경 레이어와 분리 → `Sorting Layer` 를 "Foreground" 등 커스텀 레이어로 설정

코드에서도 제어 가능:
```csharp
water.SortingLayerID = SortingLayer.NameToID("Foreground");
water.SortingOrder = 10;
```

### 부력 (Buoyancy)

물에 잠긴 `Rigidbody2D` 에 매 FixedUpdate 마다 부력·드래그를 자동 적용한다. Unity 내장 `BuoyancyEffector2D` 와 달리 **출렁이는 표면 높이(`SampleSurfaceHeight`)를 직접 샘플링**하므로 파도에 따라 뜨는 물체가 자연스럽게 흔들린다.

**동작 원리**:
- `OnTriggerEnter2D` 시 내부 HashSet 에 추가, `OnTriggerExit2D` 시 제거
- `FixedUpdate` 에서 각 바디의 위치를 로컬로 변환 → `SampleSurfaceHeight(localX)` 로 해당 X 의 표면 Y 조회
- `submergedDepth = surfaceY - bodyY` (양수일 때만 부력 적용)
- 부력: `Vector2.up * (BuoyancyForce × submergedDepth × rb.mass)`
- 드래그: `linearVelocity *= 1 - LinearDrag × dt`, `angularVelocity` 동일

**튜닝 팁**:
- 가벼운 코르크 느낌: `BuoyancyForce = 50`, `LinearDrag = 5`
- 무거운 돌 (가라앉음): 바디 `mass` 증가 + `BuoyancyForce = 15`
- 흔들리며 천천히 뜨는 오리 인형: `LinearDrag = 1.5`, `AngularDrag = 0.5`

**표면 높이 외부 샘플링**:
```csharp
float localX = water.transform.InverseTransformPoint(fish.position).x;
float surfaceY = water.SampleSurfaceHeight(localX);
Vector3 world = water.transform.TransformPoint(new Vector3(localX, surfaceY, 0));
// world.y 가 해당 위치의 수면 높이
```

### 지속 출렁임 (Ambient Wave)

두 계층으로 구성된다.

| 계층 | 처리 방식 | 특징 |
|------|-----------|------|
| 진행 파형 | 스프링 시뮬과 **별개**로 표면 정점에 직접 가산되는 해석적 변위 | 감쇠에 먹히지 않으므로 인스펙터 진폭이 화면에 그대로 나온다. 프레임레이트 독립 |
| 랜덤 임펄스 | 포인트 `Velocity` 에 주입 → 이웃으로 전파 | 실제 파동처럼 퍼지고 사라진다. 충돌 splash 와 동일 경로 |

조절 축:
- **강도**: `강도 배율`(전체) → `진폭`(파형), `세기 최소/최대`(임펄스)
- **빈도**: `파장` + `진행 속도` (시간 빈도 = 속도/파장, 인스펙터에 Hz 표시) → `간격 최소/최대`(임펄스)
- **랜덤성**: `랜덤성`(Perlin 비율) + `노이즈 밀도/속도`, `옥타브 속도비`(반복 주기 연장), `시드`(오브젝트별 위상 분리)

프리셋 예시:

| 연출 | 진폭 | 파장 | 속도 | 옥타브 | 랜덤성 | 임펄스 간격 |
|------|------|------|------|--------|--------|-------------|
| 잔잔한 호수 | 0.03 | 4 | 0.3 | 1 | 0.2 | 1.5 ~ 3 |
| 횡스크롤 기본 | 0.08 | 3 | 0.6 | 2 | 0.35 | 0.6 ~ 2 |
| 거친 파도 | 0.2 | 1.5 | 1.4 | 3 | 0.6 | 0.2 ~ 0.7 |

임펄스 세기는 **스텝당 속도** 단위이므로 화면 변위와 1:1 이 아니다. 실측(pointCount 24, Spring 0.025, Damping 0.025):

| 퍼짐 폭 | 세기 0.1 의 최대 표면 변위 |
|---------|---------------------------|
| 0 (단일 포인트) | 0.056 |
| 0.3 | 0.109 |
| 0.6 (기본) | 0.159 |
| 1.2 | 0.311 |

즉 기본 퍼짐(0.6)에서 **세기 ≈ 변위 / 1.6**. 진폭과 비슷한 크기로 맞추면 자연스럽다.

> 파형은 `Splash()` / 충돌 파동 위에 **가산**되므로 두 표현이 서로를 지우지 않는다.
> `SampleSurfaceHeight()` 도 파형을 포함하므로 부력 오브젝트가 출렁임에 맞춰 함께 흔들린다.

### 물속 셰이더 (`CAT/Effects/2D Water`)

컴포넌트를 추가하면 패키지에 동봉된 공용 기본 머티리얼이 자동 할당된다 (프로젝트에 파일이 생기지 않는다). 인스펙터의
`물 머티리얼 / 셰이더` 섹션에서 수치를 바로 편집하고, `전용 머티리얼로 복제` 버튼으로
오브젝트 전용 에셋을 만들 수 있다 (기본 머티리얼은 여러 오브젝트가 공유).

| 그룹 | 프로퍼티 | 설명 |
|------|----------|------|
| Color | Shallow / Deep Color | 수면·심층 색 (알파 포함). UV v 로 보간 |
| Color | Gradient Power | 그라디언트 집중도 |
| Color | Alpha 배율 | 전체 투명도 |
| Texture | 질감 텍스처 사용 | 키워드 `_CAT_TEXTURE`. off 면 샘플링 자체가 제거됨 |
| Texture | 질감 색조 / 세기 / 타일링 / 스크롤 | 텍스처 기반 질감 (샘플 1회, 자동 스크롤) |

#### 인스펙터 구성 (한 곳에서 편집)

머티리얼 인스펙터를 따로 오가지 않도록, 물속 효과 수치를 컴포넌트 인스펙터 안에 기능별 섹션으로 배치한다.

모든 섹션이 **접기(폴딩) + 구분선**으로 나뉘고, 접힌 상태에서는 헤더 우측에 요약(ON/OFF·값)이 표시된다.
접힘 상태는 `EditorPrefs` 에 저장되어 선택을 바꿔도 유지된다.

```
▼ 크기                        4.00 × 2.00
─────────────────────────────────────────
▼ 메시 · 표면 해상도           24점
─────────────────────────────────────────
▼ 스프링 물리 (파동 전파)      슬립
─────────────────────────────────────────
▼ 지속 출렁임 (Ambient Wave)   ON
─────────────────────────────────────────
▶ 물리 · 충돌 상호작용          OFF
─────────────────────────────────────────
▶ 부력 (Buoyancy)              OFF
─────────────────────────────────────────
▼ 렌더링 · 정렬                order 0
─────────────────────────────────────────
▼ 물속 표현 (머티리얼)         Water2D_Default
    Material [에셋] [복제] [선택]
    ▼ 색상 · 깊이
    ▼ 질감 텍스처              ON / OFF
    ▼ 코스틱 (물결 무늬)       ON
    ▼ 굴절 왜곡                ON
    ▼ 수면 거품                ON
    ▶ 경계 페이드
    ▶ 고급 (셰이더 전체 · 렌더 큐)
─────────────────────────────────────────
▶ 이벤트
─────────────────────────────────────────
  에디터 프리뷰 [▶ 60초 플레이]
  [🌊 Random Splash] [⏹ Reset Surface]
▼ ⚠ 성능 · 빌드 주의사항 (필독)
```

- **질감 텍스처 필드가 질감 섹션 안에 있다.** UI 버전은 컴포넌트의 `Graphic.mainTexture`,
  월드 버전은 머티리얼의 `_MainTex` 를 같은 자리에서 편집한다.
- 토글이 꺼진 기능의 하위 옵션은 감춘다. 값 기록은 `MaterialEditor.ShaderProperty` 를 통하므로
  Undo 와 shader_feature 키워드 동기화가 Unity 기본 동작과 동일하다.

#### 질감 텍스처 (`_MainTex`) 사용법

역할: 절차적 코스틱 위에 **작가가 만든 무늬**를 얹는 레이어. 물결 무늬·노이즈·코스틱 맵·거품 패턴 등.

- **합성 모드** `_TexBlendMode`: `0` = 곱셈(명암 있는 불투명 텍스처용), `1` = **오버레이**(알파 있는 패턴·데칼용, **기본값**)
  - 곱셈만 있던 초기 버전에서는 프로젝트의 흔한 "흰색 + 알파" 패턴 텍스처가 화면에 **전혀 나타나지 않았다**
    (알파 0 → 합성 가중치 0, 알파 1 영역은 흰색 × = 무변화). 실제 프로젝트 텍스처로 재현·확인 후 오버레이 모드를 추가했다.
- **Wrap Mode**: 타일링을 1보다 크게 쓰려면 텍스처가 `Repeat` 이어야 한다. Sprite 로 임포트한 텍스처는 기본이 `Clamp` 라
  가장자리가 늘어난다. 인스펙터가 감지해 경고 + 1클릭 변경 버튼을 제공한다.
- `질감 텍스처 사용` 토글(`_CAT_TEXTURE`)이 **켜져 있어야** 반영된다. shader_feature 라 꺼져 있으면
  샘플링 코드 자체가 컴파일에서 제거되어 텍스처를 지정해도 **완전히 무반응**이다.
  (인스펙터가 이 상태를 감지해 경고 + 1클릭 활성 버튼을 제공한다)
- UI 버전은 컴포넌트의 `질감 텍스처` 필드가 `Graphic.mainTexture` 로서 `_MainTex` 에 주입된다
  (UGUI 배칭 규약 유지). 월드 버전은 머티리얼의 `_MainTex` 에 직접 지정한다.
- 왜곡(`_CAT_DISTORT`)이 켜져 있으면 질감 UV 도 함께 흔들려 흐르는 느낌이 강해진다.
- 실측: 텍스처 ON 은 스프라이트 대비 픽셀 비용 3.23x → 3.61x (샘플 1회 추가).
| Caustics | 코스틱 사용 / 색 / 세기 / 밀도 / 속도 / 선명도 / 깊이 감쇠 | 절차적 물결 무늬 (텍스처 불필요) |
| Distortion | 굴절 왜곡 사용 / 세기 / 밀도 / 속도 | 코스틱·질감 UV 를 흔들어 굴절감 표현 |
| Foam | 수면 거품 사용 / 색 / 두께 / 부드러움 | 수면 경계 하이라이트. 두께는 **UV(v) 기준**이라 Depth 에 비례해 두꺼워진다 |
| Depth Fade | 하단 / 좌우 페이드 | 배경과의 경계 블렌딩 |

모바일 최적화 포인트:
- 텍스처 샘플 **최대 1회**, 나머지는 전부 ALU 절차적 연산 → 대역폭 부담 없음
- 기능별 `shader_feature_local_fragment` 로 사용하지 않는 연산은 컴파일 시 제거
- `if` 분기 없음 (`smoothstep`/`lerp` 로 대체), `half` 우선 사용, `#pragma target 2.0`
- SRP Batcher 호환 (`UnityPerMaterial` CBUFFER), 그림자·라이트 프로브·모션 벡터 자동 off
- GrabPass(씬 텍스처) 미사용

## UIWater2D (UI Canvas 버전)

`MaskableGraphic` 파생이라 Canvas 배칭·마스크·CanvasGroup 알파에 표준 방식으로 대응한다.
시뮬레이션은 월드 버전과 **동일한 코어**(`WaterSurface`)를 공유하므로 거동·수치 의미가 같다.

| 항목 | 월드 `Water2D` | UI `UIWater2D` |
|------|----------------|----------------|
| 렌더 | MeshFilter + MeshRenderer | CanvasRenderer (`OnPopulateMesh`) |
| 셰이더 | `CAT/Effects/2D Water` (URP) | `CAT/UI/Water2D` (UGUI) |
| 길이 단위 | 월드 유닛 | RectTransform px |
| 표면 라인 | LineRenderer (드로우콜 +1) | 셰이더 Foam 으로 대체 (드로우콜 추가 없음) |
| 상호작용 | Rigidbody2D 트리거 (opt-in) | 포인터 클릭·터치·드래그 (opt-in) |
| 부력 | 지원 | 미지원 (UI 물리 개념 없음) |

### 마스크 대응 (검증 완료)

| 마스크 | 방식 | 상태 |
|--------|------|------|
| `Mask` (스텐실) | 셰이더 Stencil 블록 + `StencilMaterial` (UGUI 표준) | ✅ 클리핑 확인 |
| `RectMask2D` | `UNITY_UI_CLIP_RECT` + `_ClipRect` (UGUI 표준) | ✅ 클리핑 확인 |
| `CanvasGroup` alpha | 정점 색상에 곱산 | ✅ |
| SoftMaskLight | (SoftMaskLight) Hidden 변형 필요 — 패키지 측에서 처리 예정 | ⏳ |

> 셰이더가 `_Time` 기반으로 애니메이션하므로 **매 프레임 머티리얼 프로퍼티를 갱신하지 않는다.**
> 덕분에 `StencilMaterial` 캐시·마스크 체인에서 자주 발생하는 stale 값 문제가 원천 차단된다.
> 단, 런타임에 물속 수치를 스크립트로 바꾼 경우에는 `SetMaterialDirty()` 를 호출해야 마스크 하위에서 반영된다.

### 기본값 (px 단위)

| 프로퍼티 | 기본값 |
|----------|--------|
| 진폭 / 파장 / 진행 속도 | 8 px / 240 px / 60 px/s |
| 랜덤 임펄스 세기 | ±4 (px/스텝), 퍼짐 60 px |
| 포인터 입력 세기 | -30, 퍼짐 80 px, 드래그 간격 0.05s |

### 사용법

```csharp
// 코드에서 파동 주입 (localX = RectTransform 로컬 X)
water.Splash(0f, -30f);
water.SplashArea(120f, -40f, 80f);

// 런타임 토글
water.AmbientEnabled = true;
water.PointerInteractionEnabled = true;

// 수면 높이 샘플링 (연출 오브젝트 정렬용, RectTransform 로컬 Y)
float y = water.SampleSurfaceHeight(0f);
```

## 테스트 씬

Package Manager 의 **Samples > Test Scene > Import** 로 가져온다 (`Samples~/TestScene/Water2D_Test.unity`). 1080×1920 기준 Screen Space Camera 캔버스에 5종 구성:

1. 기본 UI 물 2. `Mask`(스텐실) 하위 3. `RectMask2D` 하위 4. 중첩 Canvas 5. 포인터 상호작용
+ 비교용 월드 `Water2D`, 그리고 `Perf HUD`(프레임 ms · FPS · spring awake 수 표시, 물/코스틱 토글 버튼).

실기 점검 절차: 씬을 빌드에 포함해 기기에서 실행 → HUD 버튼으로 **UI 물 토글 / 코스틱 토글** 하며 avg ms 델타를 확인.

## 표면 해상도 (단일 노브)

표면은 폴리라인 테셀레이션이다 (래스터라이저에 곡선 프리미티브는 없다). 따라서 매끄러움은 **정점 수**로 결정된다.

- `표면 정점 수` 하나로 시뮬·렌더 해상도를 함께 제어한다.
- 진행 파형은 정점마다 **해석적으로** 평가되므로 정점을 늘린 만큼 정확히 매끄러워진다.
- 각짐 판정 기준은 **최단 옥타브 파장**(파장 ÷ 2^(옥타브-1))이며, 파장당 8샘플 이상을 권장. 인스펙터가 실시간 표시·경고한다.
- 임의 위치 샘플링(`SampleSurfaceHeight`, 부력 등)에는 Catmull-Rom 보간을 사용해 정점 사이도 곡선으로 얻는다.

실측 (프레임당 µs, 데스크톱):

| 표면 정점 | 월드 | UI |
|---|---|---|
| 12 | 3.42 | 11.32 |
| 24 (월드 기본) | 5.11~6.14 | 17.25 |
| 34 (UI 기본) | ~8.5 | 21.06 |
| 45 | 11.30 | 27.45 |

> 비용은 정점 수에 비례한다. 시뮬 포인트와 렌더 정점을 분리해 보았으나(곡선 분할 옵션),
> 실측상 스프링 시뮬은 사실상 무료(기본 상태에서는 슬립)여서 **분리 이득이 없어 노브를 하나로 통합**했다.

## 아키텍처

```
[Update] (Play) / [EditorTick] (에디터 프리뷰 ON, 비플레이)
        │
        ▼
[StepSimulation(dt)]                ← 1/60s 어큐뮬레이터
        │
        ▼
[SingleStep]
   ├── Hooke + damping (각 포인트)
   └── 이웃 전파 (2-pass, Δv)
        │
        ▼
[UpdateMeshVertices]                ← 상단 행 y = Height
        │
        ▼
[MeshFilter.sharedMesh]

[OnTriggerEnter2D] ─► Rigidbody2D 조회 ─► localX·impulse 계산 ─► Splash()
                        │
                        └─ (Buoyancy Enabled) ─► _submergedBodies 에 등록

[FixedUpdate] (Play + Buoyancy Enabled) ─► 잠긴 바디마다 ApplyBuoyancy
```

`OnTriggerExit2D` 에서는 `_submergedBodies` 에서 해당 `Rigidbody2D` 를 제거한다.

정점 레이아웃 (pointCount = N):
```
i=0 ... N-1     (상단, y = _points[i].Height)
i=N ... 2N-1    (하단, y = -_depth 고정)

삼각형: 세그먼트당 2개, 총 (N-1)*2 개
```

## 사용법

### 1. 기본 사용

1. 빈 GameObject 생성
2. `Add Component > CAT > Effects > 2D Water`
3. 자동 추가된 `MeshRenderer` 에 머티리얼 지정 (Sprites/Default 등)
4. 인스펙터에서 Width, Depth 조정
5. Rigidbody2D + Collider2D 부착한 오브젝트를 위에 배치 후 Play

### 2. 코드에서 Splash 주입

```csharp
using CAT.Water2D;

public class SplashOnClick : MonoBehaviour
{
    public Water2D water;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 wp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            float lx = water.transform.InverseTransformPoint(wp).x;
            // 음수: 표면이 아래로 찍히는 impulse. 양수면 위로 솟는 방향.
            water.Splash(lx, -3f);
        }
    }
}
```

읽기 전용 상태: `water.Width`, `water.Depth`, `water.PointCount`, `water.OnSplash`. 정렬: `water.SortingLayerID`, `water.SortingOrder` (setter 시 `MeshRenderer` 에 반영).

### 3. UnityEvent 로 파티클 연결

1. 씬에 ParticleSystem (Splash 효과) 배치
2. Water2D 인스펙터의 `On Splash (Vector2, float)` 에 ParticleSystem 참조 추가
3. `ParticleSystem.transform.position` 를 `Vector2` → `Vector3` 로 설정하는 래퍼 메서드 연결

### 4. 에디터 프리뷰

인스펙터의 `▶ 60초 플레이` 버튼으로 Play 모드 없이 재생한다 (남은 시간 진행바 표시, `⏹ 정지` 로 즉시 종료).
UI 버전은 게임뷰가 열려 있어야 Canvas 가 갱신된다.

## 성능 특성

실측 (Unity 6000.0.69f1, Play 모드, 데스크톱 Mono JIT). `TickAndRender` 1프레임 µs:

| 포인트 | 완전 유휴 | 파형만(기본) | 파형만+라인OFF | 파형+스프링 awake |
|---|---|---|---|---|
| 24 | 0.01 | 3.61 | 3.24 | 4.04 |
| 48 | 0.01 | 6.56 | 6.02 | 7.77 |
| 64 | 0.01 | 8.69 | 7.78 | 9.98 |

- **GC 할당 0 B / 1000프레임** (매 프레임 경로에 할당 없음)
- 모바일 CPU는 코어당 2~4배 느림 → 포인트 48·기본 설정에서 프레임당 약 15~26µs = 60fps 예산의 0.15% 내외
- 저프레임(30fps)에서는 스프링 스텝이 2배 실행 (안전 상한 8스텝)

UI 버전 실측 (같은 환경, 프레임당 µs):

| 포인트 | 시뮬 Tick(파형 ON) | Tick(유휴) | 메시 재생성 | 합계 |
|---|---|---|---|---|
| 24 | 2.93 | 0.24 | 10.56 | 13.49 |
| 48 | 5.34 | 0.25 | 16.49 | 21.83 |
| 64 | 7.25 | 0.25 | 20.15 | 27.40 |

- UI 버전은 메시 재생성(`OnPopulateMesh` + `SetMesh`)이 시뮬보다 크다 → **포인트 수가 곧 비용**
- 같은 Canvas 에 다른 UI 를 300개 두어도 유의미한 추가 비용은 관측되지 않았다 (Unity 6 부분 재배치)
- Play 모드 A/B (UI 물 5개, 데스크톱): 전체 ON 5.13ms ↔ 물 비활성 4.98ms → **5개 합계 약 0.15ms**
- 드로우콜: UI 물 5개 = 배치 4개 (마스크·중첩 Canvas 로 분리된 만큼 증가)

GPU 프래그먼트 비용 (1920×1080 전체 덮음, 40겹 오버드로 증폭 후 1겹 환산, `Sprites/Default` 대비):

| 구성 | 상대 비용 |
|---|---|
| 전 기능 OFF (그라디언트만) | 1.04x |
| 굴절 왜곡 + 거품 (코스틱 OFF) | 1.33x |
| 기본 (코스틱 포함) | 3.23x |
| 기본 + 질감 텍스처 | 3.61x |

iOS Metal 컴파일 결과: 기본 변형 133줄 / sin·cos 5개 / **텍스처 페치 0개**. Android GLES3·Vulkan 컴파일 확인.
비용의 대부분은 코스틱이며, 화면 점유 면적에 정비례한다.

| 항목 | 값 (pointCount=24 기준) |
|------|------------------------|
| 정점 수 | 48 |
| 삼각형 수 | 46 |
| 드로우콜 | 2 (물 메시 + 표면 라인. 라인 OFF 시 1) |
| Mesh 재할당 | pointCount 변경 시 1회 |

튜닝 팁:
- 작은 물컵: pointCount 8~16 (최소 8)
- 일반 어항: 24 (기본값)
- 넓은 호수: 48
- 64 초과는 이득 대비 비용만 증가 (에디터 경고 표시)
- 비용 0 의 디스플레이용 물: 물리 토글 OFF + 랜덤 임펄스 OFF + 진행 파형만 사용

## 제한 사항

- **UI(Canvas) 미지원**: World-space 전용. UI 모드는 향후 `MaskableGraphic` 파생으로 확장 가능.
- **씬 굴절(GrabPass) 없음**: 배경 텍스처를 읽지 않는다. 왜곡은 셰이더 내부 패턴에만 적용 (모바일 대역폭 보호).
- **1D 파동**: 표면은 수평 방향으로만 파동 전파. 2D wave equation 은 미지원.
- **Splash 파티클 내장 없음**: `OnSplash` 이벤트로 사용자가 직접 연결.

## 호환성

| 환경 | 지원 |
|------|------|
| Unity 6 (6000.0+) | ✅ |
| URP 17+ | ✅ |
| Built-in RP | ⚠️ 시뮬레이션은 동작하나 내장 셰이더는 URP 전용. Built-in 에서는 머티리얼을 직접 지정 |
| 모바일 (iOS/Android) | ✅ 우선 타깃 |
| `Rigidbody2D.linearVelocity` | ✅ Unity 6 표준 사용 |

## 향후 확장 후보

- UI(Canvas) 모드 (`MaskableGraphic.OnPopulateMesh`)
- 부력 고도화 (부분 잠김·표면 경계 처리, Effector2D 와의 조합 등)
- Splash 파티클 프리셋 프리팹
- 다중 소스 2D wave equation

---

## 라이선스

GNU General Public License v3.0 only (`GPL-3.0-only`) — 자세한 내용은 [LICENSE](LICENSE) 를 참고하세요. 저작권 및 저작자 고지는 [NOTICE](NOTICE.md) 를 참고하세요.

원저작권자는 zzamjak입니다. 재배포본, 수정본, 파생물은 원저작권 고지를 유지하고 GPLv3 조건에 따라 소스 공개 및 동일 라이선스 배포 의무를 따라야 합니다. 독점/폐쇄 소스 소프트웨어로 재배포하는 것은 허용되지 않습니다.
