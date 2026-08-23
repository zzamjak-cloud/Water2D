#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace CAT.Water2D
{
    /// <summary>
    /// 물 머티리얼(셰이더) 프로퍼티를 컴포넌트 인스펙터 안에 기능별 섹션으로 그린다.
    ///
    /// 머티리얼 인스펙터를 따로 오가지 않도록, 질감 텍스처·코스틱·왜곡·거품을 각각 한 곳에 모으고
    /// 토글이 꺼진 기능의 하위 옵션은 감춘다. 값 기록은 MaterialEditor.ShaderProperty 를 통하므로
    /// Undo·keyword(shader_feature) 동기화가 Unity 기본 동작과 동일하다.
    /// </summary>
    internal static class WaterMaterialSections
    {
        /// <param name="matEditor">ShaderProperty 그리기에 필요한 머티리얼 에디터</param>
        /// <param name="mat">대상 머티리얼</param>
        /// <param name="componentTextureProp">
        /// UI 버전처럼 텍스처가 컴포넌트 필드(Graphic.mainTexture)로 주입되는 경우 그 프로퍼티.
        /// null 이면 머티리얼의 _MainTex 를 직접 그린다.
        /// </param>
        public static void Draw(MaterialEditor matEditor, Material mat, SerializedProperty componentTextureProp,
                                string prefsPrefix)
        {
            if (matEditor == null || mat == null) return;

            MaterialProperty[] props = MaterialEditor.GetMaterialProperties(new Object[] { mat });

            DrawColorSection(matEditor, props, prefsPrefix);
            DrawTextureSection(matEditor, mat, props, componentTextureProp, prefsPrefix);
            DrawCausticsSection(matEditor, props, prefsPrefix);
            DrawDistortSection(matEditor, props, prefsPrefix);
            DrawFoamSection(matEditor, props, prefsPrefix);
            DrawFadeSection(matEditor, props, prefsPrefix);
        }

        private static bool Section(string prefix, string key, string title, string summary = null, bool defaultOpen = true)
        {
            return WaterInspectorUI.Section(prefix + ".Mat." + key, title, summary, defaultOpen);
        }

        #region 섹션

        private static void DrawColorSection(MaterialEditor ed, MaterialProperty[] props, string prefix)
        {
            if (!Section(prefix, "color", "색상 · 깊이")) return;
            using (new EditorGUI.IndentLevelScope())
            {
                Prop(ed, props, "_ShallowColor", "수면 색");
                Prop(ed, props, "_DeepColor", "심층 색");
                Prop(ed, props, "_GradientPower", "그라디언트 집중도");
                Prop(ed, props, "_Alpha", "전체 투명도"); // 월드 전용 (UI 는 Graphic Color 사용)
            }
        }

        private static void DrawTextureSection(MaterialEditor ed, Material mat, MaterialProperty[] props,
                                               SerializedProperty componentTextureProp, string prefix)
        {
            MaterialProperty texToggle = Find(props, "_TextureEnabled");
            if (!Section(prefix, "texture", "질감 텍스처",
                WaterInspectorUI.OnOff(texToggle != null && texToggle.floatValue > 0.5f))) return;

            using (new EditorGUI.IndentLevelScope())
            {
                MaterialProperty toggle = texToggle;
                if (toggle != null) ed.ShaderProperty(toggle, "질감 텍스처 사용");

                bool on = toggle != null && toggle.floatValue > 0.5f;
                Texture tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;

                // 텍스처 필드는 항상 이 섹션에서 편집한다 (컴포넌트/머티리얼 어느 쪽이든)
                if (componentTextureProp != null)
                {
                    EditorGUILayout.PropertyField(componentTextureProp,
                        new GUIContent("텍스처", "Graphic.mainTexture 로 셰이더 _MainTex 에 주입된다."));
                    tex = componentTextureProp.objectReferenceValue as Texture;
                }
                else
                {
                    Prop(ed, props, "_MainTex", "텍스처");
                }

                if (!on)
                {
                    if (tex != null)
                    {
                        EditorGUILayout.HelpBox(
                            "텍스처가 지정되어 있지만 '질감 텍스처 사용' 이 꺼져 있어 화면에 전혀 반영되지 않습니다.\n" +
                            "(shader_feature 라 토글이 꺼지면 샘플링 코드 자체가 제거됩니다)",
                            MessageType.Warning);
                        if (GUILayout.Button("질감 텍스처 사용 켜기") && toggle != null)
                        {
                            toggle.floatValue = 1f;
                            mat.EnableKeyword("_CAT_TEXTURE");
                            EditorUtility.SetDirty(mat);
                        }
                    }
                    return;
                }

                Prop(ed, props, "_TexBlendMode", "합성 (0=곱셈 · 1=오버레이)");
                Prop(ed, props, "_TexStrength", "세기");
                Prop(ed, props, "_TexTint", "색조");
                Prop(ed, props, "_TexTiling", "타일링 (XY)");
                Prop(ed, props, "_TexScroll", "스크롤 (XY/초)");

                if (tex == null)
                {
                    EditorGUILayout.HelpBox("텍스처가 없어 흰색으로 샘플링됩니다. 곱셈 모드에서는 변화가 없습니다.", MessageType.Info);
                    return;
                }

                DrawWrapModeWarning(mat, tex);
            }
        }

        private static void DrawCausticsSection(MaterialEditor ed, MaterialProperty[] props, string prefix)
        {
            MaterialProperty caToggle = Find(props, "_CausticsEnabled");
            if (!Section(prefix, "caustics", "코스틱 (물결 무늬)",
                WaterInspectorUI.OnOff(caToggle != null && caToggle.floatValue > 0.5f))) return;

            using (new EditorGUI.IndentLevelScope())
            {
                MaterialProperty toggle = caToggle;
                if (toggle != null) ed.ShaderProperty(toggle, "코스틱 사용");
                if (toggle != null && toggle.floatValue <= 0.5f)
                {
                    EditorGUILayout.LabelField(" ", "OFF — 픽셀 비용이 가장 크게 줄어드는 옵션입니다.", EditorStyles.miniLabel);
                    return;
                }

                Prop(ed, props, "_CausticsColor", "색");
                Prop(ed, props, "_CausticsStrength", "세기");
                Prop(ed, props, "_CausticsScale", "밀도");
                Prop(ed, props, "_CausticsSpeed", "속도");
                Prop(ed, props, "_CausticsSharpness", "선명도");
                Prop(ed, props, "_CausticsDepthBias", "깊이 감쇠");
            }
        }

        private static void DrawDistortSection(MaterialEditor ed, MaterialProperty[] props, string prefix)
        {
            MaterialProperty dToggle = Find(props, "_DistortEnabled");
            if (!Section(prefix, "distort", "굴절 왜곡",
                WaterInspectorUI.OnOff(dToggle != null && dToggle.floatValue > 0.5f))) return;

            using (new EditorGUI.IndentLevelScope())
            {
                MaterialProperty toggle = dToggle;
                if (toggle != null) ed.ShaderProperty(toggle, "왜곡 사용");
                if (toggle != null && toggle.floatValue <= 0.5f) return;

                Prop(ed, props, "_DistortStrength", "세기");
                Prop(ed, props, "_DistortScale", "밀도");
                Prop(ed, props, "_DistortSpeed", "속도");
                EditorGUILayout.LabelField(" ", "코스틱·질감 UV 를 함께 흔들어 흐르는 느낌을 만듭니다.", EditorStyles.miniLabel);
            }
        }

        private static void DrawFoamSection(MaterialEditor ed, MaterialProperty[] props, string prefix)
        {
            MaterialProperty fToggle = Find(props, "_FoamEnabled");
            if (!Section(prefix, "foam", "수면 거품",
                WaterInspectorUI.OnOff(fToggle != null && fToggle.floatValue > 0.5f))) return;

            using (new EditorGUI.IndentLevelScope())
            {
                MaterialProperty toggle = fToggle;
                if (toggle != null) ed.ShaderProperty(toggle, "거품 사용");
                if (toggle != null && toggle.floatValue <= 0.5f) return;

                Prop(ed, props, "_FoamColor", "색");
                Prop(ed, props, "_FoamThickness", "두께 (UV)");
                Prop(ed, props, "_FoamSoftness", "경계 부드러움");
                EditorGUILayout.LabelField(" ", "두께는 UV(v) 기준이라 Depth·Rect 높이에 비례해 두꺼워집니다.", EditorStyles.miniLabel);
            }
        }

        private static void DrawFadeSection(MaterialEditor ed, MaterialProperty[] props, string prefix)
        {
            if (!Section(prefix, "fade", "경계 페이드", null, false)) return;
            using (new EditorGUI.IndentLevelScope())
            {
                Prop(ed, props, "_BottomFade", "하단");
                Prop(ed, props, "_EdgeFade", "좌우");
            }
        }

        #endregion

        #region 유틸

        private static MaterialProperty Find(MaterialProperty[] props, string name)
        {
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i].name == name) return props[i];
            }
            return null;
        }

        /// <summary>프로퍼티가 있을 때만 그린다 (월드/UI 셰이더 차이 흡수).</summary>
        private static void Prop(MaterialEditor ed, MaterialProperty[] props, string name, string label)
        {
            MaterialProperty p = Find(props, name);
            if (p == null) return;
            ed.ShaderProperty(p, label);
        }

        /// <summary>타일링을 쓰는데 텍스처가 Repeat 가 아니면 가장자리가 늘어난다 (Sprite 임포트 기본이 Clamp).</summary>
        private static void DrawWrapModeWarning(Material mat, Texture tex)
        {
            if (!mat.HasProperty("_TexTiling")) return;

            Vector4 tiling = mat.GetVector("_TexTiling");
            bool tiled = tiling.x > 1.01f || tiling.y > 1.01f;
            if (!tiled || tex.wrapMode == TextureWrapMode.Repeat) return;

            EditorGUILayout.HelpBox(
                $"텍스처 Wrap Mode 가 {tex.wrapMode} 입니다. 타일링({tiling.x:F1}×{tiling.y:F1})이 반복되지 않고 가장자리가 늘어납니다.\n" +
                "(Sprite 로 임포트한 텍스처는 기본이 Clamp 입니다)",
                MessageType.Warning);

            if (!GUILayout.Button("텍스처 Wrap Mode → Repeat 로 변경")) return;

            string path = AssetDatabase.GetAssetPath(tex);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }

        #endregion
    }
}
#endif
