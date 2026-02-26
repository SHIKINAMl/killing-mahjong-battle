using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace KillingMahjong.Editor
{
    public class UIStyleApplier : EditorWindow
    {
        private Color panelColor = new Color(0.1f, 0.4f, 0.8f, 0.8f); // デフォルトの青色（少し透過）
        private Color outlineColor = new Color(0f, 0f, 0f, 1f);       // 黒の境界線
        private Vector2 outlineThickness = new Vector2(3f, -3f);

        [MenuItem("Tools/UI/Apply Panel Style")]
        public static void ShowWindow()
        {
            GetWindow<UIStyleApplier>("UI Style Applier");
        }

        private void OnGUI()
        {
            GUILayout.Label("選択したパネルの見た目を一括変更", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            panelColor = EditorGUILayout.ColorField("背景色 (Panel Color)", panelColor);
            outlineColor = EditorGUILayout.ColorField("枠線の色 (Outline Color)", outlineColor);
            outlineThickness = EditorGUILayout.Vector2Field("枠線の太さ (Thickness)", outlineThickness);

            EditorGUILayout.Space();

            if (GUILayout.Button("選択中のUIオブジェクトに適用", GUILayout.Height(30)))
            {
                ApplyStyleToSelected();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("ヒント: ヒエラルキー（Hierarchy）で変更したいPanelを複数選択してからボタンを押してください。", MessageType.Info);
        }

        private void ApplyStyleToSelected()
        {
            if (Selection.gameObjects.Length == 0)
            {
                Debug.LogWarning("対象のオブジェクトが選択されていません。ヒエラルキーでPanelを選択してください。");
                return;
            }

            int count = 0;
            foreach (GameObject obj in Selection.gameObjects)
            {
                Image img = obj.GetComponent<Image>();
                if (img != null)
                {
                    // 背景色の適用
                    Undo.RecordObject(img, "Apply Panel Color");
                    img.color = panelColor;

                    // Outlineの追加・設定
                    Outline outline = obj.GetComponent<Outline>();
                    if (outline == null)
                    {
                        outline = Undo.AddComponent<Outline>(obj);
                    }
                    else
                    {
                        Undo.RecordObject(outline, "Apply Panel Outline");
                    }
                    
                    outline.effectColor = outlineColor;
                    outline.effectDistance = outlineThickness;
                    // Outlineを少しだけ鮮明にするためにUse Graphic Alphaをオンにするのが一般的です
                    outline.useGraphicAlpha = true; 
                    
                    EditorUtility.SetDirty(obj);
                    count++;
                }
            }

            Debug.Log($"{count} 個のUIオブジェクトにスタイルを適用しました！");
        }
    }
}
