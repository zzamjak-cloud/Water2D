#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace CAT.Water2D
{
    /// <summary>
    /// Water2D / UIWater2D 인스펙터 공통 UI 유틸.
    /// 섹션 접기 상태는 EditorPrefs 에 저장되어 선택을 바꿔도 유지된다.
    /// </summary>
    internal static class WaterInspectorUI
    {
        private static GUIStyle _sectionFoldout;
        private static readonly Color LineDark = new Color(0f, 0f, 0f, 0.25f);
        private static readonly Color LineLight = new Color(1f, 1f, 1f, 0.09f);

        private static GUIStyle SectionFoldout
        {
            get
            {
                if (_sectionFoldout == null)
                {
                    _sectionFoldout = new GUIStyle(EditorStyles.foldout)
                    {
                        fontStyle = FontStyle.Bold
                    };
                }
                return _sectionFoldout;
            }
        }

        /// <summary>가로 구분선.</summary>
        public static void Separator(float spaceBefore = 6f, float spaceAfter = 3f)
        {
            EditorGUILayout.Space(spaceBefore);
            Rect r = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, EditorGUIUtility.isProSkin ? LineLight : LineDark);
            EditorGUILayout.Space(spaceAfter);
        }

        /// <summary>
        /// 구분선 + 접기 헤더를 그리고 펼침 여부를 반환한다.
        /// </summary>
        /// <param name="prefsKey">접힘 상태 저장 키 (에디터별 고유 접두사 권장)</param>
        /// <param name="title">헤더 제목</param>
        /// <param name="summary">접혔을 때 우측에 표시할 요약 (선택)</param>
        /// <param name="defaultOpen">기본 펼침 여부</param>
        public static bool Section(string prefsKey, string title, string summary = null, bool defaultOpen = true)
        {
            Separator();

            bool open = EditorPrefs.GetBool(prefsKey, defaultOpen);

            Rect line = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth, EditorGUIUtility.singleLineHeight);
            Rect foldoutRect = line;
            bool hasSummary = !string.IsNullOrEmpty(summary);

            if (hasSummary)
            {
                float summaryWidth = Mathf.Min(line.width * 0.5f, 220f);
                foldoutRect.width = line.width - summaryWidth;

                Rect summaryRect = line;
                summaryRect.xMin = foldoutRect.xMax;
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.LabelField(summaryRect, summary, EditorStyles.miniLabel);
                }
            }

            bool next = EditorGUI.Foldout(foldoutRect, open, title, true, SectionFoldout);
            if (next != open) EditorPrefs.SetBool(prefsKey, next);

            if (next) EditorGUILayout.Space(2);
            return next;
        }

        /// <summary>토글 하나만으로 켜고 끄는 기능 섹션의 요약 문자열.</summary>
        public static string OnOff(bool on) => on ? "ON" : "OFF";
    }
}
#endif
