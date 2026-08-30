using System;
using System.Collections.Generic;
using KillingMahjong.Managers.Reactions;
using KillingMahjong.UI;
using UnityEditor;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    public partial class ReactionRuleEditorWindow
    {
        private void DrawConditions(ReactionRule rule)
        {
            if (rule.conditions == null) rule.conditions = new List<ReactionCondition>();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("どんなとき", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("＋条件を足す", GUILayout.Width(100f)))
            {
                Record("条件を追加");
                rule.conditions.Add(new ReactionCondition());
            }
            EditorGUILayout.EndHorizontal();

            if (rule.conditions.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "条件がありません。このきっかけが起きたら毎回このルールが当たります。\n"
                    + "同じきっかけで下にあるルールは出られなくなるので、いちばん下に置いてください。",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("すべて満たしたときだけ出ます。", WrapMini);

            var vars = ReactionVariableCatalog.For(rule.trigger);
            var varLabels = new string[vars.Length];
            for (int i = 0; i < vars.Length; i++) varLabels[i] = vars[i].label;

            int remove = -1;
            for (int i = 0; i < rule.conditions.Count; i++)
            {
                var c = rule.conditions[i];
                if (c == null) { rule.conditions[i] = c = new ReactionCondition(); }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                int varIndex = IndexOfVar(vars, c.key);
                int nextVar = EditorGUILayout.Popup(varIndex, varLabels, GUILayout.Width(200f));
                if (nextVar != varIndex && nextVar >= 0)
                {
                    Record("条件の項目を変更");
                    c.key = vars[nextVar].key;
                    c.op = CompareOp.Equal;
                    c.number = 0f;
                    c.text = "";
                    varIndex = nextVar;
                }

                var info = varIndex >= 0 ? vars[varIndex] : null;
                if (info == null)
                {
                    EditorGUILayout.LabelField("← 項目を選んでください", WrapMini);
                }
                else
                {
                    DrawConditionValue(c, info);
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("−", GUILayout.Width(24f))) remove = i;
                EditorGUILayout.EndHorizontal();

                if (info != null && !string.IsNullOrEmpty(info.help))
                    EditorGUILayout.LabelField(info.help, WrapMini);

                EditorGUILayout.EndVertical();
            }

            if (remove >= 0)
            {
                Record("条件を削除");
                rule.conditions.RemoveAt(remove);
            }
        }

        private static int IndexOfVar(ReactionVarInfo[] vars, string key)
        {
            for (int i = 0; i < vars.Length; i++) if (vars[i].key == key) return i;
            return -1;
        }

        private void DrawConditionValue(ReactionCondition c, ReactionVarInfo info)
        {
            switch (info.kind)
            {
                case VarKind.Bool:
                {
                    // 「＝ はい / ＝ いいえ」の2択に畳む。比較記号を選ばせても意味がない
                    bool current = c.op == CompareOp.NotEqual ? c.number < 0.5f : c.number >= 0.5f;
                    int idx = current ? 0 : 1;
                    int next = EditorGUILayout.Popup(idx, new[] { "はい", "いいえ" }, GUILayout.Width(90f));
                    if (next != idx)
                    {
                        Record("条件の値を変更");
                        c.op = CompareOp.Equal;
                        c.number = next == 0 ? 1f : 0f;
                    }
                    break;
                }

                case VarKind.Text:
                {
                    var nextOp = DrawOp(c, new[] { CompareOp.Equal, CompareOp.NotEqual });
                    if (nextOp != c.op) { Record("条件の比較を変更"); c.op = nextOp; }

                    if (info.choices != null && info.choices.Length > 0)
                    {
                        var labels = new string[info.choices.Length];
                        for (int i = 0; i < labels.Length; i++)
                            labels[i] = string.IsNullOrEmpty(info.choices[i]) ? "（なし）" : info.choices[i];

                        int idx = Array.IndexOf(info.choices, c.text ?? "");
                        int next = EditorGUILayout.Popup(Mathf.Max(0, idx), labels, GUILayout.Width(140f));
                        if (next != idx) { Record("条件の値を変更"); c.text = info.choices[next]; }
                    }
                    else
                    {
                        string next = EditorGUILayout.TextField(c.text ?? "", GUILayout.Width(140f));
                        if (next != c.text) { Record("条件の値を変更"); c.text = next; }
                    }
                    break;
                }

                default:
                {
                    var nextOp = DrawOp(c, new[] {
                        CompareOp.Equal, CompareOp.NotEqual,
                        CompareOp.GreaterOrEqual, CompareOp.LessOrEqual,
                        CompareOp.Greater, CompareOp.Less });
                    if (nextOp != c.op) { Record("条件の比較を変更"); c.op = nextOp; }

                    float next = EditorGUILayout.FloatField(c.number, GUILayout.Width(80f));
                    if (!Mathf.Approximately(next, c.number)) { Record("条件の値を変更"); c.number = next; }
                    break;
                }
            }
        }

        private static CompareOp DrawOp(ReactionCondition c, CompareOp[] allowed)
        {
            var labels = new string[allowed.Length];
            for (int i = 0; i < allowed.Length; i++) labels[i] = ReactionVariableCatalog.OpLabel(allowed[i]);

            int idx = Array.IndexOf(allowed, c.op);
            int next = EditorGUILayout.Popup(Mathf.Max(0, idx), labels, GUILayout.Width(90f));
            return allowed[next];
        }

        private void DrawLines(ReactionRule rule)
        {
            if (rule.lines == null) rule.lines = new List<ReactionRuleLine>();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("セリフ  (" + rule.lines.Count + "本)", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("＋セリフを足す", GUILayout.Width(110f)))
            {
                Record("セリフを追加");
                rule.lines.Add(new ReactionRuleLine());
            }
            EditorGUILayout.EndHorizontal();

            // 件数ではなく「中身のある行」で見る。枠だけ足して空のままだと、
            // 実行時もこのルールは無かったことになる
            if (!ReactionRuleEngine.HasUsableLine(rule))
            {
                EditorGUILayout.HelpBox(
                    rule.lines.Count == 0
                        ? "セリフが1本もありません。この状態ではルールは無視されます（従来のセリフが出ます）。"
                        : "セリフの欄はありますが、すべて空です。中身が無い行は数に入らないので、"
                          + "このルールは無視されます（従来のセリフが出ます）。",
                    MessageType.Error);
                return;
            }
            if (rule.lines.Count == 1)
            {
                EditorGUILayout.LabelField("2本以上書くとランダムに選ばれます。1本だと毎回同じセリフです。", WrapMini);
            }

            int remove = -1;
            for (int i = 0; i < rule.lines.Count; i++)
            {
                var line = rule.lines[i];
                if (line == null) { rule.lines[i] = line = new ReactionRuleLine(); }

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label((i + 1).ToString(), EditorStyles.miniLabel, GUILayout.Width(20f));

                int faceIdx = Array.IndexOf(_faceIds, line.faceId ?? "");
                var faceLabels = new string[_faceIds.Length];
                for (int f = 0; f < _faceIds.Length; f++)
                    faceLabels[f] = string.IsNullOrEmpty(_faceIds[f]) ? "（変えない）" : _faceIds[f];

                int nextFace = EditorGUILayout.Popup(Mathf.Max(0, faceIdx), faceLabels, GUILayout.Width(110f));
                if (nextFace != faceIdx && nextFace >= 0) { Record("表情を変更"); line.faceId = _faceIds[nextFace]; }

                var nextManfu = (ManfuType)EditorGUILayout.EnumPopup(line.manfu, GUILayout.Width(90f));
                if (nextManfu != line.manfu) { Record("漫符を変更"); line.manfu = nextManfu; }

                string nextText = EditorGUILayout.TextArea(line.text ?? "", GUILayout.MinHeight(18f));
                if (nextText != (line.text ?? "")) { Record("セリフを編集"); line.text = nextText; }

                if (GUILayout.Button("−", GUILayout.Width(24f))) remove = i;
                EditorGUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(line.text) && line.text.Length > DialogueSoftLimit)
                {
                    EditorGUILayout.HelpBox(
                        line.text.Length + " 字あります。" + DialogueSoftLimit + " 字を超えると吹き出しから見切れます。",
                        MessageType.Warning);
                }
            }

            if (remove >= 0)
            {
                Record("セリフを削除");
                rule.lines.RemoveAt(remove);
            }

            EditorGUILayout.LabelField(
                "漫符はまだゲーム側が読んでいません（設定しても今は出ません）。", WrapMini);
        }
    }
}
