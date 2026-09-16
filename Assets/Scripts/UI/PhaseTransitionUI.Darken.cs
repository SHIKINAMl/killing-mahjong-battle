using System;
using System.Collections;
using UnityEngine;

namespace KillingMahjong.UI
{
    public partial class PhaseTransitionUI
    {
        /// <summary>
        /// 暗転まわりの一発物を鳴らす（2026-09-11）。`Resources/Stingers/` から読む。
        ///
        /// **繋ぎの尺は `checkerFadeDuration` に合わせて作ってある。**
        /// `br_riser` は 1.0 秒かけて上がり切った瞬間にキックが着地する。
        /// 市松模様が晴れる瞬間と打点を合わせるためなので、
        /// `checkerFadeDuration` を変えたら音も作り直すこと。ずれると打点だけ遅れて聞こえる。
        /// </summary>
        private void PlayTransitionStinger(string name)
        {
            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio == null) return;

            // **黒帯と暗転は局ごとに何度も鳴る。** 毎回まったく同じ波形だと、
            // 3局目あたりで「また同じ音」と感じられてしまうので、わずかにばらす。
            // 音程そのものに意味がある音（能力の3段、決めさせられていたの音型）は
            // ばらしてはいけないので、そちらは None にする。
            bool pitched = name != null &&
                           (name.StartsWith("se_ability") || name == "se_collapse" ||
                            name == "se_choice" || name == "se_choice_dark");

            var jitter = pitched ? KillingMahjong.Managers.AudioManager.PitchJitter.None
                                 : KillingMahjong.Managers.AudioManager.PitchJitter.VerySmall;

            // **演出の音は拍に寄せる（2026-09-12）。** ただし近いときだけで、
            // 遠ければ待たずに鳴らす。待たせると演出そのものが遅れて見える。
            // 台詞に合わせて鳴る音（se_drop など）は曲と無関係なので寄せない。
            bool ceremonial = name != null && name.StartsWith("br_");
            if (ceremonial) audio.PlayStingerSnapped(name, jitter, 2, 0.05f);
            else audio.PlayStinger(name, jitter, 0.05f);
        }

        /// <summary>
        /// 局の頭の暗転の濃さ。**真っ暗にする**（2026-09-17、プランナーの要望）。
        ///
        /// **一度 0.55 まで薄くしたが、戻した。** 経緯を残しておく:
        ///   2026-09-13 ユーザーから「局が始まるたびに暗転すると集中が切れる」と
        ///              指摘があり、1.0 -> 0.55 にした。このとき画面の明るさは
        ///              255段階で 3.7 -> 49.3 になった（普段は約81）。
        ///   2026-09-17 プランナーから「ここはどうしても黒幕にしたい」と要望があり、
        ///              1.0 へ戻した。掛け金フェイズの後のこの場面は、
        ///              暗転で区切ること自体が狙いとのこと。
        ///
        /// **また薄くしたくなったら、ここだけ触れば戻せる。**
        /// 気になるのは濃さより「配牌が終わるまで暗いまま」の長さのほうなので、
        /// 次に手を入れるなら暗転している時間を短くすること。
        /// </summary>
        private const float RoundDarkenAlpha = 1.0f;

        private IEnumerator RoundStartDarkenRoutine(string text, Action onDarkened)
        {
            ResetVisuals();

            // 濃さを抑える。ResetVisuals のあとに当てないと戻される
            if (fullScreenCheckerImage != null)
            {
                var c = fullScreenCheckerImage.color;
                c.a = RoundDarkenAlpha;
                fullScreenCheckerImage.color = c;
            }

            // 市松模様フェードイン (暗転)。落ちていくスイープを重ねる
            PlayTransitionStinger("br_fall");
            if (fullScreenCheckerImage != null) fullScreenCheckerImage.gameObject.SetActive(true);
            if (checkerMaterial != null)
            {
                checkerMaterial.SetFloat("_AspectRatio", (float)Screen.width / Screen.height);
                checkerMaterial.SetFloat("_Progress", 0f);
            }

            float t = 0;
            while (t < checkerFadeDuration)
            {
                if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", t / checkerFadeDuration);
                t += Time.deltaTime;
                yield return null;
            }
            if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 1f);

            IsDarkenTransitioning = false;
            
            // 暗転完了のコールバック（ここで盤面をクリアする）
            onDarkened?.Invoke();

            // ドン！とテキスト表示。画面揺れと同じ「着弾」なので打撃音を当てる
            if (centerText != null)
            {
                PlayTransitionStinger("br_blackout");
                centerText.text = text;
                centerText.gameObject.SetActive(true);
                centerText.color = Color.white;
                
                t = 0;
                float duration = 0.4f;
                Vector3 initialScale = new Vector3(3f, 3f, 1f);
                Vector3 targetScale = Vector3.one;
                
                while (t < duration)
                {
                    float progress = t / duration;
                    float scaleProgress = 1f - Mathf.Pow(1f - progress, 4f); 
                    centerText.transform.localScale = Vector3.LerpUnclamped(initialScale, targetScale, scaleProgress);
                    t += Time.deltaTime;
                    yield return null;
                }
                centerText.transform.localScale = targetScale;
                
                // 画面揺れ（着弾の衝撃）
                StartCoroutine(ScreenShakeRoutine(0.2f, 20f));
            }
        }

        private IEnumerator RoundStartFadeOutRoutine(Action onComplete)
        {
            // 配牌が完了したら、局名と一緒に手混ぜ用の牌の山も消す。
            // TileClatterEffect は破棄せず、次のマッチング待ちで再利用できる。
            var transitionRect = transform as RectTransform;
            if (transitionRect != null) Effects.TileClatterEffect.Hide(transitionRect);

            // テキストを隠す
            if (centerText != null) centerText.gameObject.SetActive(false);
            if (horizontalLineRt != null) horizontalLineRt.gameObject.SetActive(false);

            // 市松模様フェードアウト (晴れる)。上がり切ったところでキックが着地する
            PlayTransitionStinger("br_riser");
            float t = 0;
            while (t < checkerFadeDuration)
            {
                if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 1f - (t / checkerFadeDuration));
                t += Time.deltaTime;
                yield return null;
            }
            if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 0f);
            if (fullScreenCheckerImage != null) fullScreenCheckerImage.gameObject.SetActive(false);

            onComplete?.Invoke();
        }
    }
}
