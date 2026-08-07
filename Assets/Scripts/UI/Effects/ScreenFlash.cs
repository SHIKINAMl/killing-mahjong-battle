using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 画面全体を一瞬だけ光らせる。「ここで何かが切り替わる」という合図に使う。
    ///
    /// **シーンには置かない。呼ばれるたびに自前の Canvas を作り、終わったら自分を消す。**
    /// 対局シーンが `UIテストシーン` と `OpeningScene` の2つあるため、シーンに置くと
    /// 片方にだけ入れる事故が起きる。実行時生成なら両方で確実に同じ挙動になる。
    ///
    /// GraphicRaycaster は**わざと付けていない**。付けると光っている一瞬だけ
    /// 全画面のクリックを吸ってしまい、その裏のボタンが反応しなくなる。
    /// </summary>
    public class ScreenFlash : MonoBehaviour
    {
        /// <summary>既定の長さ。「一瞬」に見せたいので、伸ばすとしてもこの倍まで。</summary>
        public const float DefaultDuration = 0.12f;

        /// <summary>既定の濃さ。真っ白(1.0)まで上げると眩しすぎるので抑えてある。</summary>
        public const float DefaultPeakAlpha = 0.7f;

        private Image _image;
        private Color _color;
        private float _duration;
        private float _peakAlpha;

        /// <summary>白く一瞬光らせる。</summary>
        public static void Play(float duration = DefaultDuration, float peakAlpha = DefaultPeakAlpha)
        {
            Play(Color.white, duration, peakAlpha);
        }

        /// <summary>色を指定して一瞬光らせる。</summary>
        public static void Play(Color color, float duration = DefaultDuration, float peakAlpha = DefaultPeakAlpha)
        {
            // エディタの停止中や、シーン遷移中に呼ばれても何もしない
            if (!Application.isPlaying) return;
            if (duration <= 0f || peakAlpha <= 0f) return;

            var go = new GameObject("ScreenFlash");
            var flash = go.AddComponent<ScreenFlash>();
            flash._color = color;
            flash._duration = duration;
            flash._peakAlpha = Mathf.Clamp01(peakAlpha);
            flash.Build();
            flash.StartCoroutine(flash.FadeRoutine());
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UISortingOrders.ScreenFlash;

            var imageGo = new GameObject("Flash");
            imageGo.transform.SetParent(transform, false);

            _image = imageGo.AddComponent<Image>();
            _image.color = new Color(_color.r, _color.g, _color.b, _peakAlpha);
            _image.raycastTarget = false;

            var rt = _image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private IEnumerator FadeRoutine()
        {
            // 立ち上がりは入れない。フェイズ演出の直前に差し込むものなので、
            // 光り始めを待つと「合図」ではなく「前置き」になってしまう。
            //
            // Time.timeScale に左右されないよう unscaled で進める。演出中に
            // スローがかかっても、フラッシュの長さは見た目どおりであってほしい。
            float elapsed = 0f;
            while (elapsed < _duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(_peakAlpha, 0f, elapsed / _duration);
                if (_image == null) break;
                _image.color = new Color(_color.r, _color.g, _color.b, alpha);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
