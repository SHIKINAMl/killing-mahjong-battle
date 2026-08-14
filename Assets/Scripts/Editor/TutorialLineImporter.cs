using System.Collections.Generic;
using System.IO;
using System.Text;
using KillingMahjong.Managers;
using KillingMahjong.Managers.Tutorial;
using UnityEditor;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    /// <summary>
    /// チュートリアル台本のセリフを TSV で書き出し／読み込みするツール。
    ///
    /// 敵のリアクションセリフ（<see cref="ReactionLineImporter"/>）と同じ手順に揃えてある。
    ///
    ///   1. 「書き出す」で今のセリフを TSV にする
    ///   2. Excel なりテキストエディタなりで **dialogue の列だけ** 直す
    ///   3. 「取り込む」で `Assets/Resources/TutorialLines.asset` に入る
    ///   4. Play すると差し替わっている
    ///
    /// **id の列は書き換えないこと。** どのセリフを差し替えるかの目印で、
    /// 一致しなかった行は無視される（＝元の文が出る）。消えて無言にはならない。
    ///
    /// 区切りはタブのみ。セリフに読点が入るのでカンマ区切りは受け付けない。
    /// </summary>
    public class TutorialLineImporter : EditorWindow
    {
        private const string AssetPath = "Assets/Resources/TutorialLines.asset";
        private const string DefaultTsvName = "tutorial_lines.tsv";

        private string _tsvPath = "";
        private bool _dryRun = true;
        private Vector2 _scroll;
        private string _report = "";

        [MenuItem("Tools/チュートリアル/台本のセリフを TSV で編集")]
        private static void Open()
        {
            var w = GetWindow<TutorialLineImporter>(true, "チュートリアル台本のセリフ");
            w.minSize = new Vector2(560f, 420f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("チュートリアル台本のセリフ", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "構造（局数・能力の実演・ボタンの開放順）は TutorialScenario.BuildDefault() が持ったままです。\n" +
                "ここで差し替わるのは text と speaker だけです。\n" +
                "id が一致しない行は無視され、元のセリフが出ます。",
                MessageType.Info);

            EditorGUILayout.Space();

            // ---- 書き出し ----
            EditorGUILayout.LabelField("1. 今のセリフを書き出す", EditorStyles.boldLabel);
            if (GUILayout.Button("TSV に書き出す…"))
            {
                Export();
            }

            EditorGUILayout.Space();

            // ---- 取り込み ----
            EditorGUILayout.LabelField("2. 直した TSV を取り込む", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _tsvPath = EditorGUILayout.TextField("TSV", _tsvPath);
                if (GUILayout.Button("選ぶ", GUILayout.Width(60f)))
                {
                    string p = EditorUtility.OpenFilePanel("台本セリフのTSV", "", "tsv,txt");
                    if (!string.IsNullOrEmpty(p)) _tsvPath = p;
                }
            }

            _dryRun = EditorGUILayout.ToggleLeft(
                "確認だけ（アセットに書き込まない）", _dryRun);

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_tsvPath)))
            {
                if (GUILayout.Button(_dryRun ? "確認する" : "取り込む"))
                {
                    Import();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("結果", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void Export()
        {
            string path = EditorUtility.SaveFilePanel(
                "台本セリフの書き出し", "", DefaultTsvName, "tsv");
            if (string.IsNullOrEmpty(path)) return;

            // **既に取り込んだぶんも反映した状態で書き出す。**
            // BuildDefault() の末尾で表を適用しているので、これで往復しても内容が戻らない。
            var scenario = TutorialScenario.BuildDefault();
            var rows = TutorialLineTable.Dump(scenario);

            var sb = new StringBuilder();
            sb.AppendLine("id\tspeaker\tdialogue");
            foreach (var r in rows)
            {
                sb.Append(r.id).Append('\t')
                  .Append(r.speaker).Append('\t')
                  .Append(Escape(r.text)).Append('\n');
            }

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            _tsvPath = path;
            _report = $"書き出しました（{rows.Count} 行）\n{path}\n\n" +
                      "dialogue の列だけ直して、下の「取り込む」に同じファイルを渡してください。";
            Debug.Log(_report);
        }

        private void Import()
        {
            if (!File.Exists(_tsvPath))
            {
                _report = "TSV が見つかりません: " + _tsvPath;
                return;
            }

            string[] lines = File.ReadAllLines(_tsvPath, Encoding.UTF8);
            if (lines.Length < 2)
            {
                _report = "行がありません（1行目は見出し）。";
                return;
            }

            // **列は見出し名で引く。** 並び順が変わっても、列が増えても壊れないようにする
            string[] head = lines[0].Split('\t');
            int idCol = IndexOf(head, "id");
            int textCol = IndexOf(head, "dialogue", "text", "セリフ");
            int speakerCol = IndexOf(head, "speaker", "話者");

            if (idCol < 0 || textCol < 0)
            {
                _report = "見出しに id と dialogue が要ります。1行目: " + lines[0];
                return;
            }

            // 台本を組み立てて、有効な ID の一覧を作る。
            // 打ち間違いをそのまま入れると「直したのに変わらない」で悩むことになる
            var valid = new HashSet<string>();
            foreach (var r in TutorialLineTable.Dump(TutorialScenario.BuildDefault()))
            {
                valid.Add(r.id);
            }

            var rows = new List<TutorialLineTable.Row>();
            var unknown = new List<string>();
            int blank = 0;

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] cells = lines[i].Split('\t');

                string id = Get(cells, idCol).Trim();
                if (string.IsNullOrEmpty(id)) continue;

                string text = Unescape(Get(cells, textCol));
                if (string.IsNullOrWhiteSpace(text)) { blank++; continue; }

                if (!valid.Contains(id)) { unknown.Add(id); continue; }

                var row = new TutorialLineTable.Row { id = id, text = text };

                string sp = speakerCol >= 0 ? Get(cells, speakerCol).Trim() : "";
                TutorialSpeaker parsed;
                row.speaker = !string.IsNullOrEmpty(sp) &&
                              System.Enum.TryParse(sp, true, out parsed)
                    ? parsed : TutorialSpeaker.Enemy;

                rows.Add(row);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"読めた行: {rows.Count}");
            if (blank > 0) sb.AppendLine($"空のセリフで飛ばした行: {blank}");
            if (unknown.Count > 0)
            {
                sb.AppendLine($"**知らない id が {unknown.Count} 件ありました（無視します）**");
                for (int i = 0; i < unknown.Count && i < 10; i++) sb.AppendLine("  " + unknown[i]);
                if (unknown.Count > 10) sb.AppendLine("  …");
                sb.AppendLine("  id を書き換えていないか確認してください。");
            }

            if (_dryRun)
            {
                sb.AppendLine();
                sb.AppendLine("確認だけなので書き込んでいません。");
                _report = sb.ToString();
                return;
            }

            var table = AssetDatabase.LoadAssetAtPath<TutorialLineTable>(AssetPath);
            if (table == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                table = CreateInstance<TutorialLineTable>();
                AssetDatabase.CreateAsset(table, AssetPath);
                sb.AppendLine($"アセットを作りました: {AssetPath}");
            }

            table.rows = rows;
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            sb.AppendLine();
            sb.AppendLine($"{AssetPath} に書き込みました。Play すると差し替わります。");
            _report = sb.ToString();
            Debug.Log(_report);
        }

        /// <summary>改行はセルの中に置けないので置き換える。</summary>
        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\t", " ").Replace("\r", "").Replace("\n", "\\n");
        }

        private static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\n", "\n").Trim();
        }

        private static string Get(string[] cells, int index)
        {
            return (index >= 0 && index < cells.Length) ? cells[index] : "";
        }

        private static int IndexOf(string[] head, params string[] names)
        {
            for (int i = 0; i < head.Length; i++)
            {
                string h = head[i].Trim().TrimStart('﻿');
                foreach (var n in names)
                {
                    if (string.Equals(h, n, System.StringComparison.OrdinalIgnoreCase)) return i;
                }
            }
            return -1;
        }
    }
}
