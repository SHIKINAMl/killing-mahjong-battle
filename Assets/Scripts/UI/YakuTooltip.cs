using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 役にカーソルを合わせたときに成立条件を出す吹き出し。
    ///
    /// 一覧そのものに説明文を差し込むとリストの高さが変わって並びが動くため、
    /// 別Canvasに浮かせてカーソルの近くへ出す。専用のプレハブや画像は使わない。
    ///
    /// 使い方: YakuTooltip.Show(説明文, フォント) / YakuTooltip.Hide()
    /// </summary>
    public class YakuTooltip : MonoBehaviour
    {
        private static YakuTooltip _instance;

        private RectTransform _panelRt;
        private TextMeshProUGUI _text;
        private Canvas _canvas;

        private const float MaxWidth = 420f;
        private const float PadX = 16f;
        private const float PadY = 10f;
        private const float CursorOffset = 18f;

        private static YakuTooltip Instance
        {
            get
            {
                if (_instance != null) return _instance;

                var go = new GameObject("YakuTooltipCanvas", typeof(RectTransform));
                DontDestroyOnLoad(go);

                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // カーソルより下、他の演出より上に出す
                canvas.sortingOrder = KillingMahjong.Common.UISortingOrders.MouseCursor - 1;
                var scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(800, 600);
                scaler.matchWidthOrHeight = 0f;

                _instance = go.AddComponent<YakuTooltip>();
                _instance._canvas = canvas;
                _instance.Build(go.transform);
                _instance.SetVisible(false);
                return _instance;
            }
        }

        private void Build(Transform parent)
        {
            var panel = new GameObject("Panel", typeof(RectTransform));
            _panelRt = panel.GetComponent<RectTransform>();
            _panelRt.SetParent(parent, false);
            _panelRt.anchorMin = _panelRt.anchorMax = new Vector2(0f, 0f);
            _panelRt.pivot = new Vector2(0f, 0f);

            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.07f, 0.94f);
            bg.raycastTarget = false;

            // 黒背景に埋もれないよう、細い縁を付ける
            var outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.35f);
            outline.effectDistance = new Vector2(2f, -2f);

            var textObj = new GameObject("Text", typeof(RectTransform));
            var trt = textObj.GetComponent<RectTransform>();
            trt.SetParent(_panelRt, false);
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(PadX, PadY);
            trt.offsetMax = new Vector2(-PadX, -PadY);

            _text = textObj.AddComponent<TextMeshProUGUI>();
            _text.fontSize = 18f;
            _text.color = Color.white;
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.textWrappingMode = TextWrappingModes.Normal;
            _text.raycastTarget = false;
        }

        private void SetVisible(bool v)
        {
            if (_panelRt != null) _panelRt.gameObject.SetActive(v);
        }

        public static void Show(string description, TMP_FontAsset font = null)
        {
            if (string.IsNullOrEmpty(description)) { Hide(); return; }

            var inst = Instance;
            if (font != null) inst._text.font = font;
            inst._text.text = description;

            // 文字量に合わせて枠の大きさを決める
            Vector2 pref = inst._text.GetPreferredValues(description, MaxWidth, 0f);
            float w = Mathf.Min(pref.x, MaxWidth) + PadX * 2f;
            float h = pref.y + PadY * 2f;
            inst._panelRt.sizeDelta = new Vector2(w, h);

            inst.SetVisible(true);
            inst.Follow();
        }

        public static void Hide()
        {
            if (_instance != null) _instance.SetVisible(false);
        }

        private void Update()
        {
            if (_panelRt != null && _panelRt.gameObject.activeSelf) Follow();
        }

        /// <summary>カーソルに追従させつつ、画面外へはみ出さないよう収める。</summary>
        private void Follow()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;

            Vector2 screenPos = mouse.position.ReadValue();
            float scale = _canvas != null ? _canvas.scaleFactor : 1f;
            if (scale <= 0f) scale = 1f;

            Vector2 pos = screenPos / scale;
            Vector2 size = _panelRt.sizeDelta;
            Vector2 canvasSize = ((RectTransform)_canvas.transform).rect.size;

            // 既定はカーソルの右上。はみ出す側は反対へ寄せる
            float x = pos.x + CursorOffset / scale;
            float y = pos.y + CursorOffset / scale;
            if (x + size.x > canvasSize.x) x = pos.x - CursorOffset / scale - size.x;
            if (y + size.y > canvasSize.y) y = pos.y - CursorOffset / scale - size.y;

            x = Mathf.Clamp(x, 0f, Mathf.Max(0f, canvasSize.x - size.x));
            y = Mathf.Clamp(y, 0f, Mathf.Max(0f, canvasSize.y - size.y));

            _panelRt.anchoredPosition = new Vector2(x, y);
        }
    }
}
