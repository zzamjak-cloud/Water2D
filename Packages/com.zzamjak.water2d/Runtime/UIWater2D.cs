using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CAT.Water2D
{
    /// <summary>
    /// UI Canvas 용 2D 물. RectTransform 을 채우는 quad strip 을 직접 생성하고
    /// 상단 행 정점을 파동 시뮬 결과로 밀어 올린다. 시뮬 코어는 Water2D 와 공유(<see cref="WaterSurface"/>).
    ///
    /// [렌더링]
    /// - MaskableGraphic 파생 → Canvas 배칭·Mask(스텐실)·RectMask2D(_ClipRect) 자동 대응.
    /// - 컴포넌트 추가 시 CAT/UI/Water2D 셰이더 머티리얼 에셋이 자동 생성·할당된다.
    /// - 셰이더가 시간 기반으로 애니메이션하므로 매 프레임 머티리얼 프로퍼티를 갱신하지 않는다
    ///   (StencilMaterial 캐시·SoftMaskable 체인의 stale 값 문제를 원천 회피).
    ///
    /// [단위]
    /// - 모든 길이 값은 RectTransform 로컬 단위(픽셀). 진폭 8 = 8px.
    ///
    /// [물리 = opt-in]
    /// - 포인터 상호작용은 기본 OFF. OFF 면 raycastTarget 을 건드리지 않으며 입력 처리도 하지 않는다.
    /// - 스프링 시뮬은 Splash·포인터·랜덤 임펄스로만 깨어나고 정지 시 자동 슬립한다.
    /// </summary>
    [AddComponentMenu("CAT/UI/UI Water 2D")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer), typeof(RectTransform))]
    public class UIWater2D : MaskableGraphic, IPointerDownHandler, IDragHandler
    {
        #region 직렬화 필드

        [SerializeField, Range(4, 128), Tooltip("표면 정점 수. 시뮬레이션과 렌더 해상도를 함께 결정한다.\n진행 파형은 정점마다 해석적으로 평가되므로, 정점을 늘리면 그만큼 곡선이 매끄러워진다.")]
        private int _pointCount = 34;

        [SerializeField, Range(0.001f, 0.2f), Tooltip("복원력 강도 (k). 클수록 빠르게 평형으로 돌아옴")]
        private float _springConstant = 0.025f;

        [SerializeField, Range(0.001f, 0.1f), Tooltip("감쇠 계수. 클수록 진동이 빨리 사라짐")]
        private float _damping = 0.025f;

        [SerializeField, Range(0f, 0.5f), Tooltip("좌·우 이웃 전파율. 클수록 파동이 멀리 퍼짐")]
        private float _spread = 0.25f;

        // ── 지속 출렁임 (Ambient Wave) ──────────────────────────────────

        [SerializeField, Tooltip("충돌 없이도 표면이 계속 출렁이는 지속 파동 활성화.\n스프링 시뮬을 쓰지 않는 해석적 변위이므로 물리 기능과 무관하게 동작한다.")]
        private bool _ambientEnabled = true;

        [SerializeField, Range(0f, 3f), Tooltip("지속 출렁임 전체 강도 배율")]
        private float _ambientIntensity = 1f;

        [SerializeField, Min(0f), Tooltip("진행 파형 진폭 (px)")]
        private float _waveAmplitude = 8f;

        [SerializeField, Min(1f), Tooltip("진행 파형 파장 (px). 작을수록 잔물결, 클수록 완만한 너울")]
        private float _waveLength = 420f;

        [SerializeField, Tooltip("파형 진행 속도 (px/초). 음수면 반대 방향. 시간 빈도 = 속도/파장(Hz)")]
        private float _waveSpeed = 60f;

        [SerializeField, Range(1, 4), Tooltip("중첩할 파형 개수. 여러 겹을 겹쳐 반복감을 줄인다")]
        private int _waveOctaves = 2;

        [SerializeField, Range(0.1f, 1f), Tooltip("다음 옥타브의 진폭 비율 (파장은 1/2)")]
        private float _waveOctaveFalloff = 0.5f;

        [SerializeField, Range(0.5f, 3f), Tooltip("다음 옥타브의 진행 속도 비율. 1 이 아니면 반복 주기가 길어진다")]
        private float _waveOctaveSpeedRatio = 1.6f;

        [SerializeField, Range(0f, 1f), Tooltip("랜덤성. 진폭 대비 Perlin 노이즈 비율")]
        private float _waveRandomness = 0.35f;

        [SerializeField, Min(0.0001f), Tooltip("노이즈 공간 밀도 (px 기준이라 작은 값 사용)")]
        private float _waveNoiseScale = 0.01f;

        [SerializeField, Tooltip("노이즈 시간 변화 속도")]
        private float _waveNoiseSpeed = 0.35f;

        [SerializeField, Tooltip("랜덤 시드. 여러 물 오브젝트의 위상을 분리")]
        private int _ambientSeed = 0;

        [SerializeField, Tooltip("주기적으로 랜덤 위치에 impulse 를 주입해 실제 파동을 전파시킨다.\n스프링 시뮬이 계속 깨어 있게 되므로 연출용이면 OFF 권장.")]
        private bool _randomImpulseEnabled = false;

        [SerializeField, Min(0.02f), Tooltip("랜덤 임펄스 최소 간격(초)")]
        private float _impulseIntervalMin = 0.6f;

        [SerializeField, Min(0.02f), Tooltip("랜덤 임펄스 최대 간격(초)")]
        private float _impulseIntervalMax = 2f;

        [SerializeField, Tooltip("랜덤 임펄스 세기 최소값 (음수 = 아래로 찍힘). px/스텝 단위")]
        private float _impulseForceMin = -4f;

        [SerializeField, Tooltip("랜덤 임펄스 세기 최대값 (양수 = 위로 솟음)")]
        private float _impulseForceMax = 4f;

        [SerializeField, Min(0f), Tooltip("임펄스가 퍼지는 폭 (px). 0 이면 한 포인트만 때린다")]
        private float _impulseSpread = 60f;

        // ── 포인터 상호작용 (opt-in) ────────────────────────────────────

        [SerializeField, Tooltip("클릭·터치·드래그로 물을 튀긴다. OFF 면 입력 처리를 전혀 하지 않는다.")]
        private bool _pointerInteractionEnabled = false;

        [SerializeField, Tooltip("포인터 입력 1회당 impulse 세기 (음수 = 아래로 찍힘)")]
        private float _pointerForce = -30f;

        [SerializeField, Min(0f), Tooltip("포인터 입력이 퍼지는 폭 (px)")]
        private float _pointerSpread = 80f;

        [SerializeField, Min(0f), Tooltip("드래그 중 연속 주입 최소 간격(초). 0 이면 매 이벤트마다 주입")]
        private float _dragInterval = 0.05f;

        [SerializeField, Tooltip("Splash 발생 시 호출: (world position, 적용된 force)")]
        private Water2DSplashEvent _onSplash = new Water2DSplashEvent();

        #endregion

        #region 런타임 상태

        private readonly WaterSurface _surface = new WaterSurface();

        // 질감 텍스처 (셰이더 _MainTex 로 주입)
        [SerializeField, Tooltip("셰이더의 질감 텍스처로 주입된다 (Texture Enabled 켤 때 사용)")]
        private Texture _texture;

        private float _lastDragTime;
        private int _allocatedPointCount = -1;

        private const string UIWaterShaderName = "CAT/UI/Water2D";
        private bool _shaderMissingWarned;
        private Material _runtimeMaterial;

        #endregion

        #region 공개 프로퍼티·API

        /// <summary>UGUI 는 이 텍스처를 _MainTex 로 주입한다. 질감 텍스처로 사용.</summary>
        public override Texture mainTexture => _texture != null ? _texture : s_WhiteTexture;

        /// <summary>Splash 이벤트 훅.</summary>
        public Water2DSplashEvent OnSplash => _onSplash;

        /// <summary>표면 정점 수 (시뮬 = 렌더 해상도).</summary>
        public int PointCount => _surface.PointCount;

        /// <summary>스프링 시뮬이 현재 동작 중인지. false 면 프레임 비용이 사실상 0.</summary>
        public bool IsSpringAwake => _surface.SpringAwake;

        /// <summary>지속 출렁임 on/off.</summary>
        public bool AmbientEnabled
        {
            get => _ambientEnabled;
            set => _ambientEnabled = value;
        }

        /// <summary>지속 출렁임 전체 강도 배율.</summary>
        public float AmbientIntensity
        {
            get => _ambientIntensity;
            set => _ambientIntensity = Mathf.Max(0f, value);
        }

        /// <summary>진행 파형 진폭 (px).</summary>
        public float WaveAmplitude
        {
            get => _waveAmplitude;
            set => _waveAmplitude = Mathf.Max(0f, value);
        }

        /// <summary>진행 파형 속도 (px/초). 음수는 반대 방향.</summary>
        public float WaveSpeed
        {
            get => _waveSpeed;
            set => _waveSpeed = value;
        }

        /// <summary>포인터 상호작용 on/off.</summary>
        public bool PointerInteractionEnabled
        {
            get => _pointerInteractionEnabled;
            set => _pointerInteractionEnabled = value;
        }

        /// <summary>질감 텍스처. 변경 시 머티리얼 텍스처 주입이 갱신된다.</summary>
        public Texture WaterTexture
        {
            get => _texture;
            set
            {
                if (_texture == value) return;
                _texture = value;
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// 임의 위치에 파동을 주입한다.
        /// </summary>
        /// <param name="localX">RectTransform 로컬 X (rect.xMin ~ rect.xMax)</param>
        /// <param name="force">표면 포인트 수직 속도에 가산되는 impulse (px/스텝). 음수는 아래로 찍히는 파동.</param>
        public void Splash(float localX, float force)
        {
            if (_surface.PointCount == 0) return;

            _surface.Splash(LocalXToT(localX), force);
            InvokeSplashEvent(localX, force);
        }

        /// <summary>폭을 가진 파동을 주입한다 (코사인 감쇠).</summary>
        /// <param name="spread">영향 범위 폭 (px). 0 이면 단일 포인트.</param>
        public void SplashArea(float localX, float force, float spread)
        {
            if (_surface.PointCount == 0) return;

            Rect r = GetPixelAdjustedRect();
            _surface.SplashArea(LocalXToT(localX), force, spread / Mathf.Max(0.0001f, r.width));
            InvokeSplashEvent(localX, force);
        }

        /// <summary>표면을 평형 상태로 리셋한다.</summary>
        public void ResetSurface()
        {
            _surface.Reset();
            SetVerticesDirty();
        }

        /// <summary>로컬 X 위치의 표면 높이(로컬 Y)를 반환. 수면 연출·오브젝트 정렬용.</summary>
        public float SampleSurfaceHeight(float localX)
        {
            Rect r = GetPixelAdjustedRect();
            WaterSimParams p = BuildSimParams();
            // 파형은 Rect 중심 기준 로컬 X 로 평가 (월드 버전과 동일 규약)
            return r.yMax + _surface.SampleHeight(LocalXToT(localX), localX - r.center.x, in p);
        }

        #endregion

        #region 라이프사이클

        protected override void Awake()
        {
            base.Awake();
            EnsureAllocated();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureAllocated();
            EnsureMaterial();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            if (_runtimeMaterial != null)
            {
                if (Application.isPlaying) Destroy(_runtimeMaterial);
                else DestroyImmediate(_runtimeMaterial);
                _runtimeMaterial = null;
            }
            base.OnDestroy();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            _pointCount = Mathf.Clamp(_pointCount, 4, 128);
            if (_waveLength < 1f) _waveLength = 1f;
            if (_waveNoiseScale < 0.0001f) _waveNoiseScale = 0.0001f;
            _waveOctaves = Mathf.Clamp(_waveOctaves, 1, WaterSurface.MaxOctaves);
            _impulseIntervalMin = Mathf.Max(0.02f, _impulseIntervalMin);
            _impulseIntervalMax = Mathf.Max(_impulseIntervalMin, _impulseIntervalMax);
            if (_impulseForceMax < _impulseForceMin) _impulseForceMax = _impulseForceMin;
            if (_impulseSpread < 0f) _impulseSpread = 0f;
            if (_pointerSpread < 0f) _pointerSpread = 0f;
            if (_dragInterval < 0f) _dragInterval = 0f;

            EnsureAllocated();
            _surface.ReseedRandom(_ambientSeed);
            _surface.RequestMeshUpdate();
            SetVerticesDirty();
        }
#endif

        private void Update()
        {
            if (!Application.isPlaying) return;
            TickAndRender(Time.deltaTime);
        }

        /// <summary>시뮬 1프레임 + 필요할 때만 정점 재생성.</summary>
        private void TickAndRender(float deltaTime)
        {
            WaterSimParams p = BuildSimParams();
            _surface.Tick(deltaTime, in p);
            if (_surface.NeedsMeshUpdate) SetVerticesDirty();
        }

        #endregion

        #region 메시 생성

        /// <summary>
        /// RectTransform 을 채우는 quad strip 생성. 상단 행은 파동 높이, 하단 행은 고정.
        /// UV: u = 0~1 (좌→우), v = 0(하단) ~ 1(수면).
        /// </summary>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            EnsureAllocated();

            int n = _surface.PointCount;
            if (n < 2) return;

            Rect r = GetPixelAdjustedRect();
            float dx = r.width / (n - 1);
            float invSpan = 1f / (n - 1);
            Color32 c = color;
            WaterSimParams p = BuildSimParams();
            float centerX = r.center.x;

            // 상단 행 (0..n-1) — 스프링 높이 + 정점별 해석적 진행 파형
            for (int i = 0; i < n; i++)
            {
                float x = r.xMin + i * dx;
                float y = r.yMax + _surface.SpringHeightAt(i) + _surface.EvaluateAmbient(x - centerX, in p);
                vh.AddVert(new Vector3(x, y, 0f), c, new Vector2(i * invSpan, 1f));
            }

            // 하단 행 (n..2n-1)
            for (int i = 0; i < n; i++)
            {
                float x = r.xMin + i * dx;
                vh.AddVert(new Vector3(x, r.yMin, 0f), c, new Vector2(i * invSpan, 0f));
            }

            for (int i = 0; i < n - 1; i++)
            {
                int topL = i;
                int topR = i + 1;
                int botL = n + i;
                int botR = n + i + 1;

                vh.AddTriangle(topL, topR, botL);
                vh.AddTriangle(topR, botR, botL);
            }
        }

        private void EnsureAllocated()
        {
            int n = Mathf.Clamp(_pointCount, 4, 128);
            _surface.Configure(n, _ambientSeed);
            _allocatedPointCount = n;
        }

        #endregion

        #region 머티리얼

        /// <summary>
        /// 물 머티리얼을 보장한다. 에디터에서는 에셋을, 런타임 폴백은 셰이더 인스턴스를 사용.
        /// </summary>
        private void EnsureMaterial()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && NeedsEditorMaterialAsset())
            {
                Material asset = LoadDefaultMaterialAsset();
                if (asset != null) { material = asset; return; }
                RequestEditorDefaultMaterial();
                return;
            }
#endif
            if (m_Material != null) return;

            // 패키지 동봉 기본 머티리얼 (Resources) — 프로젝트에 에셋을 만들지 않는다
            Material packaged = LoadPackagedDefaultMaterial();
            if (packaged != null) { material = packaged; return; }

            Shader shader = Shader.Find(UIWaterShaderName);
            if (shader == null)
            {
                if (!_shaderMissingWarned)
                {
                    _shaderMissingWarned = true;
                    Debug.LogWarning($"[UIWater2D] 셰이더 '{UIWaterShaderName}' 를 찾을 수 없습니다. 머티리얼을 직접 지정하세요.", this);
                }
                return;
            }

            _runtimeMaterial = new Material(shader)
            {
                name = "CAT UIWater2D (Runtime)",
                hideFlags = HideFlags.DontSave
            };
            ApplyDefaultWaterKeywords(_runtimeMaterial);
            material = _runtimeMaterial;
        }

        /// <summary>새 머티리얼의 기본 표현 기능(코스틱·왜곡·거품)을 켠다.</summary>
        private static void ApplyDefaultWaterKeywords(Material m)
        {
            if (m == null) return;

            m.SetFloat(PropCausticsEnabled, 1f);
            m.SetFloat(PropDistortEnabled, 1f);
            m.SetFloat(PropFoamEnabled, 1f);
            m.SetFloat(PropTextureEnabled, 0f);
            m.SetFloat(PropTexBlendMode, 1f); // 오버레이 (알파 있는 패턴 텍스처가 기본적으로 보이도록)

            m.EnableKeyword(KeywordCaustics);
            m.EnableKeyword(KeywordDistort);
            m.EnableKeyword(KeywordFoam);
            m.DisableKeyword(KeywordTexture);
        }

        private static readonly int PropTextureEnabled = Shader.PropertyToID("_TextureEnabled");
        private static readonly int PropTexBlendMode = Shader.PropertyToID("_TexBlendMode");
        private static readonly int PropCausticsEnabled = Shader.PropertyToID("_CausticsEnabled");
        private static readonly int PropDistortEnabled = Shader.PropertyToID("_DistortEnabled");
        private static readonly int PropFoamEnabled = Shader.PropertyToID("_FoamEnabled");

        private const string KeywordTexture = "_CAT_TEXTURE";
        private const string KeywordCaustics = "_CAT_CAUSTICS";
        private const string KeywordDistort = "_CAT_DISTORT";
        private const string KeywordFoam = "_CAT_FOAM";

        /// <summary>패키지 동봉 기본 머티리얼 경로 (Resources 기준, 확장자 없음).</summary>
        private const string PackagedDefaultMaterialPath = "Water2D/UIWater2D_Default";

        /// <summary>패키지에 동봉된 읽기 전용 기본 머티리얼 (빌드 포함 보장).</summary>
        public static Material LoadPackagedDefaultMaterial()
        {
            return Resources.Load<Material>(PackagedDefaultMaterialPath);
        }

        #endregion

        #region 포인터 상호작용

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_pointerInteractionEnabled) return;
            InjectPointer(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_pointerInteractionEnabled) return;

            // 드래그는 프레임마다 들어오므로 최소 간격으로 제한
            float now = Time.unscaledTime;
            if (_dragInterval > 0f && now - _lastDragTime < _dragInterval) return;
            _lastDragTime = now;

            InjectPointer(eventData);
        }

        private void InjectPointer(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                return;
            }

            SplashArea(local.x, _pointerForce, _pointerSpread);
        }

        #endregion

        #region 내부

        /// <summary>현재 인스펙터 값으로 시뮬 파라미터를 구성한다 (struct, 할당 없음).</summary>
        private WaterSimParams BuildSimParams()
        {
            Rect r = GetPixelAdjustedRect();

            WaterSimParams p;
            p.Width = Mathf.Max(0.0001f, r.width);
            p.SpringConstant = _springConstant;
            p.Damping = _damping;
            p.Spread = _spread;

            p.AmbientEnabled = _ambientEnabled;
            p.AmbientIntensity = _ambientIntensity;
            p.WaveAmplitude = _waveAmplitude;
            p.WaveLength = _waveLength;
            p.WaveSpeed = _waveSpeed;
            p.WaveOctaves = _waveOctaves;
            p.WaveOctaveFalloff = _waveOctaveFalloff;
            p.WaveOctaveSpeedRatio = _waveOctaveSpeedRatio;
            p.WaveRandomness = _waveRandomness;
            p.WaveNoiseScale = _waveNoiseScale;
            p.WaveNoiseSpeed = _waveNoiseSpeed;

            p.RandomImpulseEnabled = _randomImpulseEnabled;
            p.ImpulseIntervalMin = _impulseIntervalMin;
            p.ImpulseIntervalMax = _impulseIntervalMax;
            p.ImpulseForceMin = _impulseForceMin;
            p.ImpulseForceMax = _impulseForceMax;
            p.ImpulseSpread = _impulseSpread;
            return p;
        }

        /// <summary>로컬 X → 0~1 정규 좌표.</summary>
        private float LocalXToT(float localX)
        {
            Rect r = GetPixelAdjustedRect();
            return Mathf.Clamp01((localX - r.xMin) / Mathf.Max(0.0001f, r.width));
        }

        private void InvokeSplashEvent(float localX, float force)
        {
            if (_onSplash == null) return;

            Rect r = GetPixelAdjustedRect();
            WaterSimParams p = BuildSimParams();
            float y = r.yMax + _surface.SampleHeight(LocalXToT(localX), localX - r.center.x, in p);
            Vector3 world = rectTransform.TransformPoint(new Vector3(localX, y, 0f));
            _onSplash.Invoke(new Vector2(world.x, world.y), force);
        }

        #endregion

        #region 에디터

