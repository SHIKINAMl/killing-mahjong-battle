using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.Managers
{
    [System.Serializable]
    public class CsvDialogueEntry
    {
        public string Condition;   // 状況
        public string Pose;        // 体
        public string Expression;  // 表情
        public string Dialogue1;   // セリフ1
        public string Dialogue2;   // セリフ2
    }

    /// <summary>
    /// Resources フォルダから CSV を読み込み、特定の状況（キー）に応じたセリフや表情を管理するクラス
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        [Header("Parsed CSV Data (Auto-loaded)")]
        public List<CsvDialogueEntry> parsedDialogues = new List<CsvDialogueEntry>();

        private void Awake()
        {
            if (Instance == null) {
                Instance = this;
                LoadCSV();
            } else {
                Destroy(gameObject);
            }
        }

        [ContextMenu("Reload CSV")]
        private void LoadCSV()
        {
            TextAsset csvFile = Resources.Load<TextAsset>("MajangGameTaskBoard - セリフ一覧");
            if (csvFile == null)
            {
                Debug.LogWarning("[DialogueManager] CSV file 'MajangGameTaskBoard - セリフ一覧' not found in Resources!");
                return;
            }

            parsedDialogues.Clear();

            string[] lines = csvFile.text.Split('\n');
            
            int conditionIdx = -1;
            int poseIdx = -1;
            int expressionIdx = -1;
            int dialogue1Idx = -1;
            int dialogue2Idx = -1;

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] rawCols = line.Split(new char[] { ',', '\t' });
                
                List<string> exactCols = new List<string>();
                foreach (string c in rawCols)
                {
                    exactCols.Add(c.Trim());
                }

                if (exactCols.Count < 1) continue;

                string col0 = exactCols[0].Replace("\uFEFF", "").Trim();

                // ヘッダー行の解析
                if (col0 == "状況")
                {
                    for (int i = 0; i < exactCols.Count; i++)
                    {
                        string headerName = exactCols[i].Replace("\uFEFF", "").Trim();
                        if (headerName == "状況") conditionIdx = i;
                        else if (headerName == "体" || headerName == "ポーズ") poseIdx = i;
                        else if (headerName == "表情") expressionIdx = i;
                        else if (headerName == "セリフ" || headerName == "セリフ1") dialogue1Idx = i;
                        else if (headerName == "セリフ2") dialogue2Idx = i;
                    }
                    continue; // ヘッダー行自体はデータとして扱わない
                }

                // ヘッダーが見つかるまではデータを読まない
                if (conditionIdx == -1) continue;

                string condition = exactCols[conditionIdx];
                if (string.IsNullOrEmpty(condition)) continue;
                
                // ヘッダーのような行を除外
                if (condition == "対局フェイズのセリフ" || 
                    condition == "山牌構築中のセリフ" || condition == "セリフの再生中に打牌が行われた場合") continue;

                var entry = new CsvDialogueEntry();
                entry.Condition = condition;
                
                if (poseIdx != -1 && exactCols.Count > poseIdx)
                    entry.Pose = exactCols[poseIdx];
                if (expressionIdx != -1 && exactCols.Count > expressionIdx) 
                    entry.Expression = exactCols[expressionIdx];
                if (dialogue1Idx != -1 && exactCols.Count > dialogue1Idx) 
                    entry.Dialogue1 = exactCols[dialogue1Idx];
                if (dialogue2Idx != -1 && exactCols.Count > dialogue2Idx) 
                    entry.Dialogue2 = exactCols[dialogue2Idx];

                // セリフ1とセリフ2が両方空の場合はスキップ
                if (string.IsNullOrEmpty(entry.Dialogue1) && string.IsNullOrEmpty(entry.Dialogue2)) continue;

                parsedDialogues.Add(entry);
                Debug.Log($"[DialogueManager] 読み込み成功: {entry.Condition} / 表情:{entry.Expression} / セリフ1:{entry.Dialogue1}");
            }
            Debug.Log($"[DialogueManager] Loaded {parsedDialogues.Count} dialogue conditions.");
        }

        public CsvDialogueEntry GetDialogueEntry(string condition)
        {
            return parsedDialogues.Find(e => e.Condition == condition);
        }

#if UNITY_EDITOR
        [ContextMenu("Save to CSV")]
        private void SaveToCSV()
        {
            string path = Application.dataPath + "/Resources/MajangGameTaskBoard - セリフ一覧.csv";
            
            // UTF-8 (BOM付き)で保存するとExcel等で文字化けしにくい
            System.Text.Encoding encoding = new System.Text.UTF8Encoding(true);
            using (System.IO.StreamWriter writer = new System.IO.StreamWriter(path, false, encoding))
            {
                // ヘッダーを書き込む
                writer.WriteLine("状況,体,表情,セリフ,セリフ2");
                
                // データを書き込む
                foreach (var entry in parsedDialogues)
                {
                    string condition = EscapeCSV(entry.Condition);
                    string pose = EscapeCSV(entry.Pose);
                    string expression = EscapeCSV(entry.Expression);
                    string dialogue1 = EscapeCSV(entry.Dialogue1);
                    string dialogue2 = EscapeCSV(entry.Dialogue2);
                    
                    writer.WriteLine($"{condition},{pose},{expression},{dialogue1},{dialogue2}");
                }
            }
            
            UnityEditor.AssetDatabase.Refresh();
            Debug.Log($"[DialogueManager] インスペクターの内容をCSVに保存しました: {path}");
        }

        private string EscapeCSV(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            // カンマ、ダブルクォーテーション、改行が含まれる場合はエスケープする
            if (str.Contains(",") || str.Contains("\"") || str.Contains("\n") || str.Contains("\r"))
            {
                return "\"" + str.Replace("\"", "\"\"") + "\"";
            }
            return str;
        }
#endif
    }
}
