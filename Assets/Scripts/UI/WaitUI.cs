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

        [Header("Layout")]
        [Tooltip("枠（親パネル）の内側に確保する左右の余白")]
        [SerializeField] private float framePadding = 4f;

        /// <summary>牌をどこまで重ねてよいか（牌幅に対する割合）。これ以上詰められない場合は縮小に切り替える。</summary>
        private const float MaxOverlapRatio = 0.5f;

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
            yield return null; // レイアウト更新を1フレーム待つ（WebGL互換）

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
            ClearRenderedTiles();

            int startIndex = pageIndex * MaxTilesPerPage;
            int count = Mathf.Min(MaxTilesPerPage, currentWaitTileIds.Count - startIndex);
            if (count <= 0) return;

            // 牌の実寸はプレハブから取る。決め打ちにすると枠のサイズ計算がずれて牌がはみ出す。
            Vector2 tileSize = GetTileSize();
            float tileWidth = tileSize.x;

            float spacing = 5f;
            if (count == 2) spacing = -3f;
            else if (count >= 3) spacing = -8f;

            // 牌はドット絵なので等倍を保ちたい。
            // 枠に収まらないときは、まず重なりを詰め、それでも無理なときだけ縮小する。
            float scale = 1.0f;
            float available = GetAvailableWidth();

            if (available > 0f)
            {
                float contentWidth = (count * tileWidth) + ((count - 1) * spacing);

                if (contentWidth > available && count > 1)
                {
                    float tightened = (available - (count * tileWidth)) / (count - 1);
                    spacing = Mathf.Max(tightened, -tileWidth * MaxOverlapRatio);
                    contentWidth = (count * tileWidth) + ((count - 1) * spacing);
                }

                if (contentWidth > available)
                {
                    scale = available / contentWidth;
                }
            }

            for (int i = 0; i < count; i++)
            {
                int id = currentWaitTileIds[startIndex + i];
                if (tilePrefab == null || waitContainer == null) return;

                GameObject obj = Instantiate(tilePrefab, waitContainer);
                activeWaitTiles.Add(obj);
                
                RectTransform rt = obj.GetComponent<RectTransform>();
                if (rt != null)
                {
                    // 並べるのは下の HorizontalLayoutGroup。ここでは大きさだけ決める。
                    // childControlWidth = false なので、レイアウトは sizeDelta をそのまま使う。
                    rt.localScale = Vector3.one;
                    rt.sizeDelta = new Vector2(tileSize.x * scale, tileSize.y * scale);
                }

                TileVisual visual = obj.GetComponent<TileVisual>();
                if (visual != null && tileResourceManager != null)
                {
                    // 待ち牌は枠に収めるため隙間を詰めて（時には重ねて）並べるので、
                    // 影が隣の牌の上に落ちて牌そのものが汚れて見える。ここだけ影を切る。
                    visual.SetShadowEnabled(false);
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
            hlg.spacing = spacing * scale;

            var csf = waitContainer.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (csf != null) Destroy(csf);

            // コンテナは中身とぴったり同じ幅にする。
            // ピボットが中央なので、これだけで枠の中で中央揃えになる。
            float finalTotalWidth = (count * tileSize.x * scale) + ((count - 1) * spacing * scale);
            RectTransform containerRt = waitContainer.GetComponent<RectTransform>();
            if (containerRt != null)
            {
                containerRt.sizeDelta = new Vector2(finalTotalWidth, containerRt.sizeDelta.y);

                // 枠の中に置いている間は、常に枠の中央へ。
                // ピボットが中央なので、これで中身も枠の中央に揃う。
                // （ダイアログ中央表示のために親を移し替えているときは触らない）
                bool movedToDialog = isOriginalSaved && containerRt.parent != originalParent;
                if (!movedToDialog)
                {
                    containerRt.anchoredPosition = new Vector2(0f, containerRt.anchoredPosition.y);
                }
            }
        }

        /// <summary>牌1枚の実寸。決め打ちにするとプレハブ差し替えで枠の計算が壊れる。</summary>
        private Vector2 GetTileSize()
        {
            if (tilePrefab != null)
            {
                RectTransform prefabRect = tilePrefab.GetComponent<RectTransform>();
                if (prefabRect != null && prefabRect.sizeDelta.x > 0f) return prefabRect.sizeDelta;
            }
            return new Vector2(45f, 40f);
        }

        /// <summary>枠（親パネル）の内寸。牌はこの幅に収める。</summary>
        private float GetAvailableWidth()
        {
            if (waitContainer == null) return 0f;

            RectTransform frame = waitContainer.parent as RectTransform;
            if (frame == null) return 0f;

            return Mathf.Max(0f, frame.rect.width - (framePadding * 2f));
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
            ClearRenderedTiles();
        }

        /// <summary>
        /// 描画済みの牌を片付ける。
        ///
        /// Destroy はフレーム末まで遅延するので、同じフレームで作り直すと
        /// 古い牌がレイアウトに残ったまま並べられ、枠からはみ出す。
        /// 先に親から外して、レイアウトの計算対象から即座に抜く。
        /// </summary>
        private void ClearRenderedTiles()
        {
            foreach (var t in activeWaitTiles)
            {
                if (t == null) continue;
                t.transform.SetParent(null, false);
                Destroy(t);
            }
            activeWaitTiles.Clear();

            // 追跡漏れがあっても枠に残さない
            if (waitContainer == null) return;

            for (int i = waitContainer.childCount - 1; i >= 0; i--)
            {
                GameObject child = waitContainer.GetChild(i).gameObject;
                child.transform.SetParent(null, false);
                Destroy(child);
            }
        }
    }
}
