using UnityEditor;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    public partial class ReactionRuleEditorWindow
    {
        private static Color DeadColor
        {
            get
            {
                return EditorGUIUtility.isProSkin
                    ? new Color(1f, 0.45f, 0.40f)
                    : new Color(0.72f, 0.11f, 0.08f);
            }
        }

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

        private static void DrawDeadBanner(string reason)
        {
            var prev = GUI.color;
            GUI.color = DeadColor;
            EditorGUILayout.LabelField("× このルールは出ません。", WrapMiniBold);
            GUI.color = prev;
            EditorGUILayout.LabelField("理由: " + reason, WrapMini);
        }
    }
}
