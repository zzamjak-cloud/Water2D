using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CAT.Water2D
{
    /// <summary>
    /// Splash 이벤트. (world position, 적용된 force)
    /// </summary>
    [System.Serializable]
    public class Water2DSplashEvent : UnityEvent<Vector2, float> { }

    /// <summary>
    /// 버텍스 기반 2D 물 시뮬레이션. 가로로 배치된 표면 포인트를 스프링처럼 거동시키고
    /// 좌·우 이웃으로 파동을 전파한다. 상단은 웨이브, 하단은 고정된 사각형 quad strip.
    ///
    /// [렌더링]
    /// - World-space MeshRenderer 방식. 컴포넌트 추가 시 CAT/Effects/2D Water 셰이더 기반
    ///   머티리얼 에셋이 자동 생성·할당된다 (인스펙터에서 다른 머티리얼로 교체 가능).
    /// - 물속 색상·질감 수치는 머티리얼(셰이더) 프로퍼티로 조절한다.
    ///
    /// [지속 출렁임]
    /// - Ambient Wave: 충돌 없이도 표면이 계속 출렁인다.
    ///   진행 파형(해석적 변위) + 랜덤 임펄스(실제 파동 주입) 2단 구성.
    ///
    /// [물리 기능 = opt-in]
    /// - Interaction Enabled / Buoyancy Enabled 가 모두 OFF 면 BoxCollider2D 를 비활성해
    ///   트리거 콜백·물리 브로드페이즈 비용을 제거한다. 기본값은 둘 다 OFF (디스플레이 전용).
    /// - 스프링 시뮬은 충돌·Splash·랜덤 임펄스로만 깨어나고, 표면이 정지하면 자동으로 잠든다.
    ///   잠든 동안에는 스텝 연산과 정점 업로드를 모두 생략한다.
    ///
    /// [공개 API]
    /// - Splash(localX, force): 외부 스크립트에서 임의 위치에 힘 주입
    /// - OnSplash UnityEvent: splash 발생 시 파티클 등 훅 연결
    ///
    /// [모바일 최적화]
    /// - Mesh 는 OnEnable 에서 1회 생성 (HideFlags.DontSave)
    /// - 정점/삼각형 배열은 pointCount 변경 시에만 재할당
    /// - 시뮬레이션은 고정 스텝 1/60s 어큐뮬레이터로 프레임레이트 독립
    /// - Update 에서 new/LINQ 금지
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("CAT/Effects/2D Water")]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider2D))]
    public class Water2D : MonoBehaviour
    {
        #region 직렬화 필드

        [SerializeField, Tooltip("물 본체 가로 폭")]
        private float _width = 4f;

        [SerializeField, Tooltip("물 본체 세로 깊이 (상단=0, 하단=-depth)")]
        private float _depth = 2f;

        [SerializeField, Range(4, 128), Tooltip("표면 정점 수. 시뮬레이션과 렌더 해상도를 함께 결정한다.\n진행 파형은 정점마다 해석적으로 평가되므로, 정점을 늘리면 그만큼 곡선이 매끄러워진다.")]
        private int _pointCount = 24;

        [SerializeField, Range(0.001f, 0.2f), Tooltip("복원력 강도 (k). 클수록 빠르게 평형으로 돌아옴")]
        private float _springConstant = 0.025f;

        [SerializeField, Range(0.001f, 0.1f), Tooltip("감쇠 계수. 클수록 진동이 빨리 사라짐")]
        private float _damping = 0.025f;

        [SerializeField, Range(0f, 0.5f), Tooltip("좌·우 이웃 전파율. 클수록 파동이 멀리 퍼짐")]
        private float _spread = 0.25f;

        [SerializeField, Tooltip("충돌 상호작용 활성화. OFF 면 BoxCollider2D 가 비활성되어 트리거 콜백·물리 브로드페이즈 비용이 0 이 된다.\n디스플레이용 물이면 OFF 로 두세요.")]
        private bool _interactionEnabled = false;

        [SerializeField, Tooltip("진입 속도(Y)에 대한 impulse 계수")]
        private float _velocityMultiplier = 0.1f;

        [SerializeField, Tooltip("질량에 대한 impulse 계수")]
        private float _massMultiplier = 0.05f;

        [SerializeField, Min(0f), Tooltip("단일 진입당 최대 impulse 절댓값 (클램프)")]
        private float _maxImpulse = 5f;

        [SerializeField, Tooltip("부력 시스템 활성화. 물에 잠긴 Rigidbody2D 에 매 FixedUpdate 로 힘을 가한다.")]
        private bool _buoyancyEnabled = false;

        [SerializeField, Min(0f), Tooltip("단위 잠김 깊이 × 질량당 위 방향 힘. 기본값 30 은 일반 2D 씬 중력(-9.81)에서 자연스러운 부유.")]
        private float _buoyancyForce = 30f;

        [SerializeField, Range(0f, 20f), Tooltip("수중 선형 감쇠(초당). 수직·수평 속도에 적용되어 물 속에서 천천히 감속.")]
        private float _linearDrag = 3f;

        [SerializeField, Range(0f, 20f), Tooltip("수중 각속도 감쇠(초당).")]
        private float _angularDrag = 1f;

        [SerializeField, Tooltip("MeshRenderer 의 Sorting Layer ID (SpriteRenderer 와 공유).")]
        private int _sortingLayerID = 0;

        [SerializeField, Tooltip("MeshRenderer 의 Order in Layer. SpriteRenderer 와 같은 레이어 내에서 앞뒤 정렬.")]
        private int _sortingOrder = 0;

        [SerializeField, Tooltip("물 머티리얼. 비어 있으면 CAT/Effects/2D Water 셰이더 머티리얼이 자동 생성·할당된다.")]
        private Material _material;

        [SerializeField, Tooltip("Splash 발생 시 호출: (world position, 적용된 force)")]
        private Water2DSplashEvent _onSplash = new Water2DSplashEvent();

        [SerializeField, Tooltip("물 표면 라인 렌더링 활성화")]
        private bool _surfaceLineEnabled = true;

        [SerializeField, Min(0f), Tooltip("물 표면 라인 두께(로컬 단위)")]
        private float _surfaceLineThickness = 0.06f;

        [SerializeField, Tooltip("물 표면 라인 색상")]
        private Color _surfaceLineColor = Color.white;

        // ── 지속 출렁임 (Ambient Wave) ──────────────────────────────────

        [SerializeField, Tooltip("충돌 없이도 표면이 계속 출렁이는 지속 파동 활성화 (횡스크롤 물 표현용).\n스프링 시뮬을 쓰지 않는 해석적 변위이므로 물리 기능과 무관하게 동작한다.")]
        private bool _ambientEnabled = true;

        [SerializeField, Range(0f, 3f), Tooltip("지속 출렁임 전체 강도 배율. 파형 진폭과 랜덤 임펄스 세기에 함께 곱해진다.")]
        private float _ambientIntensity = 1f;

        [SerializeField, Min(0f), Tooltip("진행 파형의 기본 진폭(로컬 단위). 표면이 위아래로 흔들리는 크기.")]
        private float _waveAmplitude = 0.08f;

        [SerializeField, Min(0.01f), Tooltip("진행 파형의 파장(로컬 단위). 작을수록 잔물결, 클수록 완만한 너울.")]
        private float _waveLength = 3f;

        [SerializeField, Tooltip("파형 진행 속도(로컬 단위/초). 음수면 반대 방향으로 흐른다. 시간 빈도 = 속도/파장(Hz).")]
        private float _waveSpeed = 0.6f;

        [SerializeField, Range(1, 4), Tooltip("중첩할 파형 개수. 여러 겹을 겹쳐 반복감을 줄인다.")]
        private int _waveOctaves = 2;

        [SerializeField, Range(0.1f, 1f), Tooltip("다음 옥타브의 진폭 비율. 옥타브마다 파장은 절반이 된다.")]
        private float _waveOctaveFalloff = 0.5f;

        [SerializeField, Range(0.5f, 3f), Tooltip("다음 옥타브의 진행 속도 비율. 1 이 아니면 파형이 서로 어긋나 반복 주기가 길어진다.")]
        private float _waveOctaveSpeedRatio = 1.6f;

        [SerializeField, Range(0f, 1f), Tooltip("랜덤성. 진폭 대비 Perlin 노이즈 비율. 0 이면 완전 규칙적인 사인파.")]
        private float _waveRandomness = 0.35f;

        [SerializeField, Min(0.01f), Tooltip("노이즈 공간 밀도. 클수록 잘게 흔들린다.")]
        private float _waveNoiseScale = 0.5f;

        [SerializeField, Tooltip("노이즈 시간 변화 속도. 클수록 랜덤 성분이 빠르게 변한다.")]
        private float _waveNoiseSpeed = 0.35f;

        [SerializeField, Tooltip("랜덤 시드. 같은 씬의 여러 물 오브젝트가 서로 다른 위상을 갖게 한다.")]
        private int _ambientSeed = 0;

        [SerializeField, Tooltip("주기적으로 랜덤 위치에 impulse 를 주입해 실제 파동을 전파시킨다. 자연스러움은 올라가지만\n스프링 시뮬이 계속 깨어 있게 되므로 CPU 비용이 발생한다. 디스플레이 전용이면 OFF 권장.")]
        private bool _randomImpulseEnabled = false;

        [SerializeField, Min(0.02f), Tooltip("랜덤 임펄스 최소 간격(초). 빈도 하한.")]
        private float _impulseIntervalMin = 0.6f;

        [SerializeField, Min(0.02f), Tooltip("랜덤 임펄스 최대 간격(초). 빈도 상한.")]
        private float _impulseIntervalMax = 2f;

        [SerializeField, Tooltip("랜덤 임펄스 세기 최소값. 음수는 아래로 찍히는 파동.\n기본 설정(퍼짐 0.6, Spring 0.025)에서 세기 0.05 ≈ 표면 변위 0.08.")]
        private float _impulseForceMin = -0.05f;

        [SerializeField, Tooltip("랜덤 임펄스 세기 최대값. 양수는 위로 솟는 파동.\n퍼짐 폭이 커지면 같은 세기로도 변위가 커진다 (0.3→약 2배, 1.2→약 5배).")]
        private float _impulseForceMax = 0.05f;

        [SerializeField, Min(0f), Tooltip("임펄스가 퍼지는 로컬 폭. 0 이면 한 포인트만 때려 날카로워진다.")]
        private float _impulseSpread = 0.6f;

        #endregion

        #region 런타임 상태

        // 시뮬레이션 코어 (Water2D / UIWater2D 공용)
        private readonly WaterSurface _surface = new WaterSurface();

        // 메시
        private Mesh _mesh;
        private Vector3[] _vertices = System.Array.Empty<Vector3>();
        private Vector2[] _uvs = System.Array.Empty<Vector2>();
        private int[] _triangles = System.Array.Empty<int>();

        // 컴포넌트 캐시
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private BoxCollider2D _collider;
        private LineRenderer _surfaceLineRenderer;
        private Material _surfaceLineMaterial;

        // 재할당 플래그
        private int _allocatedPointCount = -1;
        private Vector3[] _surfaceLinePositions = System.Array.Empty<Vector3>();

        // 진행 파형 캐시: 메시·표면 라인이 프레임당 1회 평가를 공유 (중복 평가 제거)
        private float[] _ambientCache = System.Array.Empty<float>();

        // 표면 라인 설정 재적용 필요 여부 (매 프레임 프로퍼티 재설정 방지)
        private bool _surfaceLineConfigDirty = true;

        // 물 속에 잠긴 Rigidbody2D 추적 (부력 적용 대상)
        private readonly System.Collections.Generic.HashSet<Rigidbody2D> _submergedBodies
            = new System.Collections.Generic.HashSet<Rigidbody2D>();

        // 메시 업데이트 플래그 (모바일 최적화)
        private const MeshUpdateFlags MeshFlags =
            MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds;

        // OnValidate 에서 에디터 시 재빌드가 필요함을 지연 표시
        private bool _rebuildRequested;

        // 머티리얼
        private Material _runtimeMaterial;   // 자동 생성분 (에셋이 아닌 인스턴스)
        private bool _shaderMissingWarned;
        public const string WaterShaderName = "CAT/Effects/2D Water";

        // 셰이더 프로퍼티 ID 캐시 (모바일 최적화: 문자열 해싱 반복 방지)
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
        private const string PackagedDefaultMaterialPath = "Water2D/Water2D_Default";

        /// <summary>
        /// 패키지에 동봉된 읽기 전용 기본 머티리얼. Resources 이므로 빌드에 항상 포함되고,
        /// 이 머티리얼이 셰이더를 참조하므로 셰이더 포함도 함께 보장된다.
        /// </summary>
        public static Material LoadPackagedDefaultMaterial()
        {
            return Resources.Load<Material>(PackagedDefaultMaterialPath);
        }

        #endregion

        #region 공개 프로퍼티·API

        /// <summary>물 본체 가로 폭 (로컬).</summary>
        public float Width => _width;

        /// <summary>물 본체 세로 깊이.</summary>
        public float Depth => _depth;

        /// <summary>표면 정점 수 (시뮬 = 렌더 해상도).</summary>
        public int PointCount => _surface.PointCount;

        /// <summary>Splash 이벤트 훅.</summary>
        public Water2DSplashEvent OnSplash => _onSplash;

        /// <summary>물 머티리얼. 설정 시 MeshRenderer 에 즉시 반영된다.</summary>
        public Material WaterMaterial
        {
            get => _material;
            set
            {
                _material = value;
                if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
                if (_meshRenderer != null && _meshRenderer.sharedMaterial != value)
                {
                    _meshRenderer.sharedMaterial = value;
                }
            }
        }

        /// <summary>충돌 상호작용 on/off. OFF 면 BoxCollider2D 가 비활성된다.</summary>
        public bool InteractionEnabled
        {
            get => _interactionEnabled;
            set { _interactionEnabled = value; SetupCollider(); }
        }

        /// <summary>부력 on/off. OFF 면 FixedUpdate 처리와 콜라이더가 필요 없으면 비활성된다.</summary>
        public bool BuoyancyEnabled
        {
            get => _buoyancyEnabled;
            set
            {
                _buoyancyEnabled = value;
                if (!value) _submergedBodies.Clear();
                SetupCollider();
            }
        }

        /// <summary>표면 라인 on/off.</summary>
        public bool SurfaceLineEnabled
        {
            get => _surfaceLineEnabled;
            set
            {
                if (_surfaceLineEnabled == value) return;
                _surfaceLineEnabled = value;
                _surfaceLineConfigDirty = true;
                _surface.RequestMeshUpdate();
            }
        }

        /// <summary>스프링 시뮬이 현재 동작 중인지. false 면 프레임 비용이 사실상 0.</summary>
        public bool IsSpringAwake => _surface.SpringAwake;

        /// <summary>지속 출렁임 on/off. 끄면 표면 변위가 다음 프레임에 0 으로 정리된다.</summary>
        public bool AmbientEnabled
        {
            get => _ambientEnabled;
            set => _ambientEnabled = value;
        }

        /// <summary>지속 출렁임 전체 강도 배율. 바람·상황 연출에 따라 런타임에서 보간해도 안전하다.</summary>
        public float AmbientIntensity
        {
            get => _ambientIntensity;
            set => _ambientIntensity = Mathf.Max(0f, value);
        }

        /// <summary>진행 파형 진폭(로컬 단위).</summary>
        public float WaveAmplitude
        {
            get => _waveAmplitude;
            set => _waveAmplitude = Mathf.Max(0f, value);
        }

        /// <summary>진행 파형 속도(로컬 단위/초). 음수는 반대 방향.</summary>
        public float WaveSpeed
        {
            get => _waveSpeed;
            set => _waveSpeed = value;
        }

        /// <summary>Sorting Layer ID (Unity 내부 hash).</summary>
        public int SortingLayerID
        {
            get => _sortingLayerID;
            set { _sortingLayerID = value; ApplySortingToRenderer(); }
        }

        /// <summary>Order in Layer.</summary>
        public int SortingOrder
        {
            get => _sortingOrder;
            set { _sortingOrder = value; ApplySortingToRenderer(); }
        }

        /// <summary>
        /// 임의 위치에 파동을 주입한다.
        /// </summary>
        /// <param name="localX">-width/2 ~ +width/2 범위의 로컬 X 좌표</param>
        /// <param name="force">표면 포인트 수직 속도에 가산되는 impulse. 양수는 표면이 위로 솟는 방향, 음수는 아래로 찍히는 방향.</param>
        public void Splash(float localX, float force)
        {
            if (_surface.PointCount == 0) return;

            _surface.Splash(LocalXToT(localX), force);
            InvokeSplashEvent(localX, force);
        }

        /// <summary>
        /// 지정 로컬 X 주변에 폭을 가진 파동을 주입한다. 단일 포인트 대비 부드러운 형태.
        /// </summary>
        /// <param name="localX">-width/2 ~ +width/2 범위의 로컬 X 좌표</param>
        /// <param name="force">중심 포인트에 가산되는 impulse (주변은 코사인 감쇠)</param>
        /// <param name="spread">영향 범위의 로컬 폭. 0 이면 단일 포인트.</param>
        public void SplashArea(float localX, float force, float spread)
        {
            if (_surface.PointCount == 0) return;

            _surface.SplashArea(LocalXToT(localX), force, spread / Mathf.Max(0.0001f, _width));
            InvokeSplashEvent(localX, force);
        }

        private void InvokeSplashEvent(float localX, float force)
        {
            if (_onSplash == null) return;

            WaterSimParams p = BuildSimParams();
            float height = _surface.SampleHeight(LocalXToT(localX), localX, in p);
            Vector3 worldPos = transform.TransformPoint(new Vector3(localX, height, 0f));
            _onSplash.Invoke(new Vector2(worldPos.x, worldPos.y), force);
        }

        /// <summary>표면을 평형 상태로 리셋한다.</summary>
        public void ResetSurface()
        {
            _surface.Reset();
            UpdateMeshVertices();
        }

        /// <summary>에디터에서 pointCount/width/depth 변경 후 즉시 메시를 다시 만든다.</summary>
        public void RebuildMeshIfDirty()
        {
            _rebuildRequested = true;
            CacheComponents();
            EnsureAllocated();
            BuildMeshTopology();
            UpdateMeshVertices();
            SetupCollider();
            ApplySortingToRenderer();
        }

        #endregion

        #region 라이프사이클

        private void Awake()
        {
            CacheComponents();
            EnsureAllocated();
            BuildMeshTopology();
            UpdateMeshVertices();
            SetupCollider();
            ApplySortingToRenderer();
        }

        private void OnEnable()
        {
            CacheComponents();
            EnsureAllocated();
            BuildMeshTopology();
            UpdateMeshVertices();
            SetupCollider();
            ApplySortingToRenderer();
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh);
                else DestroyImmediate(_mesh);
                _mesh = null;
            }

            if (_surfaceLineMaterial != null)
            {
                if (Application.isPlaying) Destroy(_surfaceLineMaterial);
                else DestroyImmediate(_surfaceLineMaterial);
                _surfaceLineMaterial = null;
            }

            if (_runtimeMaterial != null)
            {
                if (_material == _runtimeMaterial) _material = null;
                if (Application.isPlaying) Destroy(_runtimeMaterial);
                else DestroyImmediate(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }

        private void OnValidate()
        {
            if (_width < 0.01f) _width = 0.01f;
            if (_depth < 0.01f) _depth = 0.01f;
            _pointCount = Mathf.Clamp(_pointCount, 4, 128);
            if (_surfaceLineThickness < 0f) _surfaceLineThickness = 0f;
            _surfaceLineConfigDirty = true;

            // 지속 출렁임 값 정리
            if (_waveLength < 0.01f) _waveLength = 0.01f;
            if (_waveNoiseScale < 0.01f) _waveNoiseScale = 0.01f;
            _waveOctaves = Mathf.Clamp(_waveOctaves, 1, WaterSurface.MaxOctaves);
            _impulseIntervalMin = Mathf.Max(0.02f, _impulseIntervalMin);
            _impulseIntervalMax = Mathf.Max(_impulseIntervalMin, _impulseIntervalMax);
            if (_impulseForceMax < _impulseForceMin) _impulseForceMax = _impulseForceMin;
            if (_impulseSpread < 0f) _impulseSpread = 0f;
            _surface.ReseedRandom(_ambientSeed);

            _rebuildRequested = true;
        }

        private void Update()
        {
            if (_rebuildRequested)
            {
                EnsureAllocated();
                BuildMeshTopology();
                SetupCollider();
                ApplySortingToRenderer();
                EnsureMaterial();
                _surface.RequestMeshUpdate(); // 유휴 상태에서도 변경 사항 1회 반영
                _rebuildRequested = false;
            }

            if (Application.isPlaying) TickAndRender(Time.deltaTime);
        }

        /// <summary>
        /// 시뮬 1프레임 + 필요할 때만 메시 반영.
        /// 지속 출렁임 OFF · 스프링 슬립 상태에서는 정점 업로드조차 하지 않는다.
        /// </summary>
        private void TickAndRender(float deltaTime)
        {
            WaterSimParams p = BuildSimParams();
            _surface.Tick(deltaTime, in p);
            if (_surface.NeedsMeshUpdate) UpdateMeshVertices();
        }

        #endregion

        #region 초기화 / 메시 구성

        private void CacheComponents()
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
            if (_collider == null) _collider = GetComponent<BoxCollider2D>();
            if (_surfaceLineRenderer == null) _surfaceLineRenderer = GetComponent<LineRenderer>();
            if (_surfaceLineRenderer == null) _surfaceLineRenderer = gameObject.AddComponent<LineRenderer>();

            if (_mesh == null)
            {
                _mesh = new Mesh
                {
                    name = "Water2D Mesh",
                    hideFlags = HideFlags.DontSave
                };
                _mesh.MarkDynamic();
                _meshFilter.sharedMesh = _mesh;
            }

            if (_surfaceLineMaterial == null)
            {
                Shader lineShader = Shader.Find("Sprites/Default");
                if (lineShader != null)
                {
                    _surfaceLineMaterial = new Material(lineShader)
                    {
                        name = "Water2D Surface Line",
                        hideFlags = HideFlags.DontSave
                    };
                }
            }

            ConfigureSurfaceLineRenderer(true);
            EnsureMaterial();
            ApplyRendererPerfSettings();
        }

        /// <summary>
        /// 물 머티리얼을 보장한다. 인스펙터 지정 → MeshRenderer 기존 → 셰이더 기반 자동 생성 순.
        /// 자동 생성분은 HideFlags.DontSave 인스턴스이며 OnDestroy 에서 해제한다.
        /// </summary>
        private void EnsureMaterial()
        {
            if (_meshRenderer == null) return;

            if (_material == null) _material = _meshRenderer.sharedMaterial;

#if UNITY_EDITOR
            // 에디터(비플레이)에서는 항상 머티리얼 에셋을 쓴다.
            // 자동 생성된 비영속 인스턴스는 씬을 매번 dirty 로 만들고 수치가 저장되지 않으므로 에셋으로 승격.
            if (!Application.isPlaying && NeedsEditorMaterialAsset())
            {
                // 에셋이 이미 있으면 즉시 승격 (LoadAssetAtPath 는 이 타이밍에도 안전).
                Material existingAsset = LoadDefaultMaterialAsset();
                if (existingAsset != null) PromoteToMaterialAsset(existingAsset);
                else RequestEditorDefaultMaterial(); // 신규 생성만 안전한 타이밍으로 미룸

                if (_material == null) return;
            }
#endif

            if (_material == null)
            {
                // 패키지 동봉 기본 머티리얼 (Resources) — 프로젝트에 에셋을 만들지 않는다
                Material packaged = LoadPackagedDefaultMaterial();
                if (packaged != null)
                {
                    _material = packaged;
                    if (_meshRenderer.sharedMaterial != _material) _meshRenderer.sharedMaterial = _material;
                    return;
                }

                Shader shader = Shader.Find(WaterShaderName);
                if (shader == null)
                {
                    if (!_shaderMissingWarned)
                    {
                        _shaderMissingWarned = true;
                        Debug.LogWarning($"[Water2D] 셰이더 '{WaterShaderName}' 를 찾을 수 없습니다. 머티리얼을 직접 지정하세요.", this);
                    }
                    return;
                }

                _runtimeMaterial = new Material(shader)
                {
                    name = "CAT Water2D (Runtime)",
                    hideFlags = HideFlags.DontSave
                };
                ApplyDefaultWaterKeywords(_runtimeMaterial);
                _material = _runtimeMaterial;
            }

            if (_meshRenderer.sharedMaterial != _material) _meshRenderer.sharedMaterial = _material;
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

        /// <summary>2D 물에 불필요한 렌더러 기능을 끈다 (모바일 렌더링 비용 절감).</summary>
        private void ApplyRendererPerfSettings()
        {
            if (_meshRenderer == null) return;
            if (_meshRenderer.shadowCastingMode == ShadowCastingMode.Off && !_meshRenderer.receiveShadows) return;

            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            _meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            _meshRenderer.allowOcclusionWhenDynamic = false;
        }

        private void EnsureAllocated()
        {
            int n = Mathf.Max(2, _pointCount);
            _surface.Configure(n, _ambientSeed);
            if (_allocatedPointCount == n) return;

            _surfaceLineConfigDirty = true; // 정점 수 변경 → positionCount 재설정 필요

            int vcount = n * 2;
            _vertices = new Vector3[vcount];
            _uvs = new Vector2[vcount];
            _triangles = new int[(n - 1) * 6];
            _surfaceLinePositions = new Vector3[n];
            _ambientCache = new float[n];
            _allocatedPointCount = n;
        }

        /// <summary>정점 레이아웃과 삼각형 인덱스를 설정한다. 정점 Y 값은 별도 UpdateMeshVertices 에서 갱신.</summary>
        private void BuildMeshTopology()
        {
            if (_mesh == null) return;

            int n = _surface.PointCount;
            float halfW = _width * 0.5f;
            float dx = _width / (n - 1);

            // 상단 행 (i = 0..n-1), 하단 행 (i = n..2n-1)
            for (int i = 0; i < n; i++)
            {
                float x = -halfW + i * dx;
                _vertices[i] = new Vector3(x, 0f, 0f); // 상단 (Height 는 UpdateMeshVertices 에서 적용)
                _vertices[n + i] = new Vector3(x, -_depth, 0f);

                float u = (float)i / (n - 1);
                _uvs[i] = new Vector2(u, 1f);
                _uvs[n + i] = new Vector2(u, 0f);
            }

            // quad strip: 각 세그먼트당 2개 삼각형 (CCW)
            int ti = 0;
            for (int i = 0; i < n - 1; i++)
            {
                int topL = i;
                int topR = i + 1;
                int botL = n + i;
                int botR = n + i + 1;

                _triangles[ti++] = topL;
                _triangles[ti++] = topR;
                _triangles[ti++] = botL;

                _triangles[ti++] = topR;
                _triangles[ti++] = botR;
                _triangles[ti++] = botL;
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices, 0, _vertices.Length, MeshFlags);
            _mesh.SetUVs(0, _uvs, 0, _uvs.Length, MeshFlags);
            _mesh.SetTriangles(_triangles, 0, _triangles.Length, 0, false);
            _mesh.RecalculateBounds();
        }

        /// <summary>시뮬 결과(코어 표면 높이)를 상단 정점에 반영.</summary>
        private void UpdateMeshVertices()
        {
            if (_mesh == null || _surface.PointCount < 2 || _vertices.Length < 4) return;

            int n = _surface.PointCount;
            if (_ambientCache.Length != n) return;

            float halfW = _width * 0.5f;
            float dx = _width / (n - 1);
            WaterSimParams p = BuildSimParams();

            for (int i = 0; i < n; i++)
            {
                float x = -halfW + i * dx;
                _ambientCache[i] = _surface.EvaluateAmbient(x, in p);
                _vertices[i] = new Vector3(x, _surface.SpringHeightAt(i) + _ambientCache[i], 0f);
                // 하단 행은 토폴로지 구축 시 고정값이므로 재할당 스킵 (정점 수 미변경 시 유지)
            }

            _mesh.SetVertices(_vertices, 0, _vertices.Length, MeshFlags);
            // Bounds 는 웨이브 진폭이 작을 때 매 프레임 재계산 불필요.
            // 프리셋 여유를 포함한 bounds 를 한 번만 설정해 모바일 부담 최소화.
            _mesh.bounds = new Bounds(
                new Vector3(0f, -_depth * 0.5f, 0f),
                new Vector3(_width, _depth + 2f, 0.1f));

            UpdateSurfaceLinePositions();
        }

        private void SetupCollider()
        {
            if (_collider == null) return;
            _collider.isTrigger = true;
            _collider.size = new Vector2(_width, _depth);
            _collider.offset = new Vector2(0f, -_depth * 0.5f);

            // 물리 기능이 모두 꺼져 있으면 콜라이더 자체를 비활성 (트리거·브로드페이즈 비용 제거)
            bool physicsNeeded = _interactionEnabled || _buoyancyEnabled;
            if (_collider.enabled != physicsNeeded) _collider.enabled = physicsNeeded;
        }

        /// <summary>MeshRenderer 의 Sorting Layer/Order 를 직렬화 값과 동기화.</summary>
        private void ApplySortingToRenderer()
        {
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null) return;

            // sortingLayerID 가 유효한 레이어 해시인지 확인 (삭제된 레이어 대응)
            if (!SortingLayer.IsValid(_sortingLayerID))
            {
                _sortingLayerID = 0; // Default
            }
            _meshRenderer.sortingLayerID = _sortingLayerID;
            _meshRenderer.sortingOrder = _sortingOrder;
            ApplySortingToSurfaceLine();
            _surfaceLineConfigDirty = true;
        }

        /// <summary>
        /// LineRenderer 프로퍼티 재설정. 네이티브 setter 12개 + 머티리얼 할당이라
        /// 매 프레임 호출하면 낭비이므로 dirty 플래그가 설정된 경우에만 수행한다.
        /// </summary>
        private void ConfigureSurfaceLineRenderer(bool force = false)
        {
            if (_surfaceLineRenderer == null) return;
            if (!force && !_surfaceLineConfigDirty) return;
            _surfaceLineConfigDirty = false;

            _surfaceLineRenderer.useWorldSpace = false;
            _surfaceLineRenderer.loop = false;
            _surfaceLineRenderer.textureMode = LineTextureMode.Stretch;
            _surfaceLineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _surfaceLineRenderer.receiveShadows = false;
            _surfaceLineRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            _surfaceLineRenderer.generateLightingData = false;
            _surfaceLineRenderer.alignment = LineAlignment.TransformZ;

            float clampedThickness = Mathf.Max(0f, _surfaceLineThickness);
            _surfaceLineRenderer.startWidth = clampedThickness;
            _surfaceLineRenderer.endWidth = clampedThickness;
            _surfaceLineRenderer.startColor = _surfaceLineColor;
            _surfaceLineRenderer.endColor = _surfaceLineColor;
            _surfaceLineRenderer.positionCount = _surface.PointCount;
            _surfaceLineRenderer.enabled = _surfaceLineEnabled;

            if (_surfaceLineMaterial != null)
            {
                _surfaceLineRenderer.sharedMaterial = _surfaceLineMaterial;
            }

            ApplySortingToSurfaceLine();
        }

        private void ApplySortingToSurfaceLine()
        {
            if (_surfaceLineRenderer == null) return;
            _surfaceLineRenderer.sortingLayerID = _sortingLayerID;
            _surfaceLineRenderer.sortingOrder = _sortingOrder + 1;
        }

        private void UpdateSurfaceLinePositions()
        {
            if (_surfaceLineRenderer == null || _surface.PointCount < 2) return;

            // 설정 변경이 있었던 프레임에만 프로퍼티 재적용
            ConfigureSurfaceLineRenderer();
            if (!_surfaceLineEnabled) return;

            int n = _surface.PointCount;
            if (_surfaceLinePositions == null || _surfaceLinePositions.Length != n) return;
            if (_ambientCache.Length != n) return;

            float halfW = _width * 0.5f;
            float dx = _width / (n - 1);

            // 파형은 UpdateMeshVertices 에서 계산한 캐시를 재사용 (동일 프레임)
            for (int i = 0; i < n; i++)
            {
                _surfaceLinePositions[i] = new Vector3(-halfW + i * dx, _vertices[i].y, 0f);
            }

            _surfaceLineRenderer.SetPositions(_surfaceLinePositions);
        }

        #endregion

        #region 시뮬레이션 (WaterSurface 코어 위임)

        /// <summary>현재 인스펙터 값으로 시뮬 파라미터를 구성한다 (struct, 할당 없음).</summary>
        private WaterSimParams BuildSimParams()
        {
            WaterSimParams p;
            p.Width = _width;
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
            return Mathf.Clamp01((localX + _width * 0.5f) / Mathf.Max(0.0001f, _width));
        }

        #endregion


        #region 상호작용

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!Application.isPlaying) return;
            if (!_interactionEnabled && !_buoyancyEnabled) return;

            Rigidbody2D rb = other.attachedRigidbody;
            if (rb == null) return;

            if (_interactionEnabled)
            {
                Vector3 contactWorld = other.bounds.center;
                float localX = transform.InverseTransformPoint(contactWorld).x;

                // 진입 속도(Y, 음수 = 낙하) 와 질량으로 impulse 계산
                float velY = rb.linearVelocity.y;
                float impulse = velY * _velocityMultiplier - rb.mass * _massMultiplier;
                impulse = Mathf.Clamp(impulse, -_maxImpulse, _maxImpulse);

                Splash(localX, impulse);
            }

            if (_buoyancyEnabled) _submergedBodies.Add(rb);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Rigidbody2D rb = other.attachedRigidbody;
            if (rb == null) return;
            _submergedBodies.Remove(rb);
        }

        private void FixedUpdate()
        {
            if (!Application.isPlaying || !_buoyancyEnabled) return;
            if (_submergedBodies.Count == 0) return;

            float dt = Time.fixedDeltaTime;

            // Destroy 된 Rigidbody2D 참조 제거 (프레임당 1회)
            _submergedBodies.RemoveWhere(r => r == null);

            foreach (Rigidbody2D rb in _submergedBodies)
            {
                if (rb == null || !rb.simulated) continue;
                ApplyBuoyancy(rb, dt);
            }
        }

        private void ApplyBuoyancy(Rigidbody2D rb, float dt)
        {
            // 바디 위치를 로컬로 변환해 표면 높이와 비교
            Vector3 localPos = transform.InverseTransformPoint(rb.position);
            float surfaceLocalY = SampleSurfaceHeight(localPos.x);
            float submergedDepth = surfaceLocalY - localPos.y;

            if (submergedDepth <= 0f) return;

            // 부력: 잠김 깊이 × 질량 × 힘 계수 (위 방향)
            float buoyMag = _buoyancyForce * submergedDepth * rb.mass;
            rb.AddForce(new Vector2(0f, buoyMag), ForceMode2D.Force);

            // 수중 드래그 (선형·각속도)
            float linearFactor = Mathf.Max(0f, 1f - _linearDrag * dt);
            float angularFactor = Mathf.Max(0f, 1f - _angularDrag * dt);
            rb.linearVelocity *= linearFactor;
            rb.angularVelocity *= angularFactor;
        }

        /// <summary>
        /// 로컬 X 좌표의 표면 높이(로컬 Y)를 이웃 포인트 선형 보간으로 반환.
        /// 부력 계산 외에도 외부 스크립트에서 수면 높이 샘플링에 사용 가능.
        /// </summary>
        public float SampleSurfaceHeight(float localX)
        {
            WaterSimParams p = BuildSimParams();
            return _surface.SampleHeight(LocalXToT(localX), localX, in p);
        }

        #endregion

        #region 에디터 프리뷰

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 프리뷰 1스텝. 커스텀 에디터(Water2DEditor)가 EditorApplication.update 에서 구동한다.
        /// 컴포넌트가 직접 에디터 루프를 잡지 않으므로 프레임 간격이 균일하고 버벅임이 없다.
        /// </summary>
        public void EditorAdvance(float deltaTime)
        {
            if (Application.isPlaying) return;

            CacheComponents();
            EnsureAllocated();
            TickAndRender(deltaTime);
        }

        /// <summary>프리뷰 종료 시 표면을 평평하게 정리한다.</summary>
        public void EditorStopPreview()
        {
            _surface.Reset(true);
            UpdateMeshVertices();
        }

        /// <summary>에디터에서 표면 포인트 배열을 읽기 전용으로 노출 (기즈모용).</summary>
        public WaterPoint[] EditorGetPoints() => _surface.GetPointsUnsafe();

        /// <summary>컴포넌트 추가 시 호출. 기본 물 머티리얼 에셋을 만들어 즉시 할당한다.</summary>
        private void Reset()
        {
            CacheComponents();

            if (_material == null || _material == _runtimeMaterial)
            {
                Material asset = LoadOrCreateDefaultMaterialAsset();
                if (asset != null) WaterMaterial = asset;
            }

            RebuildMeshIfDirty();
        }

        [System.NonSerialized] private bool _defaultMaterialRequested;

        /// <summary>에디터에서 머티리얼 에셋 할당이 필요한 상태인지. (없음 또는 자동 생성된 비영속 인스턴스)</summary>
        private bool NeedsEditorMaterialAsset()
        {
            if (_material == null) return true;
            // 에셋(패키지 동봉분 포함)이면 그대로 사용. 과거 버전이 만든 비영속 인스턴스만 교체 대상.
            if (EditorUtility.IsPersistent(_material)) return false;

            return _material.shader != null && _material.shader.name == WaterShaderName;
        }

        /// <summary>다음 에디터 틱에 기본 머티리얼 에셋을 로드·생성해 할당한다. (AssetDatabase 안전 타이밍)</summary>
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
                if (asset != null) PromoteToMaterialAsset(asset);
            };
        }

        /// <summary>머티리얼 에셋으로 교체하고, 자동 생성된 비영속 인스턴스는 정리한다.</summary>
        private void PromoteToMaterialAsset(Material asset)
        {
            if (asset == null || _material == asset) return;

            Material stale = _material;
            WaterMaterial = asset;
            EditorUtility.SetDirty(this);

            if (stale != null && !EditorUtility.IsPersistent(stale))
            {
                if (_runtimeMaterial == stale) _runtimeMaterial = null;
                DestroyImmediate(stale);
            }
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
            Material source = _material;
            Shader shader = source != null ? source.shader : Shader.Find(WaterShaderName);
            if (shader == null) return null;

            string defaultName = "Water2D_" + SanitizeFileName(gameObject.name);
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

            WaterMaterial = created;
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
