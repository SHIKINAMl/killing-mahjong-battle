using UnityEditor;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    public partial class ReactionEditorWindow
    {
        // ------------------------------------------------------------------
        // 見た目の小物
        // ------------------------------------------------------------------

        /// <summary>
        /// 説明文から Markdown の飾りを落とす。
        /// **IMGUI は ** も ` も解釈しない**ので、そのまま渡すと記号が画面に出てしまう。
        /// 目次の文字列は他の資料と読み比べるためにソース上は Markdown のまま置いておき、
        /// 画面に出す直前でここを通す。
        /// </summary>
        private static string P(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("**", "").Replace("`", "");
        }

        /// <summary>Unity のスキンによって読める赤が違うので、明暗で切り替える。</summary>
        private static Color DeadColor
        {
            get
            {
                return EditorGUIUtility.isProSkin
                    ? new Color(1f, 0.45f, 0.40f)
                    : new Color(0.72f, 0.11f, 0.08f);
            }
        }

        private static GUIStyle _wrapMini, _wrapMiniBold, _redMini, _redFoldout;

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

        private static GUIStyle RedMini
        {
            get
            {
                // 色はスキンで変わるので毎回入れ直す。生成し直すと GC が増えるため style だけ使い回す
                if (_redMini == null) _redMini = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                var c = DeadColor;
                _redMini.normal.textColor = c;
                return _redMini;
            }
        }

        private static GUIStyle RedFoldout
        {
            get
            {
                if (_redFoldout == null) _redFoldout = new GUIStyle(EditorStyles.foldoutHeader);
                var c = DeadColor;
                _redFoldout.normal.textColor = c;
                _redFoldout.onNormal.textColor = c;
                _redFoldout.focused.textColor = c;
                _redFoldout.onFocused.textColor = c;
                _redFoldout.hover.textColor = c;
                _redFoldout.onHover.textColor = c;
                _redFoldout.active.textColor = c;
                _redFoldout.onActive.textColor = c;
                return _redFoldout;
            }
        }

        /// <summary>「出ません」の赤い帯。理由まで書かないと直しようがない。</summary>
        private static void DrawDeadBanner(string reason)
        {
            var prev = GUI.color;
            GUI.color = DeadColor;
            EditorGUILayout.LabelField("× ここに書いても画面には出ません。", WrapMiniBold);
            GUI.color = prev;
            EditorGUILayout.LabelField("理由: " + P(reason), WrapMini);
        }
    }
}
