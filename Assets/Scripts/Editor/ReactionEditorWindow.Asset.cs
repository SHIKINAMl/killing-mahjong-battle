using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KillingMahjong.UI;
using UnityEditor;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    public partial class ReactionEditorWindow
    {
        private void LoadAll()
        {
            LoadCsv();
            LoadCharacter();
        }

        private void LoadCharacter()
        {
            if (_character == null)
            {
                // シーンに置かれた EnemyInfoUI が指しているものを当てにできないので、
                // プロジェクト内で reactions がいちばん多いものを既定にする。
                // 空の残骸（Preafb/差配麻雀.asset）を掴んで「何も無い」と誤解しないため
                var guids = AssetDatabase.FindAssets("t:" + nameof(CharacterData));
                int best = -1;
                foreach (var g in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    var cd = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
                    if (cd == null) continue;
                    int n = cd.reactions == null ? 0 : cd.reactions.Count;
                    if (n > best) { best = n; _character = cd; }
                }
            }
            RefreshSpriteIds();
        }

        private void RefreshSpriteIds()
        {
            var faces = new List<string> { "" };
            var bodies = new List<string> { "" };
            if (_character != null)
            {
                if (_character.faceSprites != null)
                    foreach (var s in _character.faceSprites)
                    {
                        // blink は瞬き専用。候補に混ぜると誤用を誘う
                        if (s != null && !string.IsNullOrEmpty(s.id) && s.id != "blink") faces.Add(s.id);
                    }
                if (_character.bodySprites != null)
                    foreach (var s in _character.bodySprites)
                        if (s != null && !string.IsNullOrEmpty(s.id)) bodies.Add(s.id);
            }
            _faceIds = faces.ToArray();
            _bodyIds = bodies.ToArray();
        }

        private void LoadCsv()
        {
            _rows.Clear();
            _added.Clear();
            _csvDirty = false;
            _csvError = null;
            _rawLines = null;

            string full = Path.GetFullPath(CsvPath);
            if (!File.Exists(full))
            {
                _csvError = "CSV が見つかりません:\n" + CsvPath;
                return;
            }

            string text;
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(full);
                text = File.ReadAllText(full);
            }
            catch (IOException e) { _csvError = "読み込めませんでした: " + e.Message; return; }

            // BOM は ReadAllText が黙って剥がすので、生のバイトで見る。
            // 元のファイルに付いていなければ付けずに書き戻す（無駄な差分を出さないため）
            _hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            _eol = text.Contains("\r\n") ? "\r\n" : "\n";

            _rawLines = new List<string>(text.Split(new[] { _eol }, StringSplitOptions.None));

            for (int i = 0; i < _rawLines.Count; i++)
            {
                // DialogueManager と同じ切り方。カンマとタブのどちらでも列が割れる
                string[] cols = _rawLines[i].Split(',', '\t');
                for (int c = 0; c < cols.Length; c++) cols[c] = cols[c].Replace("﻿", "").Trim();
                if (cols.Length == 0) continue;

                if (_iCond < 0)
                {
                    if (cols[0] != "状況") continue;
                    for (int c = 0; c < cols.Length; c++)
                    {
                        if (cols[c] == "状況") _iCond = c;
                        else if (cols[c] == "体" || cols[c] == "ポーズ") _iPose = c;
                        else if (cols[c] == "表情") _iExpr = c;
                        else if (cols[c] == "セリフ" || cols[c] == "セリフ1") _iDlg1 = c;
                        else if (cols[c] == "セリフ2") _iDlg2 = c;
                    }
                    _headerCols = cols.Length;
                    continue;
                }

                if (_iCond >= cols.Length || string.IsNullOrEmpty(cols[_iCond])) continue;

                var row = new Row { lineIndex = i, condition = cols[_iCond] };
                if (_iPose >= 0 && _iPose < cols.Length) row.pose = cols[_iPose];
                if (_iExpr >= 0 && _iExpr < cols.Length) row.expression = cols[_iExpr];
                if (_iDlg1 >= 0 && _iDlg1 < cols.Length) row.dialogue1 = cols[_iDlg1];
                if (_iDlg2 >= 0 && _iDlg2 < cols.Length) row.dialogue2 = cols[_iDlg2];

                // 同じ状況名が2度出てきたら、先に書かれている方が勝つ。
                // DialogueManager.GetDialogueEntry が Find（先頭一致）で引いているため
                if (!_rows.ContainsKey(row.condition)) _rows[row.condition] = row;
            }

            if (_iCond < 0) _csvError = "見出し行（1列目が「状況」）が見つかりません。";
        }

        private bool SaveCsv()
        {
            if (_rawLines == null) return false;

            var problems = new List<string>();
            foreach (var r in AllRows())
            {
                // DialogueManager は引用符を解釈しない（Split するだけ）ので、
                // カンマやタブが1つでも混ざると列がずれて別のセリフに化ける
                foreach (var pair in new[] {
                    new KeyValuePair<string, string>("体", r.pose),
                    new KeyValuePair<string, string>("表情", r.expression),
                    new KeyValuePair<string, string>("セリフ", r.dialogue1),
                    new KeyValuePair<string, string>("セリフ2", r.dialogue2) })
                {
                    if (string.IsNullOrEmpty(pair.Value)) continue;
                    if (pair.Value.IndexOf(',') >= 0 || pair.Value.IndexOf('\t') >= 0)
                        problems.Add($"「{r.condition}」の{pair.Key}に , か Tab が入っています（列がずれるので使えません。読点は 、 を使ってください）");
                    if (pair.Value.IndexOf('\n') >= 0 || pair.Value.IndexOf('\r') >= 0)
                        problems.Add($"「{r.condition}」の{pair.Key}に改行が入っています");
                }
            }

            if (problems.Count > 0)
            {
                EditorUtility.DisplayDialog("保存できません",
                    string.Join("\n", problems.ToArray()), "閉じる");
                return false;
            }

            var outLines = new List<string>(_rawLines);
            foreach (var r in AllRows())
            {
                if (r.lineIndex < 0) continue;
                outLines[r.lineIndex] = BuildLine(r);
            }
            foreach (var r in _added)
            {
                if (r.lineIndex >= 0) continue;
                outLines.Add(BuildLine(r));
                r.lineIndex = outLines.Count - 1;
            }

            string body = string.Join(_eol, outLines.ToArray());
            try
            {
                File.WriteAllText(Path.GetFullPath(CsvPath), body, new UTF8Encoding(_hasBom));
            }
            catch (IOException e)
            {
                EditorUtility.DisplayDialog("保存できません", e.Message, "閉じる");
                return false;
            }

            AssetDatabase.ImportAsset(CsvPath);
            _rawLines = outLines;
            _csvDirty = false;
            return true;
        }

        private string BuildLine(Row r)
        {
            string[] cols;
            if (r.lineIndex >= 0 && r.lineIndex < _rawLines.Count)
                cols = _rawLines[r.lineIndex].Split(',');
            else
                cols = new string[_headerCols];

            int need = Mathf.Max(_headerCols, Mathf.Max(Mathf.Max(_iCond, _iPose), Mathf.Max(Mathf.Max(_iExpr, _iDlg1), _iDlg2)) + 1);
            if (cols.Length < need)
            {
                var grown = new string[need];
                for (int i = 0; i < grown.Length; i++) grown[i] = i < cols.Length ? cols[i] : "";
                cols = grown;
            }
            for (int i = 0; i < cols.Length; i++) if (cols[i] == null) cols[i] = "";

            if (_iCond >= 0) cols[_iCond] = r.condition;
            if (_iPose >= 0) cols[_iPose] = r.pose ?? "";
            if (_iExpr >= 0) cols[_iExpr] = r.expression ?? "";
            if (_iDlg1 >= 0) cols[_iDlg1] = r.dialogue1 ?? "";
            if (_iDlg2 >= 0) cols[_iDlg2] = r.dialogue2 ?? "";

            return string.Join(",", cols);
        }

        private IEnumerable<Row> AllRows()
        {
            foreach (var kv in _rows) yield return kv.Value;
            foreach (var r in _added) if (!_rows.ContainsKey(r.condition)) yield return r;
        }

        private Row FindRow(string condition)
        {
            Row r;
            if (_rows.TryGetValue(condition, out r)) return r;
            foreach (var a in _added) if (a.condition == condition) return a;
            return null;
        }

        private Row CreateRow(string condition)
        {
            var r = new Row { condition = condition, pose = "通常" };
            _added.Add(r);
            _rows[condition] = r;
            _csvDirty = true;
            return r;
        }
    }
}
