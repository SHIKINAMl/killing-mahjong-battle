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

        private Queue<GameObject> _tilePool = new Queue<GameObject>();
        private Transform _poolContainer;

        private void InitPool()
        {
            if (_poolContainer == null)
            {
                GameObject poolObj = new GameObject("TilePoolContainer");
                poolObj.transform.SetParent(this.transform, false);
                poolObj.SetActive(false);
                _poolContainer = poolObj.transform;
            }
        }

        public GameObject GetTileFromPool(Transform parent)
        {
            if (_poolContainer == null) InitPool();

            GameObject obj;
            if (_tilePool.Count > 0)
            {
                obj = _tilePool.Dequeue();
                obj.transform.SetParent(parent, false);
                obj.SetActive(true);
                
                var cg = obj.GetComponent<CanvasGroup>();
                if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = true; }
                obj.transform.localScale = Vector3.one;

                var interactions = obj.GetComponentsInChildren<TileInteraction>(true);
                foreach (var i in interactions) i.enabled = true;

                // 全ての子ImageのraycastTargetを復元するが、色は変更しない（DoraなどのOverlayが白塗りになるのを防ぐ）
                var images = obj.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                foreach (var img in images) { img.raycastTarget = true; }

                // ルートのImage（もしあれば）のカラーだけは白に戻す（フェード等のリセット）
                var rootImg = obj.GetComponent<UnityEngine.UI.Image>();
                if (rootImg != null) rootImg.color = Color.white;

                var btn = obj.GetComponent<UnityEngine.UI.Button>();
                if (btn != null) btn.interactable = true;
            }
            else
            {
                obj = Instantiate(uiManager.TilePrefab, parent);
            }
            return obj;
        }

        public void ReturnTileToPool(GameObject obj)
        {
            if (obj == null) return;
            if (_poolContainer == null) InitPool();
            
            obj.transform.SetParent(_poolContainer, false);
            obj.SetActive(false);
            _tilePool.Enqueue(obj);
        }

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
                            ReturnTileToPool(t.gameObject);
                        }
                    }
                    uiManager.HandUI.GetHandSlots().Clear();
                }
                if (uiManager.WallUI != null)
                {
                    // 矢印インジケータを牌スロットから安全に切り離してからプールに返す
                    uiManager.WallUI.SetActiveDiscardTile(null);
                    for (int i = uiManager.WallUI.GetWallSlots().Count - 1; i >= 0; i--)
                    {
                        Transform t = uiManager.WallUI.GetWallSlots()[i];
                        if (t != null) {
                            ReturnTileToPool(t.gameObject);
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
                    GameObject obj = GetTileFromPool(transform);
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
                    GameObject obj = GetTileFromPool(transform);
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

                    List<int> handTilesToRemove = new List<int>();
                    // 打牌フェイズ以降のみ手牌を壁から減らす（掛け金フェイズまでは壁を減らさない）
                    if (board.CurrentEnemyHandTiles != null && uiManager.CurrentPhaseStatus >= RoundStatus.Discard)
                    {
                        handTilesToRemove.AddRange(board.CurrentEnemyHandTiles);
                    }

                    for (int i = 0; i < board.OriginalEnemyWallTiles.Count; i++)
                    {
                        int actualTileId = board.OriginalEnemyWallTiles[i];

                        if (handTilesToRemove.Contains(actualTileId))
                        {
                            handTilesToRemove.Remove(actualTileId);
                            continue;
                        }

                        displayIdsForWall.Add(actualTileId);

                        GameObject obj = GetTileFromPool(transform);
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
                            foreach(var interaction in interactions) interaction.enabled = false;
                            var visual = rt.GetComponent<TileVisual>();
                            if (visual != null) visual.SetHoverHighlight(false);
                            var images = rt.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                            foreach(var img in images) img.raycastTarget = false;
                            
                            var btn = rt.gameObject.GetComponent<UnityEngine.UI.Button>();
                            if (btn != null) btn.interactable = false;
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
            var visual = rt.GetComponent<TileVisual>();
            if (visual != null)
            {
                visual.SetHoverHighlight(false);
                visual.SetFuritenHighlight(false);
                visual.SetExposed(false);
                
                if (uiManager.TileResourceManager != null)
                {
                    visual.SetTile(id, uiManager.TileResourceManager.GetTileSprite(id));
                }
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
                    Vector3 startPos = movedTile.position;
                    uiManager.HandUI.AddTileToHand(movedTile, tileId);
                    StartCoroutine(AnimateTileMovementRoutine(movedTile, startPos, 0.25f));
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
                Vector3 startPos = movedTile.position;
                uiManager.HandUI.RemoveTileFromHand(movedTile, tileId);
                uiManager.WallUI.ReturnTileToWall(movedTile, tileId);
                StartCoroutine(AnimateTileMovementRoutine(movedTile, startPos, 0.25f));
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

        public IEnumerator PlayHandSortAnimationRoutine()
        {
            if (uiManager.HandUI == null) yield break;

            var handSlots = uiManager.HandUI.GetHandSlots();
            if (handSlots == null || handSlots.Count == 0) yield break;

            var board = BoardStateManager.Instance;
            var currentHandIds = board.CurrentHandTiles;

            Dictionary<RectTransform, Vector2> startPositions = new Dictionary<RectTransform, Vector2>();
            foreach (var slot in handSlots)
            {
                startPositions[slot] = slot.anchoredPosition;
            }

            List<RectTransform> sortedSlots = new List<RectTransform>();
            List<RectTransform> unsortedSlots = new List<RectTransform>(handSlots);
            
            foreach (int targetId in currentHandIds)
            {
                RectTransform foundSlot = null;
                foreach(var slot in unsortedSlots)
                {
                    var visual = slot.GetComponent<TileVisual>();
                    if (visual != null && visual.GetId() == targetId)
                    {
                        foundSlot = slot;
                        break;
                    }
                }
                if (foundSlot != null)
                {
                    sortedSlots.Add(foundSlot);
                    unsortedSlots.Remove(foundSlot);
                }
                else if (unsortedSlots.Count > 0)
                {
                    sortedSlots.Add(unsortedSlots[0]);
                    unsortedSlots.RemoveAt(0);
                }
            }

            for (int i = 0; i < sortedSlots.Count; i++)
            {
                sortedSlots[i].SetSiblingIndex(i);
            }

            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(uiManager.HandUI.GetComponent<RectTransform>());
            yield return null;

            Dictionary<RectTransform, Vector2> targetPositions = new Dictionary<RectTransform, Vector2>();
            foreach (var slot in sortedSlots)
            {
                targetPositions[slot] = slot.anchoredPosition;
            }

            var layoutGroup = uiManager.HandUI.GetComponentInChildren<UnityEngine.UI.LayoutGroup>();
            bool wasLayoutEnabled = false;
            if (layoutGroup != null)
            {
                wasLayoutEnabled = layoutGroup.enabled;
                layoutGroup.enabled = false;
            }

            foreach (var slot in sortedSlots)
            {
                slot.anchoredPosition = startPositions[slot];
            }

            float duration = 0.4f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 3f);

                foreach (var slot in sortedSlots)
                {
                    slot.anchoredPosition = Vector2.Lerp(startPositions[slot], targetPositions[slot], t);
                }
                yield return null;
            }

            foreach (var slot in sortedSlots)
            {
                slot.anchoredPosition = targetPositions[slot];
            }

            if (layoutGroup != null)
            {
                layoutGroup.enabled = wasLayoutEnabled;
            }

            uiManager.HandUI.GetHandSlots().Clear();
            uiManager.HandUI.GetHandSlots().AddRange(sortedSlots);

            RebuildAllTilesFromState(null);
        }

        public IEnumerator PlayTransitionAnimationRoutine(System.Action onUpdateState)
        {
            if (uiManager.WallUI == null && uiManager.HandUI == null)
            {
                onUpdateState?.Invoke();
                yield break;
            }

            var allSlots = new List<RectTransform>();
            if (uiManager.WallUI != null) allSlots.AddRange(uiManager.WallUI.GetWallSlots());
            if (uiManager.HandUI != null) allSlots.AddRange(uiManager.HandUI.GetHandSlots());

            var oldData = new List<(int tileId, Vector3 worldPos, Quaternion rotation, Vector3 lossyScale, Vector2 rectSize, Vector2 pivot)>();
            
            // rootCanvasの取得を、UIスロット自身から行う（GameUIManagerがCanvasの外にある場合に対応）
            Canvas rootCanvas = null;
            if (allSlots.Count > 0 && allSlots[0] != null)
            {
                rootCanvas = allSlots[0].GetComponentInParent<Canvas>();
                while (rootCanvas != null && rootCanvas.isRootCanvas == false)
                {
                    var parentCanvas = rootCanvas.transform.parent?.GetComponentInParent<Canvas>();
                    if (parentCanvas == null) break;
                    rootCanvas = parentCanvas;
                }
            }

            foreach (var slot in allSlots)
            {
                if (slot == null || !slot.gameObject.activeInHierarchy) continue;
                var interaction = slot.GetComponent<TileInteraction>();
                if (interaction != null)
                {
                    oldData.Add((interaction.TileId, slot.position, slot.rotation, slot.lossyScale, slot.rect.size, slot.pivot));
                }
            }

            // 2. 最前面用コンテナ生成 (rootCanvasの子)
            GameObject animCanvasObj = new GameObject("TransitionAnimContainer", typeof(RectTransform));
            if (rootCanvas != null)
            {
                animCanvasObj.transform.SetParent(rootCanvas.transform, false);
            }
            // SetAsLastSiblingで同階層内の最前面描画にする
            animCanvasObj.transform.SetAsLastSibling();
            animCanvasObj.layer = rootCanvas != null ? rootCanvas.gameObject.layer : 5;

            var animCanvasRt = animCanvasObj.GetComponent<RectTransform>();
            animCanvasRt.anchorMin = Vector2.zero;
            animCanvasRt.anchorMax = Vector2.one;
            animCanvasRt.offsetMin = Vector2.zero;
            animCanvasRt.offsetMax = Vector2.zero;
            animCanvasRt.localScale = Vector3.one;

            // Nested Canvas特有の座標ズレやスケールバグを避けるため、CanvasのAddComponentは行わない。
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(animCanvasRt);

            var dummies = new List<(RectTransform rt, int tileId, Vector3 startPos, Quaternion startRot, Vector3 startScale)>();
            foreach (var data in oldData)
            {
                GameObject dummyObj = GetTileFromPool(animCanvasObj.transform);
                var rt = dummyObj.GetComponent<RectTransform>();
                
                // 親のアンカー設定に影響されないよう中心固定
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = data.pivot;
                rt.sizeDelta = data.rectSize;
                
                // ワールド空間でのスケールを一致させるため、親のlossyScaleで割った値をlocalScaleに代入
                Vector3 pLossy = animCanvasObj.transform.lossyScale;
                rt.localScale = new Vector3(
                    pLossy.x != 0 ? data.lossyScale.x / pLossy.x : data.lossyScale.x,
                    pLossy.y != 0 ? data.lossyScale.y / pLossy.y : data.lossyScale.y,
                    pLossy.z != 0 ? data.lossyScale.z / pLossy.z : data.lossyScale.z
                );
                
                rt.rotation = data.rotation;
                rt.position = data.worldPos;

                var visual = dummyObj.GetComponent<TileVisual>();
                if (visual != null && uiManager.TileResourceManager != null)
                {
                    visual.SetTile(data.tileId, uiManager.TileResourceManager.GetTileSprite(data.tileId), uiManager.TileResourceManager);
                }

                var interaction = dummyObj.GetComponent<TileInteraction>();
                if (interaction != null) interaction.enabled = false;

                var cg = dummyObj.GetComponent<CanvasGroup>();
                if (cg == null) cg = dummyObj.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = false;
                cg.alpha = 1f;

                var allImages = dummyObj.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                foreach (var img in allImages)
                {
                    img.color = new Color(img.color.r, img.color.g, img.color.b, 1f);
                }

                dummies.Add((rt, data.tileId, rt.position, rt.rotation, rt.localScale));
            }

            uiManager.SetIsTransitioning(true);

            onUpdateState?.Invoke();
            
            uiManager.SetIsTransitioning(false);
            RebuildAllTilesFromState(null);
            uiManager.SetIsTransitioning(true);

            var newSlots = new List<RectTransform>();
            if (uiManager.WallUI != null) newSlots.AddRange(uiManager.WallUI.GetWallSlots());
            if (uiManager.HandUI != null) newSlots.AddRange(uiManager.HandUI.GetHandSlots());

            foreach (var slot in newSlots)
            {
                if (slot == null || !slot.gameObject.activeInHierarchy) continue;
                var cg = slot.GetComponent<CanvasGroup>();
                if (cg == null) cg = slot.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
            }

            if (uiManager.HandUI != null) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(uiManager.HandUI.GetComponent<RectTransform>());
            if (uiManager.WallUI != null) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(uiManager.WallUI.GetComponent<RectTransform>());
            yield return null;

            // 3. ゴール地点のワールドパラメータを取得
            var availableNewData = new List<(int id, Vector3 targetPos, Quaternion targetRot, Vector3 targetScale)>();
            Vector3 pLossyEnd = animCanvasObj.transform.lossyScale;
            foreach (var slot in newSlots)
            {
                if (slot == null || !slot.gameObject.activeInHierarchy) continue;
                var interaction = slot.GetComponent<TileInteraction>();
                if (interaction != null)
                {
                    // ゴール地点も同様に親のスケールを考慮して逆算
                    Vector3 localSc = new Vector3(
                        pLossyEnd.x != 0 ? slot.lossyScale.x / pLossyEnd.x : slot.lossyScale.x,
                        pLossyEnd.y != 0 ? slot.lossyScale.y / pLossyEnd.y : slot.lossyScale.y,
                        pLossyEnd.z != 0 ? slot.lossyScale.z / pLossyEnd.z : slot.lossyScale.z
                    );
                    availableNewData.Add((interaction.TileId, slot.position, slot.rotation, localSc));
                }
            }

            var targetPositions = new Dictionary<RectTransform, (Vector3 pos, Quaternion rot, Vector3 scale, bool isFading)>();

            foreach (var dummy in dummies)
            {
                var rt = dummy.rt;
                int id = dummy.tileId;

                int matchIdx = availableNewData.FindIndex(d => d.id == id);
                if (matchIdx != -1)
                {
                    var target = availableNewData[matchIdx];
                    targetPositions[rt] = (target.targetPos, target.targetRot, target.targetScale, false);
                    availableNewData.RemoveAt(matchIdx);
                }
                else
                {
                    targetPositions[rt] = (dummy.startPos, dummy.startRot, dummy.startScale, true);
                }
            }

            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 3f);

                foreach (var dummy in dummies)
                {
                    var rt = dummy.rt;
                    if (targetPositions.TryGetValue(rt, out var target))
                    {
                        if (target.isFading)
                        {
                            var cg = rt.GetComponent<CanvasGroup>();
                            if (cg != null) cg.alpha = 1f - t;
                        }
                        else
                        {
                            rt.position = Vector3.Lerp(dummy.startPos, target.pos, t);
                            rt.rotation = Quaternion.Lerp(dummy.startRot, target.rot, t);
                            rt.localScale = Vector3.Lerp(dummy.startScale, target.scale, t);
                        }
                    }
                }
                yield return null;
            }

            foreach (var dummy in dummies)
            {
                if (dummy.rt != null)
                {
                    ReturnTileToPool(dummy.rt.gameObject);
                }
            }

            if (animCanvasObj != null)
            {
                Destroy(animCanvasObj);
            }

            foreach (var slot in newSlots)
            {
                if (slot == null) continue;
                var cg = slot.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
            }

            uiManager.SetIsTransitioning(false);
        }
    }
}
