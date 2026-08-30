using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using KillingMahjong.Common;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    public partial class HandUI : HandBaseUI, IBeginDragHandler, IDragHandler, IEndDragHandler
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

            // シーンの表記は "Decide" / "Auto" のままなので、実行時に日本語へ直す。
            // **複製より先に直す**（reselect などは decideButton を Instantiate して作るため、
            // ここで直しておかないと英語のまま複製される）。
            // シーンを触らないのは、対局シーンが2つあって片方だけ直す事故を避けるため。
            SetButtonLabel(decideButton, "決定");
            SetButtonLabel(autoManganButton, "おまかせ");

            // **複製より先に整える。** 以降の reselect / autoDiscard / peek は
            // decideButton の Instantiate なので、ここで揃えておけば全部が揃った形で複製される。
            NormalizeActionButtons();

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
        /// <summary>
        /// 「手牌を見る」を「決定」の位置からどれだけ下げるか(px)。
        ///
        /// 山牌の下段の下端は画面 y=550（矩形）、複製したままのボタン上端は y=545 で 5px 食い込む。
        /// 8 下げると矩形で 3px、絵で約 10px 空く（牌の絵は 40px の矩形の中で下に 8px 余白がある）。
        /// これ以上下げると画面の下端に貼り付くので、増やすならボタンの高さごと見直すこと。
        /// </summary>
        private const float PeekButtonDropY = 8f;

        // ---- 進行の案内（調整値。シーンではなくここを触る）----
        //
        // このゲームは「山牌から13枚選んで手牌を組む」「打牌フェイズで切るのは手牌ではなく山牌」
        // という独自ルールなのに、画面のどこにもそれが書いていなかった。
        // **麻雀の常識では画面最下段＝自分の手牌**なので、初見はまず取り違える。

        /// <summary>案内を出すか。うるさければ false に</summary>
        private const bool ShowPhaseGuide = true;

        /// <summary>
        /// 山牌の上端から、どれだけ上に置くか。
        ///
        /// **固定の高さにはできない。** 手牌選択では山牌が2段に積まれて背が高く、
        /// 打牌フェイズでは減って低くなるので、決め打ちだと片方で牌に重なる。
        /// 毎回いまの山牌の上端を測って、そこから持ち上げる。
        /// </summary>
        private const float PhaseGuideLift = 22f;

        /// <summary>山牌が見つからないときの逃げ場（画面下端からの高さ）</summary>
        private const float PhaseGuideFallbackY = 118f;

        private const float PhaseGuideFontSize = 15f;
        private static readonly Color PhaseGuideColor = new Color(1f, 1f, 1f, 0.92f);

        private TMPro.TextMeshProUGUI _phaseGuide;
        private int _lastGuideHandCount = -1;
        private RoundStatus _lastGuidePhase = (RoundStatus)(-1);

        /// <summary>案内の器。シーンには置かない（対局シーンが2つあるため）</summary>
        /// <summary>手牌の枚数。ルール上13枚で固定</summary>
        private const int HandSize = 13;

        private RectTransform _wallRect;

        /// <summary>
        /// **必ず base を呼ぶこと。** 基底の Update は牌を目標座標へ毎フレーム補間しており、
        /// ここで隠すと牌がアニメーションしなくなる（`new` で隠すと基底は呼ばれない）。
        /// </summary>
        protected override void Update()
        {
            base.Update();

            // 牌をクリックしても UpdateLayout が呼ばれるとは限らないので、
            // 枚数の表示だけはここで追う（変化が無ければ何もしない）
            if (gameUIManager != null) UpdatePhaseGuide(gameUIManager.CurrentPhaseStatus);
        }
        private const float ActionButtonWidth = 120f;
        private const float ActionButtonHeight = 40f;

        /// <summary>
        /// ボタンの外側に空ける画面端からの余白。左右で同じ値を使うので対称になる。
        ///
        /// 65 だと「決定」の右上に手前のスマホUI（懐中時計の絵）が 8px まで迫り、
        /// 押し間違いを誘う詰まり方になっていた。90 まで広げて両方を内側へ寄せている
        /// （左右で同じ値なので対称は保たれる。片側だけ動かすとこの対称が崩れる）。
        /// </summary>
        private const float ActionButtonEdgeMargin = 90f;

        /// <summary>文字がボタンの縁に触れないようにする内側の余白。</summary>
        private const float ActionButtonTextPadding = 14f;

        /// <summary>
        /// 「決定」と「おまかせ」を同じ大きさにし、画面中心に対して左右対称に置く。
        ///
        /// 親（HandPanel）自体が画面中心から x=+10 ずれているため、単に ±同値 を入れても
        /// 画面上では対称にならない。親のずれを引いてから左右を決める。
        /// </summary>
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


        public void MoveCursor(int direction)
        {
            currentSelectionIndex += direction;
            if (currentSelectionIndex < 0) currentSelectionIndex = 0;
            if (currentSelectionIndex >= handSlots.Count) currentSelectionIndex = handSlots.Count - 1;
            
            UpdateCursorPosition();
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

                return KillingMahjong.Common.TileId.CompareForDisplay(idA, idB);
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

                // **13枚そろうまで押せなくする。** 足りないまま押しても
                // サーバーに弾かれるだけで、何が悪いのか画面からは分からなかった。
                // チュートリアルは台本が開放の順番を決めているので触らない。
                if (showDecide && (gameUIManager == null || !gameUIManager.IsTutorialMode))
                {
                    int handCount = (Managers.BoardStateManager.Instance != null &&
                                     Managers.BoardStateManager.Instance.CurrentHandTiles != null)
                                    ? Managers.BoardStateManager.Instance.CurrentHandTiles.Count : 0;
                    decideButton.interactable = (handCount == HandSize);
                }
                else if (decideButton != null)
                {
                    decideButton.interactable = true;
                }
            }
            if (autoManganButton != null)
            {
                autoManganButton.gameObject.SetActive(showAuto);
            }
            if (reselectButton != null)
            {
                // チュートリアルでは台本どおりに進めたいので出さない（要望15）
                //
                // **相手を待っている間は出したままでよい**（取り下げは仕様）。
                // 引っ込めるのは相手も確定して掛け金フェイズへ移る直前だけ。
                // ここが無いと phase_change が届くまでの隙間を連打で抜けられ、
                // 受理済みの手牌に select_cancel が飛んでしまう。
                bool canReselect = (phaseStatus == RoundStatus.HandSelection) && isSubmitted
                    && gameUIManager != null && !gameUIManager.IsTransitioning
                    && !gameUIManager.IsMulliganSelection && !gameUIManager.IsTutorialMode
                    && (gameUIManager.HandSelectionController == null
                        || !gameUIManager.HandSelectionController.IsSelectionLockedIn);
                reselectButton.gameObject.SetActive(canReselect);
            }
            UpdatePhaseGuide(phaseStatus);

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
