using System.Collections;
using System.Collections.Generic;
using KillingMahjong.Managers;
using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 牌の移動アニメーション制御（GameUIVisualController から分離）。
    /// - 山⇔手牌の移動時に、親を付け替えた牌を元のスクリーン座標へ戻して滑らかに見せる処理
    /// - 手牌の並び替え（ソート）アニメーション
    /// - 状態更新と連動した移動トランジション
    ///
    /// 注意: プールで使い回す牌UIに対して外部UIを SetParent で子付けしない
    /// プロジェクトルールに従い、このクラスも牌自体の親変更（Hand/Wall間の正規の移動）
    /// 以外の親子関係操作は行わない。
    /// </summary>
    public class TileMoveAnimator
    {
        private readonly GameUIManager uiManager;
        private readonly GameUIVisualController visualController;

        public TileMoveAnimator(GameUIManager uiManager, GameUIVisualController visualController)
        {
            this.uiManager = uiManager;
            this.visualController = visualController;
        }

        private Vector2 GetScreenPosition(RectTransform rt)
        {
            Canvas canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                cam = canvas.worldCamera;
                if (cam == null) cam = Camera.main;
            }
            return RectTransformUtility.WorldToScreenPoint(cam, rt.position);
        }

        /// <summary>山→手牌への移動（BoardStateManager.OnTileMovedToHand に対応）</summary>
        public void HandleTileMovedToHand(int tileId)
        {
            if (uiManager.IsTransitioning) return;

            if (uiManager.WallUI != null && uiManager.HandUI != null)
            {
                RectTransform movedTile = uiManager.WallUI.GrabTile(tileId);
                if (movedTile != null)
                {
                    Vector2 startScreenPos = GetScreenPosition(movedTile);
                    uiManager.HandUI.AddTileToHand(movedTile, tileId);

                    Camera cam = null;
                    Canvas canvas = uiManager.HandUI.GetComponentInParent<Canvas>();
                    if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    {
                        cam = canvas.worldCamera;
                        if (cam == null) cam = Camera.main;
                    }

                    Vector2 localPoint;
                    RectTransform container = movedTile.parent as RectTransform;
                    if (container != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(container, startScreenPos, cam, out localPoint))
                    {
                        movedTile.anchoredPosition = localPoint;
                    }
                    else
                    {
                        movedTile.anchoredPosition = Vector2.zero;
                    }
                }
            }
        }

        /// <summary>手牌→山への移動（BoardStateManager.OnTileMovedToWall に対応）</summary>
        public void HandleTileMovedToWall(int tileId)
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
                Vector2 startScreenPos = GetScreenPosition(movedTile);
                uiManager.HandUI.RemoveTileFromHand(movedTile, tileId);
                uiManager.WallUI.ReturnTileToWall(movedTile, tileId);

                Camera cam = null;
                Canvas canvas = uiManager.WallUI.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    cam = canvas.worldCamera;
                    if (cam == null) cam = Camera.main;
                }

                Vector2 localPoint;
                RectTransform container = movedTile.parent as RectTransform;
                if (container != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(container, startScreenPos, cam, out localPoint))
                {
                    movedTile.anchoredPosition = localPoint;
                }
                else
                {
                    movedTile.anchoredPosition = Vector2.zero;
                }
            }
        }

        /// <summary>手牌の並び順を現在の状態に合わせてアニメーション付きでソートする</summary>
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
                foreach (var slot in unsortedSlots)
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

            visualController.RebuildAllTilesFromState(null);
        }

        /// <summary>状態更新（onUpdateState）の前後差分を検出し、移動した牌を視覚的に移動させる</summary>
        public IEnumerator PlayTransitionAnimationRoutine(System.Action onUpdateState)
        {
            if (uiManager.WallUI == null && uiManager.HandUI == null)
            {
                onUpdateState?.Invoke();
                yield break;
            }

            uiManager.SetIsTransitioning(true);

            // 1. 状態更新前の配置を記録
            List<int> oldHand = new List<int>(BoardStateManager.Instance.CurrentHandTiles);
            List<int> oldWall = new List<int>(BoardStateManager.Instance.CurrentWallTiles);

            // 2. 状態を更新（IsTransitioning=true なので RebuildAllTilesFromState はスキップされる）
            onUpdateState?.Invoke();

            List<int> newHand = new List<int>(BoardStateManager.Instance.CurrentHandTiles);
            List<int> newWall = new List<int>(BoardStateManager.Instance.CurrentWallTiles);

            List<int> movedToHand = new List<int>();
            List<int> oldHandTemp = new List<int>(oldHand);
            foreach (int id in newHand)
            {
                if (oldHandTemp.Contains(id)) oldHandTemp.Remove(id);
                else movedToHand.Add(id);
            }

            List<int> movedToWall = new List<int>();
            List<int> oldWallTemp = new List<int>(oldWall);
            foreach (int id in newWall)
            {
                if (oldWallTemp.Contains(id)) oldWallTemp.Remove(id);
                else movedToWall.Add(id);
            }

            // 3. 手牌から山牌へ戻る牌を視覚的に移動
            foreach (int id in movedToWall)
            {
                if (uiManager.WallUI != null && uiManager.HandUI != null)
                {
                    RectTransform tileRt = null;
                    foreach (var slot in uiManager.HandUI.GetHandSlots())
                    {
                        var inter = slot.GetComponent<TileInteraction>();
                        if (inter != null && inter.TileId == id) { tileRt = slot; break; }
                    }
                    if (tileRt != null)
                    {
                        Vector2 startScreenPos = GetScreenPosition(tileRt);

                        uiManager.HandUI.RemoveTileFromHand(tileRt, id);
                        uiManager.WallUI.ReturnTileToWall(tileRt, id);

                        // アニメーションのため、親が変わった直後の位置を元に戻す
                        Camera cam = null;
                        Canvas canvas = uiManager.WallUI.GetComponentInParent<Canvas>();
                        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                        {
                            cam = canvas.worldCamera;
                            if (cam == null) cam = Camera.main;
                        }

                        Vector2 localPoint;
                        RectTransform container = tileRt.parent as RectTransform;
                        if (container != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(container, startScreenPos, cam, out localPoint))
                        {
                            tileRt.anchoredPosition = localPoint;
                        }
                    }
                }
            }

            // 4. 山牌から手牌へ移動する牌を視覚的に移動
            foreach (int id in movedToHand)
            {
                if (uiManager.WallUI != null && uiManager.HandUI != null)
                {
                    RectTransform tileRt = uiManager.WallUI.GrabTileById(id);
                    if (tileRt != null)
                    {
                        Vector2 startScreenPos = GetScreenPosition(tileRt);

                        uiManager.HandUI.AddTileToHand(tileRt, id);

                        // アニメーションのため、親が変わった直後の位置を元に戻す
                        Camera cam = null;
                        Canvas canvas = uiManager.HandUI.GetComponentInParent<Canvas>();
                        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                        {
                            cam = canvas.worldCamera;
                            if (cam == null) cam = Camera.main;
                        }
                        Vector2 localPoint;
                        RectTransform container = tileRt.parent as RectTransform;
                        if (container != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(container, startScreenPos, cam, out localPoint))
                        {
                            tileRt.anchoredPosition = localPoint;
                        }
                    }
                }
            }

            // 5. 新しい配置で手牌のレイアウトのみ更新（WallUIの再整列は行わず、元の位置のまま穴を開けたり埋めたりする）
            if (uiManager.HandUI != null)
            {
                uiManager.HandUI.UpdateLayout(uiManager.CurrentPhaseStatus);
            }

            // 6. ユーザー指定のアニメーション時間 (0.2秒) 待機
            float duration = 0.2f;
            yield return new WaitForSeconds(duration);

            uiManager.SetIsTransitioning(false);

            // 7. Rebuild時にプール回収が走らないよう、内部の同期用キャッシュを最新化
            visualController.SyncRebuildCache();
        }
    }
}
