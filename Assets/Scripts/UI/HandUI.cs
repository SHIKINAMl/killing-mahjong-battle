using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using KillingMahjong.Common;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    public class HandUI : HandBaseUI, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform handAreaRect; // For drag detection

        // --- Dragging the Hand Panel ---
        private RectTransform panelRect;
        private Vector2 dragOffset;

        [Header("Cursor")]
        [SerializeField] private Transform cursor; // Changed from RectTransform to Transform
        
        [Header("Buttons")]
        [SerializeField] private Button decideButton;
        [SerializeField] private Button autoManganButton;

        [Tooltip("打牌フェイズの「自動: ON/OFF」ボタンを出すか。\n" +
                 "現在は非表示にしている（機能自体は AutoDiscardController に残っている）")]
        [SerializeField] private bool showAutoDiscardButton = false;

        // ---- 手牌の覗き見（打牌フェイズ・要望19）----
        //
        // **ここは意図的に SerializeField にしていない。**
        // SerializeField にすると値がシーンに焼き付き、あとからコードの既定値を変えても
        // 反映されなくなる（実際に一度この罠を踏んだ）。
        // しかも対局シーンは UIテストシーン と OpeningScene の2つあるため、
        // シーン側に持たせると片方だけ直し忘れる。調整はここを直接触ること。

        /// <summary>打牌フェイズで手牌を普段は隠し、ボタンを押している間だけ出すか。
        /// 打牌フェイズで捨てるのは山牌なので、手牌は参照情報でしかない</summary>
        private const bool HideHandUntilPeek = true;

        /// <summary>覗いている間に背景を暗くするか。
        /// 一瞬見るだけなので、暗くしない方が邪魔にならない</summary>
        private const bool PeekUseDimmer = false;

        /// <summary>暗くする場合の濃さ（PeekUseDimmer が true のときだけ効く）</summary>
        private const float PeekDimAlpha = 0.55f;

        private const string PeekButtonLabel = "手牌を見る";

        /// <summary>覗いている間、手牌を画面中央へ寄せて拡大するか。
        /// false なら普段いる場所（画面左下）にそのまま出る</summary>
        private const bool PeekCenterOverlay = true;

        /// <summary>覗いている間の手牌の位置。Canvas の中央が原点。
        /// 上げすぎるとキャラのセリフの吹き出しと被る</summary>
        private static readonly Vector2 PeekOverlayPos = new Vector2(0f, -165f);

        private const float PeekOverlayScale = 1.3f;
        
        public RectTransform AutoManganButtonRect => autoManganButton != null ? autoManganButton.GetComponent<RectTransform>() : null;
        public RectTransform DecideButtonRect => decideButton != null ? decideButton.GetComponent<RectTransform>() : null;

        private int currentSelectionIndex = 0;

        private Button reselectButton;
        private Button autoDiscardButton;
        private bool isAutoDiscardEnabled = false;

        private Button peekButton;
        private bool isPeeking;
        private Canvas peekTilesCanvas;   // 覗いている間だけ手牌を前面へ出す
        private Image peekDimmer;

        // 中央へ寄せる前の状態。戻すために覚えておく
        private bool peekTransformSaved;
        private Vector2 peekOriginalPos;
        private Vector3 peekOriginalScale;

        public bool IsAutoDiscardEnabled
        {
            get => isAutoDiscardEnabled;
            set
            {
                isAutoDiscardEnabled = value;
                UpdateAutoDiscardButtonText();
            }
        }

        private void Start()
        {
            panelRect = GetComponent<RectTransform>();
            decideButton.onClick.AddListener(OnDecideClicked);
            autoManganButton.onClick.AddListener(OnAutoManganClicked);
            UpdateCursorPosition();

            if (decideButton != null)
            {
                reselectButton = Instantiate(decideButton, decideButton.transform.parent);
                reselectButton.name = "ReselectButton";
                reselectButton.onClick.RemoveAllListeners();
                reselectButton.onClick.AddListener(OnReselectClicked);
                
                var tmp = reselectButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (tmp != null) tmp.text = "選び直す";
                var txt = reselectButton.GetComponentInChildren<UnityEngine.UI.Text>();
                if (txt != null) txt.text = "選び直す";

                reselectButton.gameObject.SetActive(false);

                autoDiscardButton = Instantiate(decideButton, decideButton.transform.parent);
                autoDiscardButton.name = "AutoDiscardButton";
                autoDiscardButton.onClick.RemoveAllListeners();
                autoDiscardButton.onClick.AddListener(OnAutoDiscardClicked);
                
                RectTransform rt = autoDiscardButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, rt.anchoredPosition.y + 80);
                }
                UpdateAutoDiscardButtonText();
                autoDiscardButton.gameObject.SetActive(false);

                CreatePeekButton();
            }
        }

        /// <summary>
        /// 手牌を覗くボタンを作る。
        ///
        /// 打牌フェイズでは「決定」が非表示になる（UpdateLayout 参照）ので、
        /// 位置をずらさずそのまま同じ場所に置いてよい。
        /// シーンに直接置かないのは、対局シーンが2つ（UIテストシーン / OpeningScene）あり
        /// 両方に同じ物を置くと片方だけ直し忘れるため。既存の「選び直す」等と同じやり方。
        /// </summary>
        private void CreatePeekButton()
        {
            peekButton = Instantiate(decideButton, decideButton.transform.parent);
            peekButton.name = "HandPeekButton";
            peekButton.onClick.RemoveAllListeners();   // 押しっぱなしで見るので click は使わない

            var tmp = peekButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = PeekButtonLabel;
                // 複製元の「決定」は2文字ぶんの幅しかないので、そのままだと
                // 「手牌を見る」の末尾が切れる。幅に収まるまで縮める
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 10f;
                tmp.fontSizeMax = tmp.fontSize;
                tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                tmp.overflowMode = TMPro.TextOverflowModes.Overflow;
            }
            var txt = peekButton.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = PeekButtonLabel;
                txt.resizeTextForBestFit = true;
            }

            // **Canvas を足したら GraphicRaycaster も要る。**
            // Graphic は最寄りの Canvas に登録されるため、Canvas を足した時点で
            // 親の GraphicRaycaster の探索対象から外れ、クリックが一切届かなくなる
            Ensure<Canvas>(peekButton.gameObject);
            Ensure<GraphicRaycaster>(peekButton.gameObject);

            var hold = peekButton.gameObject.AddComponent<HoldButton>();
            hold.onHoldStart.AddListener(() => SetPeek(true));
            hold.onHoldEnd.AddListener(() => SetPeek(false));

            peekButton.gameObject.SetActive(false);
        }

        /// <summary>打牌フェイズで手牌を隠す対象か。</summary>
        private bool IsPeekPhase(RoundStatus phaseStatus)
        {
            if (!HideHandUntilPeek) return false;
            if (phaseStatus != RoundStatus.Discard) return false;
            // チュートリアルは台本どおりに手牌を見せる場面があるので触らない
            if (gameUIManager != null && gameUIManager.IsTutorialMode) return false;
            return true;
        }

        private void SetPeek(bool on)
        {
            if (isPeeking == on) return;
            isPeeking = on;
            ApplyPeekVisual(on);

            // 手牌を実際に表示させるのは UpdateLayout。
            // **重なり順の設定も中央寄せも、その後でなければならない。**
            // 非アクティブなオブジェクトの Canvas は overrideSorting が入らず、
            // 表示前は子が非アクティブで牌の範囲も測れない
            if (gameUIManager != null) UpdateLayout(gameUIManager.CurrentPhaseStatus);
            ApplyPeekTilesOrder(on);
            ApplyPeekPlacement(on);
        }

        /// <summary>暗幕の表示と、手牌を前面へ出すかどうかを切り替える。</summary>
        private void ApplyPeekVisual(bool on)
        {
            if (PeekUseDimmer)
            {
                EnsurePeekDimmer();
                if (peekDimmer != null) peekDimmer.gameObject.SetActive(on);
            }
            else if (peekDimmer != null)
            {
                peekDimmer.gameObject.SetActive(false);
            }

        }

        /// <summary>
        /// 覗いている間だけ手牌を前面へ出す。
        ///
        /// **必ず手牌が表示された後に呼ぶこと。**
        /// 非アクティブな GameObject の Canvas に `overrideSorting` を代入しても効かず、
        /// 手牌は親の Canvas（order 1）のまま描かれて、山牌（WallCanvas order 2）に隠れる。
        /// 毎回入れ直しているのはそのため。
        /// </summary>
        private void ApplyPeekTilesOrder(bool on)
        {
            if (discardPhaseContainer == null) return;
            if (peekTilesCanvas == null) peekTilesCanvas = Ensure<Canvas>(discardPhaseContainer.gameObject);

            if (on)
            {
                peekTilesCanvas.enabled = true;
                peekTilesCanvas.overrideSorting = true;
                peekTilesCanvas.sortingOrder = UISortingOrders.HandPeekTiles;
            }
            else
            {
                // 覗いていない間は無効にしておく。有効なままだと和了・結果フェイズでも
                // 手牌が結果パネルより手前に出てしまう
                peekTilesCanvas.enabled = false;
            }
        }

        /// <summary>
        /// 覗いている間だけ手牌を画面中央へ寄せて拡大する。
        ///
        /// アンカーやピボットは触らない。コンテナの中の牌はコンテナ基準で配置されているので、
        /// アンカーを変えると牌の位置まで動いてしまう。
        /// 代わりに**牌全体の実際の範囲**（子の境界）の中心が目標へ来るよう平行移動する。
        /// コンテナの rect の中心は牌の見た目の中心とは限らないため。
        /// </summary>
        private void ApplyPeekPlacement(bool on)
        {
            if (!PeekCenterOverlay) return;

            var rt = discardPhaseContainer as RectTransform;
            if (rt == null) return;

            if (!on)
            {
                if (!peekTransformSaved) return;
                rt.localScale = peekOriginalScale;
                rt.anchoredPosition = peekOriginalPos;
                peekTransformSaved = false;
                return;
            }

            if (peekTransformSaved) return;   // 二重に保存しない

            // 表示前だと子が非アクティブで範囲を測れない。呼ぶ順番の間違いに気づけるようにする
            if (!rt.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[HandUI] 手牌が非表示のまま中央寄せを計算しようとした。UpdateLayout の後に呼ぶこと");
                return;
            }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var canvasRt = (canvas.rootCanvas != null ? canvas.rootCanvas : canvas).transform as RectTransform;
            if (canvasRt == null) return;

            peekOriginalPos = rt.anchoredPosition;
            peekOriginalScale = rt.localScale;

            // 先に拡大してから範囲を測る（拡大後の見た目で中心を合わせたいため）
            rt.localScale = Vector3.one * PeekOverlayScale;

            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRt, rt);
            if (bounds.size.x < 1f || bounds.size.y < 1f)
            {
                // 測れていない。ここで動かすと画面外へ飛ぶので、拡大だけに留める
                Debug.LogWarning("[HandUI] 手牌の範囲を測れなかったので中央寄せを見送った");
                peekTransformSaved = true;
                return;
            }

            Vector3 targetWorld = canvasRt.TransformPoint(new Vector3(PeekOverlayPos.x, PeekOverlayPos.y, 0f));
            Vector3 centerWorld = canvasRt.TransformPoint(bounds.center);
            rt.position += (targetWorld - centerWorld);
            peekTransformSaved = true;
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        private void EnsurePeekDimmer()
        {
            if (peekDimmer != null) return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var root = canvas.rootCanvas != null ? canvas.rootCanvas.transform : canvas.transform;

            var go = new GameObject("HandPeekDimmer", typeof(RectTransform));
            go.transform.SetParent(root, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var c = go.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = UISortingOrders.HandPeekDimmer;

            peekDimmer = go.AddComponent<Image>();
            peekDimmer.color = new Color(0f, 0f, 0f, PeekDimAlpha);
            // クリックを吸うと、押しっぱなしの解放（OnPointerUp）が
            // ボタンに届かなくなって手牌が出たまま固まる
            peekDimmer.raycastTarget = false;

            go.SetActive(false);
        }

        private void OnAutoDiscardClicked()
        {
            IsAutoDiscardEnabled = !IsAutoDiscardEnabled;
            if (IsAutoDiscardEnabled && gameUIManager != null && gameUIManager.CurrentPhaseStatus == RoundStatus.Discard)
            {
                var autoDiscard = gameUIManager.GetComponent<AutoDiscardController>();
                if (autoDiscard == null)
                {
                    autoDiscard = gameUIManager.gameObject.AddComponent<AutoDiscardController>();
                }
                autoDiscard.CheckAndExecuteAutoDiscard();
            }
        }

        private void UpdateAutoDiscardButtonText()
        {
            if (autoDiscardButton != null)
            {
                string t = IsAutoDiscardEnabled ? "自動: ON" : "自動: OFF";
                var tmp = autoDiscardButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (tmp != null) tmp.text = t;
                var txt = autoDiscardButton.GetComponentInChildren<UnityEngine.UI.Text>();
                if (txt != null) txt.text = t;

                var img = autoDiscardButton.GetComponent<Image>();
                if (img != null) img.color = IsAutoDiscardEnabled ? Color.green : Color.red;
            }
        }

        private void OnReselectClicked()
        {
            if (gameUIManager != null && gameUIManager.CurrentPhaseStatus == RoundStatus.HandSelection)
            {
                gameUIManager.CancelHandSelection();
            }
        }

        // --- Drag Panel Implementation ---
        public void OnBeginDrag(PointerEventData eventData)
        {
            // パネル移動用 (タイル自体のドラッグの妨げにならないよう必要に応じて背景などをターゲットにします)
            if (panelRect != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    panelRect, eventData.position, eventData.pressEventCamera, out dragOffset);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (panelRect != null)
            {
                RectTransform parentRect = panelRect.parent as RectTransform;
                if (parentRect != null)
                {
                    Vector2 localPointerPosition;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parentRect, eventData.position, eventData.pressEventCamera, out localPointerPosition))
                    {
                        panelRect.localPosition = localPointerPosition - dragOffset;
                    }
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // 必要に応じてスナップ処理等
        }

        private int GetCategoryPriority(List<TileData> list)
        {
            if (list.Count == 0) return 99;
            var cat = list[0].Category;
            switch (cat)
            {
                case TileCategory.Souzu: return 1;
                case TileCategory.Manzu: return 2;
                case TileCategory.Pinzu: return 3;
                case TileCategory.Honor: return 4;
                default: return 99;
            }
        }

        public void MoveCursor(int direction)
        {
            currentSelectionIndex += direction;
            if (currentSelectionIndex < 0) currentSelectionIndex = 0;
            if (currentSelectionIndex >= handSlots.Count) currentSelectionIndex = handSlots.Count - 1;
            
            UpdateCursorPosition();
        }

        private void UpdateCursorPosition()
        {
            if (handSlots.Count > 0 && currentSelectionIndex < handSlots.Count)
            {
                // Uses World Position now
                if (handSlots[currentSelectionIndex] != null)
                    cursor.position = handSlots[currentSelectionIndex].position;
            }
        }

        private void OnDecideClicked()
        {
            if (gameUIManager == null) return;
            if (isSubmitted || gameUIManager.IsTransitioning) return;

            if (gameUIManager.CurrentPhaseStatus == RoundStatus.Discard)
            {
                gameUIManager.DiscardSelectedTile();
            }
            else if (gameUIManager.CurrentPhaseStatus == RoundStatus.HandSelection)
            {
                Debug.Log($"Decide Clicked. Current Hand Count: {handSlots.Count}");
                if (handSlots.Count == 13)
                {
                    gameUIManager.CompleteHandSelection();
                }
                else
                {
                    Debug.LogWarning("Hand must have exactly 13 tiles to proceed!");
                }
            }
        }

        private void OnAutoManganClicked()
        {
            Debug.Log("Auto Mangan Hand Clicked");
            if (gameUIManager == null) return;
            if (isSubmitted || gameUIManager.IsTransitioning) return;

            if (gameUIManager.CurrentPhaseStatus == RoundStatus.HandSelection)
            {
                if (gameUIManager.IsTutorialMode && gameUIManager.TutorialManager != null)
                {
                    gameUIManager.TutorialManager.ApplyMockAutoMangan();
                }
                else
                {
                    gameUIManager.SelectManganHand();
                }
            }
        }
        public bool IsPointInHandArea(Vector2 screenPoint)
        {
            if (handAreaRect == null) 
            {
                // Fallback to container if not assigned
                 var rt = handSlotContainer as RectTransform;
                 if (rt != null) return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint);
                 return false;
            }
            return RectTransformUtility.RectangleContainsScreenPoint(handAreaRect, screenPoint);
        }
        private bool isSubmitted = false;
        public bool IsSubmitted => isSubmitted;

        public void SetSubmittedState(bool submitted)
        {
            isSubmitted = submitted;
            if (gameUIManager != null) UpdateLayout(gameUIManager.CurrentPhaseStatus);
        }

        public override void SortHandSlots()
        {
            handSlots.Sort((a, b) =>
            {
                var ia = a.GetComponent<TileInteraction>();
                var ib = b.GetComponent<TileInteraction>();
                int idA = (ia != null) ? ia.TileId : 0;
                int idB = (ib != null) ? ib.TileId : 0;

                int baseA = idA & 0x1F;
                int baseB = idB & 0x1F;
                if (baseA != baseB) return baseA.CompareTo(baseB);
                return idA.CompareTo(idB);
            });

            for (int i = 0; i < handSlots.Count; i++)
            {
                handSlots[i].SetSiblingIndex(i);
            }

            if (gameUIManager != null) UpdateLayout(gameUIManager.CurrentPhaseStatus);
        }

        public override void UpdateLayout(RoundStatus phaseStatus)
        {
            base.UpdateLayout(phaseStatus);

            bool showButtons = (phaseStatus == RoundStatus.HandSelection) && !isSubmitted && (gameUIManager == null || (!gameUIManager.IsMulliganSelection && !gameUIManager.IsOpponentSkillProcessing));

            // チュートリアルでは「13枚選ぶ → 自動 → 決定」の順にボタンを開放する
            bool showAuto = showButtons;
            bool showDecide = showButtons;
            if (showButtons && gameUIManager != null && gameUIManager.IsTutorialMode && gameUIManager.TutorialManager != null)
            {
                showAuto = gameUIManager.TutorialManager.IsAutoButtonVisible;
                showDecide = gameUIManager.TutorialManager.IsDecideButtonVisible;
            }

            if (decideButton != null)
            {
                decideButton.gameObject.SetActive(showDecide);
            }
            if (autoManganButton != null)
            {
                autoManganButton.gameObject.SetActive(showAuto);
            }
            if (reselectButton != null)
            {
                // チュートリアルでは台本どおりに進めたいので出さない（要望15）
                bool canReselect = (phaseStatus == RoundStatus.HandSelection) && isSubmitted
                    && gameUIManager != null && !gameUIManager.IsTransitioning
                    && !gameUIManager.IsMulliganSelection && !gameUIManager.IsTutorialMode;
                reselectButton.gameObject.SetActive(canReselect);
            }
            if (autoDiscardButton != null)
            {
                // 自動打牌ボタンは非表示にする（要望5）。
                // 機能そのもの（AutoDiscardController）は残してあるので、
                // showAutoDiscardButton を true に戻せば元どおり出る。
                autoDiscardButton.gameObject.SetActive(
                    showAutoDiscardButton && phaseStatus == RoundStatus.Discard);
            }

            // --- 手牌の覗き見（要望19） ---
            bool peekPhase = IsPeekPhase(phaseStatus);

            // 打牌フェイズを抜けたら覗き状態を必ず戻す。
            // ここで SetPeek を呼ぶと UpdateLayout が再帰するので、状態だけ直接畳む
            if (!peekPhase && isPeeking)
            {
                isPeeking = false;
                ApplyPeekVisual(false);
                ApplyPeekPlacement(false);   // 位置と拡大も元へ戻す
            }

            if (peekButton != null)
            {
                peekButton.gameObject.SetActive(peekPhase);
                if (peekPhase)
                {
                    // **覗いている間だけ**前面に出す。中央へ寄せた手牌(86)にボタンが
                    // 隠れて押せなくなるのを防ぐため。
                    //
                    // 覗いていない間まで 87 に置いてはいけない。賭け金確定後のフェーズ演出は
                    // PhaseTransitionBase(19) で描かれるので、ボタンが演出の前に残ってしまう。
                    // 非アクティブなうちに設定しても効かないので、表示した後に入れ直す
                    var bc = peekButton.GetComponent<Canvas>();
                    if (bc != null)
                    {
                        bc.overrideSorting = true;
                        bc.sortingOrder = isPeeking
                            ? UISortingOrders.HandPeekTiles + 1
                            : UISortingOrders.HandPeekButtonIdle;
                    }
                }
            }
            if (peekPhase && discardPhaseContainer != null)
            {
                // base.UpdateLayout が打牌フェイズでは常に表示にするので、ここで上書きする
                discardPhaseContainer.gameObject.SetActive(isPeeking);
            }
        }
    }
}
