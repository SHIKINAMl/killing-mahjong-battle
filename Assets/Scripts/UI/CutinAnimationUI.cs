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
        /// **大きいほど、揺れが強く、溜めと間が長くなる。**
        /// 同じ絵で大小を表すために、これらを一緒に動かしている。
        /// </param>
        public void PlayCutin(Sprite characterSprite, Sprite faceSprite, TMP_FontAsset font,
                              string cutinText, System.Action onComplete, float intensity = 0f)
        {
            StartCoroutine(CutinRoutine(characterSprite, faceSprite, font, cutinText, onComplete,
                                        Mathf.Clamp01(intensity)));
        }

        /// <summary>叩いた直後の影の位置（立ち絵・文字からのずれ）。ここからジリジリ動く。</summary>
        private static readonly Vector2 CharShadowBase = new Vector2(12f, -6f);
        private static readonly Vector2 GlyphShadowBase = new Vector2(8f, -7f);

        /// <summary>
        /// 文字は少し下に出て、すぐ上の正しい位置へ瞬間移動する（2026-10-20、ユーザー指示「五ミリくらい」）。
        /// 800×600 の画面を普通の大きさで見て、16 単位が 5mm くらい。
        /// </summary>
        private const float GlyphHopDown = 16f;
        private const float GlyphHopDelay = 0.04f;

        /// <summary>「ロ」を叩いた時の立ち絵の明るさ（0＝真っ黒、1＝そのまま）。「ン」で 1 になる。</summary>
        private const float CharPeekLevel = 0.4f;

        /// <summary>
        /// 立ち絵が色づいた瞬間からの赤い影のずれ。真後ろに隠れた所から、行き過ぎて戻るまでをコマ打ちで出す（2026-10-20）。
        /// </summary>
        private static Vector2 CharShadowPop(float since)
        {
            if (since < 0.035f) return Vector2.zero;
            if (since < 0.1f) return CharShadowBase * 2f;
            return CharShadowBase;
        }

        /// <summary>「ロン」の文字を収める、帯の中の右半分（帯の中心からの距離）。</summary>
        private const float TextAreaRight = 385f;
        private const float TextAreaWidth = 400f;

        /// <summary>
        /// 影のゆっくりしたずれ。時間でなめらかに変わる値を、2単位の格子へ丸める（1ドットずつ動く）。
        /// </summary>
        private static Vector2 Drift(float seed, float time, float rangeX, float rangeY)
        {
            float x = (Mathf.PerlinNoise(seed, time * 1.6f) - 0.5f) * 2f * rangeX;
            float y = (Mathf.PerlinNoise(time * 1.6f, seed + 7f) - 0.5f) * 2f * rangeY;
            return new Vector2(Mathf.Round(x / 2f) * 2f, Mathf.Round(y / 2f) * 2f);
        }

        /// <summary>叩きつける単位に分ける。1文字ずつ。「！」「!」などの記号は前の文字に付ける。</summary>
        private static System.Collections.Generic.List<string> SplitSlamParts(string text)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (char c in text)
            {
                bool mark = c == '！' || c == '!' || c == '？' || c == '?' || c == '…' || c == 'ー';
                if (mark && parts.Count > 0) parts[parts.Count - 1] += c;
                else parts.Add(c.ToString());
            }
            return parts;
        }

        /// <summary>
        /// 立ち絵と同じ形の影を、すぐ後ろの兄弟として作る。形だけを <paramref name="color"/> の1色で塗る
        /// （<c>Resources/Presentation/SilhouetteFillUI.shader</c>）。シェーダが無ければ作らない。
        /// </summary>
        private static Image CreateSilhouette(PresentationScope scope, RectTransform source, Sprite sprite, Color color)
        {
            var shader = Resources.Load<Shader>("Presentation/SilhouetteFillUI");
            if (shader == null || !shader.isSupported || sprite == null) return null;
            var material = new Material(shader) { name = "SilhouetteFillUI (Instance)" };
            scope.AddCleanup(() => { if (material != null) Destroy(material); });
            var go = new GameObject("CharacterShadow", typeof(RectTransform));
            go.transform.SetParent(source.parent, false);
            go.transform.SetSiblingIndex(source.GetSiblingIndex());
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.material = material;
            image.color = color;
            image.raycastTarget = false;
            var rt = image.rectTransform;
            rt.anchorMin = source.anchorMin;
            rt.anchorMax = source.anchorMax;
            rt.sizeDelta = source.sizeDelta;
            rt.localRotation = source.localRotation;
            rt.anchoredPosition = source.anchoredPosition + CharShadowBase;
            return image;
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
            stripeImg.color = Color.white; // 既存の切り抜き形状だけに使用する。

            Mask stripeMask = stripeObj.AddComponent<Mask>();
            stripeMask.showMaskGraphic = false;
            // 最初の子なので、後から挿入する靄・立ち絵より奥になる。
            RectTransform picture = Effects.CutinPixelBackgroundUI.Attach(stripeRt);

            // 4. キャラクター画像。**背景の絵の中だけに写す**（2026-10-20、ユーザー指示）。
            // 絵（picture）は帯の傾きを打ち消して水平なので、キャラは傾けずに置く。
            // 位置は、帯の中に置いていたころの (-200, -150) を、帯の傾き 15 度ぶん回したもの。
            GameObject charObj = new GameObject("CharacterImage");
            charObj.transform.SetParent(picture != null ? picture : stripeRt, false);
            Image charImg = charObj.AddComponent<Image>();
            charImg.sprite = characterSprite;
            charImg.preserveAspect = true;
            RectTransform charRt = charObj.GetComponent<RectTransform>();
            charRt.anchorMin = new Vector2(0.5f, 0.5f);
            charRt.anchorMax = new Vector2(0.5f, 0.5f);
            charRt.sizeDelta = new Vector2(1000f, 1000f);
            charRt.localRotation = picture != null ? Quaternion.identity : Quaternion.Euler(0, 0, -15f);
            charRt.anchoredPosition = picture != null ? new Vector2(-154f, -196f) : new Vector2(-200f, -150f);

            // 立ち絵の背面に置き、寄り・傾き・マスク・終了をカットインと共有する。
            Effects.RonAuraUI.BehindCharacter(charRt);

            // 4.2 立ち絵の後ろの赤い影（2026-10-20）。形だけを赤1色で塗り、叩いたあとジリジリと少しだけ動く
            Image charShadow = CreateSilhouette(scope, charRt, characterSprite, new Color(0.78f, 0.03f, 0.1f, 0.9f));
            RectTransform charShadowRt = charShadow != null ? charShadow.rectTransform : null;
            Vector2 charHome = charRt.anchoredPosition;
            if (charShadow != null) charShadow.gameObject.SetActive(false);

            // 立ち絵は**最初は真っ暗**で、「ロ」で少しだけ見え、「ロン」が揃った（最後の文字を叩いた）瞬間に
            // パッと色づく（2026-10-20）。色を黒にすると、絵の形のまま黒く写る
            charImg.color = Color.black;
            Image faceImg = null;

            // 4.5 顔パーツ画像（身体の上に乗せる）
            if (faceSprite != null)
            {
                GameObject faceObj = new GameObject("FaceImage");
                faceObj.transform.SetParent(charRt, false); // 身体の子要素にする
                faceImg = faceObj.AddComponent<Image>();
                faceImg.color = Color.black;
                faceImg.sprite = faceSprite;
                faceImg.preserveAspect = true;
                RectTransform faceRt = faceObj.GetComponent<RectTransform>();
                faceRt.anchorMin = Vector2.zero;
                faceRt.anchorMax = Vector2.one;
                faceRt.sizeDelta = Vector2.zero;
                faceRt.anchoredPosition = Vector2.zero;
            }

            // 5. テキスト。**1文字ずつ叩きつける**（「ロ」「ン」がバンバンと出る。2026-10-20）。
            // 「！」などの記号は前の文字と一緒に出す
            GameObject textObj = new GameObject("RonText", typeof(RectTransform));
            textObj.transform.SetParent(revealRt, false); // revealRtの子にする
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.5f, 0.5f);
            textRt.anchorMax = new Vector2(0.5f, 0.5f);
            textRt.sizeDelta = new Vector2(1000f, 400f);
            textRt.localRotation = Quaternion.Euler(0, 0, 0f); // 親が15度傾いているので0度
            textRt.anchoredPosition = new Vector2(300f, 0); // 右側に寄せる

            var glyphs = new System.Collections.Generic.List<TextMeshProUGUI>();
            float lineWidth = 0f;
            foreach (string part in SplitSlamParts(string.IsNullOrEmpty(cutinText) ? "ロン！！" : cutinText))
            {
                var glyphObj = new GameObject("RonGlyph", typeof(RectTransform));
                glyphObj.transform.SetParent(textRt, false);
                TextMeshProUGUI glyph = glyphObj.AddComponent<TextMeshProUGUI>();
                glyph.text = part;
                if (font != null) glyph.font = font;
                glyph.color = Color.white;
                glyph.fontSize = KillingMahjong.Common.UITypography.Giant;
                glyph.fontStyle = FontStyles.Bold | FontStyles.Italic;
                glyph.alignment = TextAlignmentOptions.Center;
                glyph.textWrappingMode = TextWrappingModes.NoWrap;
                glyph.outlineWidth = 0.2f;
                glyph.outlineColor = Color.black;
                glyph.raycastTarget = false;
                float w = glyph.GetPreferredValues(part).x;
                glyph.rectTransform.anchorMin = glyph.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                glyph.rectTransform.sizeDelta = new Vector2(w + 40f, 400f);
                glyph.rectTransform.anchoredPosition = new Vector2(lineWidth + w * 0.5f, 0f);
                lineWidth += w;
                glyphs.Add(glyph);
            }
            var glyphHomes = new Vector2[glyphs.Count];
            for (int i = 0; i < glyphs.Count; i++)
            {
                glyphs[i].rectTransform.anchoredPosition -= new Vector2(lineWidth * 0.5f, 0f);
                glyphHomes[i] = glyphs[i].rectTransform.anchoredPosition;
            }
            // 文字の並びを画面の右半分に収める。1文字ずつにしたら、2文字目が画面の右から切れた
            float textFit = Mathf.Min(1f, TextAreaWidth / Mathf.Max(1f, lineWidth));
            textRt.localScale = Vector3.one * textFit;
            float textX = Mathf.Min(300f, TextAreaRight - lineWidth * textFit * 0.5f);

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

            // 文字の影。黒に紫が少し滲んだ色で、文字が出たあと少しずつずれる（2026-10-20）
            var glyphShadows = new TextMeshProUGUI[glyphs.Count];
            for (int i = 0; i < glyphs.Count; i++)
            {
                glyphShadows[i] = CreateGhost(glyphs[i], "RonGlyphShadow", new Color(0.07f, 0.01f, 0.1f, 0.95f));
                glyphShadows[i].textWrappingMode = TextWrappingModes.NoWrap;
                glyphShadows[i].outlineWidth = 0.28f;
                glyphShadows[i].outlineColor = new Color32(70, 22, 92, 255);
                glyphShadows[i].gameObject.SetActive(false);
                glyphs[i].gameObject.SetActive(false);
            }

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
            textRt.anchoredPosition = new Vector2(textX, 0);
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
                float k = PresentationTiming.Sample(t) / holdIn;
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
                audio.PlayImpactSE();
                audio.PlayRonVoice();
            }
            revealObj.SetActive(true);
            revealImg.fillAmount = 1f;
            lines.Progress = 1f;
            Effects.ScreenQuake.Play(Mathf.Lerp(22f, 42f, intensity),
                                     Mathf.Lerp(0.45f, 0.75f, intensity));

            // 光は白を2コマ、そのあと赤く残して引く
            flashImg.color = new Color(1f, 1f, 1f, 0.9f);

            // 文字を1つずつ出す（パッ、パッ）。**大きさは変えず、その場に一度に出す**（2026-10-20、ユーザー指示）。
            // 前は手前の大きいところから落ちてきて潰れていたが、作り物っぽく見えるのでやめた。
            // 出す間合いは前と同じ（音ハメを崩さない）。
            // 2文字目からは音と揺れを足すが、光は1文字目だけ（白が2回だと目が痛い）
            float slamTime = 0.07f;
            float stopTime = Mathf.Lerp(0.08f, 0.13f, intensity);
            float extraSlams = 0f;
            for (int g = 0; g < glyphs.Count; g++)
            {
                if (g > 0)
                {
                    if (audio != null) audio.PlayImpactSE();
                    Effects.ScreenQuake.Play(Mathf.Lerp(14f, 26f, intensity), 0.3f);
                }
                RectTransform glyphRt = glyphs[g].rectTransform;
                // 文字の並びは画面に収めるため縮めてあるので、そのぶん割り戻して 5mm にする
                glyphRt.anchoredPosition = glyphHomes[g] + new Vector2(0f, -GlyphHopDown / textFit);
                // 1文字目は白い光（0.05 秒）に隠れるので、光が赤に変わってから跳ぶ
                float hopAt = g == 0 ? 0.05f + GlyphHopDelay : GlyphHopDelay;
                glyphs[g].gameObject.SetActive(true);
                // 最後の文字で立ち絵を色づけ、赤い影を後ろからずらして出す。
                // **音ハメ**：この文字の衝撃音（上の PlayImpactSE）と同じコマ。声の「ン」もここに来る
                bool pop = g == glyphs.Count - 1;
                if (!pop)
                {
                    // 途中の文字では少しだけ見せる。文字が多いときは、叩くたびに一段ずつ明るくする
                    float level = CharPeekLevel * (g + 1) / (glyphs.Count - 1);
                    var peek = new Color(level, level, level, 1f);
                    charImg.color = peek;
                    if (faceImg != null) faceImg.color = peek;
                }
                else
                {
                    charImg.color = Color.white;
                    if (faceImg != null) faceImg.color = Color.white;
                    if (charShadowRt != null)
                    {
                        charShadowRt.anchoredPosition = charHome;
                        charShadowRt.gameObject.SetActive(true);
                    }
                }
                float stop = g == 0 ? stopTime : stopTime * 0.75f;
                t = 0f;
                while (t < slamTime + stop)
                {
                    if (t >= hopAt) glyphRt.anchoredPosition = glyphHomes[g];
                    if (g == 0 && t > 0.05f)
                        flashImg.color = new Color(0.85f, 0f, 0.1f, t < slamTime ? 0.5f : 0.4f);
                    if (pop && charShadowRt != null) charShadowRt.anchoredPosition = charHome + CharShadowPop(t);
                    t += Time.deltaTime;
                    yield return null;
                }
                // 出た文字の後ろに影を出す
                glyphRt.anchoredPosition = glyphHomes[g];
                glyphShadows[g].rectTransform.anchoredPosition = glyphHomes[g] + GlyphShadowBase;
                glyphShadows[g].gameObject.SetActive(true);
                if (g > 0) extraSlams += slamTime + stop;
            }

            // ② 余韻。文字とキャラは止めたまま、**後ろの影だけを少しずつずらす**（2026-10-20）。
            // 前はキャラへゆっくり寄っていたが、寄るのをやめ、影がジリジリ動く形にした。光と線は引いていく。
            // 2文字目以降で延びたぶんを差し引き、全体の長さは前と同じくらいに保つ
            float lingerTime = Mathf.Max(0.7f, Mathf.Lerp(0.95f, 1.3f, intensity) - extraSlams);
            float flashFrom = flashImg.color.a;
            float nextStep = 0f;
            float seed = Random.value * 10f;
            t = 0f;
            while (t < lingerTime)
            {
                float k = Mathf.Clamp01(PresentationTiming.Sample(t) / lingerTime);
                // 赤は短く引く。長く残すと顔が赤く塗りつぶされて、誰が叩いたのか見えない
                flashImg.color = new Color(0.85f, 0f, 0.1f, Mathf.Lerp(flashFrom, 0f, Mathf.Clamp01(PresentationTiming.Sample(t) / 0.16f)));
                lines.Progress = Mathf.Lerp(1f, 0.45f, k);

                // 影はコマ打ちで、2単位ずつ動かす（なめらかに動かすと、ジリジリ感が出ない）
                if (t >= nextStep)
                {
                    nextStep = t + 1f / 10f;
                    if (charShadowRt != null)
                        charShadowRt.anchoredPosition = charHome + CharShadowBase + Drift(seed, t, 5f, 3f);
                    for (int g = 0; g < glyphs.Count; g++)
                        glyphShadows[g].rectTransform.anchoredPosition = glyphHomes[g] + GlyphShadowBase + Drift(seed + 3f + g, t, 4f, 3f);
                }

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
                float progress = PresentationTiming.Sample(t) / outDuration;
                canvasGroup.alpha = 1f - progress;
                t += Time.deltaTime;
                yield return null;
            }

            Destroy(root);
            onComplete?.Invoke();
        }
    }
}
