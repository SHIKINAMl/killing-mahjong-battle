using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// チュートリアル冒頭の契約書で、「私＿＿＿」の下線の上に名前を書いてもらう入力欄（2026-10-20）。
    ///
    /// 契約書は1枚絵（Assets/Sprites/Tutorial/contract_paper.png、400x300 を 800x600 に2倍で表示）。
    /// 下線は絵の (125〜290, 262) あたり。800x600 では x=250〜580、上から 524 で、真ん中からは (+15, -224)。
    /// 欄はそこから 4 上げて (+15, -220) に置く（2026-10-22、「私」の字と下端をそろえる。-224 だと名前が「私」より 4 ドット下がっていた）。
    /// さらに (+15, -216)・高さ 26 にした（2026-10-23 プランナー指摘「名前入力欄もうちょい上にして、軸線と被ってる」）。
    /// 実際の下線は真ん中から -232 あたりで、高さ 34 の欄の下端（-237）が線にかかっていた。字の下端は「私」とほぼそろったまま
    ///
    /// **シーンに置いてある物が正**（<see cref="SceneFirst"/>）。ここの数字は、シーンに無かったときに作るための控え。
    /// Web 版で日本語を打てるように、<see cref="WebImeInput"/> も同じ物に付ける。
    /// 押したときの動き（入力が変わったとき）は <see cref="KillingMahjong.Managers.OpeningSequenceManager"/> がつなぐ。
    /// </summary>
    public static class ContractNameField
    {
        public const string ObjectName = "NameInput";

        private static readonly Vector2 FieldPosition = new Vector2(15f, -216f);
        private static readonly Vector2 FieldSize = new Vector2(320f, 26f);
        private const float FontSize = 22f;
        private static readonly Color InkColor = new Color32(58, 36, 26, 255);
        private static readonly Color PlaceholderColor = new Color32(58, 36, 26, 110);

        /// <summary>契約書（<paramref name="paper"/>）の子にある入力欄を返す。無ければ作る。</summary>
        public static TMP_InputField Ensure(Transform paper, TMP_FontAsset font)
        {
            return Ensure(paper, font, FieldPosition);
        }

        /// <summary>
        /// 置き場所を変えて使う（対局の前に名前を聞く <see cref="MatchNamePromptUI"/>。2026-10-23）。
        /// <paramref name="position"/> は作るときだけ使う。
        /// </summary>
        public static TMP_InputField Ensure(Transform paper, TMP_FontAsset font, Vector2 position)
        {
            var fieldObj = SceneFirst.Child(paper, ObjectName, out bool made,
                typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(WebImeInput));
            var input = fieldObj.GetComponent<TMP_InputField>();
            if (!made) return input;

            var rt = fieldObj.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = FieldSize;
            rt.anchoredPosition = position;

            // 下線は絵に描いてあるので、入力欄の背景は透明にする（クリックは受ける）
            fieldObj.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);

            var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(fieldObj.transform, false);
            var art = area.GetComponent<RectTransform>();
            art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(6f, 0f); art.offsetMax = new Vector2(-6f, 0f);

            var placeholder = MakeText(area.transform, "Placeholder", font, PlaceholderColor);
            placeholder.text = "名前を書く";
            placeholder.fontStyle = FontStyles.Normal;
            var text = MakeText(area.transform, "Text", font, InkColor);

            input.textViewport = art;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = KillingMahjong.Core.PlayerProfile.MaxNameLength;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = InkColor;
            input.customCaretColor = true;
            input.selectionColor = new Color32(200, 60, 60, 90);
            return input;
        }

        private static TextMeshProUGUI MakeText(Transform parent, string name, TMP_FontAsset font, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = FontSize;
            t.color = color;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }
    }
}
