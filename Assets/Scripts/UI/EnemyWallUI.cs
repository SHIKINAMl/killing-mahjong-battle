using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    public class EnemyWallUI : MonoBehaviour
    {
        [Header("Enemy Wall Configuration")]
        [SerializeField] private Transform enemyWallContainer;
        
        [Header("Layout Settings")]
        [SerializeField] private Vector2 startPosition = new Vector2(0, -150);
        [SerializeField] private float tileIntervalX = 35f; // Inspectorで調整可能
        [SerializeField] private float rowIntervalY = 60f; // Inspectorで調整可能
        [SerializeField] private int maxSlotsPerRow = 11; // 21 tiles max, so 11 + 10

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

            int currentSlot = 0;
            int tileIndex = 0;

            float blockWidth = (Mathf.Min(tileIds.Count, maxSlotsPerRow) - 1) * tileIntervalX;
            float startX = startPosition.x - (blockWidth / 2f);
            
            // LayoutGroup がどこにアタッチされていても強制的に無効化・削除して座標計算の衝突を防ぐ
            var layoutGroups = GetComponentsInChildren<UnityEngine.UI.LayoutGroup>(true);
            foreach (var lg in layoutGroups)
            {
                lg.enabled = false;
                Destroy(lg);
            }

            // startY を大幅に上に上げる（敵の壁牌らしく相手キャラの近くに配置）
            float actualStartY = 150f;

            foreach(var id in tileIds)
            {
                if (tileIndex >= generatedTiles.Count) break;

                RectTransform slot = generatedTiles[tileIndex++];
                slot.SetParent(enemyWallContainer, false);
                
                slot.localScale = new Vector3(0.7f, 0.7f, 0.7f); // Scale down to 70%
                slot.anchorMin = new Vector2(0.5f, 0.5f);
                slot.anchorMax = new Vector2(0.5f, 0.5f);
                slot.pivot = new Vector2(0.5f, 0.5f);
                
                int r = currentSlot / maxSlotsPerRow;
                int c = currentSlot % maxSlotsPerRow;
                
                float targetX = startX + c * tileIntervalX; // going right
                float targetY = actualStartY - r * rowIntervalY; // going down
                
                slot.anchoredPosition = new Vector2(targetX, targetY);
                
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
