using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.UI
{
    public class WaitUI : MonoBehaviour
    {
        [Header("Wait UI Settings")]
        [SerializeField] private RectTransform waitContainer;
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private TileResourceManager tileResourceManager;

        private List<GameObject> activeWaitTiles = new List<GameObject>();

        public void DisplayWaits(List<int> waitTileIds)
        {
            ClearWaits();

            if (waitTileIds == null || waitTileIds.Count == 0)
            {
                // まだテンパイしていない、または待ちがない
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            foreach (int id in waitTileIds)
            {
                if (tilePrefab == null || waitContainer == null) return;

                GameObject obj = Instantiate(tilePrefab, waitContainer);
                activeWaitTiles.Add(obj);

                TileVisual visual = obj.GetComponent<TileVisual>();
                if (visual != null && tileResourceManager != null)
                {
                    visual.SetTile(id, tileResourceManager.GetTileSprite(id));
                    
                    if (KillingMahjong.Managers.BoardStateManager.Instance.NonManganWaitTiles.Contains(id))
                    {
                        visual.SetAlpha(0.3f); // 透明度をさらに薄くして強調
                    }
                    else 
                    {
                        visual.SetAlpha(1.0f);
                    }
                }

                // 待ち牌表示用なので、クリック判定などはオフにする
                var interaction = obj.GetComponent<TileInteraction>();
                if (interaction != null)
                {
                    Destroy(interaction); // クリック不要
                }
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            ClearWaits();
        }

        public void ClearWaits()
        {
            foreach (var t in activeWaitTiles)
            {
                if (t != null) Destroy(t);
            }
            activeWaitTiles.Clear();
        }
    }
}
