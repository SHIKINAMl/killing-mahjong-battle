using System;
using System.Collections.Generic;
using KillingMahjong.UI;
using UnityEditor;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    public partial class ReactionEditorWindow
    {
        // ---- 対局中タブ ----

        private void DrawMatchTab()
        {
            foreach (var g in CsvGroups)
            {
                EditorGUILayout.LabelField(g.title, EditorStyles.boldLabel);
                if (!string.IsNullOrEmpty(g.note)) EditorGUILayout.LabelField(P(g.note), WrapMini);
                foreach (var spec in g.specs) DrawCsvSpec(spec);
                EditorGUILayout.Space();
            }

            DrawUnlistedRows();
        }

        /// <summary>
        /// 目次に載っていない状況名を最後にまとめて出す。
        /// **CSV に足したのに目次を更新し忘れた行を隠さないため。** 見えないと二重に書き足す事故になる。
        /// </summary>
        private void DrawUnlistedRows()
        {
            var known = new HashSet<string>();
            foreach (var g in CsvGroups) foreach (var s in g.specs) known.Add(s.condition);

            var rest = new List<Row>();
            foreach (var r in AllRows())
            {
                if (known.Contains(r.condition)) continue;
                if (r.condition.StartsWith("クリックされた時")) continue;   // クリックタブの担当
                rest.Add(r);
            }
            if (rest.Count == 0) return;

            rest.Sort((a, b) => a.lineIndex.CompareTo(b.lineIndex));

            EditorGUILayout.LabelField("目次に載っていない行", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "CSV にあるけれど、このウィンドウの目次に説明が書かれていない状況名です。"
                + "いつ出るのか（そもそも出るのか）はコードを読んで確かめてください。", WrapMini);

            foreach (var r in rest)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(r.condition, EditorStyles.miniBoldLabel);
                DrawRowFields(r);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawCsvSpec(CsvSpec spec)
        {
            var row = FindRow(spec.condition);
            bool dead = !string.IsNullOrEmpty(spec.dead);
            string key = "csv:" + spec.condition;
            bool collapsed = _collapsed.Contains(key);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            string head = dead
                ? spec.condition + "   ×  出ません"
                : spec.condition + (row == null ? "   （行がありません）" : "");

            bool open = EditorGUILayout.Foldout(!collapsed, head, true, dead ? RedFoldout : EditorStyles.foldoutHeader);
            if (open == collapsed) { if (open) _collapsed.Remove(key); else _collapsed.Add(key); }

            // 畳んでいても「出ません」だけは見せる。閉じた枠に書き足す事故を防ぐ
            if (dead && !open) DrawDeadBanner(spec.dead);

            if (!open) { EditorGUILayout.EndVertical(); return; }

            if (dead) DrawDeadBanner(spec.dead);
            EditorGUILayout.LabelField("いつ出るか: " + P(spec.when), WrapMini);

            if (row == null)
            {
                EditorGUILayout.LabelField("（この状況の行が CSV にありません。無言になります）", WrapMini);
                if (GUILayout.Button("この行を作る", GUILayout.Width(120f))) CreateRow(spec.condition);
                EditorGUILayout.EndVertical();
                return;
            }

            DrawRowFields(row);
            EditorGUILayout.EndVertical();
        }

        private void DrawRowFields(Row row)
        {
            EditorGUILayout.BeginHorizontal();
            row.pose = DrawIdPopup("体", row.pose, _bodyIds, 40f, 90f);
            row.expression = DrawIdPopup("表情", row.expression, _faceIds, 40f, 110f);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            DrawTextField("セリフ", ref row.dialogue1);
            DrawTextField("セリフ2", ref row.dialogue2);

            // セリフ2 だけ書かれていても DialogueManager は拾うが、
            // 1 が空だと最初の吹き出しが出ないまま2つ目が流れる。分かりにくいので注意を出す
            if (string.IsNullOrEmpty(row.dialogue1) && !string.IsNullOrEmpty(row.dialogue2))
            {
                EditorGUILayout.HelpBox("セリフ が空でセリフ2 だけ入っています。セリフ2 が単独で流れます。", MessageType.Warning);
            }

            WarnAboutText(row.dialogue1);
            WarnAboutText(row.dialogue2);
            WarnAboutId(row.pose, _bodyIds, "体");
            WarnAboutId(row.expression, _faceIds, "表情");
        }

        private void DrawTextField(string label, ref string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(48f));
            string next = EditorGUILayout.TextArea(value ?? "", GUILayout.MinHeight(18f));
            EditorGUILayout.EndHorizontal();
            if (next != (value ?? "")) { value = next; _csvDirty = true; }
        }

        private string DrawIdPopup(string label, string value, string[] ids, float labelW, float popupW)
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(labelW));

            int index = Array.IndexOf(ids, value ?? "");
            if (index < 0)
            {
                // アセットに無いIDが CSV に書かれている。勝手に消さず、そのまま選択肢に足して見せる
                var grown = new string[ids.Length + 1];
                Array.Copy(ids, grown, ids.Length);
                grown[ids.Length] = value;
                ids = grown;
                index = ids.Length - 1;
            }

            var labels = new string[ids.Length];
            for (int i = 0; i < ids.Length; i++) labels[i] = string.IsNullOrEmpty(ids[i]) ? "（変えない）" : ids[i];

            int next = EditorGUILayout.Popup(index, labels, GUILayout.Width(popupW));
            if (next != index) { _csvDirty = true; return ids[next]; }
            return value;
        }

        private static void WarnAboutText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (text.Length > DialogueSoftLimit)
                EditorGUILayout.HelpBox($"{text.Length} 字あります。{DialogueSoftLimit} 字を超えると吹き出しから見切れます。", MessageType.Warning);
            if (text.IndexOf(',') >= 0 || text.IndexOf('\t') >= 0)
                EditorGUILayout.HelpBox(", や Tab は列の区切りとして読まれてしまいます。読点は 、 を使ってください。", MessageType.Error);
        }

        private void WarnAboutId(string id, string[] known, string what)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (Array.IndexOf(known, id) >= 0) return;
            EditorGUILayout.HelpBox(
                $"{what}「{id}」は {(_character != null ? _character.name : "CharacterData")} に登録されていません。"
                + "絵が見つからないので、表情は変わらずキャラが跳ねるだけになります。", MessageType.Error);
        }
    }
}
