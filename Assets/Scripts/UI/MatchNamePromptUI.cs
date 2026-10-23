using System;
using System.Collections;
using KillingMahjong.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 名前を書かないまま対局へ進もうとしたときに、先に名前を書いてもらう欄（2026-10-23 ユーザー指示、案A）。
    ///
    /// 名前を書く場所はチュートリアル冒頭の契約書だけで、タイトルの「ルールを知っている方：対局へ」や
    /// 「デバッグ部屋」から入ると名前なしで対局できていた（サーバーは名前を登録した人の対人戦しかランキングに入れない）。
    /// 対局を選ぶ画面を開く入口（<see cref="TitleUIManager"/> の OpenMatchMenu。タイトルと部屋で同じ）で、
    /// 名前が無ければこれを先に出す。
    ///
    /// 入力欄とクリックで打てるキーボードは契約書と同じ物（<see cref="ContractNameField"/>・<see cref="ContractKeyboardUI"/>）。
    /// 開くとキーボードも出す（展示はマウスだけで遊ぶ）。「決定」は名前が書いてあるときだけ押せる。
    /// 「もどる」で、対局を選ぶ画面へは進まずに元の画面へ戻る。
    ///
    /// **画面の部品はシーンに置く**（<see cref="SceneFirst"/>。タイトルシーンと部屋シーンの両方に焼き込む）。
    /// ここの数字は、シーンに無かったときに作るための控え。
    /// </summary>
    public sealed class MatchNamePromptUI : MonoBehaviour
    {
        public const string RootName = "MatchNamePrompt";

        private static readonly Vector2 PanelSize = new Vector2(520f, 250f);
        private static readonly Color DimColor = new Color32(8, 3, 8, 220);
        private static readonly Color InkColor = new Color32(58, 36, 26, 255);
        private static readonly Color NoteColor = new Color32(58, 36, 26, 170);
        private static readonly Color PaperColor = new Color32(236, 226, 204, 255);
        private static readonly Color OkColor = new Color32(150, 40, 48, 255);
        private static readonly Color ButtonInkColor = new Color32(250, 240, 228, 255);

        /// <summary>閉じるとき、キーボードが下がり切るのを待ってから隠す秒数</summary>
        private const float CloseSeconds = .25f;

        private RectTransform frame;
        private TMP_InputField input;
        private ContractKeyboardUI keyboard;
        private Button okButton;
        private Action onDone, onCancel;
        private Coroutine closing;

        public bool IsOpen => gameObject.activeSelf && closing == null;

        /// <summary>シーンの物を探して（無ければ作って）つなぐ。</summary>
        public static MatchNamePromptUI Ensure()
        {
            var root = SceneFirst.Root(RootName, out bool made, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            if (made)
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = Common.UISortingOrders.MatchNamePrompt;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(800f, 600f);
                // 後ろを暗くし、クリックも止める（後ろの「どこでも押せば開始」に届かせない）
                root.GetComponent<Image>().color = DimColor;
            }
            var prompt = root.GetComponent<MatchNamePromptUI>();
            if (prompt == null) prompt = root.AddComponent<MatchNamePromptUI>();
            prompt.Build();
            if (made) root.SetActive(false);
            return prompt;
        }

        /// <summary>**エディタ用。** シーンに置く（<c>SceneFirstBaker</c> から呼ぶ）。作った物の名前を返す</summary>
        public static string BakeForEditor()
        {
            Ensure();
            return SceneFirst.Report(nameof(MatchNamePromptUI));
        }

        private void Build()
        {
            var font = Resources.Load<TMP_FontAsset>("PixelMplus10_DynamicFixed");

            // 契約書の紙の色の板に、インクの色の縁。キーボードが出ると、この板ごと持ち上がる
            frame = Box(transform, "Frame", PanelSize + new Vector2(8f, 8f), Vector2.zero, InkColor);
            var panel = Box(frame, "Panel", PanelSize, Vector2.zero, PaperColor);

            Label(panel, "Heading", "対局の前に、名前を書いてください", new Vector2(0f, 82f), new Vector2(480f, 30f), 20f, InkColor, font);
            Label(panel, "Note", "ランキングにのる名前です", new Vector2(0f, 54f), new Vector2(480f, 20f), 14f, NoteColor, font);

            input = ContractNameField.Ensure(panel, font, new Vector2(0f, 4f));
            // 下線（契約書は紙の絵に描いてあるが、こちらは無地なので引く）
            Box(panel, "Underline", new Vector2(330f, 2f), new Vector2(0f, -12f), InkColor);

            var back = MakeButton(panel, "Back", "もどる", new Vector2(-120f, -82f), InkColor, font);
            okButton = MakeButton(panel, "Ok", "決定", new Vector2(120f, -82f), OkColor, font);
            back.onClick.AddListener(Cancel);
            okButton.onClick.AddListener(Confirm);

            input.onValueChanged.RemoveAllListeners();
            input.onValueChanged.AddListener(_ => UpdateOk());
            input.onSubmit.RemoveAllListeners();
            input.onSubmit.AddListener(_ => Confirm());

            keyboard = ContractKeyboardUI.Ensure(frame, font);
            keyboard.Bind(input, frame);
        }

        /// <summary>
        /// 出す。<paramref name="done"/> は名前を保存したあと、<paramref name="cancel"/> は「もどる」のあとに呼ぶ。
        /// </summary>
        public void Open(Action done, Action cancel)
        {
            onDone = done;
            onCancel = cancel;
            if (closing != null) { StopCoroutine(closing); closing = null; }
            gameObject.SetActive(true);
            input.interactable = true;
            input.text = "";
            UpdateOk();
            input.ActivateInputField();
            keyboard.Show();
        }

        private void UpdateOk()
        {
            if (okButton != null) okButton.interactable = PlayerProfile.Normalize(input.text).Length > 0;
        }

        private void Confirm()
        {
            if (!IsOpen || okButton == null || !okButton.interactable) return;
            if (!PlayerProfile.SetName(input.text)) return;
            var done = onDone;
            Close();
            done?.Invoke();
        }

        private void Cancel()
        {
            if (!IsOpen) return;
            var cancel = onCancel;
            Close();
            cancel?.Invoke();
        }

        /// <summary>キーボードを下げ（板も元の高さへ戻し）、下がり切ってから隠す</summary>
        private void Close()
        {
            onDone = onCancel = null;
            input.DeactivateInputField();
            input.interactable = false;
            keyboard.Hide(true);
            closing = StartCoroutine(HideAfterSlide());
        }

        private IEnumerator HideAfterSlide()
        {
            yield return new WaitForSecondsRealtime(CloseSeconds);
            closing = null;
            gameObject.SetActive(false);
        }

        private static RectTransform Box(Transform parent, string name, Vector2 size, Vector2 position, Color color)
        {
            var go = SceneFirst.Child(parent, name, out bool made, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            if (made)
            {
                rt.sizeDelta = size;
                rt.anchoredPosition = position;
                var image = go.GetComponent<Image>();
                image.color = color;
                image.raycastTarget = false;
            }
            return rt;
        }

        private static TMP_Text Label(Transform parent, string name, string text, Vector2 position, Vector2 size,
            float fontSize, Color color, TMP_FontAsset font)
        {
            var go = SceneFirst.Child(parent, name, out bool made, typeof(RectTransform), typeof(TextMeshProUGUI));
            var label = go.GetComponent<TextMeshProUGUI>();
            if (made)
            {
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = size;
                rt.anchoredPosition = position;
                if (font != null) label.font = font;
                label.fontSize = fontSize;
                label.color = color;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.raycastTarget = false;
                label.text = text;
            }
            return label;
        }

        private static Button MakeButton(Transform parent, string name, string text, Vector2 position, Color color,
            TMP_FontAsset font)
        {
            var go = SceneFirst.Child(parent, name, out bool made, typeof(RectTransform), typeof(Image), typeof(Button));
            var button = go.GetComponent<Button>();
            if (made)
            {
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(180f, 44f);
                rt.anchoredPosition = position;
                var image = go.GetComponent<Image>();
                image.color = color;
                button.targetGraphic = image;
                Label(go.transform, "Text", text, Vector2.zero, rt.sizeDelta, 20f, ButtonInkColor, font);
            }
            button.onClick.RemoveAllListeners();
            if (go.GetComponent<UIButtonHoverEffect>() == null) go.AddComponent<UIButtonHoverEffect>();
            return button;
        }
    }
}
