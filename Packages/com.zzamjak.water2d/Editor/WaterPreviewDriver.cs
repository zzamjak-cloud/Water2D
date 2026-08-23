#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace CAT.Water2D
{
    /// <summary>
    /// 에디터 프리뷰 구동기. 커스텀 에디터가 EditorApplication.update 루프를 소유하고
    /// 지정 시간(기본 60초) 동안 대상의 EditorAdvance 를 호출한다.
    ///
    /// 컴포넌트가 직접 에디터 루프를 잡으면 인스펙터·씬뷰 리페인트 타이밍에 종속되어
    /// 프레임 간격이 튀는데(버벅임), 이 방식은 매 틱에서 플레이어 루프와 뷰 리페인트를
    /// 명시적으로 밀어주므로 재생이 균일하다.
    /// </summary>
    internal sealed class WaterPreviewDriver
    {
        /// <summary>프리뷰 재생 시간(초).</summary>
        public const float Duration = 60f;

        private readonly System.Action<float> _advance;
        private readonly System.Action _onStop;
        private readonly Editor _owner;
        private readonly bool _repaintGameView;

        private double _startTime;
        private double _lastTime;
        private bool _isPlaying;

        public bool IsPlaying => _isPlaying;

        /// <summary>남은 시간(초).</summary>
        public float Remaining => _isPlaying
            ? Mathf.Max(0f, Duration - (float)(EditorApplication.timeSinceStartup - _startTime))
            : 0f;

        /// <param name="owner">리페인트 대상 인스펙터</param>
        /// <param name="advance">deltaTime 을 받아 1스텝 진행</param>
        /// <param name="onStop">정지 시 정리 콜백</param>
        /// <param name="repaintGameView">Canvas(UI) 처럼 게임뷰 갱신이 필요한 경우 true</param>
        public WaterPreviewDriver(Editor owner, System.Action<float> advance, System.Action onStop, bool repaintGameView)
        {
            _owner = owner;
            _advance = advance;
            _onStop = onStop;
            _repaintGameView = repaintGameView;
        }

        public void Start()
        {
            if (_isPlaying) return;

            _startTime = EditorApplication.timeSinceStartup;
            _lastTime = _startTime;
            _isPlaying = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        public void Stop()
        {
            EditorApplication.update -= Tick;
            if (!_isPlaying) return;

            _isPlaying = false;
            if (_onStop != null) _onStop();
            RepaintViews();
        }

        /// <summary>에디터가 사라질 때 반드시 호출 (루프 누수 방지).</summary>
        public void Dispose() => EditorApplication.update -= Tick;

        private void Tick()
        {
            if (!_isPlaying || Application.isPlaying) { Stop(); return; }

            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _lastTime);
            _lastTime = now;

            if (now - _startTime >= Duration) { Stop(); return; }

            if (_advance != null) _advance(Mathf.Clamp(dt, 0f, 0.1f));
            RepaintViews();
        }

        private void RepaintViews()
        {
            if (_owner != null) _owner.Repaint();
            SceneView.RepaintAll();

            if (_repaintGameView)
            {
                // Canvas 갱신·배치는 에디터 플레이어 루프에서 수행되므로 명시적으로 밀어준다.
                EditorApplication.QueuePlayerLoopUpdate();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }

        /// <summary>프리뷰 시작/정지 버튼 + 남은 시간 표시.</summary>
        public void DrawGUI(string helpText)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("에디터 프리뷰", EditorStyles.boldLabel);

                if (!_isPlaying)
                {
                    if (GUILayout.Button($"▶ {Duration:F0}초 플레이", GUILayout.Height(28))) Start();
                    if (!string.IsNullOrEmpty(helpText))
                    {
                        EditorGUILayout.LabelField(helpText, EditorStyles.miniLabel);
                    }
                }
                else
                {
                    float remaining = Remaining;
                    Rect bar = GUILayoutUtility.GetRect(1f, 18f);
                    EditorGUI.ProgressBar(bar, 1f - remaining / Duration, $"재생 중 · 남은 시간 {remaining:F0}초");
                    if (GUILayout.Button("⏹ 정지", GUILayout.Height(24))) Stop();
                }
            }
        }
    }
}
#endif
