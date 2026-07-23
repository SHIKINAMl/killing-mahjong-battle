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

        private System.Collections.Generic.List<GameObject> activeWaitTiles = new System.Collections.Generic.List<GameObject>();
        
        private System.Collections.Generic.List<int> currentWaitTileIds = new System.Collections.Generic.List<int>();
        private int currentPage = 0;
        private Coroutine paginationCoroutine = null;
        private const int MaxTilesPerPage = 3;

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
            currentWaitTileIds = waitTileIds;
            currentPage = 0;

            if (currentWaitTileIds.Count > MaxTilesPerPage)
            {
                paginationCoroutine = StartCoroutine(PaginationRoutine());
            }
            else
            {
                CanvasGroup cg = waitContainer.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
                RenderPage(0);
            }
        }

        private void RenderPage(int pageIndex)
        {
            // まず既存のオブジェクトを破棄
            foreach (var t in activeWaitTiles)
            {
                if (t != null) Destroy(t);
            }
            activeWaitTiles.Clear();

            int startIndex = pageIndex * MaxTilesPerPage;
            int count = Mathf.Min(MaxTilesPerPage, currentWaitTileIds.Count - startIndex);
            if (count <= 0) return;

            float tileWidth = 18f;
            float spacing = 5f;
            float scale = 1.0f; // 常に1.0で綺麗に表示する

            for (int i = 0; i < count; i++)
            {
                int id = currentWaitTileIds[startIndex + i];
                if (tilePrefab == null || waitContainer == null) return;

                GameObject obj = Instantiate(tilePrefab, waitContainer);
                activeWaitTiles.Add(obj);
                
                RectTransform rt = obj.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    
                    float actualTileWidth = tileWidth * scale;
                    float actualSpacing = spacing * scale;
                    float actualTotalWidth = (count * actualTileWidth) + ((count - 1) * actualSpacing);
                    float actualStartX = -actualTotalWidth / 2f + actualTileWidth / 2f;
                    
                    rt.anchoredPosition = new Vector2(actualStartX + i * (actualTileWidth + actualSpacing), 0);
                    rt.localScale = new Vector3(scale, scale, 1f);
                }

                TileVisual visual = obj.GetComponent<TileVisual>();
                if (visual != null && tileResourceManager != null)
                {
                    visual.SetTile(id, tileResourceManager.GetTileSprite(id));
                    visual.SetExposed(false);
                    visual.SetFuritenHighlight(false);

                    if (KillingMahjong.Managers.BoardStateManager.Instance.NonManganWaitTiles.Contains(id))
                    {
                        visual.SetAlpha(0.3f);
                    }
                    else 
                    {
                        visual.SetAlpha(1.0f);
                    }
                }

                var interaction = obj.GetComponent<TileInteraction>();
                if (interaction != null) Destroy(interaction);
            }

            var hlg = waitContainer.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            if (hlg == null) hlg = waitContainer.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = spacing;

            var csf = waitContainer.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (csf != null) Destroy(csf);
            
            // 枠の幅を牌の数に合わせて自動調整
            float finalTotalWidth = (count * (tileWidth * scale)) + ((count - 1) * (spacing * scale));
            RectTransform containerRt = waitContainer.GetComponent<RectTransform>();
            if (containerRt != null)
            {
                // 左右に余裕（パディング）を持たせるために少し大きめに設定
                containerRt.sizeDelta = new Vector2(finalTotalWidth + 24f, containerRt.sizeDelta.y);
            }
        }

        private System.Collections.IEnumerator PaginationRoutine()
        {
            CanvasGroup cg = waitContainer.GetComponent<CanvasGroup>();
            if (cg == null) cg = waitContainer.gameObject.AddComponent<CanvasGroup>();

            int totalPages = Mathf.CeilToInt((float)currentWaitTileIds.Count / MaxTilesPerPage);

            while (true)
            {
                RenderPage(currentPage);
                cg.alpha = 1f;

                yield return new WaitForSeconds(1.6f);

                // フェードアウト
                for (float t = 0; t < 0.2f; t += Time.deltaTime)
                {
                    cg.alpha = Mathf.Lerp(1f, 0f, t / 0.2f);
                    yield return null;
                }
                cg.alpha = 0f;

                currentPage = (currentPage + 1) % totalPages;
                RenderPage(currentPage);

                // フェードイン
                for (float t = 0; t < 0.2f; t += Time.deltaTime)
                {
                    cg.alpha = Mathf.Lerp(0f, 1f, t / 0.2f);
                    yield return null;
                }
                cg.alpha = 1f;
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            ClearWaits();
        }

        public void ClearWaits()
        {
            if (paginationCoroutine != null)
            {
                StopCoroutine(paginationCoroutine);
                paginationCoroutine = null;
            }
            foreach (var t in activeWaitTiles)
            {
                if (t != null) Destroy(t);
            }
            activeWaitTiles.Clear();
        }
    }
}
