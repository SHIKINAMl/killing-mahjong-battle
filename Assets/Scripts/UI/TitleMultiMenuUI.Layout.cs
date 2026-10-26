using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    public partial class TitleMultiMenuUI
    {
        private void Build()
        {
            // TMP の既定フォントは LiberationSans で、ひらがなが全部豆腐になる。
            // パスで決め打ちせず、画面にある文字からフォントを借りる
            _font = BorrowJapaneseFont();

            // **専用の Canvas を立てる。**
            // タイトルの既存 Canvas にぶら下げると、全画面の TitleScrim（右ほど濃くする幕）が
            // メニューの上に乗り、見えているのにクリックが幕に吸われて押せなくなる。
            bool madeRoot = false;
            if (_root == null) _root = SceneFirst.Root(RootName, out madeRoot,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (madeRoot)
            {
                var canvas = _root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = Common.UISortingOrders.TitleMenuOverlay;

                // このプロジェクトの UI は 800x600 基準（CanvasFixer と揃える）
                var scaler = _root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(800f, 600f);

                Stretch(_root.GetComponent<RectTransform>());
            }

            // 背後の暗幕。タイトルのボタンを押せないようにする役目も兼ねる
            var scrim = SceneFirst.Child(_root.transform, "Scrim", out bool madeScrim,
                typeof(RectTransform), typeof(Image));
            if (madeScrim)
            {
                Stretch(scrim.GetComponent<RectTransform>());
                var scrimImg = scrim.GetComponent<Image>();
                scrimImg.color = ScrimColor;
                scrimImg.raycastTarget = true;
            }

            BuildModePanel();
            BuildFriendPanel();
            BuildPasswordPanel();
            BuildCpuPanel();

            _lastBakeReport = SceneFirst.Report("TitleMultiMenuUI");
        }

        /// <summary>シーンに置くときの、メニューの根の名前。</summary>
        public const string RootName = "MultiMenu";

        private string _lastBakeReport = "";

        /// <summary>
        /// **エディタ用。** メニューの部品のうち、シーンに無い物だけを作って置く
        /// （`SceneFirstBaker` が再生していないときに呼ぶ）。
        /// 置いたあとは非表示にしておく（全画面の暗幕を持つので、出したままだと下の画面が見えない）。
        /// </summary>
        /// <returns>シーンに無くて作った物の名前。全部置いてあったら空文字</returns>
        public string BakeForEditor()
        {
            Build();
            if (_root != null)
            {
                // 絵の位置は文字の幅から決めるので、見えている状態で一度だけ計算する
                _root.SetActive(true);
                _modePanel.SetActive(true);
                PositionFriendIllustration();
                _root.SetActive(false);
            }
            return _lastBakeReport;
        }

        /// <summary>Prefabの編集では、別シーンの同名メニューを拾わず、指定の実体だけに配置する。</summary>
        public string BakeIntoEditorRoot(GameObject placedRoot)
        {
            if (Application.isPlaying || placedRoot == null) return "再生停止中の実体を指定してください。";
            _root = placedRoot;
            return BakeForEditor();
        }

        private void BuildModePanel()
        {
            _modePanel = SceneFirst.Child(_root.transform, "ModePanel", out bool madePanel, typeof(RectTransform));
            if (madePanel) Center(_modePanel.GetComponent<RectTransform>(), new Vector2(ItemWidth, ItemHeight * 5f));

            CreateHeading(_modePanel.transform, "対局する", ItemHeight * 2f);

            CreateMenuItem(_modePanel.transform, "野良マッチ", ItemHeight * 0.9f, OnPublicSelected);
            var friendItem = CreateMenuItem(_modePanel.transform, "フレンドマッチ", -ItemHeight * 0.2f, ShowFriendPanel);
            CreateFriendIllustration(friendItem);
            CreateMenuItem(_modePanel.transform, "CPUと対戦", -ItemHeight * 1.3f, ShowCpuPanel);
            CreateMenuItem(_modePanel.transform, "もどる", -ItemHeight * 2.6f, Close);
        }

        private void CreateFriendIllustration(GameObject friendItem)
        {
            var texture = Resources.Load<Texture2D>(FriendIllustrationResourcePath);
            if (texture == null) return;

            _friendMatchLabel = friendItem.GetComponentInChildren<TMP_Text>();
            var obj = SceneFirst.Child(friendItem.transform, "MatchIllustration", out bool created,
                typeof(RectTransform), typeof(RawImage));
            _friendIllustration = obj.GetComponent<RectTransform>();

            // **置いてある絵は動かさない。** 位置はシーン（Prefab）で決める。
            // 文字の幅から位置を計算するのは、ここで作ったときだけ
            _positionIllustrationFromText = created;
            if (!created) return;

            Center(_friendIllustration, new Vector2(texture.width, texture.height) * FriendIllustrationScale);

            var image = obj.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
        }

        /// <summary>絵の位置を、文字の幅から計算して決めるか。シーンに置いてあった絵なら false。</summary>
        private bool _positionIllustrationFromText;

        private void PositionFriendIllustration()
        {
            if (!_positionIllustrationFromText) return;
            if (_friendIllustration == null || _friendMatchLabel == null) return;

            Canvas.ForceUpdateCanvases();
            _friendMatchLabel.ForceMeshUpdate();
            var bounds = _friendMatchLabel.textBounds;
            var friendRect = (RectTransform)_friendIllustration.parent;
            float textRight = friendRect.InverseTransformPoint(_friendMatchLabel.transform.TransformPoint(
                new Vector3(bounds.max.x, bounds.center.y, 0f))).x;

            // 元の800x600画像の透明な余白は残し、見える絵の左端・中央で位置を合わせる。
            // 承認済み配置: 文字の右端から20px、絵の高さ47.52px。クリック領域は増やさない。
            _friendIllustration.anchoredPosition = new Vector2(
                textRight + FriendIllustrationGap + _friendIllustration.sizeDelta.x * 0.5f - 121f * FriendIllustrationScale,
                _friendIllustration.sizeDelta.y * 0.5f - 255f * FriendIllustrationScale);
        }

        private void BuildFriendPanel()
        {
            _friendPanel = SceneFirst.Child(_root.transform, "FriendPanel", out bool madePanel, typeof(RectTransform));
            if (madePanel) Center(_friendPanel.GetComponent<RectTransform>(), new Vector2(ItemWidth, ItemHeight * 5f));

            CreateHeading(_friendPanel.transform, "フレンドマッチ", ItemHeight * 2f);
            CreateMenuItem(_friendPanel.transform, "部屋を作る", ItemHeight * 0.9f, OnPrivateCreateSelected);
            CreateMenuItem(_friendPanel.transform, "あいことばで入る", -ItemHeight * 0.2f, ShowPasswordPanel);
            CreateMenuItem(_friendPanel.transform, "もどる", -ItemHeight * 2.1f, ShowModePanel);

            _friendPanel.SetActive(false);
        }

        private void BuildPasswordPanel()
        {
            _passwordPanel = SceneFirst.Child(_root.transform, "PasswordPanel", out bool madePanel, typeof(RectTransform));
            if (madePanel) Center(_passwordPanel.GetComponent<RectTransform>(), new Vector2(ItemWidth, ItemHeight * 5f));

            CreateHeading(_passwordPanel.transform, "あいことばを入力", ItemHeight * 2f);

            var fieldObj = SceneFirst.Child(_passwordPanel.transform, "PasswordInput", out bool madeField,
                typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            _passwordInput = fieldObj.GetComponent<TMP_InputField>();
            if (madeField)
            {
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

                _passwordInput.textViewport = textArea.GetComponent<RectTransform>();
                _passwordInput.textComponent = textComp;
                _passwordInput.characterLimit = MatchJoinRequest.PasswordLength;
                // 合言葉は英大文字と数字だけ。小文字で打たれても MatchJoinRequest 側で直す
                _passwordInput.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
            }
            // 置いてある入力欄は使い回すので、前につないだ物を外してからつなぐ
            _passwordInput.onSubmit.RemoveAllListeners();
            _passwordInput.onSubmit.AddListener(_ => OnPasswordSubmit());

            var errorObj = SceneFirst.Child(_passwordPanel.transform, "Error", out bool madeError,
                typeof(RectTransform), typeof(TextMeshProUGUI));
            _passwordError = errorObj.GetComponent<TextMeshProUGUI>();
            if (madeError)
            {
                var ert = errorObj.GetComponent<RectTransform>();
                Center(ert, new Vector2(ItemWidth, ItemHeight * 0.7f));
                ert.anchoredPosition = new Vector2(0f, ItemHeight * 0.05f);
                ApplyFont(_passwordError);
                _passwordError.fontSize = LabelFontSize * 0.7f;
                _passwordError.color = ErrorText;
                _passwordError.alignment = TextAlignmentOptions.Center;
            }
            _passwordError.text = "";

            var statusObj = SceneFirst.Child(_passwordPanel.transform, "Status", out bool madeStatus,
                typeof(RectTransform), typeof(TextMeshProUGUI));
            _passwordStatus = statusObj.GetComponent<TextMeshProUGUI>();
            if (madeStatus)
            {
                var srt = statusObj.GetComponent<RectTransform>();
                Center(srt, new Vector2(ItemWidth, ItemHeight * 0.7f));
                srt.anchoredPosition = new Vector2(0f, ItemHeight * 1.35f);
                ApplyFont(_passwordStatus);
                _passwordStatus.fontSize = LabelFontSize * 0.7f;
                _passwordStatus.color = MenuText;
                _passwordStatus.alignment = TextAlignmentOptions.Center;
                _passwordStatus.raycastTarget = false;
            }
            _passwordStatus.text = "";

            CreateMenuItem(_passwordPanel.transform, "決定", -ItemHeight * 0.8f, OnPasswordSubmit);
            CreateMenuItem(_passwordPanel.transform, "もどる", -ItemHeight * 1.9f, ShowFriendPanel);

            _passwordPanel.SetActive(false);
        }

        // ------------------------------------------------------------------

        private void CreateHeading(Transform parent, string text, float y)
        {
            var obj = SceneFirst.Child(parent, "Heading", out bool created,
                typeof(RectTransform), typeof(TextMeshProUGUI));
            if (!created) return;

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

        private GameObject CreateMenuItem(Transform parent, string label, float y, Action onClick)
        {
            var obj = SceneFirst.Child(parent, label, out bool created,
                typeof(RectTransform), typeof(Image), typeof(Button));
            if (!created)
            {
                // 置いてあるボタンは使い回すので、前につないだ物を外してからつなぐ
                UIInteractionFeedback.Attach(obj);
                var placed = obj.GetComponent<Button>();
                placed.onClick.RemoveAllListeners();
                placed.onClick.AddListener(() => onClick?.Invoke());
                return obj;
            }

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

            UIInteractionFeedback.Attach(obj);
            obj.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
            return obj;
        }

        private void ApplyFont(TMP_Text tmp)
        {
            if (_font != null) tmp.font = _font;
        }

        /// <summary>
        /// 選択肢や案内文には、読みやすい本文用のフォントを使う。
        /// </summary>
        private static TMP_FontAsset BorrowJapaneseFont()
        {
            return GameUiTypography.Body;
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
