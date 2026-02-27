using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    public class HandUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Hand Slots")]
        [SerializeField] private Transform handSlotContainer;
        // 以前のInspectorで設定されていたnullの要素が残ってクラッシュするのを防ぐため、シリアライズ対象外にする
        private List<RectTransform> handSlots = new List<RectTransform>();
        public List<RectTransform> GetHandSlots() 
        {
            if (handSlots == null) handSlots = new List<RectTransform>();
            return handSlots;
        }

        [SerializeField] private TileResourceManager tileResourceManager;
        [SerializeField] private RectTransform handAreaRect; // For drag detection

        [Header("Layout Settings")]
        [SerializeField] private float tileSpacingX = 50f;
        [SerializeField] private float tileSpacingY = 70f;
        
        // --- Dragging the Hand Panel ---
        private RectTransform panelRect;
        private Vector2 dragOffset;

        private GameUIManager gameUIManager;

        public void Setup(GameUIManager manager)
        {
            this.gameUIManager = manager;
        }

        [Header("Cursor")]
        [SerializeField] private Transform cursor; // Changed from RectTransform to Transform
        
        [Header("Buttons")]
        [SerializeField] private Button decideButton;
        [SerializeField] private Button autoManganButton;

        private int currentSelectionIndex = 0;

        private void Start()
        {
            panelRect = GetComponent<RectTransform>();
            decideButton.onClick.AddListener(OnDecideClicked);
            autoManganButton.onClick.AddListener(OnAutoManganClicked);
            UpdateCursorPosition();
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
                Vector2 localPointerPosition;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    panelRect.parent as RectTransform, eventData.position, eventData.pressEventCamera, out localPointerPosition))
                {
                    panelRect.localPosition = localPointerPosition - dragOffset;
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // 必要に応じてスナップ処理等
        }

        public void AddTileToHand(RectTransform tileTransform, int tileId)
        {
            if (tileTransform == null) return;
            
            // 既存の手牌リストに追加
            handSlots.Add(tileTransform);

            // コンテナ移動
            tileTransform.SetParent(handSlotContainer, false);
            tileTransform.localPosition = Vector3.zero;
            
            RectTransform rt = tileTransform.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition3D = Vector3.zero;
            }
            
            tileTransform.localRotation = Quaternion.identity;
            tileTransform.localScale = Vector3.one;
            tileTransform.gameObject.SetActive(true);

            // インタラクション設定の更新（Handに移動したフラグをTrueにする）
            var interaction = tileTransform.GetComponent<TileInteraction>();
            if (interaction != null && gameUIManager != null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                interaction.Initialize(tileId, true, gameUIManager, canvas);
            }

            Debug.Log($"HandUI: Added tile {tileId}. Total handSlots: {handSlots.Count}");
            
            // LayoutGroup が並び替えてくれるため、ここでのソートや自前整列処理はいったん省略します。
            // (本来は TileID順 に並び変えるなら Transform の SiblingIndex を操作します)
            if (gameUIManager != null)
            {
                UpdateLayout(gameUIManager.CurrentPhaseStatus);
            }
            else
            {
                UpdateLayout(""); // Fallback
            }
        }

        public void RemoveTileFromHand(RectTransform tileTransform, int tileId)
        {
            if (handSlots.Contains(tileTransform))
            {
                handSlots.Remove(tileTransform);
                // インタラクション設定の更新（Wallに移動したフラグをFalseにする）
                var interaction = tileTransform.GetComponent<TileInteraction>();
                if (interaction != null && gameUIManager != null)
                {
                    Canvas canvas = GetComponentInParent<Canvas>();
                    interaction.Initialize(tileId, false, gameUIManager, canvas);
                }
            }
        }

        public void UpdateLayout(string phaseStatus)
        {
            var layoutGroup = handSlotContainer.GetComponent<UnityEngine.UI.LayoutGroup>();
            
            // 手牌選択フェイズなどは既存のLayoutGroup（もしあれば）に任せるか、単純にならべる
            if (phaseStatus != "discard")
            {
                if (layoutGroup != null) layoutGroup.enabled = true;
                
                // 既存の簡易ソート（ID順）
                handSlots.Sort((a, b) => 
                {
                    var interactionA = a.GetComponent<TileInteraction>();
                    var interactionB = b.GetComponent<TileInteraction>();
                    if (interactionA == null || interactionB == null) return 0;
                    return interactionA.TileId.CompareTo(interactionB.TileId);
                });

                for (int i = 0; i < handSlots.Count; i++)
                {
                    handSlots[i].SetSiblingIndex(i);
                }
                return;
            }

            // ---------------------------------------------
            // 打牌（Discard）フェイズ時の特別なレイアウトとソート
            // ---------------------------------------------
            if (layoutGroup != null) layoutGroup.enabled = false;

            // 1. ソート処理: 萬子 → 筒子 → 索子 → 字牌 の順、かつ数字順
            handSlots.Sort((a, b) => 
            {
                var intA = a.GetComponent<TileInteraction>();
                var intB = b.GetComponent<TileInteraction>();
                if (intA == null || intB == null) return 0;

                TileData dataA = new TileData(intA.TileId);
                TileData dataB = new TileData(intB.TileId);

                // カテゴリで比較 (0:Manzu, 1:Pinzu, 2:Souzu, 3:Honor)
                if (dataA.Category != dataB.Category)
                {
                    return dataA.Category.CompareTo(dataB.Category);
                }
                
                // 同じカテゴリなら数字で比較 (1-9)
                if (dataA.Number != dataB.Number)
                {
                    return dataA.Number.CompareTo(dataB.Number);
                }
                
                // 完全一致
                return 0;
            });

            // SiblingIndexを更新（見た目の重なり順対応）
            for (int i = 0; i < handSlots.Count; i++)
            {
                handSlots[i].SetSiblingIndex(i);
            }

            // 2. 2列 × 7枚 の直接配置（隙間なし）
            // 「奥から左に」→ Yを奥(上)から手前(下)へ、列を右から左へ並べる想定の実装
            if (handSlots.Count == 0) return;
            
            RectTransform firstTileRT = handSlots[0].GetComponent<RectTransform>();
            float tileWidth = firstTileRT.rect.width;
            float tileHeight = firstTileRT.rect.height;

            for (int i = 0; i < handSlots.Count; i++)
            {
                // 1行につき7牌 (2行×7列を想定)
                int rowIndex = i / 7; // 0:奥(上)の行, 1:手前(下)の行
                int colIndex = i % 7; // 0〜6: 左から右へのインデックス

                // Inspectorで設定した独自の間隔を使用（デフォルトは画像のサイズ等）
                float w = tileSpacingX;
                float h = tileSpacingY;

                // 牌同士をピタッとくっつける場合、Inspectorで `tileSpacingX` と `tileSpacingY` を 
                // タイル画像ジャストの幅・高さに設定してください。

                // 奥(上)から手前(下)へ： 行番号が増えるほどYはマイナス方向へ
                float targetY = -rowIndex * h;
                
                // 左から右へ： 列番号が増えるほどXはプラス方向へ
                float targetX = colIndex * w;

                RectTransform rt = handSlots[i].GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0, 1); // 左上を基準に変更
                    rt.anchorMax = new Vector2(0, 1);
                    rt.pivot = new Vector2(0, 1);
                    rt.anchoredPosition = new Vector2(targetX, targetY);
                }
            }
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

            if (gameUIManager.CurrentPhaseStatus == "discard")
            {
                gameUIManager.DiscardSelectedTile();
            }
            else if (gameUIManager.CurrentPhaseStatus == "hand_selection")
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
            Debug.Log("Auto Mangan Clicked");
            // Notify Game logic to auto-complete hand
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
    }
}
