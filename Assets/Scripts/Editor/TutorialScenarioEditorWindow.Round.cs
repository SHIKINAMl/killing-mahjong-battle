using System.Collections.Generic;
using KillingMahjong.Managers;
using UnityEditor;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    public partial class TutorialScenarioEditorWindow
    {
        private void DrawRoundList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(210f));
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            EditorGUILayout.LabelField("局", EditorStyles.boldLabel);

            for (int i = 0; i < _asset.rounds.Count; i++)
            {
                var r = _asset.rounds[i];
                string label = r == null ? "(空)" : string.IsNullOrEmpty(r.label) ? "第" + (i + 1) + "局" : r.label;
                int count = r == null ? 0 : CountLines(r);

                bool on = _roundIndex == i;
                if (GUILayout.Toggle(on, label + "  (" + count + ")", "Button") != on)
                {
                    _roundIndex = i;
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.Space();

            bool endOn = _roundIndex == -1;
            int endCount = _asset.endingLines == null ? 0 : _asset.endingLines.Count;
            if (GUILayout.Toggle(endOn, "全局終了後  (" + endCount + ")", "Button") != endOn)
            {
                _roundIndex = -1;
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private static int CountLines(TutorialRoundData r)
        {
            int n = 0;
            foreach (var s in Slots)
            {
                var list = s.get(r);
                if (list != null) n += list.Count;
            }
            if (r.abilityShowcases != null)
            {
                foreach (var sc in r.abilityShowcases)
                {
                    if (sc == null) continue;
                    if (sc.beforeLines != null) n += sc.beforeLines.Count;
                    if (sc.afterLines != null) n += sc.afterLines.Count;
                }
            }
            return n;
        }

        private void DrawRoundBody()
        {
            EditorGUILayout.BeginVertical();
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            if (_roundIndex == -1)
            {
                EditorGUILayout.LabelField("全局終了後", EditorStyles.boldLabel);
                if (_asset.endingLines == null) _asset.endingLines = new List<TutorialLine>();
                DrawLineList("ending", "エンディング", "すべての局が終わったあと。このあとタイトルへ戻る",
                    _asset.endingLines, v => _asset.endingLines = v);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            if (_roundIndex < 0 || _roundIndex >= _asset.rounds.Count)
            {
                EditorGUILayout.HelpBox("左から局を選んでください。", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            var round = _asset.rounds[_roundIndex];
            if (round == null)
            {
                EditorGUILayout.HelpBox("この局のデータが空です。", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField(round.label, EditorStyles.boldLabel);
            DrawRoundConditions(round);
            EditorGUILayout.Space();

            for (int i = 0; i < Slots.Length; i++)
            {
                var slot = Slots[i];
                var list = slot.get(round);
                if (list == null) { list = new List<TutorialLine>(); slot.set(round, list); }

                // 「導入」だけは盤面を出す位置が行番号で決まっている。
                // 見えないままだと行を足しただけで演出が変わるので、ここで一緒に見せる
                DrawLineList("r" + _roundIndex + "." + slot.fieldName, slot.title, slot.help,
                    list, v => slot.set(round, v),
                    slot.fieldName == "introLines" ? round : null,
                    GetSkipReason(slot.fieldName, round, _asset, _roundIndex),
                    HasDefaultFallback(slot.fieldName));

                // 能力の実演は「前ふり」のすぐあとに挟まる。順番どおりの位置で出す
                if (slot.fieldName == "abilityIntroLines") DrawShowcases(round);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// この局でどの枠が実際に流れるかを決めているフラグ。
        /// 読み取り専用で見せる。ここを変えると台本の構造が変わるので、第2段階の担当。
        /// </summary>
        private void DrawRoundConditions(TutorialRoundData round)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("結末: " + round.outcome, GUILayout.Width(150f));
                EditorGUILayout.LabelField("能力を使う: " + (round.enemyUsesAbility ? "はい" : "いいえ"), GUILayout.Width(130f));
                EditorGUILayout.LabelField("手動で組ませる: " + (round.allowManualHandSelection ? "はい" : "いいえ"), GUILayout.Width(150f));
                EditorGUILayout.LabelField("役一覧へ誘導: " + (round.guideToYakuList ? "はい" : "いいえ"));
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawShowcases(TutorialRoundData round)
        {
            if (round.abilityShowcases == null || round.abilityShowcases.Count == 0) return;

            for (int s = 0; s < round.abilityShowcases.Count; s++)
            {
                var sc = round.abilityShowcases[s];
                if (sc == null) continue;

                string name = string.IsNullOrEmpty(sc.skillType) ? "能力" + (s + 1) : sc.skillType;
                string key = "r" + _roundIndex + ".abilityShowcases[" + s + "]";

                if (sc.beforeLines == null) sc.beforeLines = new List<TutorialLine>();
                if (sc.afterLines == null) sc.afterLines = new List<TutorialLine>();

                // 実演は能力の局でしか回らない（RunRound の enemyUsesAbility の中）
                string skip = round.enemyUsesAbility
                    ? null
                    : "この局は能力を使わない局です（能力を使う: いいえ）。";

                var scLocal = sc;
                DrawLineList(key + ".beforeLines", "　└ 実演" + (s + 1) + "「" + name + "」の前",
                    "この能力を実際に見せる直前", sc.beforeLines, v => scLocal.beforeLines = v, null, skip);
                DrawLineList(key + ".afterLines", "　└ 実演" + (s + 1) + "「" + name + "」のあと",
                    "この能力を見せ終わった直後", sc.afterLines, v => scLocal.afterLines = v, null, skip);
            }
        }
    }
}
