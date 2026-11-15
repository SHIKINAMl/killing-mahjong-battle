using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// ロン入力直後の手振り、破裂吹き出し、体力表示の揺れ。
    /// 画面、盤面、立ち絵には ScreenQuake / ScreenFlash を使わない。
    /// 勢いは吹き出しの叩きつけ・集中線・音・体力表示の揺れで出す（2026-10-18）。
    /// </summary>
    public static class RonImpactEffect
    {
        private const float HandFrameSeconds = 0.22f;
        private const float HandRiseSeconds = 0.14f;
        // 2026-10-18「自分からロンする時の勢いも強くして」で、揺れと間を大きくした（前は 0.30秒 / 0.56秒 / 12px）
        private const float HpShakeSeconds = 0.45f;
        private const float CalloutHoldSeconds = 0.85f;
        private const float HpShakePixels = 22f;

        /// <summary>吹き出しを叩きつけるときの、出始めの大きさ（倍）と、そこから落ちる時間。</summary>
        private const float SlamFromScale = 2.4f;
        private const float SlamSeconds = 0.06f;
        /// <summary>当たった瞬間に潰れた形で止める時間（ヒットストップ）。</summary>
        private const float HitStopSeconds = 0.07f;
        /// <summary>吹き出しの震えの幅[px]。だんだん収まる。</summary>
        private const float CalloutTremblePixels = 6f;

        private static readonly string[] HandFrameNames =
        {
            "UI/RonHands/ron_hand_01",
            "UI/RonHands/ron_hand_02",
            "UI/RonHands/ron_hand_03",
        };

        // ---- ボタン・手・吹き出しを1つの流れにつなぐ（2026-11-15 ユーザー指示） ----
        // 「せっかく吹き出しが出るならロンボタンと吹き出し、また手の部分も全部演出としてつながっている感じに」。
        // 押した雫のボタンをその場に残し、下から伸びた手の指先がそれを押し込み、雫が弾けて
        // 同じ場所で血しぶきの「ロン!!!」になる（吹き出しは km-docs screenshots/20261114/ron_bubble の案A）。

        /// <summary>手の絵（400x300）での、手3の指先の位置（画素。左上が原点）</summary>
        private static readonly Vector2 FingerTipPixel = new Vector2(254f, 219f);
        /// <summary>手の絵の1画素を何単位で映すか（線の太さをボタンの縁と揃えるため2倍のまま）</summary>
        private const float HandDot = 2f;
        /// <summary>指先を当てる所。雫のボタンの中心からのずれ（雫の下寄りの丸い所）</summary>
        private static readonly Vector2 FingerAimOnDrop = new Vector2(0f, -50f);
        /// <summary>指先が雫に届いてから弾けるまで（押し込んで潰れている間）</summary>
        private const float PressSeconds = 0.08f;
        /// <summary>血しぶきの吹き出しの絵（160x120 を2倍で映す）</summary>
        private const string BloodCalloutPath = "UI/RonCallout/ron_callout_blood";

        /// <summary>
        /// 押された雫のボタンを残したまま、手1・手2で指を伸ばし、手3で指先が雫を押し込む。
        /// 雫が弾けた所に血しぶきの「ロン!!!」を叩きつけ、体力表示を揺らしてから決着処理へ返す。
        /// <paramref name="ronButton"/> が無いとき（試写など）は画面の真ん中にボタンがあるものとして動かす。
        /// </summary>
        public static IEnumerator Play(MonoBehaviour runner, IList<RectTransform> hpPanels, RectTransform ronButton = null)
        {
            if (runner == null) yield break;

            GameObject overlay = CreateOverlay(out RectTransform overlayRt);
            // 作ったばかりの Canvas はまだ大きさが決まっていないので、CanvasScaler（800x600・Expand）と同じ式で測る
            float overlayScale = Mathf.Max(0.0001f, Mathf.Min(Screen.width / 800f, Screen.height / 600f));
            float overlayHeight = Screen.height / overlayScale;

            // 押されたボタンを、この演出の画面に同じ位置・同じ大きさで写す（本物のボタンは呼び出し側で隠す）
            Vector2 dropPosition = Vector2.zero;
            Vector2 dropSize = new Vector2(320f, 216f);
            Sprite pressedSprite = null;
            if (ronButton != null)
            {
                var button = ronButton.GetComponent<Button>();
                var buttonImage = ronButton.GetComponent<Image>();
                pressedSprite = button != null && button.spriteState.pressedSprite != null
                    ? button.spriteState.pressedSprite
                    : buttonImage != null ? buttonImage.sprite : null;
                // ロンのボタンは Screen Space - Overlay なので、ワールド座標がそのまま画面の画素になる
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, ronButton.TransformPoint(ronButton.rect.center));
                dropPosition = (screen - new Vector2(Screen.width, Screen.height) * .5f) / overlayScale;
                dropSize = ronButton.rect.size * ronButton.lossyScale.x / overlayScale;
            }
            Image drop = null;
            if (pressedSprite != null)
            {
                drop = CreateImage("RonDropPressed", overlayRt);
                drop.sprite = pressedSprite;
                SetRect(drop.rectTransform, dropPosition, dropSize);
            }

            // 手は画面の下端から伸ばす。指先が雫に届く高さまで持ち上げ、下にできる隙間は腕の最下段の1列を伸ばして埋める
            Image handImage = CreateImage("RonHand", overlayRt);
            handImage.preserveAspect = true;
            SetRect(handImage.rectTransform, Vector2.zero, new Vector2(400f * HandDot, 300f * HandDot));
            handImage.rectTransform.anchorMin = handImage.rectTransform.anchorMax = new Vector2(.5f, 0);
            handImage.rectTransform.pivot = new Vector2(.5f, 0);
            Vector2 aim = dropPosition + FingerAimOnDrop * (dropSize.y / 216f);
            float tipFromBottom = (300f - FingerTipPixel.y) * HandDot;
            float handX = Mathf.Round(aim.x - (FingerTipPixel.x - 200f) * HandDot);
            float handLift = Mathf.Max(0f, Mathf.Round(aim.y + overlayHeight * .5f - tipFromBottom));
            Image armImage = CreateImage("RonArm", handImage.rectTransform);
            armImage.rectTransform.anchorMin = new Vector2(0f, 0f);
            armImage.rectTransform.anchorMax = new Vector2(1f, 0f);
            armImage.rectTransform.pivot = new Vector2(.5f, 1f);
            armImage.rectTransform.anchoredPosition = Vector2.zero;
            armImage.rectTransform.sizeDelta = new Vector2(0f, handLift + 4f);
            armImage.gameObject.SetActive(handLift > 0f);

            // 声「それ、ロン」は、指が動いている間に「それ」、吹き出しで「ロン」（2026-10-15）
            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio != null) audio.PlayRonInputSore();

            bool displayedAnyHand = false;
            Sprite handSprite = null, armSprite = null;
            for (int i = 0; i < HandFrameNames.Length; i++)
            {
                Texture2D handTexture = Resources.Load<Texture2D>(HandFrameNames[i]);
                if (handTexture == null)
                {
                    Debug.LogError("[RonImpactEffect] ロン用の手画像が見つかりません: Resources/" + HandFrameNames[i]);
                    continue;
                }

                Sprite previousHandSprite = handImage.sprite, previousArmSprite = armImage.sprite;
                handSprite = Sprite.Create(handTexture, new Rect(0f, 0f, handTexture.width, handTexture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                armSprite = Sprite.Create(handTexture, new Rect(0f, 0f, handTexture.width, 1f), new Vector2(0.5f, 0.5f), 100f);
                handImage.sprite = handSprite;
                armImage.sprite = armSprite;
                if (previousHandSprite != null) Object.Destroy(previousHandSprite);
                if (previousArmSprite != null) Object.Destroy(previousArmSprite);
                handImage.gameObject.SetActive(true);
                displayedAnyHand = true;
                if (i == 0) yield return RaiseHandFromBottom(handImage.rectTransform, new Vector2(handX, handLift));
                else if (i < HandFrameNames.Length - 1) yield return new WaitForSeconds(HandFrameSeconds);
            }

            if (!displayedAnyHand) handImage.gameObject.SetActive(false);

            // 指先が雫を押し込む。雫は横へ潰れる
            if (drop != null)
            {
                drop.rectTransform.localScale = new Vector3(1.08f, 0.86f, 1f);
                drop.rectTransform.anchoredPosition = dropPosition + new Vector2(0f, -dropSize.y * .07f);
                yield return new WaitForSeconds(PressSeconds);
                drop.gameObject.SetActive(false);
            }

            // 雫が弾けた所に、血しぶきの「ロン!!!」
            var lines = CreateFocusLines(overlayRt, dropPosition);
            Texture2D bloodTexture = Resources.Load<Texture2D>(BloodCalloutPath);
            Sprite bloodSprite = null, burstSprite = null, innerSprite = null;
            Texture2D burstTexture = null, innerTexture = null;
            RectTransform callout;
            if (bloodTexture != null)
            {
                bloodSprite = Sprite.Create(bloodTexture, new Rect(0f, 0f, bloodTexture.width, bloodTexture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                callout = CreateBloodCallout(overlayRt, bloodSprite, dropPosition,
                    new Vector2(bloodTexture.width * 2f, bloodTexture.height * 2f));
            }
            else
            {
                Debug.LogError("[RonImpactEffect] ロンの吹き出しの絵が見つかりません: Resources/" + BloodCalloutPath);
                burstTexture = CreateBurstTexture(BurstDot, 0f);
                innerTexture = CreateBurstTexture(BurstDot, BurstRim);
                burstSprite = Sprite.Create(burstTexture, new Rect(0f, 0f, burstTexture.width, burstTexture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                innerSprite = Sprite.Create(innerTexture, new Rect(0f, 0f, innerTexture.width, innerTexture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                callout = CreateBurstCallout(overlayRt, burstSprite, innerSprite);
                callout.anchoredPosition = dropPosition;
            }
            RonAuraUI.BehindCallout(callout);

            // 吹き出し・声・ドン・揺れを**同じ1コマに揃える**
            if (audio != null)
            {
                audio.PlayRonInputRon();
                audio.PlayImpactSE();
            }

            if (hpPanels != null)
            {
                for (int i = 0; i < hpPanels.Count; i++)
                {
                    if (hpPanels[i] != null)
                        runner.StartCoroutine(ShakeHpRoutine(hpPanels[i], HpShakePixels));
                }
            }

            yield return SlamCallout(callout, lines, CalloutHoldSeconds);
            Object.Destroy(overlay);
            if (handSprite != null) Object.Destroy(handSprite);
            if (armSprite != null) Object.Destroy(armSprite);
            if (bloodSprite != null) Object.Destroy(bloodSprite);
            if (burstSprite != null) Object.Destroy(burstSprite);
            if (burstTexture != null) Object.Destroy(burstTexture);
            if (innerSprite != null) Object.Destroy(innerSprite);
            if (innerTexture != null) Object.Destroy(innerTexture);
        }

        /// <summary>血しぶきの「ロン!!!」（字も絵に描き込んである）。叩きつけと震えは入れ物ごと動かす。</summary>
        private static RectTransform CreateBloodCallout(RectTransform root, Sprite sprite, Vector2 position, Vector2 size)
        {
            var group = new GameObject("RonBurst", typeof(RectTransform));
            group.transform.SetParent(root, false);
            var parent = (RectTransform)group.transform;
            SetRect(parent, position, size);
            Image splash = CreateImage("RonBloodSplash", parent);
            splash.sprite = sprite;
            SetRect(splash.rectTransform, Vector2.zero, size);
            return parent;
        }

        /// <summary>
        /// 吹き出しを大きいところから叩きつけ、潰れた形で一瞬止めてから、震わせながら収める。
        /// 吹き出しの中だけを白く光らせる（画面全体は光らせない）。
        /// </summary>
        private static IEnumerator SlamCallout(RectTransform callout, Effects.PerspectiveFocusLines lines, float total)
        {
            Image inner = callout.Find("RonBurstInner")?.GetComponent<Image>();
            Color innerColor = inner != null ? inner.color : Color.white;
            if (inner != null) inner.color = Color.white;
            if (lines != null) lines.Progress = 1f;
            Vector2 home = callout.anchoredPosition;

            float t = 0f;
            while (t < SlamSeconds)
            {
                float k = PresentationTiming.Sample(t) / SlamSeconds;
                callout.localScale = Vector3.one * Mathf.Lerp(SlamFromScale, 1f, k * k);
                t += Time.deltaTime;
                yield return null;
            }

            // ヒットストップ。潰れた形のまま止める
            callout.localScale = new Vector3(1.12f, 0.86f, 1f);
            yield return new WaitForSeconds(HitStopSeconds);

            float rest = Mathf.Max(0.1f, total - SlamSeconds - HitStopSeconds);
            float nextJitter = 0f;
            Vector2 jitter = Vector2.zero;
            t = 0f;
            while (t < rest && callout != null)
            {
                float k = Mathf.Clamp01(PresentationTiming.Sample(t) / rest);
                // 少し行き過ぎてから戻る
                float settle = Mathf.Clamp01(PresentationTiming.Sample(t) / 0.16f);
                float over = Mathf.Sin(settle * Mathf.PI) * 0.08f;
                callout.localScale = Vector3.Lerp(new Vector3(1.12f, 0.86f, 1f), Vector3.one, settle) + Vector3.one * over;
                if (inner != null) inner.color = Color.Lerp(Color.white, innerColor, Mathf.Clamp01(PresentationTiming.Sample(t) / 0.12f));
                if (lines != null) lines.Progress = Mathf.Lerp(1f, 0f, Mathf.Clamp01(PresentationTiming.Sample(t) / 0.5f));

                // 震えはコマ打ち（毎秒15回）
                if (t >= nextJitter)
                {
                    float amp = CalloutTremblePixels * (1f - k);
                    jitter = new Vector2(Mathf.Round(Random.Range(-amp, amp)), Mathf.Round(Random.Range(-amp, amp)));
                    nextJitter = t + 1f / 15f;
                }
                callout.anchoredPosition = home + jitter;

                t += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>吹き出しへ向かう集中線。手と吹き出しより奥に置く。</summary>
        private static Effects.PerspectiveFocusLines CreateFocusLines(RectTransform overlay, Vector2 center)
        {
            var go = new GameObject("RonFocusLines", typeof(RectTransform));
            go.transform.SetParent(overlay, false);
            go.transform.SetAsFirstSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var lines = go.AddComponent<Effects.PerspectiveFocusLines>();
            lines.raycastTarget = false;
            Canvas.ForceUpdateCanvases();
            float w = Mathf.Max(1f, rt.rect.width);
            lines.Setup(center, new Vector2(190f, 150f), w / 800f);
            lines.Progress = 0f;
            return lines;
        }

        private static IEnumerator RaiseHandFromBottom(RectTransform hand, Vector2 target)
        {
            float start = -hand.rect.height;
            hand.anchoredPosition = new Vector2(target.x, start);
            for (float elapsed = 0; elapsed < HandFrameSeconds; elapsed += Time.deltaTime)
            {
                float progress = Mathf.Clamp01(PresentationTiming.Sample(elapsed) / HandRiseSeconds);
                float eased = 1 - Mathf.Pow(1 - progress, 3);
                hand.anchoredPosition = new Vector2(target.x, Mathf.Round(Mathf.Lerp(start, target.y, eased)));
                yield return null;
            }
            hand.anchoredPosition = target;
        }

        private static GameObject CreateOverlay(out RectTransform overlayRt)
        {
            GameObject overlay = new GameObject(
                "RonInputCinematicOverlay",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = UISortingOrders.RonAnimation - 1;

            CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            overlay.GetComponent<GraphicRaycaster>().enabled = false;

            overlayRt = overlay.GetComponent<RectTransform>();
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;
            return overlay;
        }

        private static Image CreateImage(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        /// <summary>吹き出しの中心。画面中央からのずれ。</summary>
        private static readonly Vector2 BurstPosition = new Vector2(202f, 62f);

        /// <summary>吹き出しを1つの入れ物にまとめて作る。叩きつけと震えは入れ物ごと動かす。</summary>
        // ---- 吹き出しの見た目（2026-11-12 MnoA「吹き出しももう少し雰囲気が合うように」） ----
        // 前はクリーム色の漫画の吹き出しに、太字の「ロン!!!」を少し傾けて載せていた。
        // ほかの絵（手牌のボタン・型の札）に合わせ、黒い縁・赤い面・明るい字のべた塗りのドット絵にする。
        // 縁はなめらかな線ではなく1ドット＝2単位の階段。字はドットフォントを太字にも傾けもせず、
        // 1ドットが揃う大きさ（40）で置く（傾けたり太字にしたりすると、ドットが崩れてにじむ）。

        /// <summary>吹き出しの絵の1ドットが何単位か</summary>
        private const int BurstDot = 2;
        /// <summary>黒い縁の太さ（単位）</summary>
        private const float BurstRim = 12f;
        private static readonly Color32 BurstInk = new Color32(20, 12, 16, 255);
        private static readonly Color32 BurstFace = new Color32(194, 39, 45, 255);
        private static readonly Color32 BurstText = new Color32(250, 246, 228, 255);

        private static RectTransform CreateBurstCallout(RectTransform root, Sprite burstSprite, Sprite innerSprite)
        {
            var group = new GameObject("RonBurst", typeof(RectTransform));
            group.transform.SetParent(root, false);
            var parent = (RectTransform)group.transform;
            SetRect(parent, BurstPosition, new Vector2(292f, 234f));
            Vector2 position = Vector2.zero;

            Image outline = CreateImage("RonBurstOutline", parent);
            outline.sprite = burstSprite;
            outline.color = BurstInk;
            SetRect(outline.rectTransform, position, new Vector2(292f, 234f));

            Image inner = CreateImage("RonBurstInner", parent);
            inner.sprite = innerSprite;
            inner.color = BurstFace;
            SetRect(inner.rectTransform, position, new Vector2(292f, 234f));

            GameObject textObject = new GameObject("RonBurstText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = "\u30ed\u30f3!!!";
            // **吹き出しの赤い面の中に収める。** 40（フォントの4倍）で「ロン!!!」が幅 140。
            // 50 では谷のところで縁へはみ出した（2026-10-18 にも、はみ出して「コン!!」に見えたことがある）
            var pixelFont = Resources.Load<TMP_FontAsset>("PixelMplus10_DynamicFixed");
            if (pixelFont != null) text.font = pixelFont;
            text.enableAutoSizing = false;
            text.fontSize = 40f;
            text.fontStyle = FontStyles.Normal;
            text.color = BurstText;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            SetRect(text.rectTransform, position, new Vector2(170f, 60f));
            return parent;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>
        /// PDF最終ページの太い輪郭を、既存UIだけで再現する不規則な破裂形。
        /// 外側と内側で同じスプライトを重ね、濃色の太枠を作る。
        /// 戦況グラフのロンの吹き出し（MatchMomentumUI）も同じ形を使う。
        /// </summary>
        internal static Texture2D CreateBurstTexture() => CreateBurstTexture(1, 0f);

        /// <param name="dot">1ドットを何単位にするか。絵はこの分だけ小さく作る</param>
        /// <param name="inset">形を内側へ縮める幅（単位）。縁の内側の面を作るときに使う</param>
        internal static Texture2D CreateBurstTexture(int dot, float inset)
        {
            dot = Mathf.Max(1, dot);
            int width = 292 / dot;
            int height = 234 / dot;
            const int pointCount = 20;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;

            float[] px = new float[pointCount];
            float[] py = new float[pointCount];
            float[] jitter =
            {
                1.00f, 0.92f, 1.08f, 0.88f, 1.04f,
                0.93f, 1.10f, 0.87f, 1.02f, 0.94f,
                1.09f, 0.89f, 1.05f, 0.91f, 1.07f,
                0.86f, 1.03f, 0.90f, 1.11f, 0.88f
            };
            for (int i = 0; i < pointCount; i++)
            {
                float angle = (-90f + 360f * i / pointCount) * Mathf.Deg2Rad;
                float baseRadius = i % 2 == 0 ? 112f : 88f;
                float radius = (baseRadius * jitter[i] - inset) / dot;
                px[i] = width * 0.5f + Mathf.Cos(angle) * radius;
                py[i] = height * 0.5f + Mathf.Sin(angle) * radius;
            }

            Color transparent = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool inside = false;
                    int previous = pointCount - 1;
                    for (int i = 0; i < pointCount; i++)
                    {
                        bool crosses = ((py[i] > y) != (py[previous] > y)) &&
                            x < (px[previous] - px[i]) * (y - py[i]) /
                            (py[previous] - py[i]) + px[i];
                        if (crosses) inside = !inside;
                        previous = i;
                    }
                    texture.SetPixel(x, y, inside ? Color.white : transparent);
                }
            }

            texture.Apply(false, false);
            return texture;
        }

        /// <summary>
        /// FloatingAnimator が位置を上書きするため、体力表示だけ一時停止して揺らす。
        /// </summary>
        private static IEnumerator ShakeHpRoutine(RectTransform target, float amplitude)
        {
            if (target == null) yield break;

            Behaviour floatingAnimator = target.GetComponent("FloatingAnimator") as Behaviour;
            bool wasFloating = floatingAnimator != null && floatingAnimator.enabled;
            if (floatingAnimator != null) floatingAnimator.enabled = false;

            Vector2 home = target.anchoredPosition;
            EdgeCuts(target, out bool cutLeft, out bool cutRight, out bool cutBottom, out bool cutTop);
            float elapsed = 0f;
            while (elapsed < HpShakeSeconds && target != null)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(PresentationTiming.Sample(elapsed) / HpShakeSeconds);
                float decay = 1f - progress;
                float offset = Mathf.Sin(progress * Mathf.PI * 8f) * amplitude * decay;
                var shake = new Vector2(offset, -offset * 0.35f);
                // 画面の端で切れている絵は、その端から内側へは動かさない（切れ目が見えてしまう。
                // 2026-10-15 のユーザーの指摘「右のhpゲージが揺れて、画像になってない部分が現れる」）。
                // 外側へだけ振れば、揺れは見えたまま切れ目は画面の外に留まる
                if (cutRight) shake.x = Mathf.Max(shake.x, 0f);
                if (cutLeft) shake.x = Mathf.Min(shake.x, 0f);
                if (cutTop) shake.y = Mathf.Max(shake.y, 0f);
                if (cutBottom) shake.y = Mathf.Min(shake.y, 0f);
                target.anchoredPosition = home + shake;
                yield return null;
            }

            if (target != null) target.anchoredPosition = home;
            if (floatingAnimator != null) floatingAnimator.enabled = wasFloating;
        }

        /// <summary>
        /// 揺らす物の絵（見えている Graphic）が、画面のどの端に接しているか・はみ出しているか。
        /// 端から少しでも内側に収まっていれば「切れていない」とする。
        /// </summary>
        private static void EdgeCuts(RectTransform target, out bool left, out bool right, out bool bottom, out bool top)
        {
            left = right = bottom = top = false;
            const float touch = 2f; // 画面の端から 2px 以内なら、端で切れているとみなす
            var corners = new Vector3[4];
            foreach (var graphic in target.GetComponentsInChildren<Graphic>())
            {
                if (!graphic.isActiveAndEnabled || graphic.color.a <= 0.01f) continue;
                var canvas = graphic.canvas;
                Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                graphic.rectTransform.GetWorldCorners(corners);
                for (int i = 0; i < 4; i++)
                {
                    Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
                    if (p.x <= touch) left = true;
                    if (p.x >= Screen.width - touch) right = true;
                    if (p.y <= touch) bottom = true;
                    if (p.y >= Screen.height - touch) top = true;
                }
            }
        }
    }
}
