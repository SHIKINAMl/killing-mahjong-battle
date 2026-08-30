using System;
using System.Collections.Generic;
using KillingMahjong.Managers;
using UnityEditor;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    public partial class TutorialScenarioEditorWindow
    {
        /// <summary>
        /// セリフ1枠ぶんの描画。追加・削除・並べ替えまでここで行う。
        ///
        /// <paramref name="revealOwner"/> を渡すと「導入」用の追加表示が出る。
        /// `revealBoardAfterLineIndex` が行番号を指しているため、
        /// **行を足し引きすると盤面が出るタイミングが黙って動く。** それを画面に出すため。
        /// </summary>
        private void DrawLineList(string key, string title, string help,
                                  List<TutorialLine> lines, Action<List<TutorialLine>> writeBack,
                                  TutorialRoundData revealOwner = null,
                                  string skipReason = null, bool hasDefaultFallback = false)
        {
            bool collapsed = _collapsed.Contains(key);
            bool skipped = !string.IsNullOrEmpty(skipReason);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            var headStyle = EditorStyles.foldoutHeader;
            if (skipped)
            {
                headStyle = new GUIStyle(EditorStyles.foldoutHeader);
                var red = SkipColor;
                headStyle.normal.textColor = red;
                headStyle.onNormal.textColor = red;
                headStyle.focused.textColor = red;
                headStyle.onFocused.textColor = red;
                headStyle.hover.textColor = red;
                headStyle.onHover.textColor = red;
                headStyle.active.textColor = red;
                headStyle.onActive.textColor = red;
            }

            string headText = skipped
                ? title + "  (" + lines.Count + "行)   ×  この局では流れません"
                : title + "  (" + lines.Count + "行)";

            bool open = EditorGUILayout.Foldout(!collapsed, headText, true, headStyle);
            if (open == collapsed)
            {
                if (open) _collapsed.Remove(key); else _collapsed.Add(key);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("＋行を足す", GUILayout.Width(90f)))
            {
                Record("セリフを追加");
                lines.Add(new TutorialLine("", TutorialSpeaker.Enemy));
                writeBack(lines);
                _collapsed.Remove(key);
            }
            EditorGUILayout.EndHorizontal();

            // 畳んでいても「流れません」だけは見せる。閉じた枠に書き足す事故を防ぐ
            if (skipped && !open) DrawSkipBanner(skipReason);

            if (!open) { EditorGUILayout.EndVertical(); return; }

            if (skipped) DrawSkipBanner(skipReason);

            if (!string.IsNullOrEmpty(help))
            {
                EditorGUILayout.LabelField(help, EditorStyles.miniLabel);
            }

            if (hasDefaultFallback && lines.Count == 0 && !skipped)
            {
                EditorGUILayout.HelpBox(
                    "この枠は空にすると、コードに書かれた既定のセリフが流れます（無言にはなりません）。",
                    MessageType.Info);
            }

            // 盤面を出す位置。実際に効く条件は RunRound と同じ `0 <= rev < 行数-1`
            int effectiveReveal = -1;
            if (revealOwner != null)
            {
                effectiveReveal = DrawRevealControl(revealOwner, lines.Count);
            }

            if (lines.Count == 0)
            {
                EditorGUILayout.LabelField("（この場面では何も喋りません）", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            int moveUp = -1, moveDown = -1, remove = -1;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null) { lines[i] = line = new TutorialLine("", TutorialSpeaker.Enemy); }

                EditorGUILayout.BeginHorizontal();

                GUILayout.Label((i + 1).ToString(), EditorStyles.miniLabel, GUILayout.Width(22f));

                var newSpeaker = (TutorialSpeaker)EditorGUILayout.EnumPopup(line.speaker, GUILayout.Width(80f));
                if (newSpeaker != line.speaker)
                {
                    Record("話者を変更");
                    line.speaker = newSpeaker;
                }

                var newText = EditorGUILayout.TextArea(line.text ?? "", GUILayout.MinHeight(18f));
                if (newText != line.text)
                {
                    Record("セリフを編集");
                    line.text = newText;
                }

                using (new EditorGUI.DisabledScope(i == 0))
                    if (GUILayout.Button("↑", GUILayout.Width(24f))) moveUp = i;
                using (new EditorGUI.DisabledScope(i == lines.Count - 1))
                    if (GUILayout.Button("↓", GUILayout.Width(24f))) moveDown = i;
                if (GUILayout.Button("−", GUILayout.Width(24f))) remove = i;

                EditorGUILayout.EndHorizontal();

                if (revealOwner != null && i == effectiveReveal) DrawRevealMarker();
            }

            if (revealOwner != null && effectiveReveal < 0) DrawRevealMarker();

            // ループ中にリストをいじると添字が壊れるので、確定してから1つだけ実行する
            if (moveUp > 0)
            {
                Record("セリフを並べ替え");
                var t = lines[moveUp]; lines[moveUp] = lines[moveUp - 1]; lines[moveUp - 1] = t;
            }
            else if (moveDown >= 0 && moveDown < lines.Count - 1)
            {
                Record("セリフを並べ替え");
                var t = lines[moveDown]; lines[moveDown] = lines[moveDown + 1]; lines[moveDown + 1] = t;
            }
            else if (remove >= 0)
            {
                Record("セリフを削除");
                lines.RemoveAt(remove);
            }

            writeBack(lines);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 「盤面をどこで出すか」を選ばせる。戻り値は**実際に効く**行番号（効かないときは -1）。
        ///
        /// `RunRound` の条件は `rev >= 0 && rev < introLines.Count - 1`。
        /// **範囲外の値は黙って「全部流してから」に化ける。** しかも行を1つ足すと
        /// 範囲内に戻って演出が変わる。第3局・第4局が実際にその状態だったので、
        /// 保存されている生の値と、いま効いている結果を**両方**出す。
        /// </summary>
        private int DrawRevealControl(TutorialRoundData round, int lineCount)
        {
            int raw = round.revealBoardAfterLineIndex;
            bool active = raw >= 0 && raw < lineCount - 1;

            // 選択肢: 「全部流してから」＋「N行目のあと」（有効な範囲だけ）
            int usable = Mathf.Max(0, lineCount - 1);
            var labels = new string[usable + 1];
            labels[0] = "導入を全部流し終えてから";
            for (int i = 0; i < usable; i++) labels[i + 1] = (i + 1) + "行目のあと";

            int selected = active ? raw + 1 : 0;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("盤面を出すタイミング", GUILayout.Width(130f));
            int next = EditorGUILayout.Popup(selected, labels);
            EditorGUILayout.EndHorizontal();

            if (next != selected)
            {
                Record("盤面を出すタイミングを変更");
                round.revealBoardAfterLineIndex = next == 0 ? -1 : next - 1;
                raw = round.revealBoardAfterLineIndex;
                active = raw >= 0 && raw < lineCount - 1;
            }

            // 保存値が範囲外のまま残っている場合の注意。行を足すと挙動が変わる
            if (!active && raw >= 0)
            {
                EditorGUILayout.HelpBox(
                    "保存されている値は " + raw + " ですが、導入が " + lineCount + "行しかないので効いていません"
                    + "（いまは全部流してから盤面が出ます）。\n"
                    + "この枠に行を足すと " + (raw + 1) + "行目のあとで盤面が出るように変わります。"
                    + "そうしたくなければ、上で「導入を全部流し終えてから」を選び直してください。",
                    MessageType.Warning);
            }

            return active ? raw : -1;
        }
    }
}
