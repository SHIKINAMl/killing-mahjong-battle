using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    [RequireComponent(typeof(GameUIManager))]
    public class GameUIVisualController : MonoBehaviour
    {
        private GameUIManager uiManager;
        private List<int> _lastSuppressedIndices = new List<int>();

        public void Setup(GameUIManager manager)
        {
            this.uiManager = manager;
            var board = BoardStateManager.Instance;
            board.OnBoardStateRebuilt += RebuildAllTilesFromState;
            board.OnSelectionChanged += UpdateSelectedTileVisuals;
            board.OnTileMovedToHand += HandleTileMovedToHand;
            board.OnTileMovedToWall += HandleTileMovedToWall;
        }

        private void OnDestroy()
        {
            if (BoardStateManager.Instance != null)
            {
                var board = BoardStateManager.Instance;
                board.OnBoardStateRebuilt -= RebuildAllTilesFromState;
                board.OnSelectionChanged -= UpdateSelectedTileVisuals;
                board.OnTileMovedToHand -= HandleTileMovedToHand;
                board.OnTileMovedToWall -= HandleTileMovedToWall;
            }
        }

        public void RebuildAllTilesFromState()
        {
            RebuildAllTilesFromState(null);
        }

        public void RebuildAllTilesFromState(List<int> suppressRevealWallIndexes)
        {
            _lastSuppressedIndices.Clear();
            if (uiManager.TilePrefab == null) return;
            
            bool isGameEndPhase = uiManager.CurrentPhaseStatus == RoundStatus.Agari || 
                                  uiManager.CurrentPhaseStatus == RoundStatus.Ron || 
                                  uiManager.CurrentPhaseStatus == RoundStatus.Result || 
                                  uiManager.CurrentPhaseStatus == RoundStatus.Draw;
            if (isGameEndPhase) return;

            var board = BoardStateManager.Instance;

            // 1. HandUI / WallUI を一括クリア
            if (uiManager.HandUI != null)
            {
                for (int i = uiManager.HandUI.GetHandSlots().Count - 1; i >= 0; i--)
                {
                    Transform t = uiManager.HandUI.GetHandSlots()[i];
                    if (t != null) Destroy(t.gameObject);
                }
                uiManager.HandUI.GetHandSlots().Clear();
            }
            if (uiManager.WallUI != null)
            {
                for (int i = uiManager.WallUI.GetWallSlots().Count - 1; i >= 0; i--)
                {
                    Transform t = uiManager.WallUI.GetWallSlots()[i];
                    if (t != null) Destroy(t.gameObject);
                }
                uiManager.WallUI.GetWallSlots().Clear();
            }

            if (uiManager.WallUI != null)
            {
                List<int> combinedIds = new List<int>(board.CurrentWallTiles);
                combinedIds.AddRange(board.CurrentHandTiles);

                List<RectTransform> combinedGenerated = new List<RectTransform>();
                foreach (var id in combinedIds)
                {
                    GameObject obj = Instantiate(uiManager.TilePrefab, transform);
                    RectTransform rt = obj.GetComponent<RectTransform>() ?? obj.transform as RectTransform;
                    if (rt != null)
                    {
                        InitializeTileComponent(rt, id, false);
                        combinedGenerated.Add(rt);
                    }
                }

                uiManager.WallUI.LayoutWallTiles(combinedGenerated, combinedIds, board.CurrentWaitTiles, uiManager.CurrentPhaseStatus == RoundStatus.Discard);

                if (uiManager.HandUI != null)
                {
                    foreach (var id in board.CurrentHandTiles)
                    {
                        RectTransform rt = uiManager.WallUI.GrabTileById(id);
                        if (rt != null)
                        {
                            InitializeTileComponent(rt, id, true);
                            uiManager.HandUI.AddTileToHand(rt, id);
                        }
                    }
                }
            }

            // 3. Enemy HandUI
            if (uiManager.EnemyHandUI != null)
            {
                uiManager.EnemyHandUI.ClearHand();

                bool isDummyHand = board.CurrentEnemyHandTiles.Count > 1 && 
                                   board.CurrentEnemyHandTiles.TrueForAll(x => x <= 0);

                List<int> exposedActualIds = new List<int>();
                List<int> exposedWallIndexes = new List<int>();
                if (isDummyHand)
                {
                    foreach (int exposedIdx in board.ExposedEnemyHandWallIndexes)
                    {
                        if (exposedIdx >= 0 && exposedIdx < board.OriginalEnemyWallTiles.Count)
                        {
                            exposedActualIds.Add(board.OriginalEnemyWallTiles[exposedIdx]);
                            exposedWallIndexes.Add(exposedIdx);
                        }
                    }
                }

                int exposedAssigned = 0;

                for (int i = 0; i < board.CurrentEnemyHandTiles.Count; i++)
                {
                    int currentTileVal = board.CurrentEnemyHandTiles[i];
                    GameObject obj = Instantiate(uiManager.TilePrefab, transform);
                    RectTransform rt = obj.GetComponent<RectTransform>() ?? obj.transform as RectTransform;
                    if (rt != null) {
                        int visualId = -1; // Force hidden (-1)
                        int actualTileId = -1;
                        int wallIdx = -1;

                        if (isDummyHand)
                        {
                            wallIdx = currentTileVal; // usually -1 or similar dummy value
                            if (wallIdx >= 0 && wallIdx < board.OriginalEnemyWallTiles.Count) {
                                actualTileId = board.OriginalEnemyWallTiles[wallIdx];
                            }
                        }
                        else
                        {
                            actualTileId = currentTileVal;
                            wallIdx = board.OriginalEnemyWallTiles.IndexOf(actualTileId);
                        }

                        bool revealThis = false;

                        if (isDummyHand)
                        {
                            if (exposedAssigned < exposedActualIds.Count)
                            {
                                actualTileId = exposedActualIds[exposedAssigned];
                                int assignedWallIdx = exposedWallIndexes[exposedAssigned];
                                revealThis = true;
                                if (suppressRevealWallIndexes != null && suppressRevealWallIndexes.Contains(assignedWallIdx))
                                {
                                    revealThis = false;
                                    _lastSuppressedIndices.Add(i);
                                }
                                exposedAssigned++;
                            }
                        }
                        else
                        {
                            if (board.ExposedEnemyHandWallIndexes.Contains(wallIdx))
                            {
                                revealThis = true;
                                if (suppressRevealWallIndexes != null && suppressRevealWallIndexes.Contains(wallIdx))
                                {
                                    revealThis = false;
                                    _lastSuppressedIndices.Add(i);
                                }
                            }
                        }

                        InitializeTileComponent(rt, visualId, false);
                        uiManager.EnemyHandUI.AddEnemyTile(rt, visualId, actualTileId);

                        if (revealThis)
                        {
                            uiManager.EnemyHandUI.RevealTileByIndex(i);
                        }
                    }
                }
            }

            // 4. Enemy WallUI
            if (uiManager.EnemyWallUI != null)
            {
                for (int i = uiManager.EnemyWallUI.GetEnemyWallSlots().Count - 1; i >= 0; i--)
                {
                    Transform t = uiManager.EnemyWallUI.GetEnemyWallSlots()[i];
                    if (t != null) Destroy(t.gameObject);
                }
                uiManager.EnemyWallUI.GetEnemyWallSlots().Clear();

                if (uiManager.CurrentPhaseStatus == RoundStatus.Discard && suppressRevealWallIndexes == null)
                {
                    uiManager.EnemyWallUI.gameObject.SetActive(false);
                }
                else
                {
                    uiManager.EnemyWallUI.gameObject.SetActive(true);
                    List<RectTransform> enemyWallGenerated = new List<RectTransform>();
                    List<int> displayIdsForWall = new List<int>();

                    for (int i = 0; i < board.OriginalEnemyWallTiles.Count; i++)
                    {
                        if (board.CurrentEnemyHandTiles != null && board.CurrentEnemyHandTiles.Contains(i))
                            continue;

                        int actualTileId = board.OriginalEnemyWallTiles[i];
                        displayIdsForWall.Add(actualTileId);

                        GameObject obj = Instantiate(uiManager.TilePrefab, transform);
                        RectTransform rt = obj.GetComponent<RectTransform>() ?? obj.transform as RectTransform;
                        if (rt != null) {
                            int visualId = -1; // Force hidden (-1)
                            if (board.ExposedEnemyHandWallIndexes.Contains(i))
                            {
                                visualId = actualTileId; // Reveal exposed tiles even in the wall
                            }
                            InitializeTileComponent(rt, visualId, false);
                            enemyWallGenerated.Add(rt);
                        }
                    }
                    uiManager.EnemyWallUI.LayoutEnemyWallTiles(enemyWallGenerated, displayIdsForWall, uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                }
            }

            if (uiManager.WaitUI != null && (uiManager.CurrentPhaseStatus == RoundStatus.Discard || uiManager.CurrentPhaseStatus == RoundStatus.HandSelection))
            {
                if (board.CurrentWaitTiles != null && board.CurrentWaitTiles.Count > 0)
                {
                    uiManager.WaitUI.gameObject.SetActive(true);
                    uiManager.WaitUI.DisplayWaits(board.CurrentWaitTiles);
                }
            }

            if (uiManager.HandUI != null && uiManager.HandUI.IsSubmitted && uiManager.HandSelectionController != null && uiManager.HandSelectionController.AutoConfirmNextHandSelection && uiManager.CurrentPhaseStatus == RoundStatus.HandSelection)
            {
                uiManager.PhaseController?.SetMatchUIVisibility(false);
            }
        }

        public void InitializeTileComponent(RectTransform rt, int id, bool inHand)
        {
            if (uiManager.TileResourceManager != null)
            {
                var visual = rt.GetComponent<TileVisual>();
                if (visual != null) visual.SetTile(id, uiManager.TileResourceManager.GetTileSprite(id));
            }

            var interaction = rt.GetComponent<TileInteraction>();
            if (interaction == null) interaction = rt.gameObject.AddComponent<TileInteraction>();
            
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            
            interaction.Initialize(id, inHand, uiManager, canvas);
        }

        private void UpdateSelectedTileVisuals()
        {
            var selectedIds = BoardStateManager.Instance.SelectedTileIds;
            if (uiManager.WallUI != null)
            {
                foreach (var t in uiManager.WallUI.GetWallSlots())
                {
                    var interaction = t.GetComponent<TileInteraction>();
                    if (interaction != null)
                    {
                        t.localPosition = interaction.OriginalWallPosition;
                    }
                }
            }
        }

        private void HandleTileMovedToHand(int tileId)
        {
            if (uiManager.WallUI != null && uiManager.HandUI != null)
            {
                RectTransform movedTile = uiManager.WallUI.GrabTile(tileId);
                if (movedTile != null)
                {
                    Vector3 startPos = movedTile.position;
                    uiManager.HandUI.AddTileToHand(movedTile, tileId);
                    
                    if (this.gameObject.activeInHierarchy)
                    {
                        StartCoroutine(AnimateTileMovementRoutine(movedTile, startPos, 0.15f));
                    }
                }
            }
        }

        private void HandleTileMovedToWall(int tileId)
        {
            if (uiManager.HandUI == null || uiManager.WallUI == null) return;

            RectTransform movedTile = null;
            foreach (RectTransform t in uiManager.HandUI.GetHandSlots())
            {
                var interaction = t.GetComponent<TileInteraction>();
                if (interaction != null && interaction.TileId == tileId)
                {
                    movedTile = t;
                    break;
                }
            }

            if (movedTile != null)
            {
                Vector3 startPos = movedTile.position;
                uiManager.HandUI.RemoveTileFromHand(movedTile, tileId);
                uiManager.WallUI.ReturnTileToWall(movedTile, tileId);

                if (this.gameObject.activeInHierarchy)
                {
                    StartCoroutine(AnimateTileMovementRoutine(movedTile, startPos, 0.15f));
                }
            }
        }

        private System.Collections.IEnumerator AnimateTileMovementRoutine(RectTransform realTile, Vector3 startWorldPos, float duration)
        {
            if (realTile == null) yield break;

            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null) parentCanvas = FindFirstObjectByType<Canvas>();

            if (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null && startWorldPos.z != 0)
                {
                    Vector3 screenPos = mainCam.WorldToScreenPoint(startWorldPos);
                    RectTransformUtility.ScreenPointToWorldPointInRectangle(
                        parentCanvas.transform as RectTransform, 
                        screenPos, 
                        null, 
                        out startWorldPos
                    );
                }
            }

            var canvasGroup = realTile.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = realTile.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            yield return new WaitForEndOfFrame();
            
            if (realTile == null) yield break;

            Vector3 targetWorldPos = realTile.position;
            Canvas targetCanvas = realTile.GetComponentInParent<Canvas>();

            if (targetCanvas == null || targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                Camera cam = Camera.main;
                if (targetCanvas != null && targetCanvas.worldCamera != null) cam = targetCanvas.worldCamera;

                if (cam != null)
                {
                    Vector3 screenPos = cam.WorldToScreenPoint(realTile.position);
                    RectTransformUtility.ScreenPointToWorldPointInRectangle(
                        parentCanvas.transform as RectTransform, 
                        screenPos, 
                        parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera, 
                        out targetWorldPos
                    );
                }
            }

            GameObject ghost = Instantiate(realTile.gameObject, parentCanvas.transform);
            
            Destroy(ghost.GetComponent<TileInteraction>());
            Destroy(ghost.GetComponent<UnityEngine.EventSystems.EventTrigger>());

            var ghostCanvasGroup = ghost.GetComponent<CanvasGroup>();
            if (ghostCanvasGroup != null) ghostCanvasGroup.alpha = 1f;

            ghost.transform.SetAsLastSibling();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - (1f - t) * (1f - t);
                ghost.transform.position = Vector3.Lerp(startWorldPos, targetWorldPos, t);
                yield return null;
            }

            if (realTile != null)
            {
                canvasGroup.alpha = 1f;
            }
            Destroy(ghost);
        }

        public System.Collections.IEnumerator PlayPerspectiveAnimation(List<int> newlyExposed)
        {
            if (uiManager.EnemyHandUI == null) yield break;

            uiManager.EnemyHandUI.gameObject.SetActive(true);
            RebuildAllTilesFromState(newlyExposed);

            RectTransform enemyHandRT = uiManager.EnemyHandUI.GetComponent<RectTransform>();
            if (enemyHandRT == null) yield break;

            int originalSiblingIndex = enemyHandRT.GetSiblingIndex();
            enemyHandRT.SetAsLastSibling();

            Vector3 originalScale = enemyHandRT.localScale;
            Vector3 originalPos = enemyHandRT.position;
            
            yield return new WaitForEndOfFrame();

            Vector3 centerPos = originalPos;
            Canvas canvas = enemyHandRT.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    centerPos = new Vector3(Screen.width * 0.5f, Screen.height * 0.45f, originalPos.z);
                }
                else if (canvas.worldCamera != null)
                {
                    centerPos = canvas.worldCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.45f, canvas.planeDistance));
                    centerPos.z = originalPos.z;
                }
                else if (Camera.main != null)
                {
                    centerPos = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.45f, 0.5f));
                    centerPos.z = originalPos.z;
                }
            }

            Vector3 targetScale = originalScale * 1.5f;
            float duration = 0.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                enemyHandRT.localScale = Vector3.Lerp(originalScale, targetScale, t);
                enemyHandRT.position = Vector3.Lerp(originalPos, centerPos, t);
                yield return null;
            }
            enemyHandRT.localScale = targetScale;
            enemyHandRT.position = centerPos;

            yield return new WaitForSeconds(0.2f);

            foreach (int index in _lastSuppressedIndices)
            {
                uiManager.EnemyHandUI.RevealTileByIndex(index);
                yield return new WaitForSeconds(0.8f);
            }

            yield return new WaitForSeconds(0.8f);

            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                enemyHandRT.localScale = Vector3.Lerp(targetScale, originalScale, t);
                enemyHandRT.position = Vector3.Lerp(centerPos, originalPos, t);
                yield return null;
            }
            enemyHandRT.localScale = originalScale;
            enemyHandRT.position = originalPos;
            
            enemyHandRT.SetSiblingIndex(originalSiblingIndex);

            if (uiManager.PhaseController != null)
            {
                uiManager.PhaseController.HandlePhaseVisibility(uiManager.CurrentPhaseStatus);
            }
        }
    }
}
