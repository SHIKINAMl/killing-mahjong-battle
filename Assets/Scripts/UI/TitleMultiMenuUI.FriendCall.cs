using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    /// <summary>
    /// メインメニューの電話から開く、フレンドマッチの画面（2026-11-23 仮案スレッド、プランナーの構図
    /// 「FightwithFriendUi.aseprite」。2026-11-24 ユーザー指示で、まずこの画面へ遷移するようにした）。
    ///
    ///   ・「真名を告げる」: 黒い欄にフレンドマッチのコードを打ち、ENTER か「真名を告げる」を押すと部屋を探して入る
    ///   ・「条件を整える」: 部屋の設定を決める画面へ。そこに対戦用のコードが書いてある
    ///
    /// **見た目はプランナーの仮。** 背景はアセプライトの「sannkou（参考）」層、文字と欄は同じファイルの層を
    /// 切り出しただけ（Resources/UI/FriendMatch/）。絵が来たら画像を差し替える。
    /// 配置は元の 400x300 の絵の座標を 2倍（800x600 基準）にしたもの。
    ///
    /// 「条件を整える」の先は、ルームマッチング画面（TitleMultiMenuUI.RoomMatching.cs）。
    ///
    /// タイトル・部屋シーンの「対局する」→「フレンドマッチ」は、今までの文字のメニュー（FriendPanel）のまま。
    /// </summary>
    public partial class TitleMultiMenuUI
    {
        private const string FriendCallResourceDir = "UI/FriendMatch/";

        // ---- 友達対戦の背景（2026-11-28）----
        //
        // プランナー（11/27 スレッド「友達対戦画面、ライティングなし」）:
        // 「メインメニュー画面を背景絵で使い回せんかなと思案中。UIとか全部消したメインメニュー背景を表示 →
        //   ぼかしを入れたあと半透明の黒い膜をかける → この画像（電話と卓の絵）を上から置く」
        // - Backdrop: 電話を押した瞬間のメインメニュー（字とボタンを消した部屋）を写し取ってぼかした絵（MainMenuUI → ScreenBlurCapture）
        // - Film: 半透明の黒い膜
        // - Art: プランナーの FightwithFriendUi.png。書き出しの背景が白で塗られていたので、白い字以外の白だけを透明にした
        //   （絵の色は変えていない。作り方は km-docs/tools/friend_call_art/key_art.py）
        // - 押せる字（真名を告げる・条件を整える）は、同じ絵の層を切り出した物を同じ位置に重ねてある（見た目は変わらない）

        /// <summary>黒い膜の濃さ。</summary>
        private static readonly Color FriendCallFilmColor = new Color(0f, 0f, 0f, 0.5f);

        private RawImage _friendCallBackdrop;
        private Texture _friendCallBackdropDefault;
        /// <summary>開いている間だけ持つ、ぼかした部屋の絵（閉じたら捨てる）。</summary>
        private Texture2D _friendCallBlur;

        /// <summary>背景に敷く絵を決める。null なら置いてある控えの絵に戻す。渡した絵は閉じるときに捨てる。</summary>
        private void SetFriendCallBackdrop(Texture2D blurred)
        {
            ReleaseFriendCallBlur();
            _friendCallBlur = blurred;
            if (_friendCallBackdrop != null)
            {
                _friendCallBackdrop.texture = blurred != null ? blurred : _friendCallBackdropDefault;
                _friendCallBackdrop.color = Color.white;
            }
        }

        private void ReleaseFriendCallBlur()
        {
            if (_friendCallBlur == null) return;
            if (_friendCallBackdrop != null && _friendCallBackdrop.texture == _friendCallBlur)
                _friendCallBackdrop.texture = _friendCallBackdropDefault;
            Destroy(_friendCallBlur);
            _friendCallBlur = null;
        }

        private GameObject _friendCall;
        private GameObject _friendCallMain;
        private GameObject _friendCallConditions;
        private TMP_InputField _callCodeInput;
        private TMP_Text _callError;
        private TMP_Text _callStatus;

        /// <summary>いま合言葉を打っている欄（今までの入力画面か、フレンドマッチの画面か）。</summary>
        private TMP_InputField ActiveCodeInput =>
            IsRoomMatchingShown ? null
            : _friendCall != null && _friendCall.activeSelf ? _callCodeInput : _passwordInput;
        private TMP_Text ActiveCodeError =>
            IsRoomMatchingShown ? _roomStatus
            : _friendCall != null && _friendCall.activeSelf ? _callError : _passwordError;
        private TMP_Text ActiveCodeStatus =>
            IsRoomMatchingShown ? _roomStatus
            : _friendCall != null && _friendCall.activeSelf ? _callStatus : _passwordStatus;
        private bool IsRoomMatchingShown => _friendCallConditions != null && _friendCallConditions.activeInHierarchy;

        private void BuildFriendCallPanel()
        {
            _friendCall = SceneFirst.Child(_root.transform, "FriendCall", out bool madeCall, typeof(RectTransform));
            if (madeCall) Stretch(_friendCall.GetComponent<RectTransform>());

            // 背景。メインメニューから開くときは、開く直前の部屋をぼかした絵に差し替える（OpenFriend の backdrop）。
            // 置いてある絵（参考の写真）は、ぼかした絵が無いときの控え
            var backdrop = SceneFirst.Child(_friendCall.transform, "Backdrop", out bool madeBackdrop,
                typeof(RectTransform), typeof(RawImage));
            if (madeBackdrop)
            {
                Stretch(backdrop.GetComponent<RectTransform>());
                var raw = backdrop.GetComponent<RawImage>();
                raw.texture = Resources.Load<Texture2D>(FriendCallResourceDir + "FriendCallBackdrop");
                raw.raycastTarget = true; // 後ろのメニューを押させない
            }
            _friendCallBackdrop = backdrop.GetComponent<RawImage>();
            _friendCallBackdropDefault = _friendCallBackdrop.texture;

            // 半透明の黒い膜と、電話の絵（2026-11-27 プランナーの案。下の「友達対戦の背景」）
            var film = SceneFirst.Child(_friendCall.transform, "Film", out bool madeFilm, typeof(RectTransform), typeof(Image));
            if (madeFilm)
            {
                Stretch(film.GetComponent<RectTransform>());
                var image = film.GetComponent<Image>();
                image.color = FriendCallFilmColor;
                image.raycastTarget = false;
                film.transform.SetSiblingIndex(backdrop.transform.GetSiblingIndex() + 1);
            }
            var art = SceneFirst.Child(_friendCall.transform, "Art", out bool madeArt, typeof(RectTransform), typeof(RawImage));
            if (madeArt)
            {
                Stretch(art.GetComponent<RectTransform>());
                var raw = art.GetComponent<RawImage>();
                raw.texture = Resources.Load<Texture2D>(FriendCallResourceDir + "FriendCallArt");
                raw.raycastTarget = false;
                art.transform.SetSiblingIndex(film.transform.GetSiblingIndex() + 1);
            }

            BuildFriendCallMain();
            BuildRoomMatching();

            _friendCall.SetActive(false);
        }

        private void BuildFriendCallMain()
        {
            _friendCallMain = SceneFirst.Child(_friendCall.transform, "Call", out bool made, typeof(RectTransform));
            if (made) Stretch(_friendCallMain.GetComponent<RectTransform>());
            var parent = _friendCallMain.transform;

            // 「フレンドマッチ参加／真名を告げる」。押すとコードで部屋を探す
            CreateArtButton(parent, "TruthName", "FriendCallTitle", new Vector2(-115f, 150f), new Vector2(318f, 96f),
                OnPasswordSubmit);

            // コードの欄（黒い四角）
            _callCodeInput = CreateCodeBox(parent, "CodeInput", new Vector2(-116f, 67f), editable: true)
                .GetComponent<TMP_InputField>();
            _callCodeInput.onSubmit.RemoveAllListeners();
            _callCodeInput.onSubmit.AddListener(_ => OnPasswordSubmit());

            _callError = CreateSmallText(parent, "Error", new Vector2(-116f, 28f), ErrorText);
            _callStatus = CreateSmallText(parent, "Status", new Vector2(-116f, 28f), MenuText);

            // 「条件を整える」
            CreateArtButton(parent, "Conditions", "FriendCallConditions", new Vector2(215f, -66f), new Vector2(214f, 44f),
                ShowFriendCallConditions);

            PlaceBackItem(CreateMenuItem(parent, "もどる", 0f, BackFromFriendPanel));
        }

        /// <summary>画面左下の「もどる」。作ったときだけ位置を決める（置いてあれば動かさない）。</summary>
        private static void PlaceBackItem(GameObject item)
        {
            var rt = item.GetComponent<RectTransform>();
            if (rt.anchoredPosition == Vector2.zero) rt.anchoredPosition = new Vector2(-230f, -250f);
        }

        /// <summary>プランナーの絵の文字をそのまま押せるようにする。</summary>
        private void CreateArtButton(Transform parent, string name, string texture, Vector2 pos, Vector2 size,
            System.Action onClick)
        {
            var obj = SceneFirst.Child(parent, name, out bool created, typeof(RectTransform), typeof(RawImage), typeof(Button));
            if (created)
            {
                var rt = obj.GetComponent<RectTransform>();
                Center(rt, size);
                rt.anchoredPosition = pos;
                var raw = obj.GetComponent<RawImage>();
                raw.texture = Resources.Load<Texture2D>(FriendCallResourceDir + texture);
                raw.raycastTarget = true;
                obj.GetComponent<Button>().targetGraphic = raw;
            }
            UIInteractionFeedback.Attach(obj);
            var button = obj.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());
        }

        /// <summary>黒い四角の欄。<paramref name="editable"/> なら打てる欄（合言葉と同じく英数字5文字）。</summary>
        private GameObject CreateCodeBox(Transform parent, string name, Vector2 pos, bool editable)
        {
            var types = editable
                ? new[] { typeof(RectTransform), typeof(RawImage), typeof(TMP_InputField) }
                : new[] { typeof(RectTransform), typeof(RawImage) };
            var obj = SceneFirst.Child(parent, name, out bool created, types);
            if (!created) return obj;

            var rt = obj.GetComponent<RectTransform>();
            Center(rt, new Vector2(304f, 54f));
            rt.anchoredPosition = pos;
            var raw = obj.GetComponent<RawImage>();
            raw.texture = Resources.Load<Texture2D>(FriendCallResourceDir + "FriendCallCodeBox");
            raw.raycastTarget = editable;

            var textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(obj.transform, false);
            Stretch(textObj.GetComponent<RectTransform>());
            var text = textObj.GetComponent<TextMeshProUGUI>();
            ApplyFont(text);
            text.fontSize = LabelFontSize * 1.2f;
            text.color = MenuText;
            text.alignment = TextAlignmentOptions.Center;
            text.characterSpacing = 24f;
            text.raycastTarget = false;

            if (editable)
            {
                var input = obj.GetComponent<TMP_InputField>();
                input.targetGraphic = raw;
                input.textViewport = textObj.GetComponent<RectTransform>();
                input.textComponent = text;
                input.characterLimit = MatchJoinRequest.PasswordLength;
                input.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
                input.caretColor = MenuText;
                input.customCaretColor = true;
                input.selectionColor = new Color(MenuMarker.r, MenuMarker.g, MenuMarker.b, 0.5f);
            }
            return obj;
        }

        private TMP_Text CreateSmallText(Transform parent, string name, Vector2 pos, Color color)
        {
            var obj = SceneFirst.Child(parent, name, out bool created, typeof(RectTransform), typeof(TextMeshProUGUI));
            var tmp = obj.GetComponent<TextMeshProUGUI>();
            if (created)
            {
                var rt = obj.GetComponent<RectTransform>();
                Center(rt, new Vector2(304f, 24f));
                rt.anchoredPosition = pos;
                ApplyFont(tmp);
                tmp.fontSize = LabelFontSize * 0.7f;
                tmp.color = color;
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.raycastTarget = false;
            }
            tmp.text = "";
            return tmp;
        }

        // ------------------------------------------------------------------

        private void ShowFriendCallMain()
        {
            if (_isCheckingRoom || _friendCall == null) return;
            HideMenuPanels();
            _friendCall.SetActive(true);
            _friendCallMain.SetActive(true);
            _friendCallConditions.SetActive(false);

            SetPasswordError("");
            SetStatus("");
            _callCodeInput.text = "";
            _callCodeInput.Select();
            _callCodeInput.ActivateInputField();
        }

        private void ShowFriendCallConditions()
        {
            if (_isCheckingRoom || _friendCall == null) return;
            _friendCallMain.SetActive(false);
            _friendCallConditions.SetActive(true);
            ShowRoomMatching();
        }

        /// <summary>文字のメニュー（最初の画面・フレンド・合言葉・CPU）をすべて伏せる。</summary>
        private void HideMenuPanels()
        {
            if (_modePanel != null) _modePanel.SetActive(false);
            if (_friendPanel != null) _friendPanel.SetActive(false);
            if (_passwordPanel != null) _passwordPanel.SetActive(false);
            if (_cpuPanel != null) _cpuPanel.SetActive(false);
        }

        private void HideFriendCall()
        {
            if (_friendCall != null) _friendCall.SetActive(false);
            ReleaseFriendCallBlur();
        }
    }
}
