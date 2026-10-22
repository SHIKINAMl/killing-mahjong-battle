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
            // 倒れながら軽く一声あげる（2026-10-22 のユーザー指示「軽く『ぎゃー』と言わせて」）。
            // 字幕ではなく、倒れていく頭のそばに字を出して一緒に動かす（同日「倒れる絵についていく感じで」）。
            // 字は傾けずに読める向きのまま、倒れ終わったら少しして消す
            var cry = Label("FallenSeniorCry", viewport, new Vector2(160f, 40f), Vector2.zero, 26f);
            cry.text = "ぎゃー";
            Vector2 start = senior.anchoredPosition;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 1.2f)
            {
                float p = Mathf.Clamp01(t * t);
                senior.anchoredPosition = start + new Vector2(-90f, -240f) * p;
                senior.localRotation = Quaternion.Euler(0, 0, 70f * p);
                FollowHead(cry.rectTransform, senior);
                yield return null;
            }
            senior.anchoredPosition = start + new Vector2(-90f, -240f);
            senior.localRotation = Quaternion.Euler(0, 0, 70f);
            FollowHead(cry.rectTransform, senior);
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / .8f)
            {
                cry.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 2f - 1f));
                yield return null;
            }
            Destroy(cry.gameObject);

            // その場で変わる赤い面だけを重ね、既存の卓の絵は変更しない。
            var stainRoot = new GameObject("ReplayTableStain", typeof(RectTransform), typeof(PixelShapeGraphic));
            stainRoot.transform.SetParent(viewport, false);
            ((RectTransform)stainRoot.transform).sizeDelta = new Vector2(800f, 600f);
            var stain = stainRoot.GetComponent<PixelShapeGraphic>();
            stain.raycastTarget = false;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 2f)
            {
                DrawStain(stain, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            DrawStain(stain, 1f);
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

        /// <summary>
        /// 倒れていく先輩の頭の右上に字を置く。頭は絵と一緒に回るが、字そのものは傾けない。
        /// 頭は 29×29 の絵の上から4段目あたり（20倍で中心から (10, 210)）。倒れ切ると頭は画面の外へ出るので、
        /// 字は動画の枠の内側に留める
        /// </summary>
        private static void FollowHead(RectTransform label, RectTransform senior)
        {
            Vector2 head = senior.anchoredPosition + (Vector2)(senior.localRotation * new Vector3(10f, 210f, 0f));
            Vector2 at = head + new Vector2(80f, 40f);
            label.anchoredPosition = new Vector2(Mathf.Clamp(at.x, -310f, 310f), Mathf.Clamp(at.y, -240f, 270f));
        }

        private static void DrawStain(PixelShapeGraphic stain, float amount)
        {
            stain.Begin();
            Color red = new Color(.65f, .015f, .035f, .78f * amount);
            // 卓の手前から奥へ、整数単位の帯で広げる。
            for (int i = 0; i < 16; i++)
            {
                float y = -300f + i * 13f;
                float width = Mathf.Max(0f, 800f - i * 24f) * amount;
                stain.Box(-width * .5f, y, width * .5f, y + 13f, red);
            }
            stain.End();
        }
    }
}
