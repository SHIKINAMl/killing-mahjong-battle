using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    /// <summary>
    /// チュートリアルで「いま話している場所」を面で示す（2026-09-24）。
    ///
    /// **矢印だけでは何を指しているのか分からない。** 点数計算表の説明で
    /// 「素点」と「倍率」の2行を指したとき、矢印は行の横幅の真ん中に立つので、
    /// 見出しと数字のあいだの空白を指してしまっていた（実機で確認）。
    /// 点で指す代わりに、**読ませたい範囲そのものを塗る／囲む**。
    ///
    ///   <see cref="Style.Band"/>  … 表の中の行を指すとき。半透明の金色を敷く
    ///   <see cref="Style.Frame"/> … 表ぜんたいのように、外周を囲えるものを指すとき
    ///
    /// シーンには置かず、必要になったときに自分で作る。クリックは食わない
    /// （GraphicRaycaster を付けず、Image も raycastTarget を切ってある）。
    /// 説明しながらセリフを送らせるので、ここで操作を止めてはいけない。
    /// </summary>
    public class TutorialHighlightUI : MonoBehaviour
    {
        public enum Style
        {
            /// <summary>範囲を半透明の金色で塗る。表の中の行を指すとき。</summary>
            Band,

            /// <summary>範囲を金色の枠で囲む。表ぜんたいを指すとき。</summary>
            Frame,
        }

        /// <summary>対象より少し外まで見せる余白。行の文字が枠線に触れないように。</summary>
        private const float Padding = 5f;

        /// <summary>
        /// 枠（<see cref="Style.Frame"/>）のときの余白。かぎ形が太いぶん、帯のときより外へ出す。
        /// 1ドット＝2UI単位なので 8 でドット4つぶん。
        /// </summary>
        private const float FramePadding = 8f;

        /// <summary>
        /// 帯の濃さ。**このプロジェクトは Linear カラースペースなので、数字より濃く出る。**
        /// 見た目で 1/4 くらいに見せたいなら 0.25 ではなく 0.11 前後になる
        /// （実機で 0.26 を入れたら実測 0.42 相当の濃さになり、下の数字が読めなくなった）。
        /// </summary>
        private const float BandAlpha = 0.11f;

        /// <summary>明滅の深さ。基準の明るさに対する割合。</summary>
        private const float PulseDepth = 0.35f;

        private const float PulseSpeed = 3.0f;

        /// <summary>強調の色。セリフの強調（HighlightOpen）と同じ金色にそろえてある。</summary>
        private static readonly Color Gold = new Color32(0xFF, 0xD7, 0x00, 0xFF);

        private static TutorialHighlightUI _instance;

        /// <summary>
        /// 2か所目（2026-10-08）。ボルテージのゲージのように、**離れた2つを同時に示す**ときに使う。
        /// 1つの枠で両方を囲むと、あいだの盤面まで囲ってしまう。
        /// </summary>
        private static TutorialHighlightUI _second;

        private RectTransform _area;
        private Image _band;

        /// <summary>
        /// 枠の絵（2026-10-08 に作り直した）。以前は4本の線（Image）を明滅させていた。
        /// 形と動きは <see cref="TutorialHighlightFrameGraphic"/> が持っている。
        /// </summary>
        private TutorialHighlightFrameGraphic _frame;

        private RectTransform _target;
        private Canvas _canvas;
        private Style _style;

        /// <summary>
        /// 対象の範囲を示し始める。すでに出ていれば対象を差し替える。
        ///
        /// `sortingOrder` は、**資料のように手前に重なる画面の中を指すとき**に渡す。
        /// 既定（63）のままだと、その画面の下に潜って見えない。
        /// </summary>
        public static void Show(RectTransform target, Style style, int? sortingOrder = null)
        {
            if (target == null) { HideCurrent(); return; }
            // 対象を差し替えるときは、前の「2か所目」を残さない
            if (_second != null) _second.HideInternal();
            if (_instance == null) _instance = Create();
            _instance._canvas.sortingOrder = sortingOrder ?? UISortingOrders.TutorialHighlight;
            _instance.ShowInternal(target, style);
        }

        /// <summary>
        /// 2か所目を示す。先に <see cref="Show"/> で1か所目を出してから呼ぶこと
        /// （<see cref="Show"/> は2か所目を消すので、順番を逆にすると消える）。
        /// </summary>
        public static void ShowSecond(RectTransform target, Style style)
        {
            if (target == null) { if (_second != null) _second.HideInternal(); return; }
            if (_second == null) _second = Create();
            _second._canvas.sortingOrder = UISortingOrders.TutorialHighlight;
            _second.ShowInternal(target, style);
        }

        /// <summary>出ていれば消す（2か所目も）。出ていなければ何もしない。</summary>
        public static void HideCurrent()
        {
            if (_instance != null) _instance.HideInternal();
            if (_second != null) _second.HideInternal();
        }

        private static TutorialHighlightUI Create()
        {
            var go = new GameObject("TutorialHighlightCanvas", typeof(RectTransform));
            var self = go.AddComponent<TutorialHighlightUI>();
            self.Build();
            return self;
        }

        private void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = UISortingOrders.TutorialHighlight;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);
            scaler.matchWidthOrHeight = 0f;

            var areaGo = new GameObject("Area", typeof(RectTransform));
            areaGo.transform.SetParent(transform, false);
            _area = areaGo.GetComponent<RectTransform>();
            _area.anchorMin = _area.anchorMax = new Vector2(0.5f, 0.5f);
            _area.pivot = new Vector2(0.5f, 0.5f);

            _band = MakeImage(_area, "Band");
            _band.rectTransform.anchorMin = Vector2.zero;
            _band.rectTransform.anchorMax = Vector2.one;
            _band.rectTransform.offsetMin = Vector2.zero;
            _band.rectTransform.offsetMax = Vector2.zero;

            // CanvasRenderer は自分で付ける。new GameObject 経由だと RequireComponent が効かず、
            // 絵が一切出ない（PerspectiveSkillEffect などと同じ）
            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TutorialHighlightFrameGraphic));
            frameGo.transform.SetParent(_area, false);
            var frameRect = (RectTransform)frameGo.transform;
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;
            _frame = frameGo.GetComponent<TutorialHighlightFrameGraphic>();
            // 説明を読みながらセリフを送るので、クリックは絶対に食わせない
            _frame.raycastTarget = false;

            gameObject.SetActive(false);
        }

        private static Image MakeImage(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = Gold;
            // 説明を読みながらセリフを送るので、クリックは絶対に食わせない。
            img.raycastTarget = false;
            return img;
        }

        private void ShowInternal(RectTransform target, Style style)
        {
            // **同じ相手を指し直されただけなら、出るときの動きをやり直さない。**
            // 説明の途中で同じ場所を何度も指し直すことがあり、そのたびに枠が飛び込んでくると騒がしい
            bool sameAsBefore = gameObject.activeSelf && _target == target && _style == style;

            _target = target;
            _style = style;

            gameObject.SetActive(true);
            _band.gameObject.SetActive(style == Style.Band);
            _frame.gameObject.SetActive(style == Style.Frame);

            Follow();
            Pulse();

            if (style == Style.Frame && !sameAsBefore) _frame.Restart();
        }

        private void HideInternal()
        {
            _target = null;
            gameObject.SetActive(false);
        }

        // 表は説明の途中で作り直される（勝ち用 → 負け用）。位置を毎フレーム追いかけ、
        // 対象が消えたら自分も消える。
        private void LateUpdate()
        {
            if (_target == null) { HideInternal(); return; }
            Follow();
            Pulse();
        }

        private void Follow()
        {
            RectTransform selfRect = transform as RectTransform;
            if (selfRect == null) return;
            if (!UIRectUtility.TryGetLocalRect(_target, selfRect, _canvas, out Rect local)) return;

            float pad = _style == Style.Frame ? FramePadding : Padding;

            // **1ドット（2単位）の倍数に置く。** 半端な位置や大きさにすると、
            // 枠の線が半画素にかかって、ドット絵の中でそこだけ滲む
            Vector2 size = new Vector2(SnapToDot(local.width + pad * 2f), SnapToDot(local.height + pad * 2f));
            Vector2 center = local.center - selfRect.rect.center;
            Vector2 min = new Vector2(SnapToDot(center.x - size.x * 0.5f), SnapToDot(center.y - size.y * 0.5f));

            _area.sizeDelta = size;
            _area.anchoredPosition = min + size * 0.5f;
        }

        private static float SnapToDot(float v)
        {
            return Mathf.Round(v * 0.5f) * 2f;
        }

        /// <summary>帯の明滅。枠のほうは <see cref="TutorialHighlightFrameGraphic"/> が自分で動く。</summary>
        private void Pulse()
        {
            if (_style != Style.Band) return;

            float baseAlpha = BandAlpha;
            // 0.5 を中心に振らせず、**基準の明るさを上限**にする。
            // 上へ振らせると帯が濃くなりすぎて下の数字が読めなくなる。
            float t = (Mathf.Sin(Time.unscaledTime * PulseSpeed) + 1f) * 0.5f;
            float alpha = baseAlpha * (1f - PulseDepth * t);

            Color c = Gold;
            c.a = alpha;
            _band.color = c;
        }
    }
}
