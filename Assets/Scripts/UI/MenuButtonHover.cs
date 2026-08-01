using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace KillingMahjong.UI
{
    /// <summary>
    /// タイトルメニューのボタンにホバー反応を付ける。
    ///
    /// 親が VerticalLayoutGroup で位置を管理しているため、anchoredPosition は動かせない。
    /// LayoutGroup が触らない localScale と、文字色・目印で反応を出す。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MenuButtonHover : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("反応の強さ")]
        [Tooltip("ホバー時の拡大率")]
        [SerializeField] private float hoverScale = 1.05f;
        [Tooltip("押している間の拡大率")]
        [SerializeField] private float pressedScale = 0.98f;
        [Tooltip("追従の速さ。大きいほどキビキビ動く")]
        [SerializeField] private float speed = 14f;

        [Header("色")]
        [Tooltip("ホバー時の文字色。背景の赤に合わせた明るい赤")]
        [SerializeField] private Color hoverTextColor = new Color32(255, 120, 110, 255);
        [Tooltip("ホバー時に枠を光らせる色")]
        [SerializeField] private Color hoverOutlineColor = new Color32(220, 40, 40, 200);

        [Header("目印")]
        [Tooltip("ホバー中に左へ出す印。空なら出さない")]
        [SerializeField] private string markerText = "▶";

        private RectTransform _rt;
        private TMP_Text _label;
        private Color _baseTextColor;
        private Outline _outline;
        private TMP_Text _marker;

        private bool _hovering;
        private bool _pressed;
        private float _target = 1f;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _label = GetComponentInChildren<TMP_Text>(true);
            if (_label != null) _baseTextColor = _label.color;

            // 枠の発光。既に Outline があれば使い回す
            _outline = GetComponent<Outline>();
            if (_outline == null)
            {
                _outline = gameObject.AddComponent<Outline>();
                _outline.effectDistance = new Vector2(2f, -2f);
            }
            _outline.effectColor = new Color(0f, 0f, 0f, 0f); // 通常は消しておく

            if (!string.IsNullOrEmpty(markerText) && _label != null) BuildMarker();
        }

        private void BuildMarker()
        {
            var go = new GameObject("HoverMarker", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_rt, false);
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(40f, 40f);
            rt.anchoredPosition = new Vector2(-6f, 0f); // ボタンの左外側

            _marker = go.AddComponent<TextMeshProUGUI>();
            _marker.text = markerText;
            _marker.font = _label.font;
            _marker.fontSize = _label.fontSize * 0.8f;
            _marker.alignment = TextAlignmentOptions.Right;
            _marker.color = hoverTextColor;
            _marker.raycastTarget = false;
            go.SetActive(false);
        }

        private void OnDisable()
        {
            _hovering = false;
            _pressed = false;
            _target = 1f;
            if (_rt != null) _rt.localScale = Vector3.one;
            if (_label != null) _label.color = _baseTextColor;
            if (_outline != null) _outline.effectColor = new Color(0f, 0f, 0f, 0f);
            if (_marker != null) _marker.gameObject.SetActive(false);
        }

        private void Update()
        {
            float s = Mathf.Lerp(_rt.localScale.x, _target, Time.unscaledDeltaTime * speed);
            _rt.localScale = new Vector3(s, s, 1f);

            if (_label != null)
            {
                var want = _hovering ? hoverTextColor : _baseTextColor;
                _label.color = Color.Lerp(_label.color, want, Time.unscaledDeltaTime * speed);
            }
            if (_outline != null)
            {
                var want = _hovering ? hoverOutlineColor : new Color(hoverOutlineColor.r, hoverOutlineColor.g, hoverOutlineColor.b, 0f);
                _outline.effectColor = Color.Lerp(_outline.effectColor, want, Time.unscaledDeltaTime * speed);
            }
        }

        private void Refresh()
        {
            _target = _pressed ? pressedScale : (_hovering ? hoverScale : 1f);
            if (_marker != null) _marker.gameObject.SetActive(_hovering);
        }

        public void OnPointerEnter(PointerEventData e) { _hovering = true; Refresh(); }
        public void OnPointerExit(PointerEventData e) { _hovering = false; _pressed = false; Refresh(); }
        public void OnPointerDown(PointerEventData e) { _pressed = true; Refresh(); }
        public void OnPointerUp(PointerEventData e) { _pressed = false; Refresh(); }
    }
}
