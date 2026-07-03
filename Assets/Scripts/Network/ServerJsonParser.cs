using System;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using UnityEngine;

namespace KillingMahjong.Network
{
    /// <summary>
    /// サーバーJSONの手動パースユーティリティ。
    ///
    /// JsonUtility は Dictionary やトップレベルのプリミティブ配列を扱えないため、
    /// 一部のメッセージは文字列走査によるフォールバックパースが必要になる。
    /// それらのロジックを NetworkMessageHandler から切り出して集約したもの。
    /// ロジックは切り出し元と同一（挙動不変）。
    /// </summary>
    public static class ServerJsonParser
    {
        /// <summary>
        /// "{stateKey}": { ... "boost_hand_bonus": { ... } } から役名→ボーナス値の辞書を抽出する。
        /// 見つからない・パース失敗時は null を返す。
        /// </summary>
        public static Dictionary<string, int> ParseBoostHandBonusFromStateKey(string jsonString, string stateKey)
        {
            try
            {
                string pattern = $"\"{stateKey}\"\\s*:\\s*{{.*?\"boost_hand_bonus\"\\s*:\\s*{{([^}}]*?)}}";
                var match = System.Text.RegularExpressions.Regex.Match(jsonString, pattern, System.Text.RegularExpressions.RegexOptions.Singleline);
                if (!match.Success) return null;

                string dictStr = match.Groups[1].Value.Trim();
                var result = new Dictionary<string, int>();
                if (string.IsNullOrEmpty(dictStr)) return result;

                var pairs = dictStr.Split(',');
                foreach (var pair in pairs)
                {
                    var kv = pair.Split(':');
                    if (kv.Length == 2)
                    {
                        string key = kv[0].Trim().Trim('"');
                        if (int.TryParse(kv[1].Trim(), out int val))
                        {
                            result[key] = val;
                        }
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("ParseBoostHandBonus failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// "client_id" ごとに続く "{arrayKeyName}": [ ... ] を抽出し、client_id → int リストの辞書を返す。
        /// JsonUtility がプリミティブ配列をパースできない問題等のフォールバック。
        /// </summary>
        public static Dictionary<string, List<int>> ParseIntArrays(string jsonString, string arrayKeyName)
        {
            var result = new Dictionary<string, List<int>>();
            int searchFrom = 0;
            while (true)
            {
                int cidStart = jsonString.IndexOf("\"client_id\"", searchFrom);
                if (cidStart < 0) break;

                int nextCidStart = jsonString.IndexOf("\"client_id\"", cidStart + 11);
                if (nextCidStart < 0) nextCidStart = jsonString.Length;

                int valStart = jsonString.IndexOf('"', cidStart + 11) + 1;
                int valEnd = jsonString.IndexOf('"', valStart);
                if (valStart < 0 || valEnd < 0) break;
                string cid = jsonString.Substring(valStart, valEnd - valStart);

                int keyStart = jsonString.IndexOf($"\"{arrayKeyName}\"", valEnd);
                if (keyStart > 0 && keyStart < nextCidStart) // 次のデータに飛びすぎないようチェック
                {
                    int arrStart = jsonString.IndexOf('[', keyStart);
                    int arrEnd = jsonString.IndexOf(']', arrStart);
                    if (arrStart > 0 && arrEnd > arrStart)
                    {
                        string inner = jsonString.Substring(arrStart + 1, arrEnd - arrStart - 1);
                        var list = new List<int>();
                        foreach (var token in inner.Split(','))
                        {
                            if (int.TryParse(token.Trim(), out int val)) list.Add(val);
                        }
                        result[cid] = list;
                    }
                }
                searchFrom = valEnd + 1;
            }
            return result;
        }

        /// <summary>
        /// "exposedHandIndexesByPlayer": { "clientId": [..], ... } を抽出する。
        /// </summary>
        public static Dictionary<string, List<int>> ParseExposedHandIndexesByPlayer(string jsonString)
        {
            var result = new Dictionary<string, List<int>>();
            int startIdx = jsonString.IndexOf("\"exposedHandIndexesByPlayer\"");
            if (startIdx < 0) return result;

            int objStart = jsonString.IndexOf('{', startIdx);
            if (objStart < 0) return result;

            int objEnd = FindMatchingBracket(jsonString, objStart, '{', '}');
            if (objEnd < 0) return result;

            string innerObj = jsonString.Substring(objStart + 1, objEnd - objStart - 1);

            int searchIdx = 0;
            while (true)
            {
                int quote1 = innerObj.IndexOf('"', searchIdx);
                if (quote1 < 0) break;
                int quote2 = innerObj.IndexOf('"', quote1 + 1);
                if (quote2 < 0) break;

                string clientId = innerObj.Substring(quote1 + 1, quote2 - quote1 - 1);

                int arrStart = innerObj.IndexOf('[', quote2);
                if (arrStart < 0) break;
                int arrEnd = innerObj.IndexOf(']', arrStart);
                if (arrEnd < 0) break;

                string arrStr = innerObj.Substring(arrStart + 1, arrEnd - arrStart - 1);
                var list = new List<int>();
                foreach (var token in arrStr.Split(','))
                {
                    if (int.TryParse(token.Trim(), out int val)) list.Add(val);
                }

                result[clientId] = list;
                searchIdx = arrEnd + 1;
            }
            return result;
        }

        /// <summary>
        /// "client_id" ごとの "tenpai_examples" 配列を抽出する。
        /// フラット配列 [0,1,...]（Pythonサーバーの実データ）と
        /// ネスト配列 [[0,1,...],[2,3,...]]（モッククライアント等）の両形式に対応。
        /// </summary>
        public static Dictionary<string, List<int[]>> ParseTenpaiExamples(string jsonString)
        {
            var result = new Dictionary<string, List<int[]>>();
            int searchFrom = 0;
            while (true)
            {
                int cidStart = jsonString.IndexOf("\"client_id\"", searchFrom);
                if (cidStart < 0) break;

                int nextCidStart = jsonString.IndexOf("\"client_id\"", cidStart + 11);
                if (nextCidStart < 0) nextCidStart = jsonString.Length;

                int valStart = jsonString.IndexOf('"', cidStart + 11) + 1;
                int valEnd = jsonString.IndexOf('"', valStart);
                if (valStart < 0 || valEnd < 0) break;
                string cid = jsonString.Substring(valStart, valEnd - valStart);

                int keyStart = jsonString.IndexOf("\"tenpai_examples\"", valEnd);
                if (keyStart > 0 && keyStart < nextCidStart)
                {
                    int arrStart = jsonString.IndexOf('[', keyStart);
                    if (arrStart > 0)
                    {
                        int outerArrEnd = FindMatchingBracket(jsonString, arrStart);
                        if (outerArrEnd > arrStart)
                        {
                            string inner = jsonString.Substring(arrStart + 1, outerArrEnd - arrStart - 1);
                            var examplesList = new List<int[]>();

                            int innerArrStartCheck = inner.IndexOf('[');
                            if (innerArrStartCheck < 0)
                            {
                                // フラットな配列: [0, 1, 2, ...]
                                var list = new List<int>();
                                foreach (var token in inner.Split(','))
                                {
                                    if (int.TryParse(token.Trim(), out int val)) list.Add(val);
                                }
                                if (list.Count > 0)
                                {
                                    examplesList.Add(list.ToArray());
                                }
                            }
                            else
                            {
                                // ネストされた配列: [[0, 1, ...], [2, 3, ...]]
                                int innerSearchFrom = 0;
                                while (true)
                                {
                                    int innerArrStart = inner.IndexOf('[', innerSearchFrom);
                                    if (innerArrStart < 0) break;
                                    int innerArrEnd = inner.IndexOf(']', innerArrStart);
                                    if (innerArrEnd < 0) break;

                                    string arrayStr = inner.Substring(innerArrStart + 1, innerArrEnd - innerArrStart - 1);
                                    var list = new List<int>();
                                    foreach (var token in arrayStr.Split(','))
                                    {
                                        if (int.TryParse(token.Trim(), out int val)) list.Add(val);
                                    }
                                    examplesList.Add(list.ToArray());
                                    innerSearchFrom = innerArrEnd + 1;
                                }
                            }
                            result[cid] = examplesList;
                        }
                    }
                }
                searchFrom = valEnd + 1;
            }
            return result;
        }

        /// <summary>
        /// startIndex の開き括弧に対応する閉じ括弧のインデックスを返す。見つからなければ -1。
        /// </summary>
        public static int FindMatchingBracket(string s, int startIndex, char openBracket = '[', char closeBracket = ']')
        {
            int depth = 0;
            for (int i = startIndex; i < s.Length; i++)
            {
                if (s[i] == openBracket) depth++;
                else if (s[i] == closeBracket)
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// game_end JSON の "final_scores" から自分と相手のスコアを抽出する。
        /// 抽出に成功した場合 true を返す（切り出し元 ParseGameEnd のイベント発火条件と同一）。
        /// </summary>
        public static bool TryParseFinalScores(string jsonString, string localPlayerId, out int localScore, out int enemyScore)
        {
            localScore = 0;
            enemyScore = 0;

            int scoresStart = jsonString.IndexOf("\"final_scores\"");
            if (scoresStart < 0) return false;

            int dictStart = jsonString.IndexOf('{', scoresStart);
            int dictEnd = jsonString.IndexOf('}', dictStart);
            if (dictStart < 0 || dictEnd <= dictStart) return false;

            string dictStr = jsonString.Substring(dictStart + 1, dictEnd - dictStart - 1);
            string[] pairs = dictStr.Split(',');

            foreach (var pair in pairs)
            {
                string[] kvp = pair.Split(':');
                if (kvp.Length == 2)
                {
                    string key = kvp[0].Replace("\"", "").Trim();
                    string val = kvp[1].Trim();
                    if (int.TryParse(val, out int score))
                    {
                        if (key == localPlayerId) localScore = score;
                        else enemyScore = score;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// round_end JSON から liquidation データを手動パースする（JsonUtility のフォールバック）。
        /// 見つからない・失敗時は null。
        /// </summary>
        public static LiquidationData ParseLiquidationFromJson(string jsonString)
        {
            try
            {
                int liqStart = jsonString.IndexOf("\"liquidation\"");
                if (liqStart < 0) return null;

                int objStart = jsonString.IndexOf('{', liqStart);
                if (objStart < 0) return null;

                // ネストされた {} を考慮して末尾を見つける
                int objEnd = FindMatchingBracket(jsonString, objStart, '{', '}');
                if (objEnd < 0) return null;

                string liqJson = jsonString.Substring(objStart, objEnd - objStart + 1);
                Debug.Log($"[Network] liquidation 手動パース: {liqJson}");

                LiquidationData liq = JsonUtility.FromJson<LiquidationData>(liqJson);
                return liq;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Network] liquidation 手動パース失敗: {e.Message}");
                return null;
            }
        }
    }
}
