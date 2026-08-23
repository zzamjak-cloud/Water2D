using UnityEngine;

namespace CAT.Water2D.Test
{
    /// <summary>
    /// Water2D / UIWater2D 성능 점검용 테스트 HUD. (테스트 씬 전용 — 런타임 플러그인 API 아님)
    ///
    /// 실기(모바일)에서 A/B 비교를 하기 위한 최소 도구:
    /// - 프레임 시간 이동 평균 / FPS 표시
    /// - 버튼으로 UI 물 · 월드 물 · 코스틱을 켜고 끄며 즉시 델타 확인
    ///
    /// 사용법: 테스트 씬 실행 → 화면 좌상단 HUD. 기기에서는 버튼을 터치.
    /// </summary>
    [AddComponentMenu("")]
    public class Water2DTestHud : MonoBehaviour
    {
        [SerializeField, Range(10, 240), Tooltip("이동 평균 샘플 프레임 수")]
        private int _sampleWindow = 120;

        [SerializeField, Tooltip("화면에 HUD 표시")]
        private bool _showHud = true;

        [SerializeField, Range(1, 4), Tooltip("HUD 글자 배율 (고해상도 기기 대응)")]
        private int _hudScale = 2;

        private float[] _samples;
        private int _index;
        private int _filled;

        private float _avgMs;
        private float _maxMs;
        private string _cachedText = "";
        private float _nextTextUpdate;

        private UIWater2D[] _uiWaters;
        private Water2D[] _worldWaters;

        /// <summary>최근 이동 평균 프레임 시간(ms). 외부 계측·자동화에서 읽는 용도.</summary>
        public static float LastAverageMs { get; private set; }

        private void Awake()
        {
            _samples = new float[Mathf.Max(10, _sampleWindow)];
            _uiWaters = FindObjectsByType<UIWater2D>(FindObjectsSortMode.None);
            _worldWaters = FindObjectsByType<Water2D>(FindObjectsSortMode.None);
        }

        private void Update()
        {
            float ms = Time.unscaledDeltaTime * 1000f;
            _samples[_index] = ms;
            _index = (_index + 1) % _samples.Length;
            if (_filled < _samples.Length) _filled++;

            float sum = 0f;
            float max = 0f;
            for (int i = 0; i < _filled; i++)
            {
                sum += _samples[i];
                if (_samples[i] > max) max = _samples[i];
            }
            _avgMs = _filled > 0 ? sum / _filled : 0f;
            _maxMs = max;
            LastAverageMs = _avgMs;

            // 문자열 생성은 초당 4회로 제한 (HUD 자체 비용 최소화)
            if (Time.unscaledTime >= _nextTextUpdate)
            {
                _nextTextUpdate = Time.unscaledTime + 0.25f;
                int awakeCount = 0;
                for (int i = 0; i < _uiWaters.Length; i++) if (_uiWaters[i] != null && _uiWaters[i].IsSpringAwake) awakeCount++;
                for (int i = 0; i < _worldWaters.Length; i++) if (_worldWaters[i] != null && _worldWaters[i].IsSpringAwake) awakeCount++;

                _cachedText = string.Concat(
                    "avg ", _avgMs.ToString("F2"), " ms  (", (1000f / Mathf.Max(0.0001f, _avgMs)).ToString("F0"), " fps)\n",
                    "max ", _maxMs.ToString("F2"), " ms\n",
                    "UI water ", _uiWaters.Length.ToString(), " / world ", _worldWaters.Length.ToString(),
                    "  spring awake ", awakeCount.ToString());
            }
        }

        private void ResetSamples()
        {
            _index = 0;
            _filled = 0;
        }

        private void OnGUI()
        {
            if (!_showHud) return;

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 14 * _hudScale;
            style.normal.textColor = Color.white;

            float w = 340f * _hudScale;
            float h = 110f * _hudScale;
            GUI.Box(new Rect(10f, 10f, w, h), GUIContent.none);
            GUI.Label(new Rect(20f, 16f, w - 20f, h), _cachedText, style);

            float by = 10f + h + 6f;
            float bw = 200f * _hudScale;
            float bh = 44f * _hudScale;
            GUIStyle btn = new GUIStyle(GUI.skin.button);
            btn.fontSize = 13 * _hudScale;

            if (GUI.Button(new Rect(10f, by, bw, bh), "UI 물 토글", btn))
            {
                for (int i = 0; i < _uiWaters.Length; i++)
                {
                    if (_uiWaters[i] != null) _uiWaters[i].enabled = !_uiWaters[i].enabled;
                }
                ResetSamples();
            }
            if (GUI.Button(new Rect(10f, by + bh + 4f, bw, bh), "월드 물 토글", btn))
            {
                for (int i = 0; i < _worldWaters.Length; i++)
                {
                    if (_worldWaters[i] != null) _worldWaters[i].enabled = !_worldWaters[i].enabled;
                }
                ResetSamples();
            }
            if (GUI.Button(new Rect(10f, by + (bh + 4f) * 2f, bw, bh), "코스틱 토글", btn))
            {
                ToggleCaustics();
                ResetSamples();
            }
        }

        /// <summary>씬의 물 머티리얼에서 코스틱 키워드를 토글한다 (A/B 비교용).</summary>
        private void ToggleCaustics()
        {
            for (int i = 0; i < _uiWaters.Length; i++) ToggleCausticsOn(_uiWaters[i] != null ? _uiWaters[i].material : null);
            for (int i = 0; i < _worldWaters.Length; i++)
            {
                if (_worldWaters[i] == null) continue;
                ToggleCausticsOn(_worldWaters[i].WaterMaterial);
            }
        }

        private static void ToggleCausticsOn(Material m)
        {
            if (m == null) return;
            if (m.IsKeywordEnabled("_CAT_CAUSTICS")) m.DisableKeyword("_CAT_CAUSTICS");
            else m.EnableKeyword("_CAT_CAUSTICS");
        }
    }
}
