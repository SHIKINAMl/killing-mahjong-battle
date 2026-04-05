using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    public class EnemyWallUI : MonoBehaviour
    {
        [Header("Enemy Wall Configuration")]
        [SerializeField] private Transform enemyWallContainer;
        
        [Header("Layout Settings")]
        [SerializeField] private Vector2 startPosition = new Vector2(-40, -150); // Optional positioning tweak
        [SerializeField] private float tileIntervalX = 55f;
        [SerializeField] private float rowIntervalY = 95f;
        [SerializeField] private int maxSlotsPerRow = 20;

        private GameUIManager gameUIManager;

        public void Setup(GameUIManager manager)
        {
            this.gameUIManager = manager;
        }

        private List<RectTransform> enemyWallSlots = new List<RectTransform>();
        public List<RectTransform> GetEnemyWallSlots() => enemyWallSlots;

        public void LayoutEnemyWallTiles(List<RectTransform> generatedTiles, List<int> tileIds, bool isDiscardPhase)
        {
            enemyWallSlots.Clear();

            // Simple layout for enemy wall tiles
            int currentSlot = 0;
            int tileIndex = 0;

            foreach(var id in tileIds)
            {
                if (tileIndex >= generatedTiles.Count) break;

                RectTransform slot = generatedTiles[tileIndex++];
                slot.SetParent(enemyWallContainer, false);
                
                slot.localScale = Vector3.one;
                slot.anchorMin = new Vector2(0.5f, 0.5f);
                slot.anchorMax = new Vector2(0.5f, 0.5f);
                slot.pivot = new Vector2(0.5f, 0.5f);
                
                int r = currentSlot / maxSlotsPerRow;
                int c = currentSlot % maxSlotsPerRow;
                
                float targetX = startPosition.x - c * tileIntervalX; // going left for enemy
                float targetY = startPosition.y + r * rowIntervalY; // going up or down depending on visual preference
                
                Vector3 finalPos = new Vector3(targetX, targetY, 0);
                slot.localPosition = finalPos;
                
                // Keep it facedown
                slot.localRotation = Quaternion.identity;

                enemyWallSlots.Add(slot);
                slot.gameObject.SetActive(true);

                currentSlot++;
            }
        }

        public RectTransform GrabEnemyTile()
        {
            // Just grab the last one (a random one)
            if (enemyWallSlots.Count > 0)
            {
                RectTransform t = enemyWallSlots[enemyWallSlots.Count - 1];
                enemyWallSlots.RemoveAt(enemyWallSlots.Count - 1);
                return t;
            }
            return null;
        }
    }
}
