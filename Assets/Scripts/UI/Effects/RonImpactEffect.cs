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
    /// </summary>
    public static class RonImpactEffect
    {
        private const float HandFrameSeconds = 0.22f;
        private const float HandRiseSeconds = 0.14f;
        private const float HpShakeSeconds = 0.30f;
        private const float CalloutHoldSeconds = 0.56f;
        private const float HpShakePixels = 12f;

        private static readonly string[] HandFrameNames =
        {
            "UI/RonHands/ron_hand_01",
            "UI/RonHands/ron_hand_02",
            "UI/RonHands/ron_hand_03",
        };

        /// <summary>
        /// 手1、手2を順に出し、手3への切り替えと同じフレームで「ロン!!!」を出す。
        /// 体力表示だけを同時に一度揺らしてから、既存の決着処理へ返す。
        /// </summary>
        public static IEnumerator Play(MonoBehaviour runner, IList<RectTransform> hpPanels)
        {
            if (runner == null) yield break;

            GameObject overlay = CreateOverlay(out RectTransform overlayRt);
            Image handImage = CreateImage("RonHand", overlayRt);
            handImage.preserveAspect = true;
            SetRect(handImage.rectTransform, Vector2.zero, new Vector2(800f, 600f));
            // 画像内の腕は下端まで描かれている。中央基準では縦長の画面で腕が浮く。
            handImage.rectTransform.anchorMin = handImage.rectTransform.anchorMax = new Vector2(.5f, 0);
            handImage.rectTransform.pivot = new Vector2(.5f, 0);

            // 声「それ、ロン」は、指が動いている間に「それ」、吹き出しで「ロン」（2026-10-15）
            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio != null) audio.PlayRonInputSore();

            bool displayedAnyHand = false;
            Sprite handSprite = null;
            for (int i = 0; i < HandFrameNames.Length; i++)
            {
                Texture2D handTexture = Resources.Load<Texture2D>(HandFrameNames[i]);
                if (handTexture == null)
                {
                    Debug.LogError("[RonImpactEffect] ロン用の手画像が見つかりません: Resources/" + HandFrameNames[i]);
                    continue;
                }

                Sprite previousHandSprite = handImage.sprite;
                handSprite = Sprite.Create(
                    handTexture,
                    new Rect(0f, 0f, handTexture.width, handTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                handImage.sprite = handSprite;
                if (previousHandSprite != null) Object.Destroy(previousHandSprite);
                handImage.gameObject.SetActive(true);
                displayedAnyHand = true;
                if (i == 0) yield return RaiseHandFromBottom(handImage.rectTransform);
                else if (i < HandFrameNames.Length - 1) yield return new WaitForSeconds(HandFrameSeconds);
            }

            if (!displayedAnyHand) handImage.gameObject.SetActive(false);

            Texture2D burstTexture = CreateBurstTexture();
            Sprite burstSprite = Sprite.Create(
                burstTexture,
                new Rect(0f, 0f, burstTexture.width, burstTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            CreateBurstCallout(overlayRt, burstSprite);
            if (audio != null) audio.PlayRonInputRon();

            if (hpPanels != null)
            {
                for (int i = 0; i < hpPanels.Count; i++)
                {
                    if (hpPanels[i] != null)
                        runner.StartCoroutine(ShakeHpRoutine(hpPanels[i], HpShakePixels));
                }
            }

            yield return new WaitForSeconds(CalloutHoldSeconds);
            Object.Destroy(overlay);
            if (handSprite != null) Object.Destroy(handSprite);
            Object.Destroy(burstSprite);
            Object.Destroy(burstTexture);
        }

        private static IEnumerator RaiseHandFromBottom(RectTransform hand)
        {
            float start = -hand.rect.height;
            hand.anchoredPosition = new Vector2(0, start);
            for (float elapsed = 0; elapsed < HandFrameSeconds; elapsed += Time.deltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / HandRiseSeconds);
                float eased = 1 - Mathf.Pow(1 - progress, 3);
                hand.anchoredPosition = new Vector2(0, Mathf.Round(Mathf.Lerp(start, 0, eased)));
                yield return null;
            }
            hand.anchoredPosition = Vector2.zero;
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

        private static void CreateBurstCallout(RectTransform parent, Sprite burstSprite)
        {
            Vector2 position = new Vector2(202f, 62f);

            Image outline = CreateImage("RonBurstOutline", parent);
            outline.sprite = burstSprite;
            outline.color = new Color32(52, 19, 25, 255);
            SetRect(outline.rectTransform, position, new Vector2(292f, 234f));

            Image inner = CreateImage("RonBurstInner", parent);
            inner.sprite = burstSprite;
            inner.color = new Color32(255, 248, 225, 255);
            SetRect(inner.rectTransform, position, new Vector2(252f, 202f));

            GameObject textObject = new GameObject("RonBurstText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = "\u30ed\u30f3!!!";
            text.fontSize = 57f;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color32(46, 17, 24, 255);
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            SetRect(text.rectTransform, position, new Vector2(230f, 138f));
            text.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -5f);
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
        internal static Texture2D CreateBurstTexture()
        {
            const int width = 292;
            const int height = 234;
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
                float radius = baseRadius * jitter[i];
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
                float progress = Mathf.Clamp01(elapsed / HpShakeSeconds);
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
