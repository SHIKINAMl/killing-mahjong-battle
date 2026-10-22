using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 契約書の名前の欄を押すと下から出る、画面のキーボード（2026-10-22）。
    /// ユーザー指示「文字を入力するのはできればマウス操作で完結させたい。アルファベットが全部書かれているものが
    /// 下から出て、クリックで入力できませんか」。
    ///
    /// A〜Z を順に並べ、大文字・小文字の切り替え、1文字けす、とじる を置く。押した字は入力欄（<see cref="TMP_InputField"/>）の
    /// 末尾に足すだけなので、名前の長さの上限や「次へ」が押せるかどうかは今までどおり入力欄のほうで決まる。
    /// キーボードで打つこともそのままできる。
    ///
    /// 契約書の右下のインク瓶（「次へ」）を隠さないよう左に寄せ、出ているあいだは名前の下線が見えるよう契約書を少し持ち上げる。
    ///
    /// **シーンに置いてある物が正**（<see cref="SceneFirst"/>）。ここの数字は、シーンに無かったときに作るための控え。
    /// </summary>
    public sealed class ContractKeyboardUI : MonoBehaviour
    {
        public const string ObjectName = "ContractKeyboard";

        private static readonly string[] LetterRows = { "ABCDEFGHI", "JKLMNOPQR", "STUVWXYZ" };
        private const float KeyWidth = 52f, KeyHeight = 40f, Gap = 6f, Padding = 10f;
        private const float PanelWidth = 9 * KeyWidth + 8 * Gap + Padding * 2;
        private const float PanelHeight = 4 * KeyHeight + 3 * Gap + Padding * 2;
        private const float PanelX = -112f;

        /// <summary>出ているあいだ、契約書を持ち上げる量。名前の欄（真ん中から -220）がキーボードの上に出る</summary>
        private const float PaperLift = 140f;
        private const float SlideSeconds = .22f;

        private static readonly Color PanelColor = new Color32(24, 14, 18, 238);
        private static readonly Color KeyColor = new Color32(236, 226, 204, 255);
        private static readonly Color InkColor = new Color32(58, 36, 26, 255);
        private static readonly Color FunctionKeyColor = new Color32(150, 40, 48, 255);
        private static readonly Color FunctionInkColor = new Color32(250, 240, 228, 255);

        private TMP_InputField input;
        private WebImeInput ime;
        private RectTransform paper;
        private RectTransform rect;
        private readonly TMP_Text[] letterLabels = new TMP_Text[26];
        private bool lowerCase;
        private bool shown;
        private Coroutine slide;

        public bool IsShown => shown;

        /// <summary><paramref name="paper"/>（契約書）と同じ親の下にあるキーボードを返す。無ければ作る。</summary>
        public static ContractKeyboardUI Ensure(RectTransform paper, TMP_FontAsset font)
        {
            var go = SceneFirst.Child(paper.parent, ObjectName, out bool made, typeof(RectTransform), typeof(Image),
                typeof(ContractKeyboardUI));
            var keyboard = go.GetComponent<ContractKeyboardUI>();
            if (made)
            {
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, 0f);
                rt.pivot = new Vector2(.5f, 0f);
                rt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
                rt.anchoredPosition = new Vector2(PanelX, -PanelHeight);
                go.GetComponent<Image>().color = PanelColor;
                go.transform.SetAsLastSibling();
            }
            keyboard.Build(font);
            if (made) go.SetActive(false);
            return keyboard;
        }

        private void Build(TMP_FontAsset font)
        {
            rect = (RectTransform)transform;
            float left = -PanelWidth * .5f + Padding + KeyWidth * .5f;
            float top = PanelHeight - Padding - KeyHeight * .5f;
            int index = 0;
            for (int row = 0; row < LetterRows.Length; row++)
            {
                for (int col = 0; col < LetterRows[row].Length; col++)
                {
                    char letter = LetterRows[row][col];
                    var pos = new Vector2(left + col * (KeyWidth + Gap), top - row * (KeyHeight + Gap));
                    var key = Key("Key_" + letter, letter.ToString(), pos, KeyWidth, false, font);
                    letterLabels[index++] = key.GetComponentInChildren<TMP_Text>(true);
                    int letterIndex = index - 1;
                    key.onClick.AddListener(() => Type(letterIndex));
                }
            }
            // 3段目の右端（Z の隣）に大文字・小文字の切り替え
            var shift = Key("Key_Shift", "Aa", new Vector2(left + 8 * (KeyWidth + Gap), top - 2 * (KeyHeight + Gap)),
                KeyWidth, true, font);
            shift.onClick.AddListener(ToggleCase);

            float wide = (PanelWidth - Padding * 2 - Gap) * .5f;
            float y = top - 3 * (KeyHeight + Gap);
            var back = Key("Key_Backspace", "1文字けす", new Vector2(-PanelWidth * .5f + Padding + wide * .5f, y), wide, true, font);
            back.onClick.AddListener(Backspace);
            var close = Key("Key_Close", "とじる", new Vector2(PanelWidth * .5f - Padding - wide * .5f, y), wide, true, font);
            close.onClick.AddListener(() => Hide(true));
            ApplyCase();
        }

        /// <summary>キーを1つ探して（無ければ作って）返す。位置は下端の真ん中から。</summary>
        private Button Key(string name, string label, Vector2 position, float width, bool function, TMP_FontAsset font)
        {
            var go = SceneFirst.Child(transform, name, out bool made, typeof(RectTransform), typeof(Image), typeof(Button));
            var button = go.GetComponent<Button>();
            if (made)
            {
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, 0f);
                rt.sizeDelta = new Vector2(width, KeyHeight);
                rt.anchoredPosition = position;
                var image = go.GetComponent<Image>();
                image.color = function ? FunctionKeyColor : KeyColor;
                button.targetGraphic = image;

                var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(go.transform, false);
                var trt = (RectTransform)textGo.transform;
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = trt.offsetMax = Vector2.zero;
                var text = textGo.GetComponent<TextMeshProUGUI>();
                if (font != null) text.font = font;
                text.text = label;
                text.fontSize = function ? 18f : 24f;
                text.color = function ? FunctionInkColor : InkColor;
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.raycastTarget = false;
            }
            button.onClick.RemoveAllListeners();
            if (go.GetComponent<UIButtonHoverEffect>() == null) go.AddComponent<UIButtonHoverEffect>();
            return button;
        }

        /// <summary>名前の欄と契約書につなぐ。欄を押すとキーボードが出る。</summary>
        public void Bind(TMP_InputField field, RectTransform contractPaper)
        {
            input = field;
            paper = contractPaper;
            ime = field.GetComponent<WebImeInput>();
            var trigger = field.GetComponent<EventTrigger>();
            if (trigger == null) trigger = field.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();
            // 押した瞬間に出す。Web 版では離すときには HTML の入力欄が上にあり、Unity に届かない
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            entry.callback.AddListener(_ => Show());
            trigger.triggers.Add(entry);
        }

        public void Show()
        {
            if (shown || input == null || !input.interactable) return;
            shown = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            // Web 版の日本語入力の欄は、契約書を持ち上げる前の位置に重なっている。
            // 残すとキーボードの上にかぶって押せなくなるので一度閉じ、持ち上げ終えてから新しい位置で開き直す
            bool reopenIme = ime != null && ime.IsOpen;
            if (ime != null) ime.CloseOverlay();
            StartSlide(true, true, reopenIme);
        }

        /// <summary>しまう。<paramref name="lowerPaper"/> が false なら契約書は動かさない（契約書ごと下げるとき）。</summary>
        public void Hide(bool lowerPaper)
        {
            if (!shown) return;
            shown = false;
            if (ime != null) ime.CloseOverlay();
            if (!gameObject.activeInHierarchy) return;
            StartSlide(false, lowerPaper, false);
        }

        private void StartSlide(bool show, bool movePaper, bool reopenIme)
        {
            if (slide != null) StopCoroutine(slide);
            slide = StartCoroutine(Slide(show, movePaper, reopenIme));
        }

        private IEnumerator Slide(bool show, bool movePaper, bool reopenIme)
        {
            float fromY = rect.anchoredPosition.y, toY = show ? 0f : -PanelHeight;
            float paperFrom = paper != null ? paper.anchoredPosition.y : 0f, paperTo = show ? PaperLift : 0f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / SlideSeconds)
            {
                float k = Mathf.SmoothStep(0f, 1f, t);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, Mathf.Lerp(fromY, toY, k));
                if (movePaper && paper != null)
                    paper.anchoredPosition = new Vector2(paper.anchoredPosition.x, Mathf.Lerp(paperFrom, paperTo, k));
                yield return null;
            }
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, toY);
            if (movePaper && paper != null) paper.anchoredPosition = new Vector2(paper.anchoredPosition.x, paperTo);
            slide = null;
            if (!show) gameObject.SetActive(false);
            else if (reopenIme && ime != null) ime.Reopen();
        }

        private void Type(int letterIndex)
        {
            if (input == null || !input.interactable) return;
            if (input.characterLimit > 0 && input.text.Length >= input.characterLimit) return;
            char c = (char)('A' + letterIndex);
            input.text += lowerCase ? char.ToLowerInvariant(c) : c;
            input.caretPosition = input.text.Length;
        }

        private void Backspace()
        {
            if (input == null || !input.interactable || input.text.Length == 0) return;
            input.text = input.text.Substring(0, input.text.Length - 1);
            input.caretPosition = input.text.Length;
        }

        private void ToggleCase()
        {
            lowerCase = !lowerCase;
            ApplyCase();
        }

        private void ApplyCase()
        {
            for (int i = 0; i < letterLabels.Length; i++)
            {
                if (letterLabels[i] == null) continue;
                char c = (char)('A' + i);
                letterLabels[i].text = (lowerCase ? char.ToLowerInvariant(c) : c).ToString();
            }
        }
    }
}
