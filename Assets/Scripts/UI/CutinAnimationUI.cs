using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public class CutinAnimationUI : MonoBehaviour
    {
        private readonly PresentationScopeSet presentations = new PresentationScopeSet(Debug.LogException);
        public void CancelPresentation()
        {
            presentations.CancelAll();
            StopAllCoroutines();
        }
        private void OnDisable() { CancelPresentation(); }
        /// <summary>
        /// カットインを出す。
        /// </summary>
        /// <param name="intensity">
        /// 手の大きさ。0 で満貫級、1 で役満級（2026-09-15 に追加）。
        /// **大きいほど、文字が強く叩きつけられ、寄りが深く、間が長くなる。**
        /// 同じ絵で大小を表すために、この3つを一緒に動かしている。
        /// </param>
        public void PlayCutin(Sprite characterSprite, Sprite faceSprite, TMP_FontAsset font,
                              string cutinText, System.Action onComplete, float intensity = 0f)
        {
            StartCoroutine(CutinRoutine(characterSprite, faceSprite, font, cutinText, onComplete,
                                        Mathf.Clamp01(intensity)));
        }

        /// <summary>文字の複製を本体のすぐ後ろに作る。中身と見た目は本体に合わせる。</summary>
        private static TextMeshProUGUI CreateGhost(TextMeshProUGUI source, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(source.transform.parent, false);
            go.transform.SetSiblingIndex(source.transform.GetSiblingIndex());
            var ghost = go.AddComponent<TextMeshProUGUI>();
            ghost.text = source.text;
            ghost.font = source.font;
            ghost.fontSize = source.fontSize;
            ghost.fontStyle = source.fontStyle;
            ghost.alignment = source.alignment;
            ghost.color = color;
            ghost.raycastTarget = false;
            var rt = ghost.rectTransform;
            rt.anchorMin = source.rectTransform.anchorMin;
            rt.anchorMax = source.rectTransform.anchorMax;
            rt.sizeDelta = source.rectTransform.sizeDelta;
            rt.anchoredPosition = source.rectTransform.anchoredPosition;
            return ghost;
        }

        private IEnumerator CutinRoutine(Sprite characterSprite, Sprite faceSprite, TMP_FontAsset font,
                                         string cutinText, System.Action onComplete, float intensity)
        {
            using (var scope = presentations.Begin())
                yield return scope.Run(CutinVisualsRoutine(scope, characterSprite, faceSprite, font, cutinText, onComplete, intensity));
        }

        private IEnumerator CutinVisualsRoutine(PresentationScope scope, Sprite characterSprite, Sprite faceSprite, TMP_FontAsset font,
                                         string cutinText, System.Action onComplete, float intensity)
        {
            // 1. ルートコンテナの作成
            GameObject root = scope.Own(new GameObject("CutinRoot"));
            root.transform.SetParent(transform, false);
            RectTransform rootRt = root.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.sizeDelta = Vector2.zero;
            
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = UISortingOrders.CutinAnimation;

            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;

            // 2. 背景（半透明の黒）
            GameObject bgObj = new GameObject("BgDimmer");
            bgObj.transform.SetParent(rootRt, false);
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 0.7f);
            RectTransform bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;

            // 2.5 RevealMask（右上から左下へマスク解除するためのコンテナ）
            // 全体を覆う十分なサイズを持たせ、15度傾けることで斜めにフィルされるようにする
            GameObject revealObj = new GameObject("RevealMask");
            revealObj.transform.SetParent(rootRt, false);
            RectTransform revealRt = revealObj.AddComponent<RectTransform>();
            revealRt.anchorMin = new Vector2(0.5f, 0.5f);
            revealRt.anchorMax = new Vector2(0.5f, 0.5f);
            revealRt.sizeDelta = new Vector2(4000f, 4000f);
            revealRt.localRotation = Quaternion.Euler(0, 0, 15f); // 全体を15度傾ける

            Image revealImg = revealObj.AddComponent<Image>();
            revealImg.type = Image.Type.Filled;
            revealImg.fillMethod = Image.FillMethod.Horizontal;
            revealImg.fillOrigin = (int)Image.OriginHorizontal.Right; // 右側からフィル
            revealImg.fillAmount = 0f; // 初期状態では中身が見えない

            Mask revealMask = revealObj.AddComponent<Mask>();
            revealMask.showMaskGraphic = false;

            // 3. 斜めの帯を生成
            GameObject stripeObj = new GameObject("StripeContainer");
            stripeObj.transform.SetParent(revealRt, false);
            RectTransform stripeRt = stripeObj.AddComponent<RectTransform>();
            stripeRt.anchorMin = new Vector2(0.5f, 0.5f);
            stripeRt.anchorMax = new Vector2(0.5f, 0.5f);
            stripeRt.sizeDelta = new Vector2(3500f, 600f);
            stripeRt.anchoredPosition = Vector2.zero;
            stripeRt.localRotation = Quaternion.Euler(0, 0, 0f); // 親が15度傾いているのでこちらは0度

            Image stripeImg = stripeObj.AddComponent<Image>();
            stripeImg.color = new Color(0.85f, 0f, 0.5f, 1f); // 派手なピンク系

            // 濃いピンクが背景に溶けないよう黒で縁取る。
            // Mask を付ける前に作ること。Mask は自分の子だけを切り抜くので、
            // 背面の兄弟であるこの縁は影響を受けない。
            KillingMahjong.Visuals.UIEdgeOutline.AddBehind(stripeRt, 10f);

            Mask stripeMask = stripeObj.AddComponent<Mask>();
            stripeMask.showMaskGraphic = true;

            // 4. キャラクター画像（帯の中でマスクされる）
            GameObject charObj = new GameObject("CharacterImage");
            charObj.transform.SetParent(stripeRt, false);
            Image charImg = charObj.AddComponent<Image>();
            charImg.sprite = characterSprite;
            charImg.preserveAspect = true;
            RectTransform charRt = charObj.GetComponent<RectTransform>();
            charRt.anchorMin = new Vector2(0.5f, 0.5f);
            charRt.anchorMax = new Vector2(0.5f, 0.5f);
            charRt.sizeDelta = new Vector2(1000f, 1000f);
            charRt.localRotation = Quaternion.Euler(0, 0, -15f); // キャラは傾きをキャンセルしてまっすぐに
            charRt.anchoredPosition = new Vector2(-200f, -150f); // 左側に寄せつつ、少し上に調整

            // 4.5 顔パーツ画像（身体の上に乗せる）
            if (faceSprite != null)
            {
                GameObject faceObj = new GameObject("FaceImage");
                faceObj.transform.SetParent(charRt, false); // 身体の子要素にする
                Image faceImg = faceObj.AddComponent<Image>();
                faceImg.sprite = faceSprite;
                faceImg.preserveAspect = true;
                RectTransform faceRt = faceObj.GetComponent<RectTransform>();
                faceRt.anchorMin = Vector2.zero;
                faceRt.anchorMax = Vector2.one;
                faceRt.sizeDelta = Vector2.zero;
                faceRt.anchoredPosition = Vector2.zero;
            }

            // 5. テキスト
            GameObject textObj = new GameObject("RonText");
            textObj.transform.SetParent(revealRt, false); // revealRtの子にする
            TextMeshProUGUI textMesh = textObj.AddComponent<TextMeshProUGUI>();
            textMesh.text = string.IsNullOrEmpty(cutinText) ? "ロン！！" : cutinText;
            if (font != null) textMesh.font = font;
            textMesh.color = Color.white;
            textMesh.fontSize = KillingMahjong.Common.UITypography.Giant;
            textMesh.fontStyle = FontStyles.Bold | FontStyles.Italic;
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.outlineWidth = 0.2f;
            textMesh.outlineColor = Color.black;

            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.5f, 0.5f);
            textRt.anchorMax = new Vector2(0.5f, 0.5f);
            textRt.sizeDelta = new Vector2(1000f, 400f);
            textRt.localRotation = Quaternion.Euler(0, 0, 0f); // 親が15度傾いているので0度
            textRt.anchoredPosition = new Vector2(300f, 0); // 右側に寄せる

            // 5.5 **威圧のための部品（2026-10-18）。**
            // 宣伝動画の「ロンされたら一巻の終わり」の見せ場。相手が「ばん！」と叩きつけて
            // こちらを黙らせる感じを、今ある絵だけで出す（専用の絵はまだ無い。AIでは描かない）。
            float screenW = Mathf.Max(1f, rootRt.rect.width);
            float screenH = Mathf.Max(1f, rootRt.rect.height);

            // 集中線。暗幕より手前、帯より奥。中心は文字と顔が入るぶん空ける
            var linesObj = new GameObject("FocusLines", typeof(RectTransform));
            linesObj.transform.SetParent(rootRt, false);
            linesObj.transform.SetSiblingIndex(bgObj.transform.GetSiblingIndex() + 1);
            var linesRt = (RectTransform)linesObj.transform;
            linesRt.anchorMin = Vector2.zero;
            linesRt.anchorMax = Vector2.one;
            linesRt.sizeDelta = Vector2.zero;
            var lines = linesObj.AddComponent<Effects.PerspectiveFocusLines>();
            lines.raycastTarget = false;
            lines.Setup(Vector2.zero, new Vector2(screenW * 0.34f, screenH * 0.30f), screenW / 800f);
            lines.Progress = 0f;

            // 文字の残像。赤と黒を本体の後ろでずらして、震えを見せる
            TextMeshProUGUI redGhost = CreateGhost(textMesh, "RonGhostRed", new Color(0.9f, 0.05f, 0.15f, 0.85f));
            TextMeshProUGUI darkGhost = CreateGhost(textMesh, "RonGhostDark", new Color(0.1f, 0f, 0.05f, 0.8f));
            redGhost.gameObject.SetActive(false);
            darkGhost.gameObject.SetActive(false);

            // 叩いた瞬間の光。いちばん手前
            GameObject flashObj = new GameObject("ImpactFlash");
            flashObj.transform.SetParent(rootRt, false);
            Image flashImg = flashObj.AddComponent<Image>();
            flashImg.raycastTarget = false;
            flashImg.color = new Color(1f, 1f, 1f, 0f);
            RectTransform flashRt = flashObj.GetComponent<RectTransform>();
            flashRt.anchorMin = Vector2.zero;
            flashRt.anchorMax = Vector2.one;
            flashRt.sizeDelta = Vector2.zero;

            // 6. アニメーション
            stripeRt.anchoredPosition = Vector2.zero;
            textRt.anchoredPosition = new Vector2(300f, 0);
            Vector2 textHome = textRt.anchoredPosition;
            var audio = KillingMahjong.Managers.AudioManager.Instance;

            // ⓪ 溜め。**音を沈めて、暗くして、線を寄せる。** 叩く前に一瞬黙らせると、
            // 次の一撃が大きく聞こえる。大きい手ほど長く溜める
            float holdIn = Mathf.Lerp(0.38f, 0.55f, intensity);
            bool muffledHere = false;
            if (audio != null)
            {
                // 透視の「水の底」が既に掛かっていたら、そちらの持ち物なので触らない
                if (!audio.IsBgmDeepMuffled)
                {
                    audio.SetBgmDeepMuffle(true, holdIn * 0.6f);
                    muffledHere = true;
                    scope.AddCleanup(() =>
                    {
                        var a = KillingMahjong.Managers.AudioManager.Instance;
                        if (muffledHere && a != null) a.SetBgmDeepMuffle(false, 0.25f);
                        muffledHere = false;
                    });
                }
                audio.PlayHeartbeat(KillingMahjong.Managers.HeartbeatStrength.Strong,
                                    KillingMahjong.Managers.HeartbeatSpacing.Compact);
            }

            // **溜めの間は帯を伏せる。** 塗りの量を 0 にしても、帯は隠れずに出ていた
            // （2026-10-18 の録画で確認）。帯が出ていると暗くなるのが見えず、溜めにならない
            revealObj.SetActive(false);

            const float DimPeak = 0.82f;
            bgImg.color = new Color(0, 0, 0, 0f);
            float t = 0f;
            while (t < holdIn)
            {
                float k = t / holdIn;
                // 早めに暗くして、暗いまま黙って待たせる。そこが溜めになる
                bgImg.color = new Color(0, 0, 0, Mathf.Lerp(0f, DimPeak, 1f - (1f - k) * (1f - k)));
                lines.Progress = Mathf.Lerp(0f, 0.55f, k);
                t += Time.deltaTime;
                yield return null;
            }
            bgImg.color = new Color(0, 0, 0, DimPeak);

            // ① ばん！ 帯・文字・光・音・揺れを**同じ1コマに揃える。** ずれると当たりが弱くなる
            if (audio != null)
            {
                audio.PlayStinger("br_blackout");
                audio.PlayStinger("se_skill_strike");
                audio.PlayRonVoice();
            }
            revealObj.SetActive(true);
            revealImg.fillAmount = 1f;
            lines.Progress = 1f;
            redGhost.gameObject.SetActive(true);
            darkGhost.gameObject.SetActive(true);
            Effects.ScreenQuake.Play(Mathf.Lerp(22f, 42f, intensity),
                                     Mathf.Lerp(0.45f, 0.75f, intensity));

            // 光は白を2コマ、そのあと赤く残して引く
            flashImg.color = new Color(1f, 1f, 1f, 0.9f);

            // 文字を叩きつける。大きい手ほど大きいところから落ちてくる
            float slamFrom = Mathf.Lerp(2.6f, 3.4f, intensity);
            float slamTime = 0.07f;
            t = 0f;
            while (t < slamTime)
            {
                float k = t / slamTime;
                textRt.localScale = Vector3.one * Mathf.Lerp(slamFrom, 1f, k * k);
                redGhost.rectTransform.localScale = darkGhost.rectTransform.localScale = textRt.localScale;
                if (t > 0.05f) flashImg.color = new Color(0.85f, 0f, 0.1f, 0.5f);
                t += Time.deltaTime;
                yield return null;
            }

            // ヒットストップ。**潰れた形のまま止める。** 止めた間に、目が「当たった」と読む
            float stopTime = Mathf.Lerp(0.08f, 0.13f, intensity);
            Vector3 squash = new Vector3(1.08f, 0.9f, 1f);
            textRt.localScale = squash;
            redGhost.rectTransform.localScale = darkGhost.rectTransform.localScale = squash;
            flashImg.color = new Color(0.85f, 0f, 0.1f, 0.4f);
            yield return new WaitForSeconds(stopTime);

            // ② 余韻。文字が震え続け、キャラへゆっくり寄る。光と線は引いていく
            float lingerTime = Mathf.Lerp(0.95f, 1.3f, intensity);
            float tremble = Mathf.Lerp(2f, 5f, intensity);
            float pushTo = Mathf.Lerp(1.06f, 1.16f, intensity);
            Vector3 charScale = charRt.localScale;
            float nextJitter = 0f;
            Vector2 jitter = Vector2.zero, ghostOffset = Vector2.zero;
            t = 0f;
            while (t < lingerTime)
            {
                float k = Mathf.Clamp01(t / lingerTime);
                float settle = Mathf.Clamp01(t / 0.08f);
                textRt.localScale = Vector3.Lerp(squash, Vector3.one, 1f - Mathf.Pow(1f - settle, 3f));
                charRt.localScale = charScale * Mathf.Lerp(1f, pushTo, k);
                // 赤は短く引く。長く残すと顔が赤く塗りつぶされて、誰が叩いたのか見えない
                flashImg.color = new Color(0.85f, 0f, 0.1f, Mathf.Lerp(0.4f, 0f, Mathf.Clamp01(t / 0.16f)));
                lines.Progress = Mathf.Lerp(1f, 0.45f, k);

                // 震えはコマ打ちにする（毎フレーム動かすと、ぬるっとして怖くない）
                if (t >= nextJitter)
                {
                    float amp = tremble * Mathf.Lerp(1f, 0.4f, k);
                    jitter = new Vector2(Random.Range(-amp, amp), Random.Range(-amp, amp));
                    ghostOffset = new Vector2(Random.Range(3f, 6f + tremble), Random.Range(-2f, 2f));
                    nextJitter = t + 1f / 15f;
                }
                textRt.anchoredPosition = textHome + jitter;
                redGhost.rectTransform.anchoredPosition = textRt.anchoredPosition + ghostOffset;
                darkGhost.rectTransform.anchoredPosition = textRt.anchoredPosition - ghostOffset;
                redGhost.rectTransform.localScale = darkGhost.rectTransform.localScale = textRt.localScale;

                t += Time.deltaTime;
                yield return null;
            }

            // ③ 全体がフェードアウトして消える。音もここで浮かせる
            if (muffledHere && audio != null) audio.SetBgmDeepMuffle(false, 0.25f);
            muffledHere = false;
            t = 0;
            float outDuration = 0.15f;
            while (t < outDuration)
            {
                float progress = t / outDuration;
                canvasGroup.alpha = 1f - progress;
                t += Time.deltaTime;
                yield return null;
            }

            Destroy(root);
            onComplete?.Invoke();
        }
    }
}
