using UnityEngine;
using System.Collections.Generic;

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

            if (gameUIManager != null)
            {
                UpdateLayout(gameUIManager.CurrentPhaseStatus);
            }
            else
            {
                UpdateLayout(""); // Fallback
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

        public virtual void UpdateLayout(string phaseStatus)
        {
            if (handSlotContainer == null) return;
            var layoutGroup = handSlotContainer.GetComponent<UnityEngine.UI.LayoutGroup>();
            Transform activeContainer = (phaseStatus == "discard" && discardPhaseContainer != null) 
                                        ? discardPhaseContainer : handSlotContainer;
            
            if (discardPhaseContainer != null)
            {
                discardPhaseContainer.gameObject.SetActive(activeContainer == discardPhaseContainer);
            }
            if (handSlotContainer != null)
            {
                handSlotContainer.gameObject.SetActive(activeContainer == handSlotContainer);
            }

            if (phaseStatus != "discard")
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

                TileData dataA = new TileData(intA.TileId);
                TileData dataB = new TileData(intB.TileId);

                if (dataA.Category != dataB.Category)
                {
                    return dataA.Category.CompareTo(dataB.Category);
                }
                
                if (dataA.Id != dataB.Id)
                {
                    return dataA.Id.CompareTo(dataB.Id);
                }
                
                return 0;
            });

            for (int i = 0; i < handSlots.Count; i++)
            {
                handSlots[i].SetSiblingIndex(i);
            }

            if (handSlots.Count == 0) return;
            
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
            }
        }
    }
}
