using System.Collections.Generic;
using System.IO;
using System.Text;
using KillingMahjong.UI;
using UnityEditor;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    /// <summary>
    /// 敵キャラのセリフ TSV を CharacterData（ScriptableObject）へ流し込むツール。
    ///
    /// 想定する TSV は `REACTION_LINES_BRIEF_20260803.md` §3 の形式:
    ///     trigger / face / manfu / dialogue
    ///
    /// **列は見出し名で引く**ので、並び順が変わっても、列が増えても壊れない。
    /// 区切りはタブのみ。セリフに読点が入るのでカンマ区切りは受け付けない。
    ///
    /// 取り込み方は「TSV に出てくるトリガーだけ差し替える」。
    /// 既存の GameStart / Click などには手を触れないので、
    /// 同じ TSV を何度読み直しても結果が積み上がらない。
    /// </summary>
    public class ReactionLineImporter : EditorWindow
    {
        private CharacterData _target;
        private string _tsvPath = "";
        private bool _dryRun = true;
        private Vector2 _scroll;
        private string _report = "";

        /// <summary>吹き出しは3行までなので、これを超えると見切れる</summary>
        private const int DialogueSoftLimit = 40;

        [MenuItem("Tools/リアクション/セリフTSVを取り込む")]
        private static void Open()
        {
            var w = GetWindow<ReactionLineImporter>(true, "セリフTSVの取り込み");
            w.minSize = new Vector2(560f, 420f);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "REACTION_LINES_BRIEF_20260803.md の形式で書かれた TSV を読み込み、\n" +
                "CharacterData の reactions へ流し込みます。\n" +
                "TSV に出てくるトリガーだけを差し替えるので、既存のセリフは消えません。",
                MessageType.Info);

            _target = (CharacterData)EditorGUILayout.ObjectField(
                "取り込み先", _target, typeof(CharacterData), false);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("TSV", _tsvPath, EditorStyles.textField);
                if (GUILayout.Button("選択...", GUILayout.Width(80f)))
                {
                    string p = EditorUtility.OpenFilePanel("セリフTSVを選ぶ", "", "tsv,txt,csv");
                    if (!string.IsNullOrEmpty(p)) _tsvPath = p;
                }
            }

            _dryRun = EditorGUILayout.ToggleLeft(
                "検証のみ（アセットに書き込まない）", _dryRun);

            using (new EditorGUI.DisabledScope(_target == null || string.IsNullOrEmpty(_tsvPath)))
            {
                if (GUILayout.Button(_dryRun ? "検証する" : "取り込む", GUILayout.Height(30f)))
                {
                    _report = Run(_target, _tsvPath, _dryRun);
                }
            }

            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private static string Run(CharacterData target, string path, bool dryRun)
        {
            var log = new StringBuilder();

            string text;
            try
            {
                // BOM 付き UTF-8 も想定。ReadAllText が判別してくれる
                text = File.ReadAllText(path);
            }
            catch (IOException e)
            {
                return "読み込めませんでした: " + e.Message;
            }

            // 表情IDの一覧。ここに無い顔を書かれても絵が無いので弾く
            var faceIds = new HashSet<string>();
            var usableFaces = new List<string>();
            foreach (var s in target.faceSprites)
            {
                if (s == null || string.IsNullOrEmpty(s.id)) continue;
                faceIds.Add(s.id);
                // blink は瞬き専用。案内に混ぜると誤用を誘うので候補から外す
                if (s.id != "blink") usableFaces.Add(s.id);
            }
            string faceHint = string.Join(" / ", usableFaces);

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            int colTrigger = -1, colFace = -1, colManfu = -1, colDialogue = -1;
            var parsed = new List<CharacterReaction>();
            var seenTriggers = new HashSet<ReactionTrigger>();
            int errors = 0, warnings = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                if (string.IsNullOrWhiteSpace(raw)) continue;

                string[] cols = raw.Split('\t');
                for (int c = 0; c < cols.Length; c++) cols[c] = cols[c].Replace("﻿", "").Trim();

                // 見出し行を探す。見つかるまでデータは読まない
                if (colTrigger < 0)
                {
                    for (int c = 0; c < cols.Length; c++)
                    {
                        switch (cols[c])
                        {
                            case "trigger": colTrigger = c; break;
                            case "face": colFace = c; break;
                            case "manfu": colManfu = c; break;
                            case "dialogue": colDialogue = c; break;
                        }
                    }
                    if (colTrigger >= 0) continue;

                    if (cols.Length == 1)
                    {
                        log.AppendLine($"L{i + 1}: タブがありません。TSV（タブ区切り）で保存してください");
                        errors++;
                    }
                    continue;
                }

                if (colTrigger >= cols.Length || string.IsNullOrEmpty(cols[colTrigger])) continue;

                string tRaw = cols[colTrigger];
                ReactionTrigger trigger;
                if (!System.Enum.TryParse(tRaw, false, out trigger))
                {
                    log.AppendLine($"L{i + 1}: 知らないトリガー '{tRaw}'（綴りを確認してください）");
                    errors++;
                    continue;
                }

                string face = (colFace >= 0 && colFace < cols.Length) ? cols[colFace] : "";
                string manfuRaw = (colManfu >= 0 && colManfu < cols.Length) ? cols[colManfu] : "None";
                string dialogue = (colDialogue >= 0 && colDialogue < cols.Length) ? cols[colDialogue] : "";

                if (string.IsNullOrEmpty(dialogue))
                {
                    log.AppendLine($"L{i + 1}: {tRaw} のセリフが空です");
                    errors++;
                    continue;
                }

                if (!string.IsNullOrEmpty(face) && !faceIds.Contains(face))
                {
                    log.AppendLine($"L{i + 1}: 表情 '{face}' は登録されていません（使えるのは {faceHint}）");
                    errors++;
                    continue;
                }
                if (face == "blink")
                {
                    log.AppendLine($"L{i + 1}: blink は瞬き専用です。表情には使えません");
                    errors++;
                    continue;
                }

                ManfuType manfu;
                if (string.IsNullOrEmpty(manfuRaw)) manfu = ManfuType.None;
                else if (!System.Enum.TryParse(manfuRaw, false, out manfu))
                {
                    log.AppendLine($"L{i + 1}: 知らない漫符 '{manfuRaw}'");
                    errors++;
                    continue;
                }

                if (dialogue.Length > DialogueSoftLimit)
                {
                    log.AppendLine($"L{i + 1}: {tRaw} のセリフが {dialogue.Length} 字です（{DialogueSoftLimit} 字を超えると見切れます）");
                    warnings++;
                }
                // 表示側で「」を付けるので、二重にならないよう剥がす
                if (dialogue.StartsWith("「") && dialogue.EndsWith("」"))
                {
                    dialogue = dialogue.Substring(1, dialogue.Length - 2);
                }

                parsed.Add(new CharacterReaction
                {
                    trigger = trigger,
                    bodyExpressionId = "",   // ポーズは2種しかなく現状使っていない
                    faceExpressionId = face,
                    manfuType = manfu,
                    duration = 0f,           // 0 = ReactionController の既定値を使う
                    dialogueText = dialogue,
                });
                seenTriggers.Add(trigger);
            }

            if (colTrigger < 0)
            {
                return "見出し行が見つかりません。1行目に trigger / face / manfu / dialogue が要ります。\n" + log;
            }

            // バリエーションが1本しかないトリガーは、毎回同じセリフになる
            var countPerTrigger = new Dictionary<ReactionTrigger, int>();
            foreach (var r in parsed)
            {
                countPerTrigger.TryGetValue(r.trigger, out int n);
                countPerTrigger[r.trigger] = n + 1;
            }
            foreach (var kv in countPerTrigger)
            {
                if (kv.Value < 2)
                {
                    log.AppendLine($"{kv.Key}: {kv.Value}件だけです。ランダム選択されないので毎回同じセリフになります");
                    warnings++;
                }
            }

            log.AppendLine();
            log.AppendLine($"読み取り {parsed.Count} 行 / トリガー {seenTriggers.Count} 種 / エラー {errors} / 注意 {warnings}");

            if (errors > 0)
            {
                log.AppendLine("エラーがあるので取り込みません。直してから再実行してください。");
                return log.ToString();
            }

            if (dryRun)
            {
                log.AppendLine("（検証のみ。アセットは変更していません）");
                return log.ToString();
            }

            Undo.RecordObject(target, "セリフTSVの取り込み");

            int removed = target.reactions.RemoveAll(r => r != null && seenTriggers.Contains(r.trigger));
            target.reactions.AddRange(parsed);

            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();

            log.AppendLine($"差し替えました（既存 {removed} 件を除去 → {parsed.Count} 件を追加）。");
            log.AppendLine($"reactions は合計 {target.reactions.Count} 件になりました。");
            return log.ToString();
        }
    }
}
