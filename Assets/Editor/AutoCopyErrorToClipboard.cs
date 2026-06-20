using UnityEngine;
using UnityEditor;

namespace KillingMahjong.EditorScripts
{
    /// <summary>
    /// エラーが発生した際、自動的にそのエラーメッセージとスタックトレースを
    /// クリップボードにコピーするエディター拡張スクリプト。
    /// </summary>
    [InitializeOnLoad]
    public static class AutoCopyErrorToClipboard
    {
        private static string lastErrorString = "";

        static AutoCopyErrorToClipboard()
        {
            // ログ出力のコールバックを登録
            Application.logMessageReceived += OnLogMessageReceived;
        }

        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            // コンパイルエラーや実行時エラー、例外のみを対象とする
            if (type == LogType.Error || type == LogType.Exception)
            {
                // 全く同じエラーが連続して出た場合はスパム防止のためスキップ
                if (condition == lastErrorString) return;

                lastErrorString = condition;

                // クリップボードにコピーするテキストを整形
                string copyText = $"[Unity Error]\n{condition}\n\n[Stack Trace]\n{stackTrace}";
                
                // OSのクリップボードにコピー
                GUIUtility.systemCopyBuffer = copyText;
                
                // コピーしたことをユーザーに通知（これは通常ログとして出す）
                Debug.Log("<color=yellow><b>[AutoCopy]</b></color> エラーをクリップボードに自動コピーしました！そのままチャット欄で Ctrl+V を押して貼り付けられます。");
            }
        }
    }
}
