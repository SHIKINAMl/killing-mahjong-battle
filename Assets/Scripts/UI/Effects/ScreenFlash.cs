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

        // ------------------------------------------------------------
        //  場面が変わるときの白飛ばし（2026-09-25）
        //
        //  **ユーザーが撮ってくれた『スーパーダンガンロンパ2』の録画から、
        //  1コマずつ測って写した値。** ゲーム画面だけを切り出して平均輝度を追うと
        //
        //      2.20s 180（地） → 2.23s 198 → 2.27s 235 → 2.30s 253（頂点）
        //      → そこから 2.80s まで、ほぼ**まっすぐ**に 180 へ戻る
        //
        //  だったので、立ち上がり 0.10 秒／頂点は**真っ白**／落ちは 0.50 秒の直線。
        //  合計 0.60 秒。**「一瞬パッと光る」ではなく、白で場面を切っている。**
        //
        //  短い合図（<see cref="Play"/>）とは別物なので、混ぜないこと。
        //  対局中くり返し出る所にこれを使うと、画面が白いだけの時間が増える。
        // ------------------------------------------------------------

        /// <summary>白へ上がりきるまで。参考動画で 3 コマ（30fps）かかっていた。</summary>
        private const float SceneBreakRise = 0.10f;

        /// <summary>白から戻るまで。参考動画では直線で 0.5 秒。</summary>
        private const float SceneBreakFade = 0.50f;

        private Image _image;
        private Color _color;
        private float _duration;
        private float _peakAlpha;
        private bool _sceneBreak;

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
                // **音量は 0.5 では足りない（2026-09-25 に実測）。**
                // 録画から測ると、0.5 のときの振幅は 0.035。同じ録画の打牌SEが 0.189 で、
                // **牌を1枚置く音より5倍小さい。** 合図として置く音がそれでは聞こえない。
                AudioManager.Instance.PlaySynthSound(SynthWaveType.Sine, 2600f, 1800f, 0.06f, 1.0f);
            }

            flash.StartCoroutine(flash.FadeRoutine());
        }

        /// <summary>
        /// 白で場面を切る（フロー図の「ダンロンみたいなフラッシュ」）。
        /// 参考動画から写した形で、<see cref="Play"/> の短い合図とは別物。
        /// </summary>
        public static void PlaySceneBreak(bool playSound = true)
        {
            if (!Application.isPlaying) return;

            var go = new GameObject("ScreenFlash");
            var flash = go.AddComponent<ScreenFlash>();
            flash._color = Color.white;
            flash._peakAlpha = 1f;          // 参考動画の頂点は 253/255。真っ白まで飛んでいる
            flash._sceneBreak = true;
            flash.Build();
            flash._image.color = new Color(1f, 1f, 1f, 0f);   // 立ち上がりがあるので 0 から始める

            if (playSound && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySynthSound(SynthWaveType.Sine, 2600f, 1800f, 0.06f, 1.0f);
            }

            flash.StartCoroutine(flash.SceneBreakRoutine());
        }

        private IEnumerator SceneBreakRoutine()
        {
            float t = 0f;
            while (t < SceneBreakRise)
            {
                t += Time.unscaledDeltaTime;
                if (_image == null) yield break;
                _image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / SceneBreakRise));
                yield return null;
            }

            // **落ちは「見た目が直線」になるようにする。**
            // 参考動画の輝度は、頂点から地の値までほぼ一定の傾きで戻っていた。
            // ところが **このゲームは Linear カラースペース**なので、アルファを
            // まっすぐ下げると画面の明るさは直線にならない。実測で比べると
            //
            //      経過 250ms … 参考 0.52 / アルファ直線だと 0.70
            //
            // で、**真ん中で白く居座ってから最後に落ちる**形になっていた。
            // 1.6 乗にすると 250ms で 0.50 まで来て、参考の傾きにほぼ乗る。
            const float fadeGamma = 1.6f;
            t = 0f;
            while (t < SceneBreakFade)
            {
                t += Time.unscaledDeltaTime;
                if (_image == null) yield break;
                float u = Mathf.Clamp01(t / SceneBreakFade);
                _image.color = new Color(1f, 1f, 1f, Mathf.Pow(1f - u, fadeGamma));
                yield return null;
            }

            Destroy(gameObject);
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
