using UnityEditor;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    public partial class TutorialScenarioEditorWindow
    {
        /// <summary>Unity のスキンによって読める赤が違うので、明暗で切り替える。</summary>
        private static Color SkipColor
        {
            get
            {
                return EditorGUIUtility.isProSkin
                    ? new Color(1f, 0.45f, 0.40f)    // 暗いスキン: 明るめの赤
                    : new Color(0.72f, 0.11f, 0.08f); // 明るいスキン: 濃い赤
            }
        }

        // 折り返す小さいラベル。EditorStyles のものは wordWrap が false で理由が切れる
        private static GUIStyle _wrapMini, _wrapMiniBold;
        private static GUIStyle WrapMini
        {
            get
            {
                if (_wrapMini == null) _wrapMini = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                return _wrapMini;
            }
        }
        private static GUIStyle WrapMiniBold
        {
            get
            {
                if (_wrapMiniBold == null) _wrapMiniBold = new GUIStyle(EditorStyles.miniBoldLabel) { wordWrap = true };
                return _wrapMiniBold;
            }
        }

        /// <summary>「この局では流れません」の赤い帯。理由まで書かないと直しようがない。</summary>
        private static void DrawSkipBanner(string reason)
        {
            var prev = GUI.color;
            GUI.color = SkipColor;
            EditorGUILayout.LabelField(
                "× この局では流れません。ここに書き足しても画面には出ません。", WrapMiniBold);
            GUI.color = prev;
            EditorGUILayout.LabelField("理由: " + reason, WrapMini);
        }

        private static void DrawRevealMarker()
        {
            var prev = GUI.color;
            GUI.color = new Color(0.55f, 0.85f, 1f);
            EditorGUILayout.LabelField("──── ここで盤面（山牌・手牌・ドラ・HP）が出る ────", EditorStyles.miniBoldLabel);
            GUI.color = prev;
        }

        /// <summary>
        /// Undo に積みつつ dirty を立てる。
        /// **ScriptableObject の中の List を書き換えるだけでは Unity は保存すべきだと気づかない。**
        /// 触る直前に必ずこれを通す。
        /// </summary>
        private void Record(string label)
        {
            if (_asset == null) return;
            Undo.RecordObject(_asset, label);
            EditorUtility.SetDirty(_asset);
        }
    }
}
