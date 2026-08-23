#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace CAT.Water2D
{
    [CustomEditor(typeof(Water2D))]
    [CanEditMultipleObjects]
    public class Water2DEditor : Editor
    {
        private Water2D _target;

        // SerializedProperty 캐시
        private SerializedProperty _widthProp;
        private SerializedProperty _depthProp;
        private SerializedProperty _pointCountProp;
        private SerializedProperty _springConstantProp;
        private SerializedProperty _dampingProp;
        private SerializedProperty _spreadProp;
        private SerializedProperty _interactionEnabledProp;
        private SerializedProperty _velocityMultiplierProp;
        private SerializedProperty _massMultiplierProp;
        private SerializedProperty _maxImpulseProp;
        private SerializedProperty _buoyancyEnabledProp;
        private SerializedProperty _buoyancyForceProp;
        private SerializedProperty _linearDragProp;
        private SerializedProperty _angularDragProp;
        private SerializedProperty _sortingLayerIDProp;
        private SerializedProperty _sortingOrderProp;
        private SerializedProperty _onSplashProp;
        private SerializedProperty _materialProp;

        // 표면 라인
        private SerializedProperty _surfaceLineEnabledProp;
        private SerializedProperty _surfaceLineThicknessProp;
        private SerializedProperty _surfaceLineColorProp;

        // 지속 출렁임
        private SerializedProperty _ambientEnabledProp;
        private SerializedProperty _ambientIntensityProp;
        private SerializedProperty _waveAmplitudeProp;
        private SerializedProperty _waveLengthProp;
        private SerializedProperty _waveSpeedProp;
        private SerializedProperty _waveOctavesProp;
        private SerializedProperty _waveOctaveFalloffProp;
        private SerializedProperty _waveOctaveSpeedRatioProp;
        private SerializedProperty _waveRandomnessProp;
        private SerializedProperty _waveNoiseScaleProp;
        private SerializedProperty _waveNoiseSpeedProp;
        private SerializedProperty _ambientSeedProp;
        private SerializedProperty _randomImpulseEnabledProp;
        private SerializedProperty _impulseIntervalMinProp;
        private SerializedProperty _impulseIntervalMaxProp;
        private SerializedProperty _impulseForceMinProp;
        private SerializedProperty _impulseForceMaxProp;
        private SerializedProperty _impulseSpreadProp;

        // 머티리얼 인라인 편집
        private MaterialEditor _materialEditor;
        private WaterPreviewDriver _preview;
        private const string MaterialFoldoutKey = "CAT.Water2D.MaterialFoldout";

        /// <summary>섹션 접힘 상태 저장 키.</summary>
        private static string Key(string section) => "CAT.Water2D.Sec." + section;
        private const string CautionFoldoutKey = "CAT.Water2D.CautionFoldout";

        private static readonly Color BoundsWireColor = new Color(0.4f, 0.7f, 1f, 0.8f);
        private static readonly Color SurfaceLineColor = new Color(0.2f, 0.9f, 1f, 1f);
        private static readonly Color PointDotColor = new Color(1f, 1f, 1f, 0.9f);

        private void OnEnable()
        {
            _target = target as Water2D;

            _widthProp = serializedObject.FindProperty("_width");
            _depthProp = serializedObject.FindProperty("_depth");
            _pointCountProp = serializedObject.FindProperty("_pointCount");
            _springConstantProp = serializedObject.FindProperty("_springConstant");
            _dampingProp = serializedObject.FindProperty("_damping");
            _spreadProp = serializedObject.FindProperty("_spread");
            _interactionEnabledProp = serializedObject.FindProperty("_interactionEnabled");
            _velocityMultiplierProp = serializedObject.FindProperty("_velocityMultiplier");
            _massMultiplierProp = serializedObject.FindProperty("_massMultiplier");
            _maxImpulseProp = serializedObject.FindProperty("_maxImpulse");
            _buoyancyEnabledProp = serializedObject.FindProperty("_buoyancyEnabled");
            _buoyancyForceProp = serializedObject.FindProperty("_buoyancyForce");
            _linearDragProp = serializedObject.FindProperty("_linearDrag");
            _angularDragProp = serializedObject.FindProperty("_angularDrag");
            _sortingLayerIDProp = serializedObject.FindProperty("_sortingLayerID");
            _sortingOrderProp = serializedObject.FindProperty("_sortingOrder");
            _onSplashProp = serializedObject.FindProperty("_onSplash");
            _materialProp = serializedObject.FindProperty("_material");

            _surfaceLineEnabledProp = serializedObject.FindProperty("_surfaceLineEnabled");
            _surfaceLineThicknessProp = serializedObject.FindProperty("_surfaceLineThickness");
            _surfaceLineColorProp = serializedObject.FindProperty("_surfaceLineColor");

            _ambientEnabledProp = serializedObject.FindProperty("_ambientEnabled");
            _ambientIntensityProp = serializedObject.FindProperty("_ambientIntensity");
            _waveAmplitudeProp = serializedObject.FindProperty("_waveAmplitude");
            _waveLengthProp = serializedObject.FindProperty("_waveLength");
            _waveSpeedProp = serializedObject.FindProperty("_waveSpeed");
            _waveOctavesProp = serializedObject.FindProperty("_waveOctaves");
            _waveOctaveFalloffProp = serializedObject.FindProperty("_waveOctaveFalloff");
            _waveOctaveSpeedRatioProp = serializedObject.FindProperty("_waveOctaveSpeedRatio");
            _waveRandomnessProp = serializedObject.FindProperty("_waveRandomness");
            _waveNoiseScaleProp = serializedObject.FindProperty("_waveNoiseScale");
            _waveNoiseSpeedProp = serializedObject.FindProperty("_waveNoiseSpeed");
            _ambientSeedProp = serializedObject.FindProperty("_ambientSeed");
            _randomImpulseEnabledProp = serializedObject.FindProperty("_randomImpulseEnabled");
            _impulseIntervalMinProp = serializedObject.FindProperty("_impulseIntervalMin");
            _impulseIntervalMaxProp = serializedObject.FindProperty("_impulseIntervalMax");
            _impulseForceMinProp = serializedObject.FindProperty("_impulseForceMin");
            _impulseForceMaxProp = serializedObject.FindProperty("_impulseForceMax");
            _impulseSpreadProp = serializedObject.FindProperty("_impulseSpread");
        }

        private void OnDisable()
        {
            DestroyMaterialEditor();
            if (_preview != null) { _preview.Stop(); _preview.Dispose(); _preview = null; }
        }

        /// <summary>에디터가 소유하는 프리뷰 구동기 (모든 선택 대상을 함께 진행).</summary>
        private WaterPreviewDriver Preview
        {
            get
            {
                if (_preview == null)
                {
                    _preview = new WaterPreviewDriver(this, AdvanceAll, StopAll, false);
                }
                return _preview;
            }
        }

        private void AdvanceAll(float dt)
        {
            foreach (Object o in targets)
            {
                if (o is Water2D w) w.EditorAdvance(dt);
            }
        }

        private void StopAll()
        {
            foreach (Object o in targets)
            {
                if (o is Water2D w) w.EditorStopPreview();
            }
        }

        /// <summary>토글이 켜져 있을 때만 하위 옵션을 노출. 다중 선택에서 값이 섞이면 노출한다.</summary>
        private static bool IsToggledOn(SerializedProperty toggle)
        {
            return toggle != null && (toggle.boolValue || toggle.hasMultipleDifferentValues);
        }

        private void DestroyMaterialEditor()
        {
            if (_materialEditor == null) return;
            DestroyImmediate(_materialEditor);
            _materialEditor = null;
        }

        public override void OnInspectorGUI()
        {
            if (_target == null) return;
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(4);

            DrawSize();
            DrawMeshSection();
            DrawSpringSection();
            DrawAmbientSection();
            DrawInteractionSection();
            DrawBuoyancySection();
            DrawRenderingSection();
            DrawMaterialSection();
            DrawEventsSection();

            bool changed = serializedObject.ApplyModifiedProperties();
            if (changed)
            {
                foreach (Object o in targets)
                {
                    if (o is Water2D w) w.RebuildMeshIfDirty();
                }
            }

            WaterInspectorUI.Separator(8f, 4f);
            DrawEditorPreview();
            EditorGUILayout.Space(4);
            DrawTestButtons();
            DrawCautionSection();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("🌊 Water2D", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    $"표면 {_target.PointCount}점 · 폭 {_target.Width:F2} × 깊이 {_target.Depth:F2}",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawSize()
        {
            if (!WaterInspectorUI.Section(Key("size"), "크기",
                $"{_widthProp.floatValue:F2} × {_depthProp.floatValue:F2}")) return;

            EditorGUILayout.PropertyField(_widthProp);
            EditorGUILayout.PropertyField(_depthProp);
        }

        private void DrawMeshSection()
        {
            if (!WaterInspectorUI.Section(Key("mesh"), "메시 · 표면 해상도",
                $"{Mathf.Max(2, _pointCountProp.intValue)}점")) return;

            EditorGUILayout.PropertyField(_pointCountProp, new GUIContent("표면 정점 수"));

            int verts = Mathf.Max(2, _pointCountProp.intValue);
            float width = _widthProp.floatValue;
            float waveLen = _waveLengthProp != null ? _waveLengthProp.floatValue : 0f;
            // 옥타브마다 파장이 절반이 되므로, 각짐 여부는 '최단 옥타브' 기준으로 판단해야 한다
            int octaves = _waveOctavesProp != null ? Mathf.Clamp(_waveOctavesProp.intValue, 1, 4) : 1;
            float shortestWave = waveLen / (1 << (octaves - 1));
            float samplesPerWave = (shortestWave > 0.0001f && width > 0.0001f) ? (verts - 1) * shortestWave / width : 0f;

            EditorGUILayout.LabelField(" ",
                $"메시 정점 {verts * 2}개 · 최단 옥타브 파장당 샘플 {samplesPerWave:F1}개",
                EditorStyles.miniLabel);

            if (samplesPerWave > 0f && samplesPerWave < 8f)
            {
                EditorGUILayout.HelpBox(
                    "최단 옥타브 파장당 샘플이 8개 미만이면 표면이 각져 보입니다.\n정점 수를 늘리거나, 파장을 늘리거나, 옥타브 수를 줄이세요.",
                    MessageType.Warning);
            }
            if (verts > 128)
            {
                EditorGUILayout.HelpBox("정점이 많으면 매 프레임 메시 갱신 비용이 커집니다.", MessageType.Warning);
            }
        }

        private void DrawSpringSection()
        {
            string springSummary = (Application.isPlaying || Preview.IsPlaying)
                ? (_target.IsSpringAwake ? "동작 중" : "슬립")
                : null;
            if (!WaterInspectorUI.Section(Key("spring"), "스프링 물리 (파동 전파)", springSummary)) return;

            EditorGUILayout.PropertyField(_springConstantProp);
            EditorGUILayout.PropertyField(_dampingProp);
            EditorGUILayout.PropertyField(_spreadProp);

            string state = null;
            if (Application.isPlaying || Preview.IsPlaying)
            {
                state = _target.IsSpringAwake ? "동작 중 (awake)" : "슬립 (비용 0)";
            }
            EditorGUILayout.HelpBox(
                "충돌 · Splash() · 랜덤 임펄스로만 깨어나고, 표면이 멈추면 자동 슬립합니다 (연산·정점 업로드 생략)."
                + (state != null ? "\n현재 상태: " + state : ""),
                MessageType.None);
        }

        /// <summary>지속 출렁임 설정. 토글 OFF 면 하위 옵션을 감춘다.</summary>
        private void DrawAmbientSection()
        {
            if (!WaterInspectorUI.Section(Key("ambient"), "지속 출렁임 (Ambient Wave)",
                WaterInspectorUI.OnOff(_ambientEnabledProp.boolValue))) return;

            EditorGUILayout.PropertyField(_ambientEnabledProp,
                new GUIContent("Ambient Enabled", "충돌 없이도 표면이 계속 출렁인다 (스프링 시뮬과 무관한 해석적 변위)"));
            if (!IsToggledOn(_ambientEnabledProp)) return;

            EditorGUILayout.PropertyField(_ambientIntensityProp, new GUIContent("강도 배율"));

            EditorGUILayout.LabelField("진행 파형", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_waveAmplitudeProp, new GUIContent("진폭"));
            EditorGUILayout.PropertyField(_waveLengthProp, new GUIContent("파장"));
            EditorGUILayout.PropertyField(_waveSpeedProp, new GUIContent("진행 속도"));
            EditorGUILayout.PropertyField(_waveOctavesProp, new GUIContent("옥타브 수"));
            if (_waveOctavesProp.intValue > 1 || _waveOctavesProp.hasMultipleDifferentValues)
            {
                EditorGUILayout.PropertyField(_waveOctaveFalloffProp, new GUIContent("옥타브 진폭비"));
                EditorGUILayout.PropertyField(_waveOctaveSpeedRatioProp, new GUIContent("옥타브 속도비"));
            }

            EditorGUILayout.PropertyField(_waveRandomnessProp, new GUIContent("랜덤성"));
            if (_waveRandomnessProp.floatValue > 0f || _waveRandomnessProp.hasMultipleDifferentValues)
            {
                EditorGUILayout.PropertyField(_waveNoiseScaleProp, new GUIContent("노이즈 밀도"));
                EditorGUILayout.PropertyField(_waveNoiseSpeedProp, new GUIContent("노이즈 속도"));
            }
            EditorGUILayout.PropertyField(_ambientSeedProp, new GUIContent("시드"));

            // 시간 빈도 = 진행 속도 / 파장
            float hz = _waveLengthProp.floatValue > 0.0001f
                ? Mathf.Abs(_waveSpeedProp.floatValue) / _waveLengthProp.floatValue
                : 0f;
            EditorGUILayout.LabelField(" ", $"시간 빈도 ≈ {hz:F2} Hz (주기 {(hz > 0.0001f ? 1f / hz : 0f):F2}초)",
                EditorStyles.miniLabel);
            EditorGUI.indentLevel--;

            EditorGUILayout.PropertyField(_randomImpulseEnabledProp,
                new GUIContent("랜덤 임펄스", "주기적으로 실제 파동을 주입. 스프링 시뮬이 계속 깨어 있게 된다."));
            if (!IsToggledOn(_randomImpulseEnabledProp)) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_impulseIntervalMinProp, new GUIContent("간격 최소(초)"));
            EditorGUILayout.PropertyField(_impulseIntervalMaxProp, new GUIContent("간격 최대(초)"));
            EditorGUILayout.PropertyField(_impulseForceMinProp, new GUIContent("세기 최소"));
            EditorGUILayout.PropertyField(_impulseForceMaxProp, new GUIContent("세기 최대"));
            EditorGUILayout.PropertyField(_impulseSpreadProp, new GUIContent("퍼짐 폭"));
            EditorGUI.indentLevel--;

            EditorGUILayout.HelpBox(
                "⚠ 랜덤 임펄스는 스프링 시뮬을 계속 깨워둡니다 (간격이 짧으면 사실상 상시 동작).\n" +
                "비용 0 의 디스플레이용 물이 목표라면 OFF 로 두고 진행 파형만 사용하세요.",
                MessageType.Warning);
        }

        private void DrawInteractionSection()
        {
            if (!WaterInspectorUI.Section(Key("interaction"), "물리 · 충돌 상호작용 (opt-in)",
                WaterInspectorUI.OnOff(_interactionEnabledProp.boolValue))) return;

            EditorGUILayout.PropertyField(_interactionEnabledProp,
                new GUIContent("Interaction Enabled", "Rigidbody2D 진입 시 파동 주입. OFF 면 콜라이더가 비활성된다."));
            if (!IsToggledOn(_interactionEnabledProp)) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_velocityMultiplierProp);
            EditorGUILayout.PropertyField(_massMultiplierProp);
            EditorGUILayout.PropertyField(_maxImpulseProp);
            EditorGUI.indentLevel--;
        }

        private void DrawBuoyancySection()
        {
            if (!WaterInspectorUI.Section(Key("buoyancy"), "부력 (Buoyancy, opt-in)",
                WaterInspectorUI.OnOff(_buoyancyEnabledProp.boolValue))) return;

            EditorGUILayout.PropertyField(_buoyancyEnabledProp);

            if (IsToggledOn(_buoyancyEnabledProp))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_buoyancyForceProp);
                EditorGUILayout.PropertyField(_linearDragProp);
                EditorGUILayout.PropertyField(_angularDragProp);
                EditorGUI.indentLevel--;

                EditorGUILayout.HelpBox(
                    "물에 잠긴 Rigidbody2D 는 매 FixedUpdate 에서 부력·드래그를 받습니다. 가라앉게 하려면 mass 증가 또는 Force 감소.\n" +
                    "⚠ 잠긴 바디 수에 비례해 CPU 비용이 늘어납니다. 연출용이면 OFF 로 두세요.",
                    MessageType.Info);
            }
            else if (!IsToggledOn(_interactionEnabledProp))
            {
                EditorGUILayout.HelpBox(
                    "물리 기능 모두 OFF → BoxCollider2D 가 자동 비활성되어 트리거·물리 비용이 0 입니다 (디스플레이용 권장 상태).",
                    MessageType.Info);
            }
        }

        private void DrawRenderingSection()
        {
            if (!WaterInspectorUI.Section(Key("render"), "렌더링 · 정렬",
                $"order {_sortingOrderProp.intValue}")) return;

            // Sorting Layer 드롭다운 (SpriteRenderer 와 동일한 UX)
            SortingLayer[] layers = SortingLayer.layers;
            string[] names = new string[layers.Length];
            int currentIndex = 0;
            for (int i = 0; i < layers.Length; i++)
            {
                names[i] = layers[i].name;
                if (layers[i].id == _sortingLayerIDProp.intValue) currentIndex = i;
            }

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup(
                new GUIContent("Sorting Layer", "SpriteRenderer 와 공유되는 Sorting Layer"),
                currentIndex, names);
            if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < layers.Length)
            {
                _sortingLayerIDProp.intValue = layers[newIndex].id;
            }

            EditorGUILayout.PropertyField(_sortingOrderProp,
                new GUIContent("Order in Layer", "같은 Sorting Layer 내에서 앞뒤 정렬. 큰 값일수록 앞쪽."));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("표면 라인", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_surfaceLineEnabledProp,
                new GUIContent("라인 표시", "OFF 면 드로우콜 1개와 라인 머티리얼 인스턴스가 줄어든다."));
            if (IsToggledOn(_surfaceLineEnabledProp))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_surfaceLineThicknessProp, new GUIContent("두께"));
                EditorGUILayout.PropertyField(_surfaceLineColorProp, new GUIContent("색상"));
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>물 머티리얼: 에셋 관리 + 물속 효과 수치를 이 인스펙터 안에서 섹션별로 편집.</summary>
        private void DrawMaterialSection()
        {
            Material mat = _materialProp.objectReferenceValue as Material;
            if (!WaterInspectorUI.Section(Key("material"), "물속 표현 (머티리얼)",
                mat != null ? mat.name : "없음")) return;

            if (mat == null)
            {
                EditorGUILayout.PropertyField(_materialProp, new GUIContent("Material"));
                EditorGUILayout.HelpBox("머티리얼이 없습니다. 아래 버튼으로 패키지 기본 머티리얼을 할당하세요.", MessageType.Warning);
                if (GUILayout.Button("💧 기본 머티리얼 할당", GUILayout.Height(24)))
                {
                    foreach (Object o in targets)
                    {
                        if (o is Water2D w)
                        {
                            Material created = w.LoadOrCreateDefaultMaterialAsset();
                            if (created != null) w.WaterMaterial = created;
                            EditorUtility.SetDirty(w);
                        }
                    }
                    serializedObject.Update();
                    GUIUtility.ExitGUI();
                }
                return;
            }

            if (targets.Length > 1)
            {
                EditorGUILayout.PropertyField(_materialProp, new GUIContent("Material"));
                EditorGUILayout.HelpBox("다중 선택 중에는 물속 효과 수치를 편집할 수 없습니다.", MessageType.Info);
                return;
            }

            // 머티리얼 참조 + 공유/전용 상태를 한 줄로
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(_materialProp, new GUIContent("Material"));
                if (GUILayout.Button(new GUIContent("복제", "이 오브젝트 전용 머티리얼 에셋을 만든다 (저장 위치 선택)"), GUILayout.Width(46)))
                {
                    foreach (Object o in targets)
                    {
                        if (o is Water2D w) w.CreateDedicatedMaterialAsset();
                    }
                    serializedObject.Update();
                    DestroyMaterialEditor();
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button(new GUIContent("선택", "프로젝트 창에서 머티리얼 에셋 하이라이트"), GUILayout.Width(46)))
                {
                    EditorGUIUtility.PingObject(mat);
                    GUIUtility.ExitGUI();
                }
            }

            // 패키지 동봉 머티리얼 여부는 경로로 판정한다.
            // (OpenUPM·Git 설치본은 immutable 이라 편집까지 불가, 임베디드 개발본은 편집 가능)
            string matPath = AssetDatabase.GetAssetPath(mat);
            bool packaged = !string.IsNullOrEmpty(matPath) && matPath.StartsWith("Packages/");
            bool editable = AssetDatabase.IsOpenForEdit(mat);

            if (packaged)
            {
                EditorGUILayout.HelpBox(
                    "패키지에 동봉된 공용 기본 머티리얼입니다 (프로젝트에 파일을 만들지 않습니다).\n" +
                    (editable
                        ? "이 값을 바꾸면 같은 머티리얼을 쓰는 모든 물에 적용됩니다. 개체별로 다르게 하려면 [복제] 를 쓰세요."
                        : "읽기 전용이므로 수치를 바꾸려면 [복제] 로 프로젝트 안에 전용 머티리얼을 만드세요. 저장 위치는 직접 선택합니다."),
                    MessageType.Info);
            }

            if (_materialEditor == null || _materialEditor.target != mat)
            {
                DestroyMaterialEditor();
                _materialEditor = CreateEditor(mat) as MaterialEditor;
            }
            if (_materialEditor == null) return;

            using (new EditorGUI.DisabledScope(!editable))
            {
                WaterMaterialSections.Draw(_materialEditor, mat, null, "CAT.Water2D");
            }

            EditorGUILayout.Space(2);
            bool advanced = EditorPrefs.GetBool(MaterialFoldoutKey, false);
            bool next = EditorGUILayout.Foldout(advanced, "고급 (셰이더 전체 · 렌더 큐)", true);
            if (next != advanced) EditorPrefs.SetBool(MaterialFoldoutKey, next);
            if (next)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    _materialEditor.DrawHeader();
                    _materialEditor.RenderQueueField();
                    _materialEditor.EnableInstancingField();
                }
            }
        }

        private void DrawEventsSection()
        {
            if (!WaterInspectorUI.Section(Key("events"), "이벤트", null, false)) return;

            EditorGUILayout.PropertyField(_onSplashProp);
        }

        private void DrawEditorPreview()
        {
            Preview.DrawGUI("Play 없이 시뮬레이션을 재생합니다. 아래 Random Splash 로 파동도 테스트할 수 있습니다.");
        }

        private void DrawTestButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("🌊 Random Splash", GUILayout.Height(28)))
                {
                    foreach (Object o in targets)
                    {
                        if (o is Water2D w)
                        {
                            float lx = Random.Range(-w.Width * 0.5f, w.Width * 0.5f);
                            float f = Random.Range(-3f, -1f);
                            w.Splash(lx, f);
                        }
                    }
                }
                if (GUILayout.Button("⏹ Reset Surface", GUILayout.Height(28)))
                {
                    foreach (Object o in targets)
                    {
                        if (o is Water2D w) w.ResetSurface();
                    }
                }
            }
        }

        /// <summary>실측 기반 성능·빌드 주의사항. 사용자가 반드시 인지해야 하는 항목만 모아둔다.</summary>
        private void DrawCautionSection()
        {
            if (!WaterInspectorUI.Section(CautionFoldoutKey, "⚠ 성능 · 빌드 주의사항 (필독)")) return;

            EditorGUILayout.HelpBox(
                "① GPU 비용 = 물이 덮는 화면 면적 × 픽셀 비용\n" +
                "   Sprites/Default 대비 실측: 전 기능 OFF 1.04x / 왜곡+거품 1.33x / 기본(코스틱 포함) 3.23x / +질감 3.61x\n" +
                "   비용의 대부분은 코스틱입니다. 화면 전체를 덮는 수중 연출 + 저사양 기기 조합이면 코스틱을 끄세요.\n" +
                "   텍스처 페치는 기본 0 개라 대역폭 부담은 없습니다.",
                MessageType.Info);

            EditorGUILayout.HelpBox(
                "② 물리 기능은 opt-in\n" +
                "   Interaction / Buoyancy 가 모두 OFF 면 콜라이더가 비활성되어 물리 비용이 0 입니다.\n" +
                "   스프링 시뮬도 이벤트로만 깨어나고 정지 시 자동 슬립합니다 (연산·정점 업로드 생략).",
                MessageType.Info);

            EditorGUILayout.HelpBox(
                "③ 드로우콜은 개체당 2개 (물 메시 + 표면 라인)\n" +
                "   표면 라인이 필요 없으면 끄면 1개로 줄고, 라인 머티리얼 인스턴스도 생기지 않습니다.",
                MessageType.Info);

            EditorGUILayout.HelpBox(
                "④ Foam Thickness 는 UV(v) 기준이라 Depth 에 비례해 두꺼워집니다.\n" +
                "   Depth 를 크게 쓰면 값을 그만큼 줄여야 같은 굵기로 보입니다.",
                MessageType.Info);

            EditorGUILayout.HelpBox(
                "⑤ Point Count 는 24~48 로 충분합니다 (파장당 12~18 포인트). 64 초과는 이득 대비 비용만 늡니다.\n" +
                "   저프레임(30fps)에서는 고정 스텝 누적으로 스프링 스텝이 2배 실행됩니다 (상한 8스텝).",
                MessageType.Info);
        }

        private void OnSceneGUI()
        {
            if (_target == null) return;

            Transform t = _target.transform;
            float w = _target.Width;
            float d = _target.Depth;

            // bounds wire
            Vector3 c = t.TransformPoint(new Vector3(0f, -d * 0.5f, 0f));
            Handles.color = BoundsWireColor;
            Handles.matrix = Matrix4x4.TRS(c, t.rotation, t.lossyScale);
            Handles.DrawWireCube(Vector3.zero, new Vector3(w, d, 0f));
            Handles.matrix = Matrix4x4.identity;

            // 표면 라인 (런타임 점 기반)
            WaterPoint[] points = _target.EditorGetPoints();
            if (points == null || points.Length < 2) return;

            Handles.color = SurfaceLineColor;
            float halfW = w * 0.5f;
            float dx = w / (points.Length - 1);
            Vector3 prev = t.TransformPoint(new Vector3(-halfW, points[0].Height, 0f));
            for (int i = 1; i < points.Length; i++)
            {
                float x = -halfW + i * dx;
                Vector3 cur = t.TransformPoint(new Vector3(x, points[i].Height, 0f));
                Handles.DrawLine(prev, cur);
                prev = cur;
            }

            // 포인트 도트
            Handles.color = PointDotColor;
            for (int i = 0; i < points.Length; i++)
            {
                float x = -halfW + i * dx;
                Vector3 p = t.TransformPoint(new Vector3(x, points[i].Height, 0f));
                float size = HandleUtility.GetHandleSize(p) * 0.04f;
                Handles.DotHandleCap(0, p, Quaternion.identity, size, EventType.Repaint);
            }
        }
    }
}
#endif
