using System.Collections;
using UnityEngine;

namespace KillingMahjong.Core
{
    /// <summary>
    /// 展示用の初期化（2026-10-22、ユーザー指示「ゲームを再起動して id を取得しなおすリセットボタン」）。
    /// 設定画面の「展示用リセット」から呼ぶ。
    ///
    /// 次の来場者がまっさらな状態から遊べるよう、端末に残る**名前・勝敗の記録・チュートリアルの進み具合**を消し、
    /// ゲームを起動し直す。サーバーのプレイヤー ID（<see cref="KillingMahjong.Network.PlayerIdentity"/>）は
    /// 起動している間だけ持つ物なので、起動し直せば新しく取り直す。
    /// **音量などの設定は残す**（展示のために合わせてある）。
    ///
    /// Web 版はページを読み込み直す。エディタなどでは ID を捨てて最初のシーンから始め直す。
    /// 最初のシーンは展示のタイトル（ランキングと動画の画面。2026-10-23 からビルドの先頭。前はチュートリアルの OpeningScene だった）。
    /// </summary>
    public static class ExhibitionReset
    {
        /// <summary>消す保存値。設定（SettingsManager の分）は入れない。</summary>
        private static readonly string[] Keys =
        {
            "PlayerName",                                                          // PlayerProfile
            "Stats_Wins", "Stats_Losses", "Stats_SpecialWins", "Stats_BestScore", // PlayerStatsManager
            "Tutorial_LastCompletedRound",                                         // TutorialManager.Progression
            "Tutorial_RequestedStartRound",                                        // TutorialManager
            "Tutorial_PendingReturn", "Title_ReturnToRoom",                        // TutorialNavigation
            "Reaction_LastPlayTicks",                                              // PlaySessionLog（前回の起動日）
        };

        /// <summary>
        /// Web 版で、保存を書き終える前に読み込み直さないよう待つ秒数。
        /// Web 版の PlayerPrefs はブラウザの保存領域へ後から書き込むので、すぐ読み込み直すと消した値が戻ることがある
        /// </summary>
        private const float WebSaveWaitSeconds = 1f;

        public static bool Running { get; private set; }

        /// <summary>
        /// 消して起動し直す。待つ間のコルーチンは専用の物で回す
        /// （設定画面で回すと、待つ間に画面を閉じられたとき止まってしまう）。
        /// </summary>
        public static void Run()
        {
            if (Running) return;
            Running = true;
            DeleteSaved();
            Debug.Log("[ExhibitionReset] 名前・記録・チュートリアルの進み具合を消して、起動し直します。");
            var runner = new GameObject("ExhibitionReset").AddComponent<Runner>();
            Object.DontDestroyOnLoad(runner.gameObject);
            runner.StartCoroutine(Restart(runner.gameObject));
        }

        /// <summary>
        /// 消すだけで、起動し直さない。展示のタイトル（ランキングと動画の画面）で動画が流れ始めたときに呼ぶ
        /// （2026-10-23 プランナー指示「動画流れ始めたらリセットボタンをオートで押すのもアリ」）。
        /// そこでは対局の接続を張っていないので、プレイヤー ID は捨てるだけでよい（次に繋いだとき取り直す）。
        /// 起動し直すと動画が途中で切れるので、ここでは読み込み直さない
        /// </summary>
        public static void ClearSaved()
        {
            if (Running) return;
            DeleteSaved();
            KillingMahjong.Network.PlayerIdentity.Clear();
        }

        private static void DeleteSaved()
        {
            foreach (var key in Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        private sealed class Runner : MonoBehaviour { }

        private static IEnumerator Restart(GameObject runner)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            yield return new WaitForSecondsRealtime(WebSaveWaitSeconds);
            KmReloadPage();
#else
            yield return null;
            KillingMahjong.Network.PlayerIdentity.Clear();
            Running = false;
            Object.Destroy(runner);
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void KmReloadPage();
#endif
    }
}
