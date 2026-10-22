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
            // 倒れながら軽く一声あげる（2026-10-22 のユーザー指示「軽く『ぎゃー』と言わせて」）。
            // 字幕ではなく、倒れていく頭のそばに字を出して一緒に動かす（同日「倒れる絵についていく感じで」）。
            // 字は傾けずに読める向きのまま、倒れ終わったら少しして消す
            var cry = Label("FallenSeniorCry", viewport, new Vector2(160f, 40f), Vector2.zero, 26f);
            cry.text = "ぎゃー";
            Vector2 start = senior.anchoredPosition;
            // 少しだけ前へ傾きながら沈む。**大きく回して倒さない**（2026-10-23。立ち絵を 70 度回して倒していたのが
            // 「滑稽すぎる」とプランナーに言われ、倒れ伏した絵に替えた）
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / .35f)
            {
                float p = Mathf.Clamp01(t * t);
                senior.anchoredPosition = start + new Vector2(-30f, -60f) * p;
                senior.localRotation = Quaternion.Euler(0, 0, 16f * p);
                FollowHead(cry.rectTransform, senior);
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
                    PlaceCryAtFallenHead(cry.rectTransform, senior);
                    yield return null;
                }
                senior.anchoredPosition = FallenPosition;
                PlaceCryAtFallenHead(cry.rectTransform, senior);
            }
            else
            {
                senior.anchoredPosition = start + new Vector2(-90f, -240f);
                senior.localRotation = Quaternion.Euler(0, 0, 70f);
                FollowHead(cry.rectTransform, senior);
            }
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
            // 血は倒れた先輩の下から広がる。先輩より奥に描く（手前に描くと先輩まで赤く塗られる）
            stainRoot.transform.SetSiblingIndex(senior.GetSiblingIndex());
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

        /// <summary>倒れ伏した絵の拡大率と置き場所（動画の枠の中、真ん中から）。左下の手前に、頭を左上にして伏せる</summary>
        private const float FallenScale = 16f;
        private static readonly Vector2 FallenPosition = new Vector2(-120f, -205f);

        /// <summary>倒れ伏した絵の頭（37×27 の左上寄り、左から 6・上から 7 ドットあたり）の右上に字を置く。</summary>
        private static void PlaceCryAtFallenHead(RectTransform label, RectTransform senior)
        {
            Vector2 size = senior.sizeDelta;
            Vector2 head = senior.anchoredPosition + new Vector2(-size.x * .5f + 6f * FallenScale, size.y * .5f - 7f * FallenScale);
            Vector2 at = head + new Vector2(110f, 70f);
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
