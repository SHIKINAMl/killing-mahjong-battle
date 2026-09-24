using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;
using KillingMahjong.Managers;

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
    ///
    /// **短いので、録画に1コマも入らないことがある（2026-09-25）。**
    /// `TutorialRecorder` を 60fps で回しても、既定の 0.12 秒だと拾えない回がある。
    /// 長さを 1.5 秒にして試すと確実に写るので、**録画に無いことは
    /// 「出ていない」証拠にならない。** 出ているかどうかを確かめるときは
    /// `ScreenCapture.CaptureScreenshot` で撮る。
    /// 実測では、光った瞬間に画面の平均輝度が 73 から 178 へ上がる。
    /// </summary>
    public class ScreenFlash : MonoBehaviour
    {
        /// <summary>既定の長さ。ピーク保持と減衰を合わせても、従来より長くしない。</summary>
        public const float DefaultDuration = 0.12f;

        /// <summary>既定の濃さ。真っ白(1.0)まで上げると眩しすぎるので抑えてある。</summary>
        public const float DefaultPeakAlpha = 0.7f;

        // 60fps なら約2フレーム。光が一度は画面に残り、「叩いた」合図として読める長さにする。
        private const float PeakHoldDuration = 0.03f;

        private Image _image;
        private Color _color;
        private float _duration;
        private float _peakAlpha;

        /// <summary>白く一瞬光らせる。</summary>
        public static void Play(float duration = DefaultDuration, float peakAlpha = DefaultPeakAlpha, bool playSound = true)
        {
            Play(Color.white, duration, peakAlpha, playSound);
        }

        /// <summary>色を指定して一瞬光らせる。</summary>
        public static void Play(Color color, float duration = DefaultDuration, float peakAlpha = DefaultPeakAlpha, bool playSound = true)
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

            if (playSound && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySynthSound(SynthWaveType.Sine, 2600f, 1800f, 0.06f, 0.5f);
            }

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
            // 立ち上がりは入れず、ピークを最低1フレーム描いてから落とす。
            // 秒だけで保持すると重い環境では一度も描かれずに終わるため、必ず yield する。
            float holdDuration = Mathf.Min(PeakHoldDuration, _duration);
            float holdElapsed = 0f;
            do
            {
                yield return null;
                holdElapsed += Time.unscaledDeltaTime;
            }
            while (holdElapsed < holdDuration);

            // Time.timeScale に左右されないよう unscaled で進める。直線ではなく、
            // 最初に大きく落としてから消える形にすることで、ぼんやりした明滅に見せない。
            float fadeDuration = _duration - holdDuration;
            float fadeElapsed = 0f;
            while (fadeElapsed < fadeDuration)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(fadeElapsed / fadeDuration);
                float alpha = _peakAlpha * Mathf.Pow(1f - t, 3f);
                if (_image == null) break;
                _image.color = new Color(_color.r, _color.g, _color.b, alpha);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
