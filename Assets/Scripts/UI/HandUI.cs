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

            // シーンの表記は "Decide" / "Auto" のままなので、実行時に日本語へ直す。
            // **複製より先に直す**（reselect などは decideButton を Instantiate して作るため、
            // ここで直しておかないと英語のまま複製される）。
            // シーンを触らないのは、対局シーンが2つあって片方だけ直す事故を避けるため。
            SetButtonLabel(decideButton, "決定");
            SetButtonLabel(autoManganButton, "おまかせ");

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
            // チュートリアルも本編と同じ挙動にする。
            // 台本が手牌を見せるのは手牌フェイズ（能力の実演・透視の印）だけで、
            // 打牌フェイズで手牌を指す誘導は無いため、ここで隠しても台本は壊れない
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
        private TMPro.TextMeshProUGUI EnsurePhaseGuide()
        {
            if (_phaseGuide != null) return _phaseGuide;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return null;

            var go = new GameObject("PhaseGuideText", typeof(RectTransform));
            go.transform.SetParent(canvas.rootCanvas.transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(560f, 26f);
            rt.anchoredPosition = new Vector2(0f, PhaseGuideFallbackY);

            var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.fontSize = PhaseGuideFontSize;
            tmp.color = PhaseGuideColor;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.overflowMode = TMPro.TextOverflowModes.Overflow;
            // 卓の緑にも暗い床にも載るので、縁を付けて背景から切り離す
            tmp.fontMaterial.EnableKeyword("OUTLINE_ON");
            tmp.outlineColor = Color.black;
            tmp.outlineWidth = 0.25f;

            _phaseGuide = tmp;
            return _phaseGuide;
        }

        /// <summary>
        /// 「いま何をすればいいか」を1行で出す。手牌選択では選んだ枚数も添える。
        /// **チュートリアルでは出さない**（台本が同じことを順番に喋るため）。
        /// </summary>
        private void UpdatePhaseGuide(RoundStatus phaseStatus)
        {
            if (!ShowPhaseGuide) return;

            bool tutorial = gameUIManager != null && gameUIManager.IsTutorialMode;
            bool wanted = !tutorial &&
                (phaseStatus == RoundStatus.HandSelection || phaseStatus == RoundStatus.Discard);

            if (!wanted)
            {
                if (_phaseGuide != null) _phaseGuide.gameObject.SetActive(false);
                _lastGuidePhase = (RoundStatus)(-1);
                _lastGuideHandCount = -1;
                return;
            }

            var guide = EnsurePhaseGuide();
            if (guide == null) return;

            int handCount = (Managers.BoardStateManager.Instance != null &&
                             Managers.BoardStateManager.Instance.CurrentHandTiles != null)
                            ? Managers.BoardStateManager.Instance.CurrentHandTiles.Count : 0;

            // 毎フレーム text を代入するとそのたびに文字が組み直されるので、変わったときだけ
            if (phaseStatus != _lastGuidePhase || handCount != _lastGuideHandCount)
            {
                if (phaseStatus == RoundStatus.HandSelection)
                {
                    string count = (handCount == HandSize)
                        ? $"<color=#7CE07C>{handCount} / {HandSize}</color>"
                        : $"<color=#FFD24A>{handCount} / {HandSize}</color>";
                    // **短く保つこと。** 卓の右手前に置物があり、長いと右端が隠れる
                    guide.text = $"山牌から{HandSize}枚えらぶ　{count}";
                }
                else
                {
                    guide.text = "山牌から1枚切る";
                }

                _lastGuidePhase = phaseStatus;
                _lastGuideHandCount = handCount;
            }

            PlaceGuideAboveWall(guide);
            guide.gameObject.SetActive(true);
        }

        /// <summary>手牌の枚数。ルール上13枚で固定</summary>
        private const int HandSize = 13;

        private RectTransform _wallRect;

        /// <summary>
        /// 案内を山牌のすぐ上に置く。
        ///
        /// 案内は ScreenSpace-Overlay の Canvas に下端中央アンカーで置いてあるので、
        /// 画面座標を scaleFactor で割れば、そのまま anchoredPosition.y になる。
        /// </summary>
        private void PlaceGuideAboveWall(TMPro.TextMeshProUGUI guide)
        {
            if (_wallRect == null)
            {
                var go = GameObject.Find("WallContainer");
                if (go != null) _wallRect = go.transform as RectTransform;
            }
            if (_wallRect == null) return;

            var canvas = guide.canvas;
            if (canvas == null) return;

            var corners = new Vector3[4];
            _wallRect.GetWorldCorners(corners);
            float topScreenY = RectTransformUtility.WorldToScreenPoint(null, corners[1]).y;

            float scale = Mathf.Approximately(canvas.scaleFactor, 0f) ? 1f : canvas.scaleFactor;
            float y = topScreenY / scale + PhaseGuideLift;

            var rt = guide.rectTransform;
            if (!Mathf.Approximately(rt.anchoredPosition.y, y))
            {
                rt.anchoredPosition = new Vector2(0f, y);
            }
        }

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

        /// <summary>ボタンの文字を差し替える。TMP と旧 Text の両方に対応する</summary>
        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null) return;
            var tmp = button.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (tmp != null) tmp.text = label;
            var txt = button.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (txt != null) txt.text = label;
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
