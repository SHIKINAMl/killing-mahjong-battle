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

        private List<int> _lastWallIds = new List<int>();
        private List<int> _lastHandIds = new List<int>();

        public void RebuildAllTilesFromState(List<int> suppressRevealWallIndexes)
        {
            if (uiManager.IsTransitioning)
            {
                Debug.Log("[GameUIVisualController] IsTransitioning is true. Skipping RebuildAllTilesFromState to prevent visible tile movement.");
                return;
            }

            var board = BoardStateManager.Instance;
            List<int> currentWallIds = new List<int>(board.CurrentWallTiles);
            List<int> currentHandIds = new List<int>(board.CurrentHandTiles);

            bool needFullRebuild = true;

            if (_lastWallIds.Count == currentWallIds.Count && _lastHandIds.Count == currentHandIds.Count)
            {
                List<int> oldWallIds = new List<int>(_lastWallIds);
                List<int> newWallIds = new List<int>(currentWallIds);
                foreach (int id in _lastWallIds)
                {
                    if (newWallIds.Contains(id)) { newWallIds.Remove(id); oldWallIds.Remove(id); }
                }

                List<int> oldHandIds = new List<int>(_lastHandIds);
                List<int> newHandIds = new List<int>(currentHandIds);
                foreach (int id in _lastHandIds)
                {
                    if (newHandIds.Contains(id)) { newHandIds.Remove(id); oldHandIds.Remove(id); }
                }

                if (oldWallIds.Count == newWallIds.Count && oldHandIds.Count == newHandIds.Count)
                {
                    bool success = true;
                    for (int i = 0; i < oldWallIds.Count; i++) success &= UpdateTileIdInUI(oldWallIds[i], newWallIds[i]);
                    for (int i = 0; i < oldHandIds.Count; i++) success &= UpdateTileIdInUI(oldHandIds[i], newHandIds[i]);

                    if (success)
                    {
                        needFullRebuild = false;
                    }
                }
            }

            _lastWallIds = currentWallIds;
            _lastHandIds = currentHandIds;

            _lastSuppressedIndices.Clear();
            if (uiManager.TilePrefab == null) return;
            
            bool isGameEndPhase = uiManager.CurrentPhaseStatus == RoundStatus.Agari || 
                                  uiManager.CurrentPhaseStatus == RoundStatus.Ron || 
                                  uiManager.CurrentPhaseStatus == RoundStatus.Result || 
                                  uiManager.CurrentPhaseStatus == RoundStatus.Draw;
            if (isGameEndPhase) return;

            if (needFullRebuild)
            {
                // 1. HandUI / WallUI を一括クリア
                if (uiManager.HandUI != null)
                {
                    for (int i = uiManager.HandUI.GetHandSlots().Count - 1; i >= 0; i--)
                    {
                        Transform t = uiManager.HandUI.GetHandSlots()[i];
                        if (t != null) {
                            t.SetParent(null);
                            Destroy(t.gameObject);
                        }
                    }
                    uiManager.HandUI.GetHandSlots().Clear();
                }
                if (uiManager.WallUI != null)
                {
                    for (int i = uiManager.WallUI.GetWallSlots().Count - 1; i >= 0; i--)
                    {
                        Transform t = uiManager.WallUI.GetWallSlots()[i];
                        if (t != null) {
                            t.SetParent(null);
                            Destroy(t.gameObject);
                        }
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
                    List<int> localExposedActualIds = new List<int>();
                    foreach (int exposedIdx in board.ExposedLocalHandWallIndexes)
                    {
                        if (exposedIdx >= 0 && exposedIdx < board.OriginalWallTiles.Count)
                        {
                            localExposedActualIds.Add(board.OriginalWallTiles[exposedIdx]);
                        }
                    }

                    foreach (var id in board.CurrentHandTiles)
                    {
                        RectTransform rt = uiManager.WallUI.GrabTileById(id);
                        if (rt != null)
                        {
                            InitializeTileComponent(rt, id, true);
                            uiManager.HandUI.AddTileToHand(rt, id);
                            
                        }
                        else
                        {
                            Debug.LogWarning($"[Visual] Failed to grab tile by id: {id} for Local Hand!");
                        }
                    }
                }
                }
            } // End of needFullRebuild

            // 毎回の更新で、透視状態だけはフルリビルドに関わらず必ず同期する
            SyncLocalExposedState(board);

            // 3. Enemy HandUI
            if (uiManager.EnemyHandUI != null)
            {
                uiManager.EnemyHandUI.ClearHand();

                // 透視された牌の encodedId を集計する
                List<int> exposedEncodedIds = new List<int>();
                foreach (int exposedIdx in board.ExposedEnemyHandWallIndexes)
                {
                    if (exposedIdx >= 0 && exposedIdx < board.OriginalEnemyWallTiles.Count)
                    {
                        exposedEncodedIds.Add(board.OriginalEnemyWallTiles[exposedIdx]);
                    }
                }

                for (int i = 0; i < board.CurrentEnemyHandTiles.Count; i++)
                {
                    int currentTileVal = board.CurrentEnemyHandTiles[i]; // encodedId
                    GameObject obj = Instantiate(uiManager.TilePrefab, transform);
                    RectTransform rt = obj.GetComponent<RectTransform>() ?? obj.transform as RectTransform;
                    if (rt != null) {
                        int visualId = -1; // Force hidden (-1)
                        int actualTileId = currentTileVal; // 実際の牌ID

                        InitializeTileComponent(rt, visualId, false);
                        uiManager.EnemyHandUI.AddEnemyTile(rt, visualId, actualTileId);

                        // もしこの牌の encodedId が透視済みリストにあれば表にする
                        if (exposedEncodedIds.Contains(actualTileId))
                        {
                            exposedEncodedIds.Remove(actualTileId); // 一度表にしたらリストから消す（重複防止）
                            uiManager.EnemyHandUI.RevealTileByIndex(i);
                            // 目のアイコンは出さない
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
                    if (t != null) {
                        t.SetParent(null);
                        Destroy(t.gameObject);
                    }
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
                            bool isExposedInWall = board.ExposedEnemyHandWallIndexes.Contains(i);
                            if (isExposedInWall)
                            {
                                visualId = actualTileId; // Reveal exposed tiles even in the wall
                            }
                            InitializeTileComponent(rt, visualId, false);
                            
                            // --- 相手の牌（Wall扱い）は操作・ホバー不要なので完全に無効化する ---
                            var interactions = rt.GetComponentsInChildren<TileInteraction>(true);
                            foreach(var interaction in interactions) DestroyImmediate(interaction);
                            var visual = rt.GetComponent<TileVisual>();
                            if (visual != null) visual.SetHoverHighlight(false);
                            var images = rt.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                            foreach(var img in images) img.raycastTarget = false;
                            // --------------------------------------------------------------------

                            // 敵の壁牌には目のアイコンを表示しない要望のため SetExposed(isExposedInWall) は行わない
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

        private void SyncLocalExposedState(Managers.BoardStateManager board)
        {
            List<int> localExposedActualIds = new List<int>();
            foreach (int exposedIdx in board.ExposedLocalHandWallIndexes)
            {
                if (exposedIdx >= 0 && exposedIdx < board.OriginalWallTiles.Count)
                {
                    localExposedActualIds.Add(board.OriginalWallTiles[exposedIdx]);
                }
            }

            if (uiManager.HandUI != null)
            {
                foreach (var rt in uiManager.HandUI.GetHandSlots())
                {
                    if (rt == null) continue;
                    var interaction = rt.GetComponent<TileInteraction>();
                    var visual = rt.GetComponent<TileVisual>();
                    if (interaction != null && visual != null)
                    {
                        bool isExposed = localExposedActualIds.Contains(interaction.TileId);
                        if (isExposed) localExposedActualIds.Remove(interaction.TileId);
                        visual.SetExposed(isExposed);
                    }
                }
            }

            if (uiManager.WallUI != null)
            {
                foreach (var rt in uiManager.WallUI.GetWallSlots())
                {
                    if (rt == null) continue;
                    var interaction = rt.GetComponent<TileInteraction>();
                    var visual = rt.GetComponent<TileVisual>();
                    if (interaction != null && visual != null)
                    {
                        bool isExposed = localExposedActualIds.Contains(interaction.TileId);
                        if (isExposed) localExposedActualIds.Remove(interaction.TileId);
                        visual.SetExposed(isExposed);
                    }
                }
            }
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
            if (uiManager.IsTransitioning) return;

            if (uiManager.WallUI != null && uiManager.HandUI != null)
            {
                RectTransform movedTile = uiManager.WallUI.GrabTile(tileId);
                if (movedTile != null)
                {
                    uiManager.HandUI.AddTileToHand(movedTile, tileId);
                }
            }
        }

        private void HandleTileMovedToWall(int tileId)
        {
            if (uiManager.IsTransitioning) return;

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
                uiManager.HandUI.RemoveTileFromHand(movedTile, tileId);
                uiManager.WallUI.ReturnTileToWall(movedTile, tileId);
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

            // 確実なアニメーションのために、綺麗なプレハブから生成
            GameObject ghost = Instantiate(uiManager.TilePrefab, parentCanvas.transform);
            
            // アンカーとピボットを中央(0.5)にリセット。
            RectTransform ghostRT = ghost.GetComponent<RectTransform>();
            RectTransform realRT = realTile.GetComponent<RectTransform>();
            if (ghostRT != null && realRT != null)
            {
                ghostRT.anchorMin = new Vector2(0.5f, 0.5f);
                ghostRT.anchorMax = new Vector2(0.5f, 0.5f);
                ghostRT.pivot = new Vector2(0.5f, 0.5f);
                
                // 到達後の本来のサイズが 0 でない(Layout計算済み)なら大きさを合わせる
                if (realRT.rect.width > 0 && realRT.rect.height > 0)
                {
                    ghostRT.sizeDelta = new Vector2(realRT.rect.width, realRT.rect.height);
                }
                
                // 親キャンバスと手牌での「画面上の絶対的なスケール」の差を計算し、
                // 移動中の牌(ghost)の見た目の大きさが手牌と完全に同じになるように調整する
                Vector3 parentLossy = parentCanvas.transform.lossyScale;
                Vector3 realLossy = realRT.lossyScale;
                
                if (parentLossy.x != 0 && parentLossy.y != 0 && parentLossy.z != 0)
                {
                    ghostRT.localScale = new Vector3(
                        realLossy.x / parentLossy.x,
                        realLossy.y / parentLossy.y,
                        realLossy.z / parentLossy.z
                    );
                }
                else
                {
                    ghostRT.localScale = Vector3.one;
                }
            }

            // 見た目(スプライト)をコピー
            var realVisual = realTile.GetComponent<TileVisual>();
            var ghostVisual = ghost.GetComponent<TileVisual>();
            if (realVisual != null && ghostVisual != null)
            {
                int tileId = realVisual.GetId();
                Sprite sprite = uiManager.TileResourceManager != null ? uiManager.TileResourceManager.GetTileSprite(tileId) : null;
                ghostVisual.SetTile(tileId, sprite, uiManager.TileResourceManager);
            }
            
            // インタラクションを削除
            Destroy(ghost.GetComponent<TileInteraction>());
            Destroy(ghost.GetComponent<UnityEngine.EventSystems.EventTrigger>());

            var ghostCanvasGroup = ghost.GetComponent<CanvasGroup>();
            if (ghostCanvasGroup == null) ghostCanvasGroup = ghost.AddComponent<CanvasGroup>();
            ghostCanvasGroup.alpha = 1f;
            ghostCanvasGroup.blocksRaycasts = false; // クリック妨害防止

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
            if (uiManager.EnemyWallUI == null) yield break;

            RebuildAllTilesFromState(newlyExposed);

            yield return new WaitForSeconds(0.5f);

            List<UnityEngine.UI.Image> glowingImages = new List<UnityEngine.UI.Image>();

            foreach (int index in newlyExposed)
            {
                if (index >= 0 && index < uiManager.EnemyWallUI.GetEnemyWallSlots().Count)
                {
                    Transform slot = uiManager.EnemyWallUI.GetEnemyWallSlots()[index];
                    if (slot != null && slot.childCount > 0)
                    {
                        RectTransform rt = slot.GetChild(0) as RectTransform;
                        if (rt != null)
                        {
                            int actualTileId = Managers.BoardStateManager.Instance.OriginalEnemyWallTiles[index];
                            InitializeTileComponent(rt, actualTileId, true);

                            UnityEngine.UI.Image img = rt.GetComponent<UnityEngine.UI.Image>();
                            if (img != null) glowingImages.Add(img);

                            Vector3 origScale = rt.localScale;
                            float duration = 0.25f;
                            for (float t = 0; t < duration; t += Time.deltaTime)
                            {
                                float s = Mathf.Lerp(1f, 1.3f, Mathf.PingPong(t * (1f / (duration / 2f)), 1f));
                                rt.localScale = origScale * s;
                                yield return null;
                            }
                            rt.localScale = origScale;
                        }
                    }
                }
            }

            // カットイン演出(2.0s) + HP減少(1.0s) = 計3.0s 待つ
            float waitedSoFar = 0.5f + (0.25f * newlyExposed.Count);
            float timeToWait = 3.0f - waitedSoFar;
            if (timeToWait > 0)
            {
                yield return new WaitForSeconds(timeToWait);
            }

            // 演出終了後、2秒間めくられた牌を光らせてアピール
            float glowDuration = 2.0f;
            Color originalColor = Color.white;
            Color glowColor = new Color(1.0f, 0.8f, 0.2f); // 黄色っぽく発光
            
            for (float t = 0; t < glowDuration; t += Time.deltaTime)
            {
                float pingPong = Mathf.PingPong(t * 3f, 1f); 
                Color currentColor = Color.Lerp(originalColor, glowColor, pingPong);
                foreach (var img in glowingImages)
                {
                    if (img != null) img.color = currentColor;
                }
                yield return null;
            }

            // 元の色に戻す
            foreach (var img in glowingImages)
            {
                if (img != null) img.color = originalColor;
            }

            if (uiManager.PhaseController != null)
            {
                uiManager.PhaseController.HandlePhaseVisibility(uiManager.CurrentPhaseStatus);
            }
        }

        private bool UpdateTileIdInUI(int oldId, int newId)
        {
            bool found = false;
            if (uiManager.WallUI != null)
            {
                foreach (var slot in uiManager.WallUI.GetWallSlots())
                {
                    if (slot == null) continue;
                    var interaction = slot.GetComponent<TileInteraction>();
                    if (interaction != null && interaction.TileId == oldId)
                    {
                        interaction.TileId = newId;
                        var visual = slot.GetComponent<TileVisual>();
                        if (visual != null && uiManager.TileResourceManager != null)
                        {
                            visual.SetTile(newId, uiManager.TileResourceManager.GetTileSprite(newId), uiManager.TileResourceManager);
                        }
                        found = true;
                        break;
                    }
                }
            }
            if (!found && uiManager.HandUI != null)
            {
                foreach (var slot in uiManager.HandUI.GetHandSlots())
                {
                    if (slot == null) continue;
                    var interaction = slot.GetComponent<TileInteraction>();
                    if (interaction != null && interaction.TileId == oldId)
                    {
                        interaction.TileId = newId;
                        var visual = slot.GetComponent<TileVisual>();
                        if (visual != null && uiManager.TileResourceManager != null)
                        {
                            visual.SetTile(newId, uiManager.TileResourceManager.GetTileSprite(newId), uiManager.TileResourceManager);
                        }
                        found = true;
                        break;
                    }
                }
            }
            return found;
        }
    }
}