#if UNITY_EDITOR
        [System.NonSerialized] private bool _defaultMaterialRequested;

        /// <summary>
        /// 에디터 프리뷰 1스텝. 커스텀 에디터(UIWater2DEditor)가 EditorApplication.update 에서 구동한다.
        /// </summary>
        public void EditorAdvance(float deltaTime)
        {
            if (Application.isPlaying) return;

            EnsureAllocated();
            EnsureMaterial();
            TickAndRender(deltaTime);
        }

        /// <summary>프리뷰 종료 시 표면을 평평하게 정리한다.</summary>
        public void EditorStopPreview()
        {
            _surface.Reset(true);
            SetVerticesDirty();
        }

        /// <summary>에디터에서 표면 포인트 배열을 읽기 전용으로 노출 (기즈모용).</summary>
        public WaterPoint[] EditorGetPoints() => _surface.GetPointsUnsafe();

        /// <summary>컴포넌트 추가 시 호출. 기본 UI 물 머티리얼 에셋을 만들어 즉시 할당한다.</summary>
        protected override void Reset()
        {
            base.Reset();

            raycastTarget = false; // 포인터 상호작용을 켤 때만 필요
            EnsureAllocated();

            Material asset = LoadOrCreateDefaultMaterialAsset();
            if (asset != null) material = asset;

            SetAllDirty();
        }

        private bool NeedsEditorMaterialAsset()
        {
            if (m_Material == null) return true;
            // 에셋(패키지 동봉분 포함)이면 그대로 사용. 과거 버전이 만든 비영속 인스턴스만 교체 대상.
            if (EditorUtility.IsPersistent(m_Material)) return false;

            return m_Material.shader != null && m_Material.shader.name == UIWaterShaderName;
        }

        private void RequestEditorDefaultMaterial()
        {
            if (_defaultMaterialRequested) return;
            _defaultMaterialRequested = true;

            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                _defaultMaterialRequested = false;
                if (!NeedsEditorMaterialAsset()) return;

                Material asset = LoadOrCreateDefaultMaterialAsset();
                if (asset == null) return;

                Material stale = m_Material;
                material = asset;
                EditorUtility.SetDirty(this);

                if (stale != null && !EditorUtility.IsPersistent(stale))
                {
                    if (_runtimeMaterial == stale) _runtimeMaterial = null;
                    DestroyImmediate(stale);
                }
            };
        }

        /// <summary>
        /// 복제 저장 대화상자의 초기 폴더. 마지막에 저장한 폴더를 기억하고, 없으면 현재 씬 폴더를 쓴다.
        /// (패키지 폴더는 읽기 전용이므로 프로젝트 내부여야 한다)
        /// </summary>
        private static string GetSuggestedSaveFolder()
        {
            string last = EditorPrefs.GetString(LastSaveFolderKey, string.Empty);
            if (!string.IsNullOrEmpty(last) && AssetDatabase.IsValidFolder(last)) return last;

            string scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            if (!string.IsNullOrEmpty(scenePath))
            {
                string dir = System.IO.Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(dir) && AssetDatabase.IsValidFolder(dir)) return dir;
            }
            return "Assets";
        }

        private const string LastSaveFolderKey = "CAT.Water2D.LastMaterialSaveFolder";

        /// <summary>
        /// 패키지에 동봉된 읽기 전용 기본 머티리얼을 반환한다.
        /// 프로젝트에 파일을 만들지 않으므로 컴포넌트를 붙이는 것만으로 아무 것도 오염되지 않는다.
        /// 수치를 바꾸려면 CreateDedicatedMaterialAsset() 으로 프로젝트에 복제한다.
        /// </summary>
        public Material LoadDefaultMaterialAsset() => LoadPackagedDefaultMaterial();

        /// <summary>
        /// 기본 머티리얼을 반환한다. 패키지 동봉 에셋을 쓰므로 프로젝트에 에셋을 생성하지 않는다.
        /// (이전 버전은 Assets 하위에 고정 경로로 생성했다 — 프로젝트 오염을 피하기 위해 변경)
        /// </summary>
        public Material LoadOrCreateDefaultMaterialAsset() => LoadPackagedDefaultMaterial();

        /// <summary>
        /// 이 오브젝트 전용 머티리얼 에셋을 복제해 할당한다.
        /// 저장 위치는 사용자에게 묻는다 (Timeline·ShaderGraph 등이 쓰는 UPM 관행).
        /// 취소하면 null 을 반환하고 아무 것도 바꾸지 않는다.
        /// </summary>
        public Material CreateDedicatedMaterialAsset()
        {
            Material source = m_Material;
            Shader shader = source != null ? source.shader : Shader.Find(UIWaterShaderName);
            if (shader == null) return null;

            string defaultName = "UIWater2D_" + SanitizeFileName(gameObject.name);
            string path = EditorUtility.SaveFilePanelInProject(
                "전용 물 머티리얼 저장",
                defaultName,
                "mat",
                "이 오브젝트만 사용할 머티리얼을 저장할 위치를 선택하세요.",
                GetSuggestedSaveFolder());

            if (string.IsNullOrEmpty(path)) return null; // 사용자 취소

            Material created = source != null && source.shader == shader
                ? new Material(source)
                : new Material(shader);
            if (source == null) ApplyDefaultWaterKeywords(created);
            created.name = System.IO.Path.GetFileNameWithoutExtension(path);

            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();

            string folder = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder)) EditorPrefs.SetString(LastSaveFolderKey, folder);

            material = created;
            EditorUtility.SetDirty(this);
            return created;
        }

        private static string SanitizeFileName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Water";

            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            char[] buffer = raw.ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                if (System.Array.IndexOf(invalid, buffer[i]) >= 0) buffer[i] = '_';
            }
            return new string(buffer);
        }
#endif

        #endregion
    }
}
