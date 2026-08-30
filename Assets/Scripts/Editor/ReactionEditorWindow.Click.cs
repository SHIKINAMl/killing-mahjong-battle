using System;
using System.Collections.Generic;
using KillingMahjong.UI;
using UnityEditor;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    public partial class ReactionEditorWindow
    {
        // ---- クリックタブ ----

        private void DrawClickTab()
        {
            var sceneAreas = CollectSceneClickAreas();

            EditorGUILayout.HelpBox(
                "クリックの抽選は ClickableCharacter.OnClicked の Random.Range(1, 21)。\n"
                + "1〜20 の番号しか引かれません。21 番以降は何を書いても絶対に出ません。\n"
                + "部位をクリックすると、まず「クリックされた時_部位名＋番号」を探し、"
                + "無ければ同じ番号の「クリックされた時＋番号」に落ちます。",
                MessageType.Info);

            if (sceneAreas != null)
            {
                EditorGUILayout.LabelField(
                    sceneAreas.Count == 0
                        ? "いま開いているシーンに ClickableCharacter のクリック枠がありません。"
                        : "いま開いているシーンのクリック枠: " + string.Join(" / ", new List<string>(sceneAreas).ToArray()),
                    WrapMiniBold);
            }

            DrawClickGroup("", "全体（部位の枠に当たらなかったとき、および部位専用が無いとき）", sceneAreas);

            foreach (var area in CollectClickAreaNames(sceneAreas))
            {
                DrawClickGroup(area, "部位「" + area + "」", sceneAreas);
            }
        }

        /// <summary>
        /// いま開いているシーンの `ClickableCharacter` から部位名を集める。
        /// `clickAreas` は private なので `SerializedObject` 経由で読む。
        /// **シーンを開いていない／コンポーネントが無いときは null を返す**（「枠が0個」と区別する）。
        /// </summary>
        private static List<string> CollectSceneClickAreas()
        {
            var comps = UnityEngine.Object.FindObjectsByType<ClickableCharacter>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (comps == null || comps.Length == 0) return null;

            var names = new List<string>();
            foreach (var c in comps)
            {
                var so = new SerializedObject(c);
                var list = so.FindProperty("clickAreas");
                if (list == null || !list.isArray) continue;
                for (int i = 0; i < list.arraySize; i++)
                {
                    var nameProp = list.GetArrayElementAtIndex(i).FindPropertyRelative("areaName");
                    if (nameProp == null) continue;
                    string n = nameProp.stringValue;
                    // 同じ名前の枠が複数あっても、引くセリフは1種類なのでまとめて数える
                    if (!names.Contains(n)) names.Add(n);
                }
            }
            return names;
        }

        /// <summary>CSV に書かれている部位名と、シーンに置かれている部位名の和集合</summary>
        private List<string> CollectClickAreaNames(List<string> sceneAreas)
        {
            var areas = new List<string>();
            foreach (var r in AllRows())
            {
                if (!r.condition.StartsWith("クリックされた時_")) continue;
                string rest = r.condition.Substring("クリックされた時_".Length);
                int cut = rest.Length;
                while (cut > 0 && char.IsDigit(rest[cut - 1])) cut--;
                if (cut <= 0 || cut == rest.Length) continue;
                string area = rest.Substring(0, cut);
                if (!areas.Contains(area)) areas.Add(area);
            }
            if (sceneAreas != null)
                foreach (var a in sceneAreas)
                    if (!string.IsNullOrEmpty(a) && !areas.Contains(a)) areas.Add(a);
            areas.Sort(StringComparer.Ordinal);
            return areas;
        }

        private void DrawClickGroup(string area, string title, List<string> sceneAreas)
        {
            string prefix = string.IsNullOrEmpty(area) ? "クリックされた時" : "クリックされた時_" + area;

            // この部位で埋まっている番号と、抽選の外に出てしまっている番号を数える
            var present = new List<int>();
            int outOfRange = 0;
            for (int n = 1; n <= 200; n++)
            {
                var r = FindRow(prefix + n);
                if (r == null) continue;
                if (n <= ClickLotteryMax) present.Add(n); else outOfRange++;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            // 部位専用は、埋まっていない番号がそのまま全体側へ落ちる。
            // 「5本しか書いていないのに全然出ない」の正体がこれなので、割合を数字で出す
            if (!string.IsNullOrEmpty(area))
            {
                int hit = present.Count;
                EditorGUILayout.LabelField(
                    $"1〜{ClickLotteryMax} のうち {hit} 個が埋まっています → この部位を押したとき"
                    + $"{hit * 100 / ClickLotteryMax}% が専用のセリフ、"
                    + $"残り {(ClickLotteryMax - hit) * 100 / ClickLotteryMax}% は全体のセリフに落ちます。",
                    WrapMini);

                if (sceneAreas != null && !sceneAreas.Contains(area))
                {
                    DrawDeadBanner("いま開いているシーンの ClickableCharacter に「" + area + "」の枠がありません。"
                        + "枠が無いと OnClicked にこの部位名が渡らないので、ここのセリフは1度も出ません。");
                }
            }

            if (outOfRange > 0)
            {
                DrawDeadBanner($"{ClickLotteryMax + 1} 番以降が {outOfRange} 行あります。"
                    + $"抽選は Random.Range(1, {ClickLotteryMax + 1}) なので、この番号は永久に引かれません。"
                    + $"活かすなら {ClickLotteryMax} 番までの空き番号へ移すか、ClickableCharacter.OnClicked の範囲を広げてください。");
            }

            for (int n = 1; n <= ClickLotteryMax + outOfRange + 1; n++)
            {
                string cond = prefix + n;
                var row = FindRow(cond);
                bool beyond = n > ClickLotteryMax;

                if (row == null)
                {
                    // 空き番号は1つだけ「作る」ボタンを出す。全部並べても邪魔なので
                    if (n <= ClickLotteryMax || beyond)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(
                            beyond ? $"{n} 番（作っても抽選されません）" : $"{n} 番　（空き）",
                            beyond ? RedMini : WrapMini);
                        if (!beyond && GUILayout.Button("作る", GUILayout.Width(60f))) CreateRow(cond);
                        EditorGUILayout.EndHorizontal();
                        if (beyond) break;
                    }
                    continue;
                }

                string key = "click:" + cond;
                bool collapsed = _collapsed.Contains(key);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                string head = beyond
                    ? $"{n} 番   ×  抽選されません　{Excerpt(row.dialogue1)}"
                    : $"{n} 番　{Excerpt(row.dialogue1)}";

                bool open = EditorGUILayout.Foldout(!collapsed, head, true, beyond ? RedFoldout : EditorStyles.foldoutHeader);
                if (open == collapsed) { if (open) _collapsed.Remove(key); else _collapsed.Add(key); }

                if (open) DrawRowFields(row);
                EditorGUILayout.EndVertical();
            }
        }

        private static string Excerpt(string s)
        {
            if (string.IsNullOrEmpty(s)) return "（空）";
            return s.Length <= 24 ? s : s.Substring(0, 24) + "…";
        }
    }
}
