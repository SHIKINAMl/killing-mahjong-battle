using UnityEngine;
using System.Collections.Generic;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    public class HandBaseUI : MonoBehaviour
    {
        [Header("Hand Slots")]
        [SerializeField] protected Transform handSlotContainer; // For Dealing / Hand Selection Phase
        [SerializeField] protected Transform discardPhaseContainer; // For Discard Phase (New 2-row layout)
        
        protected List<RectTransform> handSlots = new List<RectTransform>();
        public List<RectTransform> GetHandSlots() 
        {
            if (handSlots == null) handSlots = new List<RectTransform>();
            return handSlots;
        }

        [SerializeField] protected TileResourceManager tileResourceManager;

        [Header("Layout Settings")]
        [SerializeField] protected float tileSpacingX = 50f;
        [SerializeField] protected float tileSpacingY = 70f;
        
        protected GameUIManager gameUIManager;

        public virtual void Setup(GameUIManager manager)
        {
            this.gameUIManager = manager;
        }

        public virtual void AddTileToHand(RectTransform tileTransform, int tileId)
        {
            if (tileTransform == null) return;
            
            handSlots.Add(tileTransform);

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

            var interaction = tileTransform.GetComponent<TileInteraction>();
            if (interaction != null && gameUIManager != null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                interaction.Initialize(tileId, true, gameUIManager, canvas);
            }

            var visual = tileTransform.GetComponent<TileVisual>();
            if (visual != null)
            {
                visual.SetFuritenHighlight(false);
            }

            if (gameUIManager != null)
            {
                UpdateLayout(gameUIManager.CurrentPhaseStatus);
            }
            else
            {
                UpdateLayout(RoundStatus.None); // Fallback
            }
        }

        public virtual void RemoveTileFromHand(RectTransform tileTransform, int tileId)
        {
            if (handSlots.Contains(tileTransform))
            {
                handSlots.Remove(tileTransform);
                var interaction = tileTransform.GetComponent<TileInteraction>();
                if (interaction != null && gameUIManager != null)
                {
                    Canvas canvas = GetComponentInParent<Canvas>();
                    interaction.Initialize(tileId, false, gameUIManager, canvas);
                }
            }
        }

        public virtual void UpdateLayout(RoundStatus phaseStatus)
        {
            if (handSlotContainer == null) return;

            bool isGameEndPhase = phaseStatus == RoundStatus.Agari || 
                                  phaseStatus == RoundStatus.Ron || 
                                  phaseStatus == RoundStatus.Result || 
                                  phaseStatus == RoundStatus.Draw;
            if (isGameEndPhase) return;

            bool isBoardActivePhase = phaseStatus == RoundStatus.Discard;

            var layoutGroup = handSlotContainer.GetComponent<UnityEngine.UI.LayoutGroup>();
            Transform activeContainer = (isBoardActivePhase && discardPhaseContainer != null) 
                                        ? discardPhaseContainer : handSlotContainer;
            
            if (discardPhaseContainer != null)
            {
                discardPhaseContainer.gameObject.SetActive(activeContainer == discardPhaseContainer);
            }
            if (handSlotContainer != null)
            {
                handSlotContainer.gameObject.SetActive(activeContainer == handSlotContainer);
            }

            if (!isBoardActivePhase)
            {
                if (layoutGroup != null) layoutGroup.enabled = true;
                
                foreach (var st in handSlots)
                {
                    if (st.parent != activeContainer) st.SetParent(activeContainer, false);
                }
                
                handSlots.Sort((a, b) => 
                {
                    var interactionA = a.GetComponent<TileInteraction>();
                    var interactionB = b.GetComponent<TileInteraction>();
                    if (interactionA == null || interactionB == null) return 0;
                    
                    int baseA = interactionA.TileId & 0x1F;
                    int baseB = interactionB.TileId & 0x1F;
                    if (baseA != baseB) return baseA.CompareTo(baseB);
                    
                    return interactionA.TileId.CompareTo(interactionB.TileId);
                });

                for (int i = 0; i < handSlots.Count; i++)
                {
                    handSlots[i].SetSiblingIndex(i);
                }
                return;
            }

            // Discard Phase Layout
            if (layoutGroup != null) layoutGroup.enabled = false;
            
            foreach (var st in handSlots)
            {
                if (st.parent != activeContainer) st.SetParent(activeContainer, false);
            }

            handSlots.Sort((a, b) => 
            {
                var intA = a.GetComponent<TileInteraction>();
                var intB = b.GetComponent<TileInteraction>();
                if (intA == null || intB == null) return 0;

                int baseA = intA.TileId & 0x1F;
                int baseB = intB.TileId & 0x1F;
                if (baseA != baseB) return baseA.CompareTo(baseB);
                
                return intA.TileId.CompareTo(intB.TileId);
            });

            if (handSlots.Count == 0) return;
            
            // 位置を先に計算してから、行ごとにSiblingIndexを設定する
            // 2段目（row 1）が1段目（row 0）より前面に来るように、
            // 1段目を先に配置し、2段目を後に配置する
            List<RectTransform> row0 = new List<RectTransform>();
            List<RectTransform> row1 = new List<RectTransform>();

            for (int i = 0; i < handSlots.Count; i++)
            {
                int rowIndex = i / 7; 
                int colIndex = i % 7; 

                float w = tileSpacingX;
                float h = tileSpacingY;

                float targetY = -rowIndex * h;
                float targetX = colIndex * w;

                RectTransform rt = handSlots[i].GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0, 1);
                    rt.anchorMax = new Vector2(0, 1);
                    rt.pivot = new Vector2(0, 1);
                    rt.anchoredPosition = new Vector2(targetX, targetY);
                }

                if (rowIndex == 0) row0.Add(handSlots[i]);
                else row1.Add(handSlots[i]);
            }

            // 1段目を先にSiblingIndex設定（奥側）
            int sibIdx = 0;
            for (int i = 0; i < row0.Count; i++)
            {
                row0[i].SetSiblingIndex(sibIdx++);
            }
            // 2段目を後にSiblingIndex設定（手前側＝前面）
            for (int i = 0; i < row1.Count; i++)
            {
                row1[i].SetSiblingIndex(sibIdx++);
            }
        }
    }
}
