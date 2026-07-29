using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 誘導先の矩形だけを切り抜いて、それ以外を暗くするチュートリアル用マスク。
    ///
    /// 描画は <see cref="TutorialMaskGraphic"/> の1枚メッシュに任せている。
    /// 以前は上下左右4枚の Image で枠を組んでいたが、その構成では角丸もふちのぼかしも作れなかった。
    /// 旧構成のパネルがシーンに残っていても Awake で無効化するので、シーン側の作業は不要。
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class TutorialMaskUI : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Color maskColor = new Color(0, 0, 0, 0.7f);

        [Tooltip("切り抜く枠の余白。小さすぎると対象に張り付いて窮屈に見える。")]
        [SerializeField] private float padding = 18f;

        [Tooltip("穴の角丸半径")]
        [SerializeField] private float cornerRadius = 16f;

        [Tooltip("穴のふちのぼかし幅。0 でくっきり切り抜く。")]
        [SerializeField] private float edgeSoftness = 14f;

        [Tooltip("表示・非表示のフェード時間（秒）。0 で即時。")]
        [SerializeField] private float fadeDuration = 0.18f;

        [Tooltip("誘導先が切り替わったとき、穴が移動しきるまでの時間（秒）。0 で即時。")]
        [SerializeField] private float moveDuration = 0.2f;

        [Header("Graphic")]
        [Tooltip("未設定なら Awake で自動生成する")]
        [SerializeField] private TutorialMaskGraphic maskGraphic;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private RectTransform _currentTarget;

        /// <summary>いま描画している穴（Canvas ローカル座標）。</summary>
        private Rect _holeRect;
        private bool _hasHole;

        private float _alpha;
        private float _targetAlpha;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = UISortingOrders.TutorialMask;
            _canvasRect = _canvas.GetComponent<RectTransform>();

            EnsureGraphic();
            DisableLegacyPanels();

            _alpha = 0f;
            _targetAlpha = 0f;
            ApplyAlpha(0f);

            gameObject.SetActive(false);
        }

        /// <summary>描画用グラフィックを用意する。シーンに無ければ全画面の子として作る。</summary>
        private void EnsureGraphic()
        {
            if (maskGraphic == null) maskGraphic = GetComponentInChildren<TutorialMaskGraphic>(true);

            if (maskGraphic == null)
            {
                // CanvasRenderer は明示的に付ける。Graphic の RequireComponent は
                // 派生クラスを実行時 AddComponent したときには効かない。
                var go = new GameObject("MaskGraphic", typeof(RectTransform), typeof(CanvasRenderer));
                go.transform.SetParent(transform, false);
                maskGraphic = go.AddComponent<TutorialMaskGraphic>();
            }

            RectTransform rt = maskGraphic.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            maskGraphic.CornerRadius = cornerRadius;
            maskGraphic.EdgeSoftness = edgeSoftness;
            maskGraphic.raycastTarget = true;
            maskGraphic.gameObject.SetActive(true);
        }

        /// <summary>旧4枚パネル構成の名残がシーンに残っていても描画されないようにする。</summary>
        private void DisableLegacyPanels()
        {
            var images = GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.gameObject == maskGraphic.gameObject) continue;
                img.gameObject.SetActive(false);
            }
        }

        public void Show(RectTransform targetRect)
        {
            gameObject.SetActive(true);
            _targetAlpha = 1f;

            if (targetRect == null)
            {
                _currentTarget = null;
                _hasHole = false;
                if (maskGraphic != null) maskGraphic.ClearHole();
                return;
            }

            // 直前に隠れていた場合は前の穴の位置から滑らせない（画面を横切って見えてしまうため）
            bool snap = !_hasHole || _currentTarget == null;

            _currentTarget = targetRect;
            if (snap) _holeRect = CalcHoleRect(targetRect);
            _hasHole = true;

            ApplyHole(_holeRect);
        }

        public void Hide()
        {
            _currentTarget = null;
            _hasHole = false;
            _targetAlpha = 0f;

            if (fadeDuration <= 0f)
            {
                _alpha = 0f;
                ApplyAlpha(0f);
                gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!Mathf.Approximately(_alpha, _targetAlpha))
            {
                float step = fadeDuration > 0f ? Time.unscaledDeltaTime / fadeDuration : 1f;
                _alpha = Mathf.MoveTowards(_alpha, _targetAlpha, step);
                ApplyAlpha(_alpha);

                if (_alpha <= 0f && _targetAlpha <= 0f)
                {
                    gameObject.SetActive(false);
                    return;
                }
            }

            if (_currentTarget == null) return;

            Rect target = CalcHoleRect(_currentTarget);

            if (moveDuration > 0f)
            {
                float t = Mathf.Clamp01(Time.unscaledDeltaTime / moveDuration);
                _holeRect = Rect.MinMaxRect(
                    Mathf.Lerp(_holeRect.xMin, target.xMin, t),
                    Mathf.Lerp(_holeRect.yMin, target.yMin, t),
                    Mathf.Lerp(_holeRect.xMax, target.xMax, t),
                    Mathf.Lerp(_holeRect.yMax, target.yMax, t));
            }
            else
            {
                _holeRect = target;
            }

            ApplyHole(_holeRect);
        }

        /// <summary>ターゲットの矩形を Canvas ローカルに変換し、余白を足した「穴」を返す。</summary>
        private Rect CalcHoleRect(RectTransform target)
        {
            // 誘導先が別 RenderMode の Canvas（役Canvas は ScreenSpaceCamera）にいることがあるため、
            // ワールド座標を直接 InverseTransformPoint せず、スクリーン座標を経由して変換する。
            if (!UIRectUtility.TryGetLocalRect(target, _canvas, out Rect local))
            {
                return _holeRect;
            }

            return Rect.MinMaxRect(
                local.xMin - padding,
                local.yMin - padding,
                local.xMax + padding,
                local.yMax + padding);
        }

        private void ApplyHole(Rect hole)
        {
            if (maskGraphic == null) return;
            maskGraphic.SetHole(hole);
        }

        private void ApplyAlpha(float t)
        {
            if (maskGraphic == null) return;

            Color c = maskColor;
            c.a = maskColor.a * Mathf.Clamp01(t);
            maskGraphic.color = c;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (maskGraphic == null) return;
            maskGraphic.CornerRadius = cornerRadius;
            maskGraphic.EdgeSoftness = edgeSoftness;
        }
#endif
    }
}
