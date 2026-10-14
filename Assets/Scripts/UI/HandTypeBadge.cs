using KillingMahjong.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 手の型の札（2026-10-15）。型の名前を、型ごとの一色の角丸の板で囲んだもの。
    ///
    /// ユーザーが Gemini のアイコンと見比べて「角丸の文字の札で」と決めた。
    /// 板は 8x8 の角を階段状に削った絵（`Resources/HandTypes/hand_type_badge_plate`）を9分割で伸ばし、
    /// 板の1点を画面の2単位にしてほかのドット絵と粗さを揃える。
    /// 文字はゲームのフォントで出す（絵に描き込むと、名前を変えるたびに描き直しになる）。
    ///
    /// 置き場所は使う側が決める。シーンに置く画面は <see cref="Ensure"/>（SceneFirst）で、
    /// その場で組み立てる画面（和了の表）は同じ関数をその場で呼ぶ。
    /// 再生中に変えるのは、出す型（色と文字）と表示の有無だけ。
    /// </summary>
    public sealed class HandTypeBadge : MonoBehaviour
    {
        [SerializeField] private Image plate;
        [SerializeField] private TextMeshProUGUI label;

        public HandArchetype.Info Current { get; private set; }

        /// <param name="parent">置く親</param>
        /// <param name="name">子の名前</param>
        /// <param name="fontSize">文字の大きさ</param>
        /// <param name="font">文字のフォント。null なら TMP の既定</param>
        /// <param name="fitSelf">
        /// 自分で大きさを文字に合わせるか。親に並べ方（LayoutGroup）があるときは false（親に任せる）
        /// </param>
        /// <param name="made">今回作ったか。作ったときだけ位置を決めればよい</param>
        public static HandTypeBadge Ensure(Transform parent, string name, float fontSize, TMP_FontAsset font,
            bool fitSelf, out bool made)
        {
            GameObject go = SceneFirst.Child(parent, name, out made, Components);
            var badge = go.GetComponent<HandTypeBadge>();
            if (made) badge.Build(fontSize, font, fitSelf);
            return badge;
        }

        /// <summary>
        /// その場で組み立てる画面（和了の表）用。シーンを探さずに作る
        /// （<see cref="SceneFirst"/> の「置き忘れ」の記録に混ぜないため）。
        /// </summary>
        public static HandTypeBadge Create(Transform parent, string name, float fontSize, TMP_FontAsset font, bool fitSelf)
        {
            var go = new GameObject(name, Components);
            go.transform.SetParent(parent, false);
            var badge = go.GetComponent<HandTypeBadge>();
            badge.Build(fontSize, font, fitSelf);
            return badge;
        }

        private static readonly System.Type[] Components =
            { typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(HandTypeBadge) };

        private void Build(float fontSize, TMP_FontAsset font, bool fitSelf)
        {
            plate = GetComponent<Image>();
            plate.sprite = Resources.Load<Sprite>("HandTypes/hand_type_badge_plate");
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = 0.5f;
            plate.raycastTarget = false;

            var layout = GetComponent<HorizontalLayoutGroup>();
            // 1ドット＝2単位の格子に合わせた余白
            int padX = Mathf.RoundToInt(fontSize * 0.45f / 2f) * 2;
            layout.padding = new RectOffset(padX, padX, 2, 2);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;

            if (fitSelf)
            {
                var fit = gameObject.AddComponent<ContentSizeFitter>();
                fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(transform, false);
            label = textGo.GetComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
        }

        /// <summary>型を出す。null なら隠す。</summary>
        public void Show(HandArchetype.Info info)
        {
            Current = info;
            gameObject.SetActive(info != null);
            if (info == null) return;
            if (plate != null) plate.color = info.Badge;
            if (label != null)
            {
                label.text = info.Name;
                label.color = info.BadgeInk;
            }
        }

        /// <summary>型に入らないもの（役一覧の「その他」）を、灰色の札で出す。</summary>
        public void ShowPlain(string text, Color plateColor, Color ink)
        {
            Current = null;
            gameObject.SetActive(true);
            if (plate != null) plate.color = plateColor;
            if (label != null)
            {
                label.text = text;
                label.color = ink;
            }
        }
    }
}
