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
    /// **シーンには置かず、実行時に組み立てる。**
    /// タイトルのボタンは `onClick` が `TitleUIManager` へシリアライズ済みで、
    /// シーンをいじると配線が消えて「押しても何も起きない」状態になりやすい
    /// （`HANDOFF_20260802.md` で一度踏んでいる）。触らないのがいちばん安全。
    ///
    /// 見た目は `TitleScreenBuilder.RestyleMenu` に合わせてある
    /// （黒い長方形は使わず文字だけ、左に深紅の菱形）。
    /// </summary>
    public class TitleMultiMenuUI : MonoBehaviour
    {
        // TitleScreenBuilder と同じ色。あちらを変えたらここも合わせる
        private static readonly Color MenuText = new Color32(240, 232, 236, 255);
        private static readonly Color MenuMarker = new Color32(214, 40, 62, 255);
        private static readonly Color ScrimColor = new Color(0f, 0f, 0f, 0.78f);
        private static readonly Color ErrorText = new Color32(255, 120, 120, 255);

        private const float ItemHeight = 46f;
        private const float ItemWidth = 300f;
        private const float LabelFontSize = 24f;

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

        /// <summary>確認中に二重で送らないための印。</summary>
        private bool _isCheckingRoom;

        private Action<MatchJoinMode> _onDecided;

        /// <summary>
        /// メニューを出す。<paramref name="onDecided"/> は入り方が決まったときに呼ばれる
        /// （`MatchJoinRequest` はその前に設定済み）。
        /// </summary>
        public void Open(Action<MatchJoinMode> onDecided)
        {
            _onDecided = onDecided;

            if (_root == null) Build();
            if (_root == null) return;

            SetTruthNameHookVisible(false);
            _root.SetActive(true);
            ShowModePanel();
        }

        public void Close()
        {
            if (_root != null) _root.SetActive(false);
            SetTruthNameHookVisible(true);
        }

        // ------------------------------------------------------------------

        private void ShowModePanel()
        {
            // 確認の返事を待っている間に画面を替えると、結果の行き先が消える
            if (_isCheckingRoom) return;

            if (_modePanel != null) _modePanel.SetActive(true);
            if (_friendPanel != null) _friendPanel.SetActive(false);
            if (_passwordPanel != null) _passwordPanel.SetActive(false);
        }

        /// <summary>
        /// フレンドと遊ぶための入口。ここを専用パネルにしておくと、将来の部屋設定を
        /// サーバーの契約が決まった時点で安全に追加できる。
        /// </summary>
        private void ShowFriendPanel()
        {
            if (_isCheckingRoom) return;

            if (_modePanel != null) _modePanel.SetActive(false);
            if (_friendPanel != null) _friendPanel.SetActive(true);
            if (_passwordPanel != null) _passwordPanel.SetActive(false);
        }

        private void ShowPasswordPanel()
        {
            if (_isCheckingRoom) return;

            if (_modePanel != null) _modePanel.SetActive(false);
            if (_friendPanel != null) _friendPanel.SetActive(false);
            if (_passwordPanel != null) _passwordPanel.SetActive(true);

            SetPasswordError("");
            SetStatus("");
            if (_passwordInput != null)
            {
                _passwordInput.text = "";
                _passwordInput.Select();
                _passwordInput.ActivateInputField();
            }
        }

        // 野良と「部屋を作る」は失敗しようがないので、今までどおり
        // 対局シーンへ移ってから接続する。**合言葉で入るときだけ**が特別で、
        // 部屋が無ければ移ってはいけないため先に確かめる。

        private void OnPublicSelected()
        {
            if (_isCheckingRoom) return;
            MatchJoinRequest.RequestPublic();
            Decide(MatchJoinMode.Public);
        }

        private void OnPrivateCreateSelected()
        {
            if (_isCheckingRoom) return;
            MatchJoinRequest.RequestPrivateCreate();
            Decide(MatchJoinMode.PrivateCreate);
        }

        private void OnPasswordSubmit()
        {
            if (_isCheckingRoom) return;

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
                client.ConnectAsync();
            }
            else
            {
                client.SendAsync(MatchJoinRequest.BuildJoinJson());
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
            client.ResetConnectionAsync();

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
            Close();
            _onDecided?.Invoke(mode);
        }

        // ------------------------------------------------------------------

        private void Build()
        {
            // TMP の既定フォントは LiberationSans で、ひらがなが全部豆腐になる。
            // パスで決め打ちせず、画面にある文字からフォントを借りる
            _font = BorrowJapaneseFont();

            // **専用の Canvas を立てる。**
            // タイトルの既存 Canvas にぶら下げると、全画面の TitleScrim（右ほど濃くする幕）が
            // メニューの上に乗り、見えているのにクリックが幕に吸われて押せなくなる。
            _root = new GameObject("MultiMenu",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Common.UISortingOrders.TitleMenuOverlay;

            // このプロジェクトの UI は 800x600 基準（CanvasFixer と揃える）
            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);

            Stretch(_root.GetComponent<RectTransform>());

            // 背後の暗幕。タイトルのボタンを押せないようにする役目も兼ねる
            var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
            scrim.transform.SetParent(_root.transform, false);
            Stretch(scrim.GetComponent<RectTransform>());
            var scrimImg = scrim.GetComponent<Image>();
            scrimImg.color = ScrimColor;
            scrimImg.raycastTarget = true;

            BuildModePanel();
            BuildFriendPanel();
            BuildPasswordPanel();
        }

        private void BuildModePanel()
        {
            _modePanel = new GameObject("ModePanel", typeof(RectTransform));
            _modePanel.transform.SetParent(_root.transform, false);
            Center(_modePanel.GetComponent<RectTransform>(), new Vector2(ItemWidth, ItemHeight * 5f));

            CreateHeading(_modePanel.transform, "対局する", ItemHeight * 2f);

            CreateMenuItem(_modePanel.transform, "野良マッチ", ItemHeight * 0.9f, OnPublicSelected);
            CreateMenuItem(_modePanel.transform, "フレンドマッチ", -ItemHeight * 0.2f, ShowFriendPanel);
            CreateMenuItem(_modePanel.transform, "もどる", -ItemHeight * 1.5f, Close);
        }

        private void BuildFriendPanel()
        {
            _friendPanel = new GameObject("FriendPanel", typeof(RectTransform));
            _friendPanel.transform.SetParent(_root.transform, false);
            Center(_friendPanel.GetComponent<RectTransform>(), new Vector2(ItemWidth, ItemHeight * 5f));

            CreateHeading(_friendPanel.transform, "フレンドマッチ", ItemHeight * 2f);
            CreateMenuItem(_friendPanel.transform, "部屋を作る", ItemHeight * 0.9f, OnPrivateCreateSelected);
            CreateMenuItem(_friendPanel.transform, "あいことばで入る", -ItemHeight * 0.2f, ShowPasswordPanel);
            CreateCaption(_friendPanel.transform, "部屋設定は今後追加予定", -ItemHeight * 1.15f);
            CreateMenuItem(_friendPanel.transform, "もどる", -ItemHeight * 2.1f, ShowModePanel);

            _friendPanel.SetActive(false);
        }

        private void BuildPasswordPanel()
        {
            _passwordPanel = new GameObject("PasswordPanel", typeof(RectTransform));
            _passwordPanel.transform.SetParent(_root.transform, false);
            Center(_passwordPanel.GetComponent<RectTransform>(), new Vector2(ItemWidth, ItemHeight * 5f));

            CreateHeading(_passwordPanel.transform, "あいことばを入力", ItemHeight * 2f);

            var fieldObj = new GameObject("PasswordInput",
                typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            fieldObj.transform.SetParent(_passwordPanel.transform, false);
            var frt = fieldObj.GetComponent<RectTransform>();
            Center(frt, new Vector2(ItemWidth * 0.7f, ItemHeight));
            frt.anchoredPosition = new Vector2(0f, ItemHeight * 0.7f);

            var fieldBg = fieldObj.GetComponent<Image>();
            fieldBg.color = new Color(1f, 1f, 1f, 0.10f);

            var textArea = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textArea.transform.SetParent(fieldObj.transform, false);
            Stretch(textArea.GetComponent<RectTransform>());
            var textComp = textArea.GetComponent<TextMeshProUGUI>();
            ApplyFont(textComp);
            textComp.fontSize = LabelFontSize;
            textComp.color = MenuText;
            textComp.alignment = TextAlignmentOptions.Center;

            _passwordInput = fieldObj.GetComponent<TMP_InputField>();
            _passwordInput.textViewport = textArea.GetComponent<RectTransform>();
            _passwordInput.textComponent = textComp;
            _passwordInput.characterLimit = MatchJoinRequest.PasswordLength;
            // 合言葉は英大文字と数字だけ。小文字で打たれても MatchJoinRequest 側で直す
            _passwordInput.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
            _passwordInput.onSubmit.AddListener(_ => OnPasswordSubmit());

            var errorObj = new GameObject("Error", typeof(RectTransform), typeof(TextMeshProUGUI));
            errorObj.transform.SetParent(_passwordPanel.transform, false);
            var ert = errorObj.GetComponent<RectTransform>();
            Center(ert, new Vector2(ItemWidth, ItemHeight * 0.7f));
            ert.anchoredPosition = new Vector2(0f, ItemHeight * 0.05f);
            _passwordError = errorObj.GetComponent<TextMeshProUGUI>();
            ApplyFont(_passwordError);
            _passwordError.fontSize = LabelFontSize * 0.7f;
            _passwordError.color = ErrorText;
            _passwordError.alignment = TextAlignmentOptions.Center;
            _passwordError.text = "";

            var statusObj = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI));
            statusObj.transform.SetParent(_passwordPanel.transform, false);
            var srt = statusObj.GetComponent<RectTransform>();
            Center(srt, new Vector2(ItemWidth, ItemHeight * 0.7f));
            srt.anchoredPosition = new Vector2(0f, ItemHeight * 1.35f);
            _passwordStatus = statusObj.GetComponent<TextMeshProUGUI>();
            ApplyFont(_passwordStatus);
            _passwordStatus.fontSize = LabelFontSize * 0.7f;
            _passwordStatus.color = MenuText;
            _passwordStatus.alignment = TextAlignmentOptions.Center;
            _passwordStatus.text = "";
            _passwordStatus.raycastTarget = false;

            CreateMenuItem(_passwordPanel.transform, "決定", -ItemHeight * 0.8f, OnPasswordSubmit);
            CreateMenuItem(_passwordPanel.transform, "もどる", -ItemHeight * 1.9f, ShowFriendPanel);

            _passwordPanel.SetActive(false);
        }

        // ------------------------------------------------------------------

        private void CreateHeading(Transform parent, string text, float y)
        {
            var obj = new GameObject("Heading", typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            Center(rt, new Vector2(ItemWidth, ItemHeight));
            rt.anchoredPosition = new Vector2(0f, y);

            var tmp = obj.GetComponent<TextMeshProUGUI>();
            ApplyFont(tmp);
            tmp.text = text;
            tmp.fontSize = LabelFontSize * 0.85f;
            tmp.color = MenuText;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }

        private void CreateCaption(Transform parent, string text, float y)
        {
            var obj = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            Center(rt, new Vector2(ItemWidth, ItemHeight));
            rt.anchoredPosition = new Vector2(0f, y);

            var tmp = obj.GetComponent<TextMeshProUGUI>();
            ApplyFont(tmp);
            tmp.text = text;
            tmp.fontSize = LabelFontSize * 0.65f;
            tmp.color = new Color(MenuText.r, MenuText.g, MenuText.b, 0.7f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }

        private void CreateMenuItem(Transform parent, string label, float y, Action onClick)
        {
            var obj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            Center(rt, new Vector2(ItemWidth, ItemHeight));
            rt.anchoredPosition = new Vector2(0f, y);

            // 当たり判定は要るので、消さずに透明にする（RestyleMenu と同じ考え方）
            var img = obj.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = true;

            var textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(obj.transform, false);
            Stretch(textObj.GetComponent<RectTransform>());
            var tmp = textObj.GetComponent<TextMeshProUGUI>();
            ApplyFont(tmp);
            tmp.text = label;
            tmp.fontSize = LabelFontSize;
            tmp.color = MenuText;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.margin = new Vector4(38f, 0f, 0f, 0f);
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;

            // 左の菱形。タイトルのメニューと同じ印
            var marker = new GameObject("Marker", typeof(RectTransform), typeof(Image));
            marker.transform.SetParent(obj.transform, false);
            var mrt = marker.GetComponent<RectTransform>();
            mrt.anchorMin = new Vector2(0f, 0.5f);
            mrt.anchorMax = new Vector2(0f, 0.5f);
            mrt.pivot = new Vector2(0f, 0.5f);
            mrt.anchoredPosition = new Vector2(16f, 0f);
            mrt.sizeDelta = new Vector2(10f, 10f);
            mrt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var mimg = marker.GetComponent<Image>();
            mimg.color = MenuMarker;
            mimg.raycastTarget = false;

            obj.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
        }

        private void ApplyFont(TMP_Text tmp)
        {
            if (_font != null) tmp.font = _font;
        }

        /// <summary>
        /// 画面にある文字からフォントを借りる。
        /// `PixelMplus10_DynamicFixed` という名前のアセットが複数あるため、
        /// パス決め打ちで拾うと別物を掴むことがある。
        /// </summary>
        private static TMP_FontAsset BorrowJapaneseFont()
        {
            var texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in texts)
            {
                if (t != null && t.font != null) return t.font;
            }
            Debug.LogWarning("[TitleMultiMenuUI] 借りられるフォントが見つかりませんでした。日本語が豆腐になります");
            return null;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rt, Vector2 size)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
        }
    }
}
