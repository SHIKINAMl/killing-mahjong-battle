using KillingMahjong.Managers.Reactions;
using UnityEditor;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    public partial class ReactionRuleEditorWindow
    {
        private void DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(280f));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ルール一覧", EditorStyles.boldLabel);
            if (GUILayout.Button("＋", GUILayout.Width(28f)))
            {
                Record("ルールを追加");
                _asset.rules.Add(new ReactionRule());
                _selected = _asset.rules.Count - 1;
            }
            EditorGUILayout.EndHorizontal();

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

            ReactionEvent? lastEvent = null;
            for (int i = 0; i < _asset.rules.Count; i++)
            {
                var r = _asset.rules[i];
                if (r == null) { _asset.rules[i] = r = new ReactionRule(); }

                // 同じきっかけごとに区切りを入れる。順番が優先順位なので、
                // どこからどこまでが同じ勝負なのかが見えないと並べ替えられない
                if (lastEvent == null || lastEvent.Value != r.trigger)
                {
                    EditorGUILayout.Space(2f);
                    EditorGUILayout.LabelField(ReactionVariableCatalog.EventLabel(r.trigger), EditorStyles.miniBoldLabel);
                    lastEvent = r.trigger;
                }

                EditorGUILayout.BeginHorizontal();

                string mark = !r.enabled ? "（停止中）"
                            : ReactionRuleEngine.IsUnreachable(_asset, i) ? "×  "
                            : "";
                string label = mark + (string.IsNullOrEmpty(r.label) ? "(名前なし)" : r.label)
                             + "  [" + CountLines(r) + "]";

                bool on = _selected == i;
                var style = new GUIStyle("Button") { alignment = TextAnchor.MiddleLeft };
                if (!r.enabled || ReactionRuleEngine.IsUnreachable(_asset, i))
                    style.normal.textColor = DeadColor;

                if (GUILayout.Toggle(on, label, style) != on) { _selected = i; GUI.FocusControl(null); }

                using (new EditorGUI.DisabledScope(i == 0))
                    if (GUILayout.Button("↑", GUILayout.Width(22f))) { Move(i, -1); break; }
                using (new EditorGUI.DisabledScope(i == _asset.rules.Count - 1))
                    if (GUILayout.Button("↓", GUILayout.Width(22f))) { Move(i, 1); break; }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private static int CountLines(ReactionRule r)
        {
            return r.lines == null ? 0 : r.lines.Count;
        }

        private void Move(int index, int delta)
        {
            int to = index + delta;
            if (to < 0 || to >= _asset.rules.Count) return;
            Record("ルールを並べ替え");
            var t = _asset.rules[index];
            _asset.rules[index] = _asset.rules[to];
            _asset.rules[to] = t;
            if (_selected == index) _selected = to;
            else if (_selected == to) _selected = index;
        }
    }
}
