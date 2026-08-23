#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace CAT.Water2D
{
    /// <summary>
    /// UIWater2D 커스텀 인스펙터. 비활성 옵션은 감추고, 실측 기반 주의사항을 HelpBox 로 노출한다.
    /// </summary>
    [CustomEditor(typeof(UIWater2D))]
    [CanEditMultipleObjects]
    public class UIWater2DEditor : Editor
    {
        private UIWater2D _target;

        // Graphic 기본
        private SerializedProperty _colorProp;
        private SerializedProperty _raycastTargetProp;
        private SerializedProperty _maskableProp;
        private SerializedProperty _materialProp;
        private SerializedProperty _textureProp;

        // 메시 · 스프링
        private SerializedProperty _pointCountProp;
        private SerializedProperty _springConstantProp;
        private SerializedProperty _dampingProp;
        private SerializedProperty _spreadProp;

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

        // 포인터 상호작용
        private SerializedProperty _pointerInteractionEnabledProp;
        private SerializedProperty _pointerForceProp;
        private SerializedProperty _pointerSpreadProp;
        private SerializedProperty _dragIntervalProp;

        private SerializedProperty _onSplashProp;

        private MaterialEditor _materialEditor;
        private WaterPreviewDriver _preview;
        private const string MaterialFoldoutKey = "CAT.UIWater2D.MaterialFoldout";

        /// <summary>섹션 접힘 상태 저장 키.</summary>
        private static string Key(string section) => "CAT.UIWater2D.Sec." + section;
        private const string CautionFoldoutKey = "CAT.UIWater2D.CautionFoldout";

        private void OnEnable()
        {
            _target = target as UIWater2D;

            _colorProp = serializedObject.FindProperty("m_Color");
            _raycastTargetProp = serializedObject.FindProperty("m_RaycastTarget");
            _maskableProp = serializedObject.FindProperty("m_Maskable");
            _materialProp = serializedObject.FindProperty("m_Material");
            _textureProp = serializedObject.FindProperty("_texture");

            _pointCountProp = serializedObject.FindProperty("_pointCount");
            _springConstantProp = serializedObject.FindProperty("_springConstant");
            _dampingProp = serializedObject.FindProperty("_damping");
            _spreadProp = serializedObject.FindProperty("_spread");

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

            _pointerInteractionEnabledProp = serializedObject.FindProperty("_pointerInteractionEnabled");
            _pointerForceProp = serializedObject.FindProperty("_pointerForce");
            _pointerSpreadProp = serializedObject.FindProperty("_pointerSpread");
            _dragIntervalProp = serializedObject.FindProperty("_dragInterval");

            _onSplashProp = serializedObject.FindProperty("_onSplash");
        }

        private void OnDisable()
        {
            DestroyMaterialEditor();
            if (_preview != null) { _preview.Stop(); _preview.Dispose(); _preview = null; }
        }

        /// <summary>에디터가 소유하는 프리뷰 구동기. UI 는 게임뷰(Canvas) 갱신도 필요하다.</summary>
        private WaterPreviewDriver Preview
        {
            get
            {
                if (_preview == null)
                {
                    _preview = new WaterPreviewDriver(this, AdvanceAll, StopAll, true);
                }
                return _preview;
            }
        }

        private void AdvanceAll(float dt)
        {
            foreach (Object o in targets)
            {
                if (o is UIWater2D w) w.EditorAdvance(dt);
            }
        }

        private void StopAll()
        {
            foreach (Object o in targets)
            {
                if (o is UIWater2D w) w.EditorStopPreview();
            }
        }

        private void DestroyMaterialEditor()
        {
            if (_materialEditor == null) return;
            DestroyImmediate(_materialEditor);
            _materialEditor = null;
        }

        private static bool IsToggledOn(SerializedProperty toggle)
        {
            return toggle != null && (toggle.boolValue || toggle.hasMultipleDifferentValues);
        }

        public override void OnInspectorGUI()
        {
            if (_target == null) return;
            serializedObject.Update();

            DrawHeaderBox();
            EditorGUILayout.Space(4);

            DrawGraphicSection();
            DrawMeshSection();
            DrawSpringSection();
            DrawAmbientSection();
            DrawPointerSection();
            DrawMaterialSection();
            DrawEventsSection();

            bool changed = serializedObject.ApplyModifiedProperties();
            if (changed)
            {
                foreach (Object o in targets)
                {
                    if (o is UIWater2D w) w.SetAllDirty();
                }
            }

            WaterInspectorUI.Separator(8f, 4f);
            DrawEditorPreview();
            EditorGUILayout.Space(4);
            DrawTestButtons();
            DrawCautionSection();
        }

        private void DrawHeaderBox()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("🌊 UIWater2D (Canvas)", EditorStyles.boldLabel);
                Rect r = _target.rectTransform != null ? _target.rectTransform.rect : new Rect();
                EditorGUILayout.LabelField(
                    $"표면 {_target.PointCount}점 · Rect {r.width:F0} × {r.height:F0} px",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawGraphicSection()
        {
            if (!WaterInspectorUI.Section(Key("graphic"), "그래픽 · 마스크",
                _maskableProp.boolValue ? "maskable" : "maskable OFF")) return;

            EditorGUILayout.PropertyField(_colorProp, new GUIContent("Color", "정점 색상으로 곱해진다 (CanvasGroup 알파 포함)"));
            EditorGUILayout.PropertyField(_maskableProp, new GUIContent("Maskable", "Mask / RectMask2D 대응. 켜 두세요."));
            EditorGUILayout.PropertyField(_raycastTargetProp, new GUIContent("Raycast Target", "포인터 상호작용을 쓸 때만 필요"));
        }

        private void DrawMeshSection()
        {
            if (!WaterInspectorUI.Section(Key("mesh"), "메시 · 표면 해상도",
                $"{Mathf.Max(2, _pointCountProp.intValue)}점")) return;

            EditorGUILayout.PropertyField(_pointCountProp, new GUIContent("표면 정점 수"));

            int verts = Mathf.Max(2, _pointCountProp.intValue);
            float width = _target.rectTransform != null ? _target.rectTransform.rect.width : 0f;
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
            if (verts > 96)
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
                "스프링 시뮬은 Splash() · 포인터 입력 · 랜덤 임펄스로만 깨어나고, 표면이 멈추면 자동 슬립합니다."
                + (state != null ? "\n현재 상태: " + state : ""),
                MessageType.None);
        }

        private void DrawAmbientSection()
        {
            if (!WaterInspectorUI.Section(Key("ambient"), "지속 출렁임 (Ambient Wave)",
                WaterInspectorUI.OnOff(_ambientEnabledProp.boolValue))) return;

            EditorGUILayout.PropertyField(_ambientEnabledProp, new GUIContent("Ambient Enabled"));
            if (!IsToggledOn(_ambientEnabledProp)) return;

            EditorGUILayout.PropertyField(_ambientIntensityProp, new GUIContent("강도 배율"));

            EditorGUILayout.LabelField("진행 파형", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_waveAmplitudeProp, new GUIContent("진폭 (px)"));
            EditorGUILayout.PropertyField(_waveLengthProp, new GUIContent("파장 (px)"));
            EditorGUILayout.PropertyField(_waveSpeedProp, new GUIContent("진행 속도 (px/s)"));
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

            float hz = _waveLengthProp.floatValue > 0.0001f
                ? Mathf.Abs(_waveSpeedProp.floatValue) / _waveLengthProp.floatValue
                : 0f;
            EditorGUILayout.LabelField(" ", $"시간 빈도 ≈ {hz:F2} Hz (주기 {(hz > 0.0001f ? 1f / hz : 0f):F2}초)",
                EditorStyles.miniLabel);
            EditorGUI.indentLevel--;

            EditorGUILayout.PropertyField(_randomImpulseEnabledProp, new GUIContent("랜덤 임펄스"));
            if (!IsToggledOn(_randomImpulseEnabledProp)) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_impulseIntervalMinProp, new GUIContent("간격 최소(초)"));
            EditorGUILayout.PropertyField(_impulseIntervalMaxProp, new GUIContent("간격 최대(초)"));
            EditorGUILayout.PropertyField(_impulseForceMinProp, new GUIContent("세기 최소"));
            EditorGUILayout.PropertyField(_impulseForceMaxProp, new GUIContent("세기 최대"));
            EditorGUILayout.PropertyField(_impulseSpreadProp, new GUIContent("퍼짐 폭 (px)"));
            EditorGUI.indentLevel--;

            EditorGUILayout.HelpBox(
                "⚠ 랜덤 임펄스는 스프링 시뮬을 계속 깨워둡니다. 비용 0 의 연출용 물이면 OFF 로 두고 진행 파형만 사용하세요.",
                MessageType.Warning);
        }

        private void DrawPointerSection()
        {
            if (!WaterInspectorUI.Section(Key("pointer"), "포인터 상호작용 (opt-in)",
                WaterInspectorUI.OnOff(_pointerInteractionEnabledProp.boolValue))) return;

            EditorGUILayout.PropertyField(_pointerInteractionEnabledProp,
                new GUIContent("Pointer Interaction", "클릭·터치·드래그로 물을 튀긴다"));
            if (!IsToggledOn(_pointerInteractionEnabledProp)) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_pointerForceProp, new GUIContent("입력 세기"));
            EditorGUILayout.PropertyField(_pointerSpreadProp, new GUIContent("퍼짐 폭 (px)"));
            EditorGUILayout.PropertyField(_dragIntervalProp, new GUIContent("드래그 간격(초)"));
            EditorGUI.indentLevel--;

            if (!_raycastTargetProp.boolValue && !_raycastTargetProp.hasMultipleDifferentValues)
            {
                EditorGUILayout.HelpBox("Raycast Target 이 꺼져 있어 포인터 입력이 들어오지 않습니다.", MessageType.Warning);
                if (GUILayout.Button("Raycast Target 켜기"))
                {
                    _raycastTargetProp.boolValue = true;
                }
            }
        }

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
                        if (o is UIWater2D w)
                        {
                            Material created = w.LoadOrCreateDefaultMaterialAsset();
                            if (created != null) w.material = created;
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

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(_materialProp, new GUIContent("Material"));
                if (GUILayout.Button(new GUIContent("복제", "이 오브젝트 전용 머티리얼 에셋을 만든다 (저장 위치 선택)"), GUILayout.Width(46)))
                {
                    foreach (Object o in targets)
                    {
                        if (o is UIWater2D w) w.CreateDedicatedMaterialAsset();
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
                // 질감 텍스처는 컴포넌트 필드(Graphic.mainTexture)를 이 섹션 안에서 편집
                WaterMaterialSections.Draw(_materialEditor, mat, _textureProp, "CAT.UIWater2D");
            }

            EditorGUILayout.HelpBox(
                "Mask(스텐실) 하위에서는 StencilMaterial 복사본이 만들어지므로, 런타임에 스크립트로 이 값을 바꾼 뒤에는 " +
                "SetMaterialDirty() 를 호출해야 반영됩니다. (셰이더가 시간 기반이라 애니메이션 자체는 갱신 불필요)",
                MessageType.None);

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
            Preview.DrawGUI("Play 없이 시뮬레이션을 재생합니다 (게임뷰가 열려 있어야 Canvas 가 갱신됩니다).");
        }

        private void DrawTestButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("🌊 Random Splash", GUILayout.Height(28)))
                {
                    foreach (Object o in targets)
                    {
                        if (o is UIWater2D w)
                        {
                            Rect r = w.rectTransform.rect;
                            w.SplashArea(Random.Range(r.xMin, r.xMax), Random.Range(-40f, -15f), 80f);
                        }
                    }
                }
                if (GUILayout.Button("⏹ Reset Surface", GUILayout.Height(28)))
                {
                    foreach (Object o in targets)
                    {
                        if (o is UIWater2D w) w.ResetSurface();
                    }
                }
            }
        }

        private void DrawCautionSection()
        {
            if (!WaterInspectorUI.Section(CautionFoldoutKey, "⚠ 성능 · 마스크 주의사항 (필독)")) return;

            EditorGUILayout.HelpBox(
                "① 매 프레임 정점이 갱신됩니다. 실측(Unity 6, 데스크톱): 물 1개당 시뮬 2.9us + 메시 재생성 10.6us (포인트 24).\n" +
                "   같은 Canvas 에 다른 UI 를 300개 두어도 유의미한 추가 비용은 관측되지 않았습니다(부분 재배치).\n" +
                "   그래도 매우 무거운 Canvas 라면 물만 담은 중첩 Canvas 로 분리하는 편이 안전합니다.",
                MessageType.Info);

            EditorGUILayout.HelpBox(
                "② 마스크 대응: Mask(스텐실)·RectMask2D 모두 UGUI 표준 방식으로 동작합니다.\n" +
                "   Maskable 을 켜 두세요. RectMask2D 는 _ClipRect, Mask 는 스텐실로 처리됩니다.",
                MessageType.Info);

            EditorGUILayout.HelpBox(
                "③ GPU 비용은 물이 덮는 화면 면적 × 픽셀 비용입니다. 코스틱이 비용의 대부분이며,\n" +
                "   저사양 대응이 필요하면 코스틱을 끈 전용 머티리얼을 준비하세요 (런타임 키워드 토글보다 안전).",
                MessageType.Info);

            EditorGUILayout.HelpBox(
                "④ 포인터 상호작용을 쓰지 않으면 Raycast Target 을 끄세요 (그래픽 레이캐스트 대상에서 제외).",
                MessageType.Info);
        }
    }
}
#endif
