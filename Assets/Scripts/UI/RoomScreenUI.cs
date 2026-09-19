using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    /// <summary>
    /// タイトルから入る、女の子が部屋で待っているホーム画面。
    /// シーンには保存せず、タイトルシーン上に専用 Canvas として実行時に組み立てる。
    /// </summary>
    public sealed class RoomScreenUI : MonoBehaviour
    {
        private static readonly Color MenuText = new Color32(240, 232, 236, 255);
        private static readonly Color MenuMarker = new Color32(214, 40, 62, 255);
        private static readonly Color RoomDark = new Color32(22, 15, 24, 255);
        private static readonly Color RoomWall = new Color32(46, 28, 39, 255);
        private static readonly Color RoomFloor = new Color32(35, 20, 27, 255);
        private static readonly Color WoodDark = new Color32(48, 28, 28, 255);
        private static readonly Color WoodLight = new Color32(91, 53, 45, 255);

        private GameObject root;
        private GameObject content;
        private GameObject tutorialModal;
        private TMP_FontAsset font;
        private Action onMatchSelected;
        private Action onTutorialSelected;
        private Action onCollectionSelected;
        private Action onOptionSelected;
        private Action onExitSelected;
        private Action onTitleSelected;

        public bool IsOpen => root != null && root.activeSelf;

        public void Open(Action matchSelected, Action tutorialSelected, Action optionSelected, Action exitSelected,
            Action titleSelected, Action collectionSelected = null)
        {
            onMatchSelected = matchSelected;
            onTutorialSelected = tutorialSelected;
            onCollectionSelected = collectionSelected;
            onOptionSelected = optionSelected;
            onExitSelected = exitSelected;
            onTitleSelected = titleSelected;

            if (root == null) Build();
            if (root == null) return;

            root.SetActive(true);
            SetContentVisible(true);
        }

        public void Close()
        {
            if (tutorialModal != null) tutorialModal.SetActive(false);
            if (root != null) root.SetActive(false);
        }

        /// <summary>既存の設定パネルを開く間だけ、部屋の Canvas の内容を隠す。</summary>
        public void SetContentVisible(bool visible)
        {
            if (content != null && content.activeSelf != visible) content.SetActive(visible);
        }

        /// <summary>進捗がある時だけ、最初から／続きからを選ばせる小さな確認パネルを開く。</summary>
        public void OpenTutorialChoice(int savedProgress, Action<int> onStartSelected)
        {
            if (savedProgress <= 0)
            {
                onStartSelected?.Invoke(0);
                return;
            }

            if (tutorialModal == null) BuildTutorialModal();
            if (tutorialModal == null) return;

            tutorialModal.SetActive(true);

            var resume = tutorialModal.transform.Find("Panel/Resume");
            var resumeButton = resume != null ? resume.GetComponent<Button>() : null;
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveAllListeners();
                // 保存値は「完了した局数」なので、次の局を開始する。全局完走済みの場合は最終局から。
                int roundIndex = Mathf.Clamp(savedProgress, 0, 4);
                resumeButton.onClick.AddListener(() =>
                {
                    tutorialModal.SetActive(false);
                    onStartSelected?.Invoke(roundIndex);
                });
            }

            var restart = tutorialModal.transform.Find("Panel/Restart");
            var restartButton = restart != null ? restart.GetComponent<Button>() : null;
            if (restartButton != null)
            {
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(() =>
                {
                    tutorialModal.SetActive(false);
                    onStartSelected?.Invoke(0);
                });
            }

            var cancel = tutorialModal.transform.Find("Panel/Cancel");
            var cancelButton = cancel != null ? cancel.GetComponent<Button>() : null;
            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(() => tutorialModal.SetActive(false));
            }
        }

        private void Build()
        {
            font = BorrowJapaneseFont();

            root = new GameObject("RoomScreen", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UISortingOrders.TitleRoomScreen;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);
            scaler.matchWidthOrHeight = 0.5f;
            Stretch(root.GetComponent<RectTransform>());

            content = new GameObject("RoomContent", typeof(RectTransform));
            content.transform.SetParent(root.transform, false);
            Stretch(content.GetComponent<RectTransform>());

            BuildRoomBackground();
            BuildGirl();

            // **ここで空気をかぶせる（2026-09-17 のユーザー指示）。**
            // 部屋と女の子の上、メニューの下。順番が重なり順になるので、
            // これより後に作るもの（メニュー・確認パネル）は曇らない。
            Effects.SceneAtmosphere.Attach(content.transform,
                                           Effects.SceneAtmosphere.RoomVignette,
                                           Effects.SceneAtmosphere.RoomGrain,
                                           Effects.SceneAtmosphere.RoomGrainMean);

            // **光・影・奥行き・つぶやき（2026-09-17）。**
            // 空気の層より後に作る。つぶやきの吹き出しは、
            // 周辺減光や粒子で曇らせたくないため。
            Effects.RoomAmbience.Attach(content.transform, girlRectForAmbience,
                                        windowGlow, lampGlow,
                                        farLayer, midLayer, nearLayer, font);

            BuildMenuBar();
            BuildTutorialModal();
        }

        /// <summary>奥行きを出すための層。遠・中・近。歩きに合わせて別々の速さで流す。</summary>
        private RectTransform farLayer;
        private RectTransform midLayer;
        private RectTransform nearLayer;

        /// <summary>明滅させる光。`RoomAmbience` が握る。</summary>
        private Image windowGlow;
        private Image lampGlow;

        /// <summary>歩く女の子。影と視差とつぶやきが、この位置を見る。</summary>
        private RectTransform girlRectForAmbience;

        /// <summary>視差用の層を1枚作る。中身は全画面に広げておく。</summary>
        private RectTransform CreateParallaxLayer(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(content.transform, false);
            var rt = go.GetComponent<RectTransform>();
            Stretch(rt);
            return rt;
        }

        private void BuildRoomBackground()
        {
            // **3つの層に分けて置く（2026-09-17）。**
            // 平らな1枚だと、歩いても部屋が動かず書き割りに見える。
            // 遠いものほどゆっくり流すために、ここで分けておく。
            farLayer = CreateParallaxLayer("RoomLayerFar");
            midLayer = CreateParallaxLayer("RoomLayerMid");
            nearLayer = CreateParallaxLayer("RoomLayerNear");

            // 素材待ちでタイトル絵が透けないよう、まずはコードだけで室内を組む。
            // 壁・床・窓明かり・本棚・机を重ね、あとで背景画へ差し替えても他のUIに影響しない構造にする。
            CreateStretchImage(farLayer, "RoomBackdrop", RoomDark);
            CreateCenteredImage(farLayer, "RoomWall", new Vector2(0f, 100f), new Vector2(800f, 400f), RoomWall);
            CreateCenteredImage(nearLayer, "RoomFloor", new Vector2(0f, -200f), new Vector2(800f, 200f), RoomFloor);
            CreateCenteredImage(farLayer, "RoomCeilingTrim", new Vector2(0f, 286f), new Vector2(800f, 18f),
                new Color32(75, 45, 55, 255));

            CreateCenteredImage(nearLayer, "RoomRug", new Vector2(-58f, -144f), new Vector2(435f, 118f),
                new Color32(92, 47, 58, 255));
            CreateCenteredImage(nearLayer, "RoomRugInner", new Vector2(-58f, -144f), new Vector2(392f, 88f),
                new Color32(65, 35, 46, 255));

            CreateCenteredImage(farLayer, "RoomWindowFrame", new Vector2(190f, 92f), new Vector2(248f, 228f),
                new Color32(30, 21, 31, 255));
            CreateCenteredImage(farLayer, "RoomWindowNight", new Vector2(190f, 92f), new Vector2(224f, 204f),
                new Color32(38, 55, 79, 255));
            CreateCenteredImage(farLayer, "RoomWindowCrossVertical", new Vector2(190f, 92f), new Vector2(7f, 204f),
                new Color32(31, 23, 34, 255));
            CreateCenteredImage(farLayer, "RoomWindowCrossHorizontal", new Vector2(190f, 92f), new Vector2(224f, 7f),
                new Color32(31, 23, 34, 255));
            windowGlow = CreateCenteredImage(midLayer, "RoomWindowGlow", new Vector2(116f, -46f), new Vector2(314f, 106f),
                new Color(89f / 255f, 120f / 255f, 150f / 255f, 0.13f));

            CreateCenteredImage(midLayer, "RoomBookshelf", new Vector2(-304f, 8f), new Vector2(158f, 292f), WoodDark);
            CreateCenteredImage(midLayer, "RoomBookshelfInner", new Vector2(-304f, 8f), new Vector2(134f, 268f),
                new Color32(30, 20, 27, 255));
            for (int i = 0; i < 4; i++)
            {
                float y = 104f - i * 64f;
                CreateCenteredImage(midLayer, "RoomShelf" + i, new Vector2(-304f, y), new Vector2(136f, 6f), WoodLight);
                CreateCenteredImage(midLayer, "RoomBookRed" + i, new Vector2(-342f + i * 7f, y + 22f),
                    new Vector2(12f, 38f), new Color32(139, 53, 58, 255));
                CreateCenteredImage(midLayer, "RoomBookCream" + i, new Vector2(-326f + i * 8f, y + 20f),
                    new Vector2(11f, 34f), new Color32(194, 158, 119, 255));
                CreateCenteredImage(midLayer, "RoomBookBlue" + i, new Vector2(-307f + i * 5f, y + 18f),
                    new Vector2(10f, 31f), new Color32(58, 84, 111, 255));
            }

            CreateCenteredImage(midLayer, "RoomDesk", new Vector2(257f, -121f), new Vector2(168f, 106f), WoodDark);
            CreateCenteredImage(midLayer, "RoomDeskTop", new Vector2(257f, -72f), new Vector2(190f, 13f), WoodLight);
            lampGlow = CreateCenteredImage(midLayer, "RoomLampGlow", new Vector2(260f, 15f), new Vector2(94f, 108f),
                new Color(232f / 255f, 174f / 255f, 110f / 255f, 0.16f));
            CreateCenteredImage(midLayer, "RoomCurtain", new Vector2(349f, 90f), new Vector2(103f, 424f),
                new Color32(70, 24, 39, 255));
            CreateStretchImage(content.transform, "RoomAmbientShade", new Color(0f, 0f, 0f, 0.12f));
        }

        private void BuildGirl()
        {
            Sprite body = Resources.Load<Sprite>("女の子/通常時身体");
            Sprite openFace = Resources.Load<Sprite>("女の子/目を開ける顔");
            Sprite closedFace = Resources.Load<Sprite>("女の子/目を閉じる顔");
            Sprite fallback = Resources.Load<Sprite>("女の子/ピース笑顔");
            if (body == null) body = fallback;
            if (openFace == null) openFace = fallback;
            if (closedFace == null) closedFace = openFace;

            var outline = new GameObject("RoomGirlOutline", typeof(RectTransform));
            outline.transform.SetParent(content.transform, false);
            var outlineRect = outline.GetComponent<RectTransform>();
            ConfigureGirlRect(outlineRect);
            outlineRect.localScale = Vector3.one * 1.035f;
            CreateGirlLayer(outline.transform, "RoomGirlOutlineBody", body, new Color(1f, 1f, 1f, 0.90f));
            Image outlineFace = CreateGirlLayer(outline.transform, "RoomGirlOutlineFace", openFace,
                new Color(1f, 1f, 1f, 0.90f));

            var girl = new GameObject("RoomGirl", typeof(RectTransform));
            girl.transform.SetParent(content.transform, false);
            var girlRect = girl.GetComponent<RectTransform>();
            ConfigureGirlRect(girlRect);
            CreateGirlLayer(girl.transform, "RoomGirlBody", body, Color.white);
            Image face = CreateGirlLayer(girl.transform, "RoomGirlFace", openFace, Color.white);

            girlRectForAmbience = girlRect;

            var walker = girl.AddComponent<RoomGirlWalker>();
            walker.Initialize(outlineRect, outlineFace, face, openFace, closedFace,
                new Vector2(-200f, -30f), new Vector2(40f, -30f));
        }

        private static void ConfigureGirlRect(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-100f, -30f);
            rect.sizeDelta = new Vector2(340f, 340f);
        }

        private static Image CreateGirlLayer(Transform parent, string name, Sprite sprite, Color color)
        {
            var layer = new GameObject(name, typeof(RectTransform), typeof(Image));
            layer.transform.SetParent(parent, false);
            Stretch(layer.GetComponent<RectTransform>());
            var image = layer.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private void BuildMenuBar()
        {
            var bar = new GameObject("RoomMenuBar", typeof(RectTransform));
            bar.transform.SetParent(content.transform, false);
            var barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.5f, 0f);
            barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 28f);
            barRect.sizeDelta = new Vector2(740f, 64f);

            var rule = new GameObject("RoomMenuRule", typeof(RectTransform), typeof(Image));
            rule.transform.SetParent(bar.transform, false);
            var ruleRect = rule.GetComponent<RectTransform>();
            ruleRect.anchorMin = new Vector2(0.5f, 1f);
            ruleRect.anchorMax = new Vector2(0.5f, 1f);
            ruleRect.pivot = new Vector2(0.5f, 1f);
            ruleRect.anchoredPosition = Vector2.zero;
            ruleRect.sizeDelta = new Vector2(700f, 1f);
            var ruleImage = rule.GetComponent<Image>();
            ruleImage.color = new Color(240f / 255f, 232f / 255f, 236f / 255f, 0.38f);
            ruleImage.raycastTarget = false;

            // **バーは 740 幅で、5項目 x 148 でちょうど埋まっていた（-370〜+370）。**
            // コレクションを足して6項目になったので、バーを広げるのではなく1項目を詰める。
            // 参照解像度が 800x600 なので、6 x 148 = 888 にするとバーが画面からはみ出す。
            const float slot = 740f / 6f;   // 123.33
            //
            // **Act は Action ではなく Func<Action> で持つ。**
            // バーは初回の Build でしか組まれないので、ここで Action の値を焼き込むと
            // 2回目以降の Open で渡された新しいコールバックが無視される。
            // クリック時にフィールドを読み直せば、元の `() => onXxx?.Invoke()` と同じ遅延束縛になる。
            var items = new[]
            {
                new { Name = "RoomMenu_Match",      Label = "対局へ",        Size = 18f, Act = (Func<Action>)(() => onMatchSelected) },
                new { Name = "RoomMenu_Tutorial",   Label = "チュートリアル", Size = 13f, Act = (Func<Action>)(() => onTutorialSelected) },
                new { Name = "RoomMenu_Collection", Label = "コレクション",   Size = 15f, Act = (Func<Action>)(() => onCollectionSelected) },
                new { Name = "RoomMenu_Option",     Label = "設定",          Size = 18f, Act = (Func<Action>)(() => onOptionSelected) },
                new { Name = "RoomMenu_Exit",       Label = "やめる",        Size = 18f, Act = (Func<Action>)(() => onExitSelected) },
                new { Name = "RoomMenu_Title",      Label = "タイトルへ",     Size = 16f, Act = (Func<Action>)(() => onTitleSelected) },
            };
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                float x = -370f + slot * (i + 0.5f);
                CreateMenuItem(bar.transform, it.Name, it.Label, new Vector2(x, -8f), new Vector2(122f, 44f), it.Size,
                    () => it.Act()?.Invoke());
            }
        }

        private void BuildTutorialModal()
        {
            tutorialModal = new GameObject("RoomTutorialChoice", typeof(RectTransform), typeof(Image));
            tutorialModal.transform.SetParent(content.transform, false);
            Stretch(tutorialModal.GetComponent<RectTransform>());
            var scrim = tutorialModal.GetComponent<Image>();
            scrim.color = new Color(0f, 0f, 0f, 0.72f);
            scrim.raycastTarget = true;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(tutorialModal.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            Center(panelRect, new Vector2(330f, 252f));
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color32(46, 28, 39, 250);
            panelImage.raycastTarget = true;

            CreateText(panel.transform, "Heading", "チュートリアル", new Vector2(0f, 88f), new Vector2(290f, 38f), 25f,
                TextAlignmentOptions.Center, MenuText);
            CreateText(panel.transform, "Caption", "どこから始めますか", new Vector2(0f, 48f), new Vector2(290f, 28f), 16f,
                TextAlignmentOptions.Center, new Color32(221, 207, 212, 255));
            CreateMenuItem(panel.transform, "Resume", "続きから", new Vector2(0f, 5f), new Vector2(240f, 36f), 21f, null);
            CreateMenuItem(panel.transform, "Restart", "最初から", new Vector2(0f, -43f), new Vector2(240f, 36f), 21f, null);
            CreateMenuItem(panel.transform, "Cancel", "もどる", new Vector2(0f, -91f), new Vector2(240f, 36f), 19f, null);

            tutorialModal.SetActive(false);
        }

        private void CreateMenuItem(Transform parent, string name, string label, Vector2 position, Vector2 size,
            float fontSize, Action onClick)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = item.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
            item.GetComponent<Button>().targetGraphic = image;

            var marker = new GameObject("Marker", typeof(RectTransform), typeof(Image));
            marker.transform.SetParent(item.transform, false);
            var markerRect = marker.GetComponent<RectTransform>();
            markerRect.anchorMin = new Vector2(0f, 0.5f);
            markerRect.anchorMax = new Vector2(0f, 0.5f);
            markerRect.pivot = new Vector2(0f, 0.5f);
            markerRect.anchoredPosition = new Vector2(6f, 0f);
            markerRect.sizeDelta = new Vector2(10f, 10f);
            markerRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var markerImage = marker.GetComponent<Image>();
            markerImage.color = MenuMarker;
            markerImage.raycastTarget = false;

            var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(item.transform, false);
            Stretch(text.GetComponent<RectTransform>());
            var tmp = text.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = MenuText;
            // **`Left | Midline` と書かないこと。** Midline は横の中央揃えも含むので、OR すると
            // 左と中央が混ざった値になり、文字が右へずれて隣の項目の菱形に重なっていた（2026-09-19）。
            // 「チュートリアル」は 13pt でちょうど枠に収まる（14pt だと隣の菱形にかかる）
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.margin = new Vector4(24f, 0f, 0f, 0f);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;

            item.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
        }

        private void CreateText(Transform parent, string name, string text, Vector2 position, Vector2 size,
            float fontSize, TextAlignmentOptions alignment, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var tmp = textObject.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
        }

        private static Image CreateStretchImage(Transform parent, string name, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image));
            item.transform.SetParent(parent, false);
            Stretch(item.GetComponent<RectTransform>());
            var image = item.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateCenteredImage(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = item.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Center(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private static TMP_FontAsset BorrowJapaneseFont()
        {
            var labels = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var label in labels)
            {
                if (label != null && label.font != null) return label.font;
            }

            return TMP_Settings.defaultFontAsset;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }
    }

    /// <summary>部屋での待機・移動・瞬きを担当する、UI立ち絵用の小さな状態機械。</summary>
    public sealed class RoomGirlWalker : MonoBehaviour
    {
        public enum WalkerState
        {
            Idle,
            Walk
        }

        public WalkerState State { get; private set; } = WalkerState.Idle;
        public bool IsBlinking { get; private set; }

        private RectTransform girl;
        private RectTransform outline;
        private Image outlineFace;
        private Image face;
        private Sprite openFace;
        private Sprite closedFace;
        private Vector2 minPosition;
        private Vector2 maxPosition;
        private Vector2 basePosition;
        private Vector2 targetPosition;
        private float idleRemaining;
        private float walkSpeed;
        private float blinkEndsAt;
        private float nextBlinkAt;
        private bool initialized;

        public void Initialize(RectTransform outlineRect, Image outlineFaceImage, Image faceImage, Sprite openFaceSprite,
            Sprite closedFaceSprite, Vector2 min, Vector2 max)
        {
            girl = GetComponent<RectTransform>();
            outline = outlineRect;
            outlineFace = outlineFaceImage;
            face = faceImage;
            openFace = openFaceSprite;
            closedFace = closedFaceSprite;
            minPosition = min;
            maxPosition = max;
            basePosition = girl != null ? girl.anchoredPosition : Vector2.zero;
            targetPosition = basePosition;
            idleRemaining = UnityEngine.Random.Range(2.5f, 4.5f);
            nextBlinkAt = Time.unscaledTime + UnityEngine.Random.Range(3.5f, 5.5f);
            initialized = girl != null && outline != null;
            ApplyVisuals();
        }

        private void Update()
        {
            if (!initialized) return;

            float delta = Time.unscaledDeltaTime;
            if (State == WalkerState.Idle)
            {
                idleRemaining -= delta;
                if (idleRemaining <= 0f) BeginWalk();
            }
            else
            {
                basePosition = Vector2.MoveTowards(basePosition, targetPosition, walkSpeed * delta);
                if ((basePosition - targetPosition).sqrMagnitude < 0.01f)
                {
                    State = WalkerState.Idle;
                    idleRemaining = UnityEngine.Random.Range(3f, 6f);
                }
            }

            UpdateBlink();
            ApplyVisuals();
        }

        private void BeginWalk()
        {
            State = WalkerState.Walk;
            float midpoint = (minPosition.x + maxPosition.x) * 0.5f;
            float targetX = basePosition.x <= midpoint ? maxPosition.x : minPosition.x;
            targetPosition = new Vector2(targetX, basePosition.y);

            walkSpeed = UnityEngine.Random.Range(44f, 66f);
        }

        private void UpdateBlink()
        {
            float now = Time.unscaledTime;
            if (!IsBlinking && now >= nextBlinkAt)
            {
                IsBlinking = true;
                blinkEndsAt = now + 0.12f;
                if (outlineFace != null) outlineFace.sprite = closedFace;
                if (face != null) face.sprite = closedFace;
            }
            else if (IsBlinking && now >= blinkEndsAt)
            {
                IsBlinking = false;
                nextBlinkAt = now + UnityEngine.Random.Range(4f, 7f);
                if (outlineFace != null) outlineFace.sprite = openFace;
                if (face != null) face.sprite = openFace;
            }
        }

        private void ApplyVisuals()
        {
            float time = Time.unscaledTime;
            float bobAmplitude = State == WalkerState.Walk ? 4f : 1.8f;
            float bobSpeed = State == WalkerState.Walk ? 8f : 2.5f;
            Vector2 visualPosition = basePosition + Vector2.up * Mathf.Sin(time * bobSpeed) * bobAmplitude;
            float direction = State == WalkerState.Walk
                ? Mathf.Sign(targetPosition.x - basePosition.x) : 0f;
            float tilt = State == WalkerState.Walk ? -direction * 2.5f : 0f;

            girl.anchoredPosition = visualPosition;
            girl.localRotation = Quaternion.Euler(0f, 0f, tilt);
            outline.anchoredPosition = visualPosition;
            outline.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }
    }
}
