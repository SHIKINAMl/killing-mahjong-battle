using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// ロン入力直後の手振り、相手背後の閃光、宣言の文字、体力表示の揺れ。
    /// 画面、盤面、立ち絵には ScreenQuake / ScreenFlash を使わない。
    /// 勢いは背後の閃光、宣言の叩きつけ、音と体力表示の揺れで出す。
    /// </summary>
    public static class RonImpactEffect
    {
        private const float HandFrameSeconds = 0.22f;
        private const float InputDimSeconds = 0.14f;
        // 2026-10-18「自分からロンする時の勢いも強くして」で、揺れと間を大きくした（前は 0.30秒 / 0.56秒 / 12px）
        private const float HpShakeSeconds = 0.45f;
        private const float CalloutHoldSeconds = 0.85f;
        private const float HpShakePixels = 22f;

        /// <summary>吹き出しを叩きつけるときの、出始めの大きさ（倍）と、そこから落ちる時間。</summary>
        private const float SlamFromScale = 1.65f;
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
        // 押したボタンをその場に残し、画面下の手が指を伸ばすと、ボタンの先端から矢印が走って
        // 背後の閃光から「ロン！」の宣言へつなぐ。

        private const float HandWidth = 800f;
        private const float HandHeight = 600f;
        /// <summary>押されたボタンが潰れて矢印を出している時間。</summary>
        private const float PressSeconds = 0.20f;
        private const string ArrowFramesPath = "UI/RonCallout/ron_arrow_frames";
        private const float SettlementDimAlpha = 0.35f;

        /// <summary>
        /// 画面下に手1・手2・手3を順に出し、指さしと同時にボタンの先端から矢印を伸ばす。
        /// 相手の背後に閃光を走らせて「ロン！」を叩きつけ、決着処理へ返す。
        /// <paramref name="ronButton"/> が無いとき（試写など）は画面の真ん中にボタンがあるものとして動かす。
        /// </summary>
        public static IEnumerator Play(MonoBehaviour runner, IList<RectTransform> hpPanels, RectTransform ronButton = null,
            System.Action onImpact = null, System.Func<bool> waitingForResponse = null,
            PresentationScope owner = null)
        {
            if (runner == null) yield break;
            using var scope = new PresentationScope(Debug.LogException);
            owner?.AddCleanup(scope.Dispose);
            yield return scope.Run(PlayCore(runner, hpPanels, ronButton, onImpact, waitingForResponse, scope));
        }

        private static IEnumerator PlayCore(MonoBehaviour runner, IList<RectTransform> hpPanels,
            RectTransform ronButton, System.Action onImpact, System.Func<bool> waitingForResponse,
            PresentationScope scope)
        {
            GameObject overlay = scope.Own(CreateOverlay(out RectTransform overlayRt));
            Sprite handSprite = null;
            scope.AddCleanup(() =>
            {
                if (handSprite != null) Object.Destroy(handSprite);
            });
            var shade = CreateImage("RonInputDim", overlayRt);
            shade.rectTransform.anchorMin = Vector2.zero;
            shade.rectTransform.anchorMax = Vector2.one;
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            shade.color = new Color(0f, 0f, 0f, RonChanceEffect.GetDimAlpha(SettlementDimAlpha));
            RonChanceEffect.HideCurrent();
            // 作ったばかりの Canvas はまだ大きさが決まっていないので、CanvasScaler（800x600・Expand）と同じ式で測る
            float overlayScale = Mathf.Max(0.0001f, Mathf.Min(Screen.width / 800f, Screen.height / 600f));

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
                var sourceCanvas = ronButton.GetComponentInParent<Canvas>()?.rootCanvas;
                var camera = sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? sourceCanvas.worldCamera : null;
                var corners = new Vector3[4];
                ronButton.GetWorldCorners(corners);
                Vector2 lowerLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                Vector2 upperRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                Vector2 screen = (lowerLeft + upperRight) * 0.5f;
                dropPosition = (screen - new Vector2(Screen.width, Screen.height) * .5f) / overlayScale;
                dropSize = (upperRight - lowerLeft) / overlayScale;
            }
            Image drop = null;
            if (pressedSprite != null)
            {
                drop = CreateImage("RonDropPressed", overlayRt);
                drop.sprite = pressedSprite;
                SetRect(drop.rectTransform, dropPosition, dropSize);
            }

            // 原画は画面下端まで描かれている。3枚の位置を揃えたまま切り替える。
            Image handImage = CreateImage("RonHand", overlayRt);
            handImage.preserveAspect = true;
            SetRect(handImage.rectTransform, Vector2.zero, new Vector2(HandWidth, HandHeight));
            handImage.rectTransform.anchorMin = handImage.rectTransform.anchorMax = new Vector2(.5f, 0);
            handImage.rectTransform.pivot = new Vector2(.5f, 0);

            // 声「それ、ロン」は、指が動いている間に「それ」、吹き出しで「ロン」（2026-10-15）
            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio != null) audio.PlayRonInputSore();

            for (int i = 0; i < HandFrameNames.Length; i++)
            {
                Texture2D handTexture = Resources.Load<Texture2D>(HandFrameNames[i]);
                if (handTexture == null)
                {
                    Debug.LogError("[RonImpactEffect] ロン用の手画像が見つかりません: Resources/" + HandFrameNames[i]);
                    continue;
                }

                Sprite previousHandSprite = handSprite;
                handSprite = Sprite.Create(handTexture, new Rect(0f, 0f, handTexture.width, handTexture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                handImage.sprite = handSprite;
                if (previousHandSprite != null) Object.Destroy(previousHandSprite);
                if (i == 0) yield return FadeInputShade(shade);
                else if (i < HandFrameNames.Length - 1) yield return new WaitForSeconds(HandFrameSeconds);
            }

            if (handSprite == null) handImage.gameObject.SetActive(false);
            shade.color = new Color(0f, 0f, 0f, SettlementDimAlpha);

            // 指さしに合わせ、ボタンの先端から相手へ矢印が走る。
            if (drop != null)
            {
                drop.rectTransform.localScale = new Vector3(1.08f, 0.86f, 1f);
                drop.rectTransform.anchoredPosition = dropPosition + new Vector2(0f, -dropSize.y * .07f);
                yield return PlayPointingArrow(overlayRt, drop.rectTransform, dropSize);
                drop.gameObject.SetActive(false);
            }

            yield return FlashBehindEnemy(scope);
            RectTransform callout = CreateRonTitle(overlayRt, dropPosition + new Vector2(0f, -18f), scope);
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

            onImpact?.Invoke();
            yield return SlamCallout(callout, CalloutHoldSeconds);
            if (waitingForResponse != null)
            {
                callout.gameObject.SetActive(false);
                handImage.gameObject.SetActive(false);
                float until = Time.unscaledTime + 1f;
                while (waitingForResponse() && Time.unscaledTime < until) yield return null;
            }

        }

        private static IEnumerator PlayPointingArrow(RectTransform overlay, RectTransform button, Vector2 buttonSize)
        {
            Texture2D frames = Resources.Load<Texture2D>(ArrowFramesPath);
            if (frames == null)
            {
                yield return new WaitForSeconds(PressSeconds);
                yield break;
            }

            const int frameCount = 3;
            const float frameWidth = 1f / frameCount;
            var arrow = new GameObject("RonPointingArrow", typeof(RectTransform), typeof(RawImage))
                .GetComponent<RawImage>();
            arrow.transform.SetParent(overlay, false);
            arrow.transform.SetSiblingIndex(button.GetSiblingIndex());
            arrow.raycastTarget = false;
            arrow.texture = frames;

            // 矢印の原点は1コマ内の (6, 36)。ボタンの尖った上辺に合わせる。
            float scale = buttonSize.x / 320f;
            var origin = button.anchoredPosition + new Vector2(buttonSize.x * .2f, buttonSize.y * .28f);
            SetRect(arrow.rectTransform, origin + new Vector2(36f, 32f) * scale, new Vector2(96f, 80f) * scale);
            var frameDelay = new WaitForSeconds(PressSeconds / frameCount);
            for (int frame = 0; frame < frameCount; frame++)
            {
                arrow.uvRect = new Rect(frame * frameWidth, 0f, frameWidth, 1f);
                yield return frameDelay;
            }

            arrow.gameObject.SetActive(false);
            Object.Destroy(arrow.gameObject);
        }

        private static IEnumerator FlashBehindEnemy(PresentationScope scope)
        {
            var enemy = Object.FindFirstObjectByType<EnemyInfoUI>();
            SpriteRenderer portrait = enemy != null ? enemy.CharacterRenderer : null;
            var camera = Camera.main;
            Texture2D texture = Resources.Load<Texture2D>("UI/RonCallout/ron_back_flash");
            if (portrait == null || camera == null || texture == null) yield break;

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .5f), 100f);
            scope.AddCleanup(() => { if (sprite != null) Object.Destroy(sprite); });
            var flash = scope.Own(new GameObject("RonFlashBehindEnemy"));
            flash.layer = portrait.gameObject.layer;
            var renderer = flash.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = portrait.sharedMaterial;
            renderer.sortingLayerID = portrait.sortingLayerID;
            renderer.sortingOrder = portrait.sortingOrder;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            Vector3 screen = camera.WorldToScreenPoint(portrait.bounds.center);
            // 原画の顔と肩がある上寄りを横切る。画面比によらず横幅を合わせる。
            screen.y = Mathf.Clamp(screen.y + Screen.height * .17f, Screen.height * .52f, Screen.height * .72f);
            float depth = camera.WorldToScreenPoint(portrait.bounds.center).z + .05f;
            screen.z = depth;
            Vector3 center = camera.ScreenToWorldPoint(screen);
            Vector3 left = camera.ScreenToWorldPoint(new Vector3(0, screen.y, depth));
            Vector3 right = camera.ScreenToWorldPoint(new Vector3(Screen.width, screen.y, depth));
            float width = Vector3.Distance(left, right);
            float size = width / sprite.bounds.size.x;
            flash.transform.position = center;
            flash.transform.localScale = new Vector3(size * .18f, size * .32f, 1);
            yield return new WaitForSeconds(.033f);
            flash.transform.localScale = new Vector3(size * .76f, size * .75f, 1);
            yield return new WaitForSeconds(.033f);
            flash.transform.localScale = new Vector3(size * 1.1f, size, 1);
            yield return new WaitForSeconds(.05f);
            renderer.color = new Color(1, 1, 1, .25f);
            flash.transform.localScale = new Vector3(size * 1.15f, size * .25f, 1);
            yield return new WaitForSeconds(.033f);
            flash.SetActive(false);
        }

        private static RectTransform CreateRonTitle(RectTransform root, Vector2 position, PresentationScope scope)
        {
            var group = new GameObject("RonDeclaration", typeof(RectTransform));
            group.transform.SetParent(root, false);
            var rect = (RectTransform)group.transform;
            SetRect(rect, position, new Vector2(360f, 160f));
            rect.localRotation = Quaternion.Euler(0, 0, 7f);
            Texture2D texture = Resources.Load<Texture2D>("UI/RonCallout/ron_declaration");
            if (texture == null) return rect;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .5f), 100f);
            scope.AddCleanup(() => { if (sprite != null) Object.Destroy(sprite); });
            var text = CreateImage("RonText", rect);
            text.sprite = sprite;
            SetRect(text.rectTransform, Vector2.zero, new Vector2(360f, 160f));
            return rect;
        }
        /// <summary>
        /// 宣言を叩きつけ、潰れた形で一瞬止めてから、震わせながら収める。
        /// 吹き出しの中だけを白く光らせる（画面全体は光らせない）。
        /// </summary>
        private static IEnumerator SlamCallout(RectTransform callout, float total)
        {
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

        private static IEnumerator FadeInputShade(Image shade)
        {
            float startDimAlpha = shade.color.a;
            for (float elapsed = 0; elapsed < HandFrameSeconds; elapsed += Time.deltaTime)
            {
                float progress = Mathf.Clamp01(PresentationTiming.Sample(elapsed) / InputDimSeconds);
                float eased = 1 - Mathf.Pow(1 - progress, 3);
                shade.color = new Color(0f, 0f, 0f, Mathf.Lerp(startDimAlpha, SettlementDimAlpha, eased));
                yield return null;
            }
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
