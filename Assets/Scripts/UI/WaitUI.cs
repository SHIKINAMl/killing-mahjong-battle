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
        
        [Header("Dialog Position Settings")]
        [SerializeField] private Vector2 dialogCenterPosition = new Vector2(0, 50);

        private List<GameObject> activeWaitTiles = new List<GameObject>();

        private Vector2 originalPosition;
        private Vector2 originalPivot;
        private Vector2 originalAnchorMin;
        private Vector2 originalAnchorMax;
        private Vector3 originalWorldPosition;
        private bool isOriginalSaved = false;

        private void SaveOriginalRect()
        {
            if (isOriginalSaved || waitContainer == null) return;
            originalPosition = waitContainer.anchoredPosition;
            originalPivot = waitContainer.pivot;
            originalAnchorMin = waitContainer.anchorMin;
            originalAnchorMax = waitContainer.anchorMax;
            originalWorldPosition = waitContainer.position;
            isOriginalSaved = true;
        }

        public void MoveToCenter()
        {
            SaveOriginalRect();
            if (waitContainer != null)
            {
                Canvas canvas = waitContainer.GetComponent<Canvas>();
                if (canvas == null) canvas = waitContainer.gameObject.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 10000;

                UnityEngine.UI.GraphicRaycaster raycaster = waitContainer.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                if (raycaster == null) raycaster = waitContainer.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

                waitContainer.anchorMin = new Vector2(0.5f, 0.5f);
                waitContainer.anchorMax = new Vector2(0.5f, 0.5f);
                waitContainer.pivot = new Vector2(0.5f, 0.5f);
                waitContainer.anchoredPosition = dialogCenterPosition;
            }
        }

        public void MoveToOriginalPosition()
        {
            if (!isOriginalSaved || waitContainer == null) return;
            
            UnityEngine.UI.GraphicRaycaster raycaster = waitContainer.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster != null)
            {
                Destroy(raycaster);
            }

            Canvas canvas = waitContainer.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = false;
                Destroy(canvas);
            }

            waitContainer.anchorMin = originalAnchorMin;
            waitContainer.anchorMax = originalAnchorMax;
            waitContainer.pivot = originalPivot;
            waitContainer.anchoredPosition = originalPosition;
            // LayoutGroupの影響でanchoredPositionが効かない場合に備えてpositionも更新
            waitContainer.position = originalWorldPosition; 
        }

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
