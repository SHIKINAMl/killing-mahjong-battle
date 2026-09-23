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

        /// <summary>枠線の太さ。1ドット＝2UI単位なので 4 でドット2つぶん。</summary>
        private const float FrameThickness = 4f;

        /// <summary>
        /// 帯の濃さ。**このプロジェクトは Linear カラースペースなので、数字より濃く出る。**
        /// 見た目で 1/4 くらいに見せたいなら 0.25 ではなく 0.11 前後になる
        /// （実機で 0.26 を入れたら実測 0.42 相当の濃さになり、下の数字が読めなくなった）。
        /// </summary>
        private const float BandAlpha = 0.11f;

        private const float FrameAlpha = 1.0f;

        /// <summary>明滅の深さ。基準の明るさに対する割合。</summary>
        private const float PulseDepth = 0.35f;

        private const float PulseSpeed = 3.0f;

        /// <summary>強調の色。セリフの強調（HighlightOpen）と同じ金色にそろえてある。</summary>
        private static readonly Color Gold = new Color32(0xFF, 0xD7, 0x00, 0xFF);

        private static TutorialHighlightUI _instance;

        private RectTransform _area;
        private Image _band;
        private Image[] _edges;

        private RectTransform _target;
        private Canvas _canvas;
        private Style _style;

        /// <summary>対象の範囲を示し始める。すでに出ていれば対象を差し替える。</summary>
        public static void Show(RectTransform target, Style style)
        {
            if (target == null) { HideCurrent(); return; }
            if (_instance == null) _instance = Create();
            _instance.ShowInternal(target, style);
        }

        /// <summary>出ていれば消す。出ていなければ何もしない。</summary>
        public static void HideCurrent()
        {
            if (_instance != null) _instance.HideInternal();
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

            _edges = new Image[4];
            _edges[0] = MakeEdge("Top",    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, FrameThickness));
            _edges[1] = MakeEdge("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, FrameThickness));
            _edges[2] = MakeEdge("Left",   new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(FrameThickness, 0f));
            _edges[3] = MakeEdge("Right",  new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(FrameThickness, 0f));

            gameObject.SetActive(false);
        }

        private Image MakeEdge(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
        {
            Image img = MakeImage(_area, name);
            RectTransform rt = img.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            return img;
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
            _target = target;
            _style = style;

            gameObject.SetActive(true);
            _band.gameObject.SetActive(style == Style.Band);
            foreach (var e in _edges) e.gameObject.SetActive(style == Style.Frame);

            Follow();
            Pulse();
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

            _area.sizeDelta = new Vector2(local.width + Padding * 2f, local.height + Padding * 2f);
            _area.anchoredPosition = local.center - selfRect.rect.center;
        }

        private void Pulse()
        {
            float baseAlpha = _style == Style.Band ? BandAlpha : FrameAlpha;
            // 0.5 を中心に振らせず、**基準の明るさを上限**にする。
            // 上へ振らせると帯が濃くなりすぎて下の数字が読めなくなる。
            float t = (Mathf.Sin(Time.unscaledTime * PulseSpeed) + 1f) * 0.5f;
            float alpha = baseAlpha * (1f - PulseDepth * t);

            Color c = Gold;
            c.a = alpha;
            if (_style == Style.Band) _band.color = c;
            else foreach (var e in _edges) e.color = c;
        }
    }
}
