using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    public sealed partial class TutorialDrawReplayUI
    {
        public const string ContinuationEffectId = "tutorial.draw_replay_continuation";

        /// <summary>同じ動画空間の後半。シーク位置2/3から始め、終了後は閉じる入力を待つ。</summary>
        public IEnumerator PlayContinuation(bool autoAdvance = false)
        {
            automatic = autoAdvance;
            IsPaused = false;
            StepIndex = 0;
            stepCount = 5;
            progressStart = 2f / 3f;
            progressEnd = 1f;
            SetSeekProgress(progressStart);
            subtitles.Hide();
            SetCharacter("通常時身体", "通常顔");
            SetTurnCounter(17); // 「人生最後の牌」の場面。両者17打ち終える最後の1打
            yield return OpenFromPhone();
            foreach (var canvas in phase.GetComponentsInChildren<Canvas>(true))
            {
                canvas.enabled = true; canvas.overrideSorting = true; canvas.sortingOrder = 91;
            }
            yield return Step("流局に次ぐ流局……", false);
            yield return Step("さすがにアナタも無事ではいられないでしょう？", false);
            yield return Step("………", false);
            yield return Step("血を抜かれすぎてもう返事もできないのかしらね？", false);
            yield return Step("さぁ……\n人生最後の牌をどうぞ？", false);

            normal.text = "";
            // **いきなり倒れ伏した先輩の絵をフェードで出し、そのあと血を流す**（2026-10-28 要望
            // 「いきなりこの絵でいい。①先輩の倒れる立ち絵がフェードで出る ②血の出るアニメーションが流れる」）。
            // 以前は座った先輩（tutorialexe）を出して前へ傾け沈めてから、この絵へ切り替えていた。
            // 支給された絵は元の解像度と縦横比のまま出す。
            var fallen = Resources.Load<Sprite>("女の子/tutorial_girl_falldown");
            var senior = Box("FallenSenior", viewport,
                fallen != null ? new Vector2(fallen.rect.width, fallen.rect.height) * FallenScale : new Vector2(155f, 190f) * SeniorScale,
                fallen != null ? FallenPosition : SeniorPosition, Color.white);
            var seniorImage = senior.GetComponent<Image>();
            seniorImage.sprite = fallen != null ? fallen : Resources.Load<Sprite>("女の子/tutorialexe");
            // ドット絵なので、透明度も 1 秒 12 コマで段々に上げる（毎フレームなめらかにしない）
            for (float t = 0f; t < FallenFadeSeconds; t += Time.unscaledDeltaTime)
            {
                float stepped = Mathf.Floor(t * 12f) / 12f;
                seniorImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(stepped / FallenFadeSeconds));
                yield return null;
            }
            seniorImage.color = Color.white;
            // 出きってから血が流れ出すまでの、ひと呼吸
            yield return new WaitForSecondsRealtime(.4f);

            // 血を重ね、既存の卓の絵は変更しない。
            var stainRoot = new GameObject("ReplayTableStain", typeof(RectTransform), typeof(PixelShapeGraphic));
            stainRoot.transform.SetParent(viewport, false);
            ((RectTransform)stainRoot.transform).sizeDelta = new Vector2(800f, 600f);
            // 血は倒れた先輩の下から広がる。先輩より奥に描く（手前に描くと先輩まで赤く塗られる）
            stainRoot.transform.SetSiblingIndex(senior.GetSiblingIndex());
            var stain = stainRoot.GetComponent<PixelShapeGraphic>();
            stain.raycastTarget = false;
            // 流れる様子は別に回し（TutorialDrawReplayUI.Blood.cs）、閉じるボタンを待つ間も流れ続けさせる
            StartCoroutine(Bleed(stain));
            yield return new WaitForSecondsRealtime(2.5f);
            SetSeekProgress(1f);
            IsPaused = true;
            paused.text = "再生終了";
            paused.gameObject.SetActive(true);
            var close = Label("ReplayCloseLabel", transform, new Vector2(240f, 48f), new Vector2(0f, -240f), 22f);
            close.text = "閉じる▼";
            var closeRect = (RectTransform)advance.transform;
            closeRect.sizeDelta = new Vector2(240f, 48f);
            closeRect.anchoredPosition = new Vector2(0f, -240f);
            advanced = false;
            advance.gameObject.SetActive(true);
            if (automatic) yield return new WaitForSecondsRealtime(1.8f);
            else yield return new WaitUntil(() => advanced);
            advance.gameObject.SetActive(false);
            close.gameObject.SetActive(false);
            yield return ReturnToPhone();
        }

        /// <summary>1000×747の支給画像を等倍で表示。透明な余白を含むので、人物の足元が枠の下端に来る位置に置く。</summary>
        private const float FallenScale = 1f;

        /// <summary>倒れ伏した先輩が出きるまでの時間[秒]</summary>
        private const float FallenFadeSeconds = .6f;

        /// <summary>倒れ伏した絵が読めなかったときの控え（座った先輩 155×190）の拡大率と置き場所</summary>
        private const float SeniorScale = 2f;
        private static readonly Vector2 SeniorPosition = new Vector2(-171f, -75f);
        private static readonly Vector2 FallenPosition = new Vector2(-120f, -50f);
    }
}
