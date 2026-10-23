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
            progress.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 764f * progressStart);
            subtitles.Hide();
            SetCharacter("通常時身体", "通常顔");
            SetTurnCounter(17); // 「人生最後の牌」の場面。両者17打ち終える最後の1打
            transform.localScale = Vector3.one * .4f;
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime)
            {
                transform.localScale = Vector3.one * Mathf.SmoothStep(.4f, 1f, t);
                yield return null;
            }
            transform.localScale = Vector3.one;
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
            // プレイヤー側の席へ既存の先輩素材を置き、力を失って手前へ倒れる様子を出す。
            // 先輩はドット絵（29×29、2026-10-20）。20倍の 580 で出してドットを揃える
            var senior = Box("FallenSenior", viewport, new Vector2(580f, 580f),
                new Vector2(-150f, -160f), Color.white);
            senior.GetComponent<Image>().sprite = Resources.Load<Sprite>("女の子/tutorial_girl_px29_body");
            var face = Box("FallenSeniorFace", senior, senior.sizeDelta, Vector2.zero, Color.white);
            face.GetComponent<Image>().sprite = Resources.Load<Sprite>("女の子/tutorial_girl_px29_face");
            // 倒れながらの「ぎゃー」は出さない（2026-10-22 に足し、2026-10-24 ユーザー指示「『ぎゃー』っていう先輩はいらない」で外した）
            Vector2 start = senior.anchoredPosition;
            // 少しだけ前へ傾きながら沈む。**大きく回して倒さない**（2026-10-23。立ち絵を 70 度回して倒していたのが
            // 「滑稽すぎる」とプランナーに言われ、倒れ伏した絵に替えた）
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / .35f)
            {
                float p = Mathf.Clamp01(t * t);
                senior.anchoredPosition = start + new Vector2(-30f, -60f) * p;
                senior.localRotation = Quaternion.Euler(0, 0, 16f * p);
                yield return null;
            }
            // 倒れ伏した絵へ切り替える。卓の手前の縁へ上半身を投げ出した姿（37×27 のドット絵を 16 倍）。
            // **この絵だけは画像生成で作った**（2026-10-23 ユーザーが許可。AGENTS.md 第7項の例外）。
            // 先輩の 29×29 のドット絵と同じ色・同じ粗さに落としてある
            var fallen = Resources.Load<Sprite>("女の子/tutorial_senior_fallen");
            if (fallen != null)
            {
                face.gameObject.SetActive(false);
                var image = senior.GetComponent<Image>();
                image.sprite = fallen;
                senior.localRotation = Quaternion.identity;
                senior.sizeDelta = new Vector2(fallen.rect.width, fallen.rect.height) * FallenScale;
                for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / .14f)
                {
                    // 落ちた勢いで一度だけ沈む
                    senior.anchoredPosition = FallenPosition + new Vector2(0f, 14f * (1f - Mathf.Clamp01(t)));
                    yield return null;
                }
                senior.anchoredPosition = FallenPosition;
            }
            else
            {
                senior.anchoredPosition = start + new Vector2(-90f, -240f);
                senior.localRotation = Quaternion.Euler(0, 0, 70f);
            }
            // 倒れ終わってから血が流れ出すまでの間（以前は「ぎゃー」が消えるまでの時間だった。間合いは変えない）
            yield return new WaitForSecondsRealtime(.8f);

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
            progress.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 764f);
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
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime * 2.5f)
            {
                transform.localScale = Vector3.one * Mathf.SmoothStep(1f, .4f, t);
                yield return null;
            }
        }

        /// <summary>倒れ伏した絵の拡大率と置き場所（動画の枠の中、真ん中から）。左下の手前に、頭を左上にして伏せる</summary>
        private const float FallenScale = 16f;
        private static readonly Vector2 FallenPosition = new Vector2(-120f, -205f);
    }
}
