using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// ロンできる牌が出たときの「待っている間」の演出（2026-10-04、`ロン演出.pdf` より）。
    ///
    ///   画面が暗くなって
    ///   その牌だけが光ってる、点滅している状態
    ///   この時牌をきらきらさせたい
    ///
    /// 暗幕を1枚かぶせ、**その牌だけ** Canvas で手前に上げて点滅させ、
    /// <see cref="TileSparkleEffect"/> を付けてきらきらを出し続ける。
    ///
    /// **牌は動かさない。** 河のレイアウトに乗ったままなので、親を変えたり
    /// 座標をいじったりすると、演出が終わったあと並びが崩れる。手前に出すのは
    /// 牌そのものに Canvas を足して `overrideSorting` を立てるだけにして、
    /// <see cref="Hide"/> で必ず外す。
    ///
    /// **暗幕はクリックを吸わない。** 吸うとロンボタンが押せなくなる。
    ///
    /// シーンには置かない（<see cref="ScreenFlash"/> と同じ理由）。
    /// </summary>
    public class RonChanceEffect : MonoBehaviour
    {
        /// <summary>暗幕の濃さ。盤面が読める程度に残す。</summary>
        private const float DimAlpha = 0.72f;

        /// <summary>暗くなりきるまで（秒）。</summary>
        private const float FadeInDuration = 0.35f;

        /// <summary>点滅の周期（秒）。速すぎると目が痛い。</summary>
        private const float BlinkPeriod = 0.62f;

        /// <summary>点滅で縮む側の明るさ。1.0 が元の色。</summary>
        private const float BlinkDimFactor = 0.62f;

        /// <summary>点滅に合わせて牌が膨らむ量。</summary>
        private const float BlinkScaleUp = 0.10f;

        /// <summary>きらきらの1秒あたりの粒数。</summary>
        private const float SparkleRate = 16f;

        private static RonChanceEffect _current;

        private Image _dim;
        private RectTransform _tile;
        private Canvas _tileCanvas;
        private bool _addedTileCanvas;
        private Image _tileImage;
        private Color _tileBaseColor;
        private Vector3 _tileBaseScale;
        private TileSparkleEffect _sparkle;

        /// <summary>
        /// 出す。すでに出ていれば何もしない。
        /// </summary>
        /// <param name="tile">光らせる牌。null なら暗幕だけ出す</param>
        public static RonChanceEffect Show(RectTransform tile)
        {
            if (!Application.isPlaying) return null;
            if (_current != null) return _current;

            var go = new GameObject("RonChanceEffect");
            var fx = go.AddComponent<RonChanceEffect>();
            fx.Build(tile);
            _current = fx;
            return fx;
        }

        /// <summary>出ていれば片付ける。出ていなくても呼んでよい。</summary>
        public static void HideCurrent()
        {
            if (_current != null) _current.Hide();
        }

        public void Hide()
        {
            RestoreTile();
            if (_current == this) _current = null;
            if (this != null && gameObject != null) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            RestoreTile();
            if (_current == this) _current = null;
        }

        // ------------------------------------------------------------

        private void Build(RectTransform tile)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UISortingOrders.RonChanceDimmer;

            var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)dimGo.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _dim = dimGo.GetComponent<Image>();
            _dim.color = new Color(0f, 0f, 0.02f, 0f);
            _dim.raycastTarget = false;   // ロンボタンを押せなくしない

            RaiseTile(tile);

            StartCoroutine(Run());
        }

        /// <summary>牌を暗幕の上へ出す。**位置も親も変えない。**</summary>
        private void RaiseTile(RectTransform tile)
        {
            if (tile == null) return;

            _tile = tile;
            _tileBaseScale = tile.localScale;

            _tileImage = tile.GetComponent<Image>();
            if (_tileImage != null) _tileBaseColor = _tileImage.color;

            _tileCanvas = tile.GetComponent<Canvas>();
            if (_tileCanvas == null)
            {
                _tileCanvas = tile.gameObject.AddComponent<Canvas>();
                _addedTileCanvas = true;
            }
            _tileCanvas.overrideSorting = true;
            _tileCanvas.sortingOrder = UISortingOrders.RonChanceTile;

            _sparkle = TileSparkleEffect.Attach(tile);
            if (_sparkle != null)
            {
                _sparkle.RatePerSecond = SparkleRate;
                _sparkle.SetContinuous(true);
            }
        }

        private void RestoreTile()
        {
            if (_sparkle != null)
            {
                _sparkle.SetContinuous(false);
                _sparkle = null;
            }

            if (_tile != null)
            {
                _tile.localScale = _tileBaseScale;
                if (_tileImage != null) _tileImage.color = _tileBaseColor;

                if (_tileCanvas != null)
                {
                    // **足したときだけ外す。** もともと付いていた Canvas を消すと、
                    // その牌の重なり順が壊れたまま戻らない
                    if (_addedTileCanvas) Destroy(_tileCanvas);
                    else _tileCanvas.overrideSorting = false;
                }
            }

            _tile = null;
            _tileCanvas = null;
            _tileImage = null;
            _addedTileCanvas = false;
        }

        private IEnumerator Run()
        {
            float t = 0f;
            while (t < FadeInDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / FadeInDuration);
                u = u * u * (3f - 2f * u);
                if (_dim != null) _dim.color = new Color(0f, 0f, 0.02f, DimAlpha * u);
                Blink(t);
                yield return null;
            }
            if (_dim != null) _dim.color = new Color(0f, 0f, 0.02f, DimAlpha);

            // 押されるまで点滅し続ける
            while (true)
            {
                t += Time.deltaTime;
                Blink(t);
                yield return null;
            }
        }

        /// <summary>明るさと大きさを一緒に動かす。色だけだと暗幕の上では目立たない。</summary>
        private void Blink(float elapsed)
        {
            if (_tile == null) return;

            // 0..1 を行ったり来たり。端をなめらかにして、ちかちかさせない
            float phase = Mathf.PingPong(elapsed / (BlinkPeriod * 0.5f), 1f);
            float eased = phase * phase * (3f - 2f * phase);

            if (_tileImage != null)
            {
                float k = Mathf.Lerp(BlinkDimFactor, 1f, eased);
                _tileImage.color = new Color(_tileBaseColor.r * k + (1f - k) * 0.25f,
                                             _tileBaseColor.g * k + (1f - k) * 0.25f,
                                             _tileBaseColor.b * k + (1f - k) * 0.25f,
                                             _tileBaseColor.a);
            }

            _tile.localScale = _tileBaseScale * (1f + BlinkScaleUp * eased);
        }
    }
}
