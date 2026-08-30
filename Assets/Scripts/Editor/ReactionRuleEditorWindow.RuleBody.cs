using System;
using System.Collections.Generic;
using KillingMahjong.Managers;
using KillingMahjong.Managers.Reactions;
using UnityEditor;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    public partial class ReactionRuleEditorWindow
    {
        private void DrawBody()
        {
            EditorGUILayout.BeginVertical();
            _bodyScroll = EditorGUILayout.BeginScrollView(_bodyScroll);

            if (_selected < 0 || _selected >= _asset.rules.Count)
            {
                EditorGUILayout.HelpBox("左からルールを選ぶか、＋で新しく作ってください。", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            var rule = _asset.rules[_selected];

            // --- 見出し ---
            EditorGUILayout.BeginHorizontal();
            string newLabel = EditorGUILayout.TextField("名前", rule.label);
            if (newLabel != rule.label) { Record("名前を変更"); rule.label = newLabel; }

            bool newEnabled = EditorGUILayout.ToggleLeft("有効", rule.enabled, GUILayout.Width(60f));
            if (newEnabled != rule.enabled) { Record("有効を切替"); rule.enabled = newEnabled; }

            if (GUILayout.Button("複製", GUILayout.Width(50f))) Duplicate(rule);
            if (GUILayout.Button("削除", GUILayout.Width(50f))) { DeleteRule(); EditorGUILayout.EndHorizontal(); EditorGUILayout.EndScrollView(); EditorGUILayout.EndVertical(); return; }
            EditorGUILayout.EndHorizontal();

            if (ReactionRuleEngine.IsUnreachable(_asset, _selected))
            {
                DrawDeadBanner(
                    "同じきっかけで、条件が1つも無いルールが上にあります。そちらが必ず先に当たるので、"
                    + "このルールは絶対に出ません。上のルールに条件を足すか、このルールを上へ move してください。");
            }

            EditorGUILayout.Space();

            // --- きっかけ ---
            EditorGUILayout.LabelField("いつ", EditorStyles.boldLabel);
            var events = (ReactionEvent[])Enum.GetValues(typeof(ReactionEvent));
            var eventLabels = new string[events.Length];
            for (int i = 0; i < events.Length; i++) eventLabels[i] = ReactionVariableCatalog.EventLabel(events[i]);

            int evIndex = Array.IndexOf(events, rule.trigger);
            int nextEv = EditorGUILayout.Popup("きっかけ", Mathf.Max(0, evIndex), eventLabels);
            if (nextEv != evIndex)
            {
                Record("きっかけを変更");
                rule.trigger = events[nextEv];
                // 変数はイベントごとに違う。残すと存在しない条件になるので消す
                if (rule.conditions != null) rule.conditions.Clear();
            }

            EditorGUILayout.Space();

            // --- 条件 ---
            DrawConditions(rule);

            EditorGUILayout.Space();

            // --- 出し方 ---
            EditorGUILayout.LabelField("出し方", EditorStyles.boldLabel);

            var nextPriority = (ReactionPriority)EditorGUILayout.EnumPopup("優先度", rule.priority);
            if (nextPriority != rule.priority) { Record("優先度を変更"); rule.priority = nextPriority; }
            EditorGUILayout.LabelField(PriorityHelp(rule.priority), WrapMini);

            float nextCd = EditorGUILayout.FloatField("再び出せるまでの秒数", rule.cooldownSeconds);
            if (!Mathf.Approximately(nextCd, rule.cooldownSeconds)) { Record("クールダウンを変更"); rule.cooldownSeconds = Mathf.Max(0f, nextCd); }

            var nextLimit = (FireLimit)EditorGUILayout.EnumPopup("出せる回数", rule.limit);
            if (nextLimit != rule.limit) { Record("回数制限を変更"); rule.limit = nextLimit; }

            EditorGUILayout.Space();

            // --- セリフ ---
            DrawLines(rule);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private static string PriorityHelp(ReactionPriority p)
        {
            switch (p)
            {
                case ReactionPriority.Progress:
                    return "必ず出す。ロンや局の開始など、飛ばすと話が繋がらないもの向け。";
                case ReactionPriority.Situation:
                    return "出したいが、同じものが既に並んでいれば捨てる。打牌や賭けの反応向け。";
                default:
                    return "出なくてよい。ほかの演出が動いている間は無条件で捨てられ、"
                         + "全体で6秒のあいだを空ける。連打や放置への反応向け。";
            }
        }

        private void Duplicate(ReactionRule src)
        {
            Record("ルールを複製");
            var copy = new ReactionRule
            {
                label = src.label + " のコピー",
                enabled = src.enabled,
                trigger = src.trigger,
                priority = src.priority,
                cooldownSeconds = src.cooldownSeconds,
                limit = src.limit,
                conditions = new List<ReactionCondition>(),
                lines = new List<ReactionRuleLine>(),
            };
            foreach (var c in src.conditions)
                copy.conditions.Add(new ReactionCondition { key = c.key, op = c.op, number = c.number, text = c.text });
            foreach (var l in src.lines)
                copy.lines.Add(new ReactionRuleLine { text = l.text, faceId = l.faceId, manfu = l.manfu });

            _asset.rules.Insert(_selected + 1, copy);
            _selected++;
        }

        private void DeleteRule()
        {
            if (!EditorUtility.DisplayDialog("確認", "このルールを削除しますか？", "削除", "やめる")) return;
            Record("ルールを削除");
            _asset.rules.RemoveAt(_selected);
            _selected = Mathf.Min(_selected, _asset.rules.Count - 1);
        }
    }
}
