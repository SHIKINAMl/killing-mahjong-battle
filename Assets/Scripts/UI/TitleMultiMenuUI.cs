using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    /// <summary>
    /// タイトルの「対局する」を押したときに出す、対戦相手の探し方を選ぶ小さなメニュー。
    ///
    /// **メニューの部品はシーンに置いてある（`MultiMenu`。普段は非表示）。ここはそれを探して使う
    /// （2026-10-09）。** タイトルシーンと部屋シーンの両方で使うので、Prefab
    /// （`Assets/Prefabs/UI/MultiMenu.prefab`）にして両方のシーンへ置いている。
    /// 位置・大きさ・色・文字は Prefab が正で、ここに残っている数字は、
    /// シーンに無かったときに作るための控え（<see cref="SceneFirst"/>）。
    ///
    /// **押したときの動きは、再生時にコードでつなぐ。** シーンには保存しない。
    /// シーンに保存した配線は、シーンをいじると外れて「押しても何も起きない」になりやすい
    /// （`HANDOFF_20260802.md` で一度踏んでいる）。
    ///
    /// 見た目は `TitleScreenBuilder.RestyleMenu` に合わせてある
    /// （黒い長方形は使わず文字だけ、左に深紅の菱形）。
    /// </summary>
    public partial class TitleMultiMenuUI : MonoBehaviour
    {
        // TitleScreenBuilder と同じ色。あちらを変えたらここも合わせる
        private static readonly Color MenuText = new Color32(240, 232, 236, 255);
        private static readonly Color MenuMarker = new Color32(214, 40, 62, 255);
        private static readonly Color ScrimColor = new Color(0f, 0f, 0f, 0.78f);
        private static readonly Color ErrorText = new Color32(255, 120, 120, 255);

        private const float ItemHeight = 46f;
        private const float ItemWidth = 300f;
        private const float LabelFontSize = 24f;
        private const string FriendIllustrationResourcePath = "UI/FriendMatchIllustration";
        private const float FriendIllustrationScale = 0.18f;
        private const float FriendIllustrationGap = 20f;

        /// <summary>合言葉の確認を待つ上限。返事が無いまま固まらないようにする。</summary>
        private const float JoinTimeoutSeconds = 15f;

        /// <summary>対局シーンから写し取った通信クライアントの置き場（Resources 基準）。</summary>
        private const string ClientPrefabResourcePath = "Network/GameClient";

        private GameObject _root;
        private GameObject _modePanel;
        private GameObject _friendPanel;
        private GameObject _passwordPanel;
        private GameObject _truthNameHook;
        private TMP_InputField _passwordInput;
        private TMP_Text _passwordError;
        private TMP_Text _passwordStatus;
        private TMP_FontAsset _font;
        private RectTransform _friendIllustration;
        private TMP_Text _friendMatchLabel;

        /// <summary>確認中に二重で送らないための印。</summary>
        private bool _isCheckingRoom;
        private bool _hasDecided;

        private Action<MatchJoinMode> _onDecided;
        private Action _onClosed;

        /// <summary>
        /// メニューを出す。<paramref name="onDecided"/> は入り方が決まったときに呼ばれる
        /// （`MatchJoinRequest` はその前に設定済み）。
        /// </summary>
        public void Open(Action<MatchJoinMode> onDecided, Action onClosed = null)
        {
            CancelCpuJoin();
            _onDecided = onDecided;
            _hasDecided = false;
            _onClosed = onClosed;

            if (_root == null) Build();
            if (_root == null) return;

            SetTruthNameHookVisible(false);
            _root.SetActive(true);
            ShowModePanel();
        }

        public void Close()
        {
            CancelCpuJoin();
            if (_root != null) _root.SetActive(false);
            SetTruthNameHookVisible(true);
            _onClosed?.Invoke();
        }

        // ------------------------------------------------------------------

        private void ShowModePanel()
        {
            // 確認の返事を待っている間に画面を替えると、結果の行き先が消える
            if (_isCheckingRoom) return;

            if (_modePanel != null) _modePanel.SetActive(true);
            if (_friendPanel != null) _friendPanel.SetActive(false);
            if (_passwordPanel != null) _passwordPanel.SetActive(false);
            if (_cpuPanel != null) _cpuPanel.SetActive(false);
            PositionFriendIllustration();
        }

        /// <summary>
        /// フレンドと遊ぶための、部屋作成／合言葉入力の入口。
        /// </summary>
        private void ShowFriendPanel()
        {
            if (_isCheckingRoom) return;

            if (_modePanel != null) _modePanel.SetActive(false);
            if (_friendPanel != null) _friendPanel.SetActive(true);
            if (_passwordPanel != null) _passwordPanel.SetActive(false);
            if (_cpuPanel != null) _cpuPanel.SetActive(false);
        }

        private void ShowPasswordPanel()
        {
            if (_isCheckingRoom) return;

            if (_modePanel != null) _modePanel.SetActive(false);
            if (_friendPanel != null) _friendPanel.SetActive(false);
            if (_passwordPanel != null) _passwordPanel.SetActive(true);
            if (_cpuPanel != null) _cpuPanel.SetActive(false);

            SetPasswordError("");
            SetStatus("");
            if (_passwordInput != null)
            {
                _passwordInput.text = "";
                _passwordInput.Select();
                _passwordInput.ActivateInputField();
            }
        }

        // 野良と部屋作成は対局シーンで待機する。事前確認後の接続が残っている場合は、
        // そのStartは実行済みなので、選んだmodeで明示的に入り直す。

        private void OnPublicSelected()
        {
            if (_isCheckingRoom || _hasDecided) return;
            MatchJoinRequest.RequestPublic();
            ReuseClientForDirectJoin();
            Decide(MatchJoinMode.Public);
        }

        private void OnPrivateCreateSelected()
        {
            if (_isCheckingRoom || _hasDecided) return;
            MatchJoinRequest.RequestPrivateCreate();
            ReuseClientForDirectJoin();
            Decide(MatchJoinMode.PrivateCreate);
        }

        private static void ReuseClientForDirectJoin()
        {
            var client = WebSocketGameClientSample.Instance;
            if (client == null) return;
            if (client.IsConnected) client.RequestRejoin();
            else { _ = client.ConnectAsync(); }
        }

        private void OnPasswordSubmit()
        {
            if (_isCheckingRoom || _hasDecided) return;

            string input = _passwordInput != null ? _passwordInput.text : "";

            // サーバーにも同じ検証があるが、弾かれてから気づくと
            // 「押したのに何も起きない」に見える。手元で理由を出す
            if (!MatchJoinRequest.IsValidPassword(input))
            {
                SetPasswordError($"あいことばは英数字{MatchJoinRequest.PasswordLength}文字です");
                return;
            }

            MatchJoinRequest.RequestPrivateJoin(input);
            StartCoroutine(JoinPrivateRoomRoutine());
        }

        /// <summary>
        /// **対局シーンへ移る前に、その部屋が本当にあるかサーバーへ訊く。**
        ///
        /// 確かめてから繋ぎ直す、ができない点に注意。合言葉が当たった瞬間に
        /// サーバーは**その接続で**対局を成立させ、部屋を消してしまう
        /// （`websocket_server.py` の `_join_private_room`）。
        /// なので接続はここで張り、成功したらそのままシーンへ持ち越す
        /// （`WebSocketGameClientSample` が DontDestroyOnLoad で生き残る）。
        ///
        /// 失敗したときは何も消費されず接続も生きたままなので、
        /// 接続を畳んで入力し直せる状態に戻すだけでよい。
        /// </summary>
        private System.Collections.IEnumerator JoinPrivateRoomRoutine()
        {
            _isCheckingRoom = true;
            SetPasswordError("");
            SetStatus("あいことばを確認しています...");

            var client = EnsureClient();
            if (client == null)
            {
                SetStatus("");
                SetPasswordError("通信クライアントを用意できませんでした");
                _isCheckingRoom = false;
                yield break;
            }

            string error = null;
            bool matched = false;
            Action<string> onError = msg => error = msg;
            Action onMatched = () => matched = true;

            client.OnServerError += onError;
            client.OnMatchStarted += onMatched;

            // 接続していなければここで張る。既に張ってあれば connected は来ないので、
            // その場合は join を直接送る
            if (!client.IsConnected)
            {
                _ = client.ConnectAsync();
            }
            else
            {
                _ = client.SendAsync(MatchJoinRequest.BuildJoinJson());
            }

            float deadline = Time.realtimeSinceStartup + JoinTimeoutSeconds;
            while (error == null && !matched && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            client.OnServerError -= onError;
            client.OnMatchStarted -= onMatched;

            if (matched)
            {
                SetStatus("部屋に入りました");
                Decide(MatchJoinMode.PrivateJoin);
                _isCheckingRoom = false;
                yield break;
            }

            // ここへ来たら対局シーンへは行かない
            SetStatus("");
            SetPasswordError(error != null ? TranslateJoinError(error) : "サーバーから応答がありません");

            // 部屋は消費されていないので、畳んで入力し直せるようにする
            _ = client.ResetConnectionAsync();

            if (_passwordInput != null)
            {
                _passwordInput.Select();
                _passwordInput.ActivateInputField();
            }
            _isCheckingRoom = false;
        }

        /// <summary>サーバーの英語メッセージを、そのまま出しても伝わらないので言い換える。</summary>
        private static string TranslateJoinError(string serverMessage)
        {
            if (string.IsNullOrEmpty(serverMessage)) return "入れませんでした";

            if (serverMessage.Contains("Private room not found"))
                return "そのあいことばの部屋はありません";
            if (serverMessage.Contains("Cannot join your own"))
                return "自分が作った部屋には入れません";
            if (serverMessage.Contains("Already in a match"))
                return "すでに対局中です";
            if (serverMessage.Contains("Password must be"))
                return $"あいことばは英数字{MatchJoinRequest.PasswordLength}文字です";

            return serverMessage;
        }

        /// <summary>
        /// 接続クライアントを用意する。
        /// タイトルシーンには置いていないので、対局シーンから写し取った Prefab を読み込む
        /// （`Tools > Network > 通信クライアントのPrefabを作る` で作る）。
        /// </summary>
        private static WebSocketGameClientSample EnsureClient()
        {
            if (WebSocketGameClientSample.Instance != null)
            {
                return WebSocketGameClientSample.Instance;
            }

            var prefab = Resources.Load<GameObject>(ClientPrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[TitleMultiMenuUI] Resources/{ClientPrefabResourcePath} がありません。" +
                               "メニューの Tools > Network > 通信クライアントのPrefabを作る を実行してください");
                return null;
            }

            var obj = Instantiate(prefab);
            obj.name = prefab.name;
            return obj.GetComponent<WebSocketGameClientSample>();
        }

        private void SetPasswordError(string text)
        {
            if (_passwordError != null) _passwordError.text = text;
        }

        private void SetStatus(string text)
        {
            if (_passwordStatus != null) _passwordStatus.text = text;
        }

        private void SetTruthNameHookVisible(bool visible)
        {
            if (_truthNameHook == null)
            {
                _truthNameHook = GameObject.Find("TruthNameHook");
            }

            if (_truthNameHook != null) _truthNameHook.SetActive(visible);
        }

        private void Decide(MatchJoinMode mode)
        {
            if (_hasDecided) return;
            _hasDecided = true;
            Close();
            _onDecided?.Invoke(mode);
        }

        // ------------------------------------------------------------------

    }
}
