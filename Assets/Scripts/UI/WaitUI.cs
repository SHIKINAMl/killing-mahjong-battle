using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.Common;

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

        private void Awake()
        {
            if (waitContainer != null)
            {
                Canvas canvas = waitContainer.GetComponent<Canvas>();
                if (canvas == null)
                {
                    canvas = waitContainer.gameObject.AddComponent<Canvas>();
                }
                canvas.overrideSorting = false;

                UnityEngine.UI.GraphicRaycaster raycaster = waitContainer.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                if (raycaster == null)
                {
                    waitContainer.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                }
                
                // 自動レイアウトを使用せず、手動配置に切り替えるため、既存のLayoutGroupを無効化・削除
                var hlg = waitContainer.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                if (hlg != null)
                {
                    DestroyImmediate(hlg);
                }
            }
        }

        private Transform originalParent;
        private Vector3 originalLocalScale;

        private void SaveOriginalRect()
        {
            if (isOriginalSaved || waitContainer == null) return;
            originalParent = waitContainer.parent;
            originalLocalScale = waitContainer.localScale;
            originalPosition = waitContainer.anchoredPosition;
            originalPivot = waitContainer.pivot;
            originalAnchorMin = waitContainer.anchorMin;
            originalAnchorMax = waitContainer.anchorMax;
            originalWorldPosition = waitContainer.position;
            isOriginalSaved = true;
        }

        public void MoveToCenter()
        {
            gameObject.SetActive(true);
            SaveOriginalRect();
            if (waitContainer != null)
            {
                CanvasGroup cg = waitContainer.GetComponent<CanvasGroup>();
                if (cg == null) cg = waitContainer.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;

                StartCoroutine(MoveToCenterCoroutine(cg));
            }
        }

        private System.Collections.IEnumerator MoveToCenterCoroutine(CanvasGroup cg)
        {
            yield return new WaitForEndOfFrame();

            if (waitContainer != null)
            {
                // 親のスケールや位置の影響を断ち切るため、ルートのCanvasの直下に一時的に移動
                Canvas rootCanvas = GetComponentInParent<Canvas>();
                if (rootCanvas != null)
                {
                    waitContainer.SetParent(rootCanvas.rootCanvas.transform, true);
                }

                Canvas canvas = waitContainer.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = UISortingOrders.WaitDisplayFront;
                }

                UnityEngine.UI.LayoutElement layoutElement = waitContainer.GetComponent<UnityEngine.UI.LayoutElement>();
                if (layoutElement == null) layoutElement = waitContainer.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                layoutElement.ignoreLayout = true;

                waitContainer.localScale = Vector3.one; // 親の縮小スケールをリセット
                waitContainer.anchorMin = new Vector2(0.5f, 0.5f);
                waitContainer.anchorMax = new Vector2(0.5f, 0.5f);
                waitContainer.pivot = new Vector2(0.5f, 0.5f);
                // 画面中央から少し上の位置に強制配置
                waitContainer.anchoredPosition = new Vector2(0, 120f);
            }

            if (cg != null) cg.alpha = 1f;
        }

        public void MoveToOriginalPosition()
        {
            if (!isOriginalSaved || waitContainer == null) return;
            
            if (originalParent != null)
            {
                waitContainer.SetParent(originalParent, true);
            }

            Canvas canvas = waitContainer.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = false;
            }

            UnityEngine.UI.LayoutElement layoutElement = waitContainer.GetComponent<UnityEngine.UI.LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.ignoreLayout = false;
            }

            waitContainer.localScale = originalLocalScale;
            waitContainer.anchorMin = originalAnchorMin;
            waitContainer.anchorMax = originalAnchorMax;
            waitContainer.pivot = originalPivot;
            waitContainer.anchoredPosition = originalPosition;
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

            // 配置パラメータ
            // 手牌確認UIとは異なり、WaitUI側は親スケール等の影響でローカルの見た目幅が小さいため幅を狭める
            float tileWidth = 18f; // 左下・上部のWaitUIでのスケール後のおおよその幅
            float spacing = 1f;

            for (int i = 0; i < waitTileIds.Count; i++)
            {
                int id = waitTileIds[i];
                if (tilePrefab == null || waitContainer == null) return;

                GameObject obj = Instantiate(tilePrefab, waitContainer);
                activeWaitTiles.Add(obj);
                
                // --- 待ち牌が多い場合の重なり防止のためのスケール調整 ---
                float scale = waitTileIds.Count > 6 ? 0.6f : 1.0f;
                
                RectTransform rt = obj.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    
                    float actualTileWidth = tileWidth * scale; // スケールに応じた実際の幅
                    float actualSpacing = spacing * scale;
                    float actualTotalWidth = (waitTileIds.Count * actualTileWidth) + ((waitTileIds.Count - 1) * actualSpacing);
                    float actualStartX = -actualTotalWidth / 2f + actualTileWidth / 2f;
                    
                    rt.anchoredPosition = new Vector2(actualStartX + i * (actualTileWidth + actualSpacing), 0);
                    rt.localScale = new Vector3(scale, scale, 1f);
                }

                TileVisual visual = obj.GetComponent<TileVisual>();
                if (visual != null && tileResourceManager != null)
                {
                    visual.SetTile(id, tileResourceManager.GetTileSprite(id));
                    
                    // 待ち牌表示には透視マークやフリテンアラートは不要なので必ずオフにする
                    visual.SetExposed(false);
                    visual.SetFuritenHighlight(false);

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

            // レイアウトを強制的に更新して重なりを解消
            var hlg = waitContainer.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            if (hlg == null)
            {
                hlg = waitContainer.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            }
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = waitTileIds.Count > 5 ? 2f : 10f;

            // ContentSizeFitterがanchoredPositionを破壊するため無効化/削除
            var csf = waitContainer.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (csf != null)
            {
                Destroy(csf);
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
