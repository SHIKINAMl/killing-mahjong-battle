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

        /// <summary>
        /// 配牌後の片付け（<see cref="RoundStartFadeOutRoutine"/>）がもう走ったか。
        ///
        /// **置く側と片付ける側が競る。** 裏牌の山を置くのは局名を出したあと、
        /// 片付けるのは配牌が届いたとき。サーバーの配牌が 4.8秒から十数ms になり、
        /// 「片付け → そのあと置く」の順で入れ替わると、置いた山が誰にも片付けられず
        /// 手牌選択フェイズまで残る（2026-10-01 のユーザー報告、2度目）。
        ///
        /// フェイズを見て消す対策を入れてあるが、**あれは置かれる前に走ると効かない**。
        /// 順序に頼らず、片付けが済んでいたら最初から置かないことで断つ。
        /// </summary>
        private bool _roundStartCleanupDone;

        // 配牌開始の通知で立て、画面を覆って前局の盤面を消したあとに解除する。
        // 暗転の開始自体が別の演出待ちになっている場合も、配牌の反映を保留する。
        public bool IsRoundStartResetPending { get; private set; }
        private Action _additionalRoundStartDarkenedCallbacks;

        /// <summary>
        /// 配牌後の片付けが済んだか。**裏牌の山を置く側は、置く直前にこれを見る。**
        /// `IsScreenDarkened` だけでは足りない（暗転が明ける前に片付けが走ることがある）。
        /// </summary>
        public bool IsRoundStartCleanupDone { get { return _roundStartCleanupDone; } }

        /// <summary>
        /// 配牌が「暗転が上がりきる前」に終わっていたか（2026-10-04 のユーザー報告
        /// 「暗転する前に全部そろっている画面が映っています」）。
        ///
        /// **サーバーの配牌が十数msになったので、暗転より先に届くようになった。**
        /// 以前の作りだと、暗転が降りている最中でも配牌完了がそのまま晴らしに入るか、
        /// まだ暗転が始まってもいないと素通りしていた。どちらの場合も、
        /// そろった盤面が暗転にかぶる前に見えてしまう。
        ///
        /// 晴らすのは**必ず暗転が上がりきってから**にして、
        /// 先に終わっていた場合はこの旗に積んでおく。
        /// </summary>
        private bool _fadeOutPendingUntilDarkened;

        /// <summary>
        /// 暗転が降りきるのを待ってから晴らす。<see cref="PlayRoundStartFadeOut"/> が
        /// 「降りている最中」に呼ばれたときだけ走る。
        /// </summary>
        private IEnumerator FadeOutAfterDarkenRoutine(Action onComplete)
        {
            while (IsDarkenTransitioning)
            {
                yield return null;
            }

            _fadeOutPendingUntilDarkened = false;

            if (!isDarkened)
            {
                onComplete?.Invoke();
                yield break;
            }

            isDarkened = false;
            IsScreenDarkened = false;
            yield return StartCoroutine(RoundStartFadeOutRoutine(onComplete));
        }

        private IEnumerator RoundStartDarkenRoutine(string text, Action onDarkened)
        {
            // **ここで旗を倒さない。** 倒すのは配牌が始まったとき
            // （HandleDealingStarted）。配牌が暗転より先に終わっている場合に
            // ここで倒すと、「もう待っていないのに山を置く」が復活する。
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

            // 暗転完了のコールバック（ここで盤面をクリアする）
            onDarkened?.Invoke();
            var additionalCallbacks = _additionalRoundStartDarkenedCallbacks;
            _additionalRoundStartDarkenedCallbacks = null;
            additionalCallbacks?.Invoke();
            IsRoundStartResetPending = false;
            IsDarkenTransitioning = false;

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

            // **局名が出たら、裏牌の山を置く（2026-09-20 のユーザー指示）。**
            // ここから配牌が届くまではサーバー待ちで、長いと画面が文字だけで止まる。
            // 第1局だけでなく**どの局でも**混ぜられるようにする。
            // 片付けは配牌後の RoundStartFadeOutRoutine が Hide でやっている。
            // **片付けが先に走っていたら、もう置かない。** 置いても片付ける人がいない
            if (!IsTutorialScene() && !_roundStartCleanupDone)
            {
                var selfRect = transform as RectTransform;
                if (selfRect != null) Effects.TileClatterEffect.Attach(selfRect, RoundWaitClatterOffsetY);
            }
        }

        /// <summary>局名を出したあとに置く裏牌の高さ。**画面の下端から測る**（待ち画面と同じ）。</summary>
        private const float RoundWaitClatterOffsetY = 112f;

        /// <summary>チュートリアルには出さない。台本が進む画面なので、触らせる時間が無い。</summary>
        private bool IsTutorialScene()
        {
            var ui = FindFirstObjectByType<GameUIManager>();
            return ui != null && ui.IsTutorialMode;
        }

        /// <summary>
        /// 配牌が済んだ印を立て、裏牌の山を片付ける。
        ///
        /// **暗転していなくても呼べるように切り出してある。** 片付けの引き金は
        /// 配牌完了の1回きりで、暗転していないと素通りしていた。
        /// </summary>
        /// <summary>配牌待ちの始まり。ここからは裏牌の山を置いてよい。</summary>
        internal void BeginRoundStartWait()
        {
            _roundStartCleanupDone = false;
            IsRoundStartResetPending = true;

            // 前の局で積んだままになっていたら降ろす。立てっぱなしだと
            // 次の「降りている最中の配牌完了」を拾い損ねる
            _fadeOutPendingUntilDarkened = false;
        }

        internal void MarkRoundStartCleanupDone()
        {
            _roundStartCleanupDone = true;

            // TileClatterEffect は破棄せず、次のマッチング待ちで再利用できる。
            var selfRect = transform as RectTransform;
            if (selfRect != null) Effects.TileClatterEffect.Hide(selfRect);
        }

        private IEnumerator RoundStartFadeOutRoutine(Action onComplete)
        {
            // 配牌が完了したら、局名と一緒に手混ぜ用の牌の山も消す。
            // 呼び出し元（PlayRoundStartFadeOut）が先に済ませているが、
            // 直接呼ばれても困らないようにここでも通しておく。
            MarkRoundStartCleanupDone();

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
