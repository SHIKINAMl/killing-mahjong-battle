using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 女の子が部屋で待っているホーム画面（部屋シーン）。
    ///
    /// **部屋の部品はシーンに置いてある（`RoomScreen`）。ここはそれを探して使う（2026-10-09）。**
    /// 以前は再生のたびにコードで組み立て、シーンには「見本の写し」だけを置いていた。
    /// 写しはコードを変えても付いてこないので、置いてある物と再生時の物が食い違った
    /// （看板を1体足したのに、シーンには2体のままだった）。
    ///
    /// **位置・大きさ・色・文字はシーンが正。** 見た目を変えるときはシーンを直す。
    /// ここに残っている数字は、シーンに無かったときに作るための控え（<see cref="SceneFirst"/>）。
    /// 部品を足したら `Tools > UI > 実行時UIをシーンへ置く` でシーンにも置くこと。
    ///
    /// 再生時にだけ付けるもの（シーンには保存できない・しない）:
    ///   女の子の歩きと瞬き（<see cref="RoomGirlWalker"/>）、
    ///   空気の層（周辺減光と粒子。絵をその場で作る）、光の明滅・影・つぶやき。
    ///   シーンに残っているこれらの写しは、再生時に捨てて付け直す。
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
        private const float GirlBaseY = -54f;

        private GameObject root;
        private GameObject content;
        private GameObject tutorialModal;
        private GameObject menuBar;
        private GameObject backdropDim;
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

        /// <summary>
        /// 設定パネルの**背景として**部屋を見せる（2026-09-19 のユーザー指示）。
        ///
        /// 以前は設定中に部屋を丸ごと隠していて、パネルの後ろに何もない空間（カメラの空）が映っていた。
        /// 部屋は残して暗くし、下のメニューだけ隠す（残すとボタンの隙間から文字が覗く）。
        /// 暗幕が当たり判定を持つので、設定中に部屋のボタンは押せない。
        /// **設定パネルを部屋より手前に出すのは呼ぶ側の仕事**（`TitleUIManager.OpenRoomOptions`）。
        /// </summary>
        public void SetBackdropMode(bool on)
        {
            if (menuBar != null && menuBar.activeSelf == on) menuBar.SetActive(!on);
            if (backdropDim != null && backdropDim.activeSelf != on)
            {
                backdropDim.SetActive(on);
                if (on) backdropDim.transform.SetAsLastSibling();
            }
        }

        private void BuildBackdropDim()
        {
            backdropDim = SceneFirst.Child(root.transform, "RoomBackdropDim", out bool created,
                typeof(RectTransform), typeof(Image));
            if (created)
            {
                Stretch(backdropDim.GetComponent<RectTransform>());
                var image = backdropDim.GetComponent<Image>();
                image.color = new Color(0f, 0f, 0f, 0.55f);
                image.raycastTarget = true;
            }
            backdropDim.SetActive(false);
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

        private const string RootName = "RoomScreen";

        /// <summary>2026-10-09 より前のシーンでの名前。当時は再生時に捨てる「見本の写し」だった。</summary>
        private const string LegacyRootName = "SavedRoomPreview";

        private void Build()
        {
            BuildStatic();
            AttachRuntime();
            SceneFirst.Report("RoomScreenUI");
        }

        /// <summary>
        /// **エディタ用。** 部屋の部品のうち、シーンに無い物だけを作って置く
        /// （`SceneFirstBaker` が再生していないときに呼ぶ）。再生時にだけ付けるものは付けない。
        /// </summary>
        /// <returns>シーンに無くて作った物の名前。全部置いてあったら空文字</returns>
        public string BakeForEditor()
        {
            BuildStatic();
            return SceneFirst.Report("RoomScreenUI");
        }

        /// <summary>シーンに置いてある部品を探して覚える。無い物は作る。再生していなくても呼べる。</summary>
        private void BuildStatic()
        {
            font = BorrowJapaneseFont();

            root = SceneFirst.Find(RootName);
            if (root == null)
            {
                root = SceneFirst.Find(LegacyRootName);
                if (root != null) root.name = RootName;
            }
            if (root == null)
            {
                SceneFirst.NoteCreated(RootName);
                root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = UISortingOrders.TitleRoomScreen;

                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(800f, 600f);
                scaler.matchWidthOrHeight = 0.5f;
                Stretch(root.GetComponent<RectTransform>());
            }

            content = SceneFirst.Child(root.transform, "RoomContent", out bool madeContent, typeof(RectTransform));
            if (madeContent) Stretch(content.GetComponent<RectTransform>());

            BuildRoomBackground();
            BuildGirl();
            BuildMenuBar();
            BuildTutorialModal();
            BuildBackdropDim();
        }

        /// <summary>
        /// 再生時にだけ付けるもの。**絵をその場で作る**（周辺減光・粒子）か、
        /// **動きの途中の状態を持つ**（歩き・明滅・つぶやき）ので、シーンには保存できない。
        /// </summary>
        private void AttachRuntime()
        {
            if (girlRectForAmbience != null)
            {
                var girl = girlRectForAmbience.gameObject;
                var walker = girl.GetComponent<RoomGirlWalker>();
                if (walker == null) walker = girl.AddComponent<RoomGirlWalker>();
                walker.Initialize(girlFace, girlOpenFace, girlClosedFace,
                    new Vector2(-200f, GirlBaseY), new Vector2(40f, GirlBaseY));
            }

            // シーンに残っている写しを先に捨てる。残したまま付けると二重になる
            RemoveSavedCopies("SceneAtmosphere", "RoomAmbience", "RoomGirlShadow", "RoomMurmur");

            // **ここで空気をかぶせる（2026-09-17 のユーザー指示）。**
            // 部屋と女の子の上、メニューの下。
            Effects.SceneAtmosphere.Attach(content.transform,
                                           Effects.SceneAtmosphere.RoomVignette,
                                           Effects.SceneAtmosphere.RoomGrain,
                                           Effects.SceneAtmosphere.RoomGrainMean);

            // **光・影・奥行き・つぶやき（2026-09-17）。**
            // 空気の層より後に付ける。つぶやきの吹き出しは、
            // 周辺減光や粒子で曇らせたくないため。
            Effects.RoomAmbience.Attach(content.transform, girlRectForAmbience,
                                        windowGlow, lampGlow,
                                        farLayer, midLayer, nearLayer, font);

            // **並び順が重なり順。** 上の2つは末尾に足されるので、メニューと確認パネルを
            // その後ろ（手前）へ送り直す。送らないとメニューまで曇る
            if (menuBar != null) menuBar.transform.SetAsLastSibling();
            if (tutorialModal != null) tutorialModal.transform.SetAsLastSibling();
        }

        private void RemoveSavedCopies(params string[] names)
        {
            var doomed = new System.Collections.Generic.List<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t != root.transform && Array.IndexOf(names, t.name) >= 0) doomed.Add(t);
            }
            foreach (var t in doomed)
            {
                if (t == null) continue;
                // 消えるのはコマの終わり。先に外しておかないと、このコマのあいだ並び順に残る
                t.gameObject.SetActive(false);
                t.SetParent(null, false);
                Destroy(t.gameObject);
            }
        }

        private Image girlFace;
        private Sprite girlOpenFace;
        private Sprite girlClosedFace;

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
        /// <summary>
        /// 視差で流す層の、画面からはみ出させる幅（左右それぞれ）。
        ///
        /// **層は画面より広く作らないと、端が画面の中に入ってくる。**
        /// 女の子は x -200〜+40 の 240px を歩き、層はその動きの逆へ
        /// far 0.05 / mid 0.11 / near 0.19 倍ずれる。いちばん動く near で 45.6px。
        /// 余裕を見て 80px。ここを削ると、女の子が端へ行ったとき
        /// 壁や床の向こうの「何も無い背景」が覗く（2026-09-27 の指摘）。
        /// </summary>
        private const float ParallaxOverscan = 80f;

        private RectTransform CreateParallaxLayer(string name)
        {
            var go = SceneFirst.Child(content.transform, name, out bool created, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            if (!created) return rt;

            Stretch(rt);
            // 左右へ広げる。Stretch のままだと画面と同じ幅で、ずらすと端が出る
            rt.offsetMin = new Vector2(-ParallaxOverscan, rt.offsetMin.y);
            rt.offsetMax = new Vector2(ParallaxOverscan, rt.offsetMax.y);
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
            // **横幅は画面(800)ではなく、はみ出しぶんを足した幅にする。**
            // 800 のままだと、層をずらしたときに絵の端が画面の中へ入ってくる。
            const float wideW = 800f + ParallaxOverscan * 2f;

            CreateStretchImage(farLayer, "RoomBackdrop", RoomDark);
            CreateCenteredImage(farLayer, "RoomWall", new Vector2(0f, 100f), new Vector2(wideW, 400f), RoomWall);
            CreateCenteredImage(nearLayer, "RoomFloor", new Vector2(0f, -200f), new Vector2(wideW, 200f), RoomFloor);
            CreateCenteredImage(farLayer, "RoomCeilingTrim", new Vector2(0f, 286f), new Vector2(wideW, 18f),
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

            // 足元は下のメニュー境界で隠す。画像自体の切れ目を部屋の中に見せない。
            var viewport = SceneFirst.Child(content.transform, "RoomGirlViewport", out bool madeViewport,
                typeof(RectTransform), typeof(RectMask2D));
            if (madeViewport)
            {
                Stretch(viewport.GetComponent<RectTransform>());
                viewport.GetComponent<RectMask2D>().padding = new Vector4(0, 92, 0, 0);
            }
            var girl = SceneFirst.Child(viewport.transform, "RoomGirl", out bool madeGirl, typeof(RectTransform));
            var girlRect = girl.GetComponent<RectTransform>();
            if (madeGirl) ConfigureGirlRect(girlRect);
            CreateGirlLayer(girl.transform, "RoomGirlBody", body, Color.white);
            girlFace = CreateGirlLayer(girl.transform, "RoomGirlFace", openFace, Color.white);

            // 瞬きは「開いた顔」と「閉じた顔」を入れ替える。開いた顔はシーンに置いてある絵を使う
            girlOpenFace = girlFace.sprite != null ? girlFace.sprite : openFace;
            girlClosedFace = closedFace;
            girlRectForAmbience = girlRect;

            // 歩きと瞬き（RoomGirlWalker）は再生時に付ける（AttachRuntime）
        }

        private static void ConfigureGirlRect(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-100f, GirlBaseY);
            rect.sizeDelta = new Vector2(340f, 340f);
        }

        private static Image CreateGirlLayer(Transform parent, string name, Sprite sprite, Color color)
        {
            var layer = SceneFirst.Child(parent, name, out bool created, typeof(RectTransform), typeof(Image));
            var image = layer.GetComponent<Image>();
            if (!created) return image;

            Stretch(layer.GetComponent<RectTransform>());
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private void BuildMenuBar()
        {
            var bar = SceneFirst.Child(content.transform, "RoomMenuBar", out bool madeBar, typeof(RectTransform));
            menuBar = bar;
            if (madeBar)
            {
                var barRect = bar.GetComponent<RectTransform>();
                barRect.anchorMin = new Vector2(0.5f, 0f);
                barRect.anchorMax = new Vector2(0.5f, 0f);
                barRect.pivot = new Vector2(0.5f, 0f);
                barRect.anchoredPosition = new Vector2(0f, 28f);
                barRect.sizeDelta = new Vector2(740f, 64f);
            }

            var rule = SceneFirst.Child(bar.transform, "RoomMenuRule", out bool madeRule,
                typeof(RectTransform), typeof(Image));
            if (madeRule)
            {
                var ruleRect = rule.GetComponent<RectTransform>();
                ruleRect.anchorMin = new Vector2(0.5f, 1f);
                ruleRect.anchorMax = new Vector2(0.5f, 1f);
                ruleRect.pivot = new Vector2(0.5f, 1f);
                ruleRect.anchoredPosition = Vector2.zero;
                ruleRect.sizeDelta = new Vector2(700f, 1f);
                var ruleImage = rule.GetComponent<Image>();
                ruleImage.color = new Color(240f / 255f, 232f / 255f, 236f / 255f, 0.38f);
                ruleImage.raycastTarget = false;
            }

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
                var item = CreateMenuItem(bar.transform, it.Name, it.Label, new Vector2(x, -8f), new Vector2(122f, 44f),
                    it.Size, () => it.Act()?.Invoke(), out bool madeItem);
                if (it.Name == "RoomMenu_Exit")
                {
                    // 「やめる」は押せない。灰色にするのは作ったときだけ（置いてある物の色はシーンが正）
                    item.GetComponent<Button>().onClick.RemoveAllListeners();
                    if (madeItem)
                    {
                        item.GetComponent<Button>().interactable = false;
                        item.transform.Find("Label").GetComponent<TextMeshProUGUI>().color = new Color32(125, 120, 123, 255);
                        item.transform.Find("Marker").GetComponent<Image>().color = new Color32(125, 120, 123, 255);
                    }
                }
                // 看板は左の3項目に立てる。コレクションのぶんは 2026-10-09 に足した（ユーザーの指示）
                if (i < MenuSignLabels.Length)
                    CreateMenuSign(bar.transform, x, MenuSignLabels[i]);
            }
        }

        /// <summary>
        /// メニューの上に立てる看板の文字。左の項目から順（対局へ・チュートリアル・コレクション）。
        /// **看板に入るのは1行6文字まで。** それより長いものは `\n` で2行に割って書く。
        /// </summary>
        private static readonly string[] MenuSignLabels =
        {
            "対戦したい方",
            "ルールを\n知りたい方",
            "資料を\n見たい方",
        };

        private void CreateMenuSign(Transform parent, float x, string label)
        {
            string name = "RoomMenuSign_" + label.Replace("\n", "");
            if (SceneFirst.FindChild(parent, name) != null) return;   // シーンに置いてある

            Sprite sprite = Resources.Load<Sprite>("Room/MenuSignGirl");
            if (sprite == null) return;

            // 矢印の先をメニューの上に合わせ、看板の傾きに沿って文字を載せる。
            var sign = CreateCenteredImage(parent, name,
                new Vector2(x + 2f, 114f), new Vector2(192f, 144f), Color.white);
            sign.sprite = sprite;
            sign.preserveAspect = true;
            CreateText(sign.transform, "SignLabel", label, new Vector2(-21.6f, 36f),
                new Vector2(68f, 30f), 9f, TextAlignmentOptions.Center, new Color32(69, 37, 26, 255));
            var text = sign.transform.Find("SignLabel").GetComponent<TextMeshProUGUI>();
            text.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 22f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private void BuildTutorialModal()
        {
            tutorialModal = SceneFirst.Child(content.transform, "RoomTutorialChoice", out bool madeModal,
                typeof(RectTransform), typeof(Image));
            if (madeModal)
            {
                Stretch(tutorialModal.GetComponent<RectTransform>());
                var scrim = tutorialModal.GetComponent<Image>();
                scrim.color = new Color(0f, 0f, 0f, 0.72f);
                scrim.raycastTarget = true;
            }

            var panel = SceneFirst.Child(tutorialModal.transform, "Panel", out bool madePanel,
                typeof(RectTransform), typeof(Image));
            if (madePanel)
            {
                var panelRect = panel.GetComponent<RectTransform>();
                Center(panelRect, new Vector2(330f, 252f));
                var panelImage = panel.GetComponent<Image>();
                panelImage.color = new Color32(46, 28, 39, 250);
                panelImage.raycastTarget = true;
            }

            CreateText(panel.transform, "Heading", "チュートリアル", new Vector2(0f, 88f), new Vector2(290f, 38f), 25f,
                TextAlignmentOptions.Center, MenuText);
            CreateText(panel.transform, "Caption", "どこから始めますか", new Vector2(0f, 48f), new Vector2(290f, 28f), 16f,
                TextAlignmentOptions.Center, new Color32(221, 207, 212, 255));
            CreateMenuItem(panel.transform, "Resume", "続きから", new Vector2(0f, 5f), new Vector2(240f, 36f), 21f, null);
            CreateMenuItem(panel.transform, "Restart", "最初から", new Vector2(0f, -43f), new Vector2(240f, 36f), 21f, null);
            CreateMenuItem(panel.transform, "Cancel", "もどる", new Vector2(0f, -91f), new Vector2(240f, 36f), 19f, null);

            tutorialModal.SetActive(false);
        }

        private GameObject CreateMenuItem(Transform parent, string name, string label, Vector2 position, Vector2 size,
            float fontSize, Action onClick)
        {
            return CreateMenuItem(parent, name, label, position, size, fontSize, onClick, out _);
        }

        /// <summary>
        /// メニューの1項目。シーンに置いてあればそれを使い、**押したときの動きだけをつなぎ直す。**
        /// </summary>
        private GameObject CreateMenuItem(Transform parent, string name, string label, Vector2 position, Vector2 size,
            float fontSize, Action onClick, out bool created)
        {
            var item = SceneFirst.Child(parent, name, out created, typeof(RectTransform), typeof(Image), typeof(Button));
            if (!created)
            {
                // 置いてあるボタンは使い回すので、前につないだ物を外してからつなぐ
                var placed = item.GetComponent<Button>();
                placed.onClick.RemoveAllListeners();
                placed.onClick.AddListener(() => onClick?.Invoke());
                return item;
            }

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
            return item;
        }

        private void CreateText(Transform parent, string name, string text, Vector2 position, Vector2 size,
            float fontSize, TextAlignmentOptions alignment, Color color)
        {
            var textObject = SceneFirst.Child(parent, name, out bool created,
                typeof(RectTransform), typeof(TextMeshProUGUI));
            if (!created) return;

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
            var item = SceneFirst.Child(parent, name, out bool created, typeof(RectTransform), typeof(Image));
            var image = item.GetComponent<Image>();
            if (!created) return image;

            Stretch(item.GetComponent<RectTransform>());
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateCenteredImage(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var item = SceneFirst.Child(parent, name, out bool created, typeof(RectTransform), typeof(Image));
            if (!created) return item.GetComponent<Image>();

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
}
