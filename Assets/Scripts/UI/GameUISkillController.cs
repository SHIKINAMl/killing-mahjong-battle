using UnityEngine;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;
using UnityEngine.UI;
using TMPro;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    [RequireComponent(typeof(GameUIManager))]
    public class GameUISkillController : MonoBehaviour
    {
        private GameUIManager uiManager;
        private YakuSelectionUI yakuSelectionUI;

        [Header("Yaku Selection")]
        [SerializeField] private Font yakuSelectionFont;

        public bool IsMulliganSelection { get; private set; }

        private int _lastMulliganOutTileId = -1;
        private int _lastMulliganTargetIndex = -1;

        /// <summary>牌交換スキルの交換演出（分離クラス）</summary>
        private MulliganSwapAnimator _mulliganSwapAnimator;

        public void Setup(GameUIManager manager)
        {
            this.uiManager = manager;
            _mulliganSwapAnimator = new MulliganSwapAnimator(manager);

            if (mulliganCanvas != null)
            {
                mulliganCanvas.SetActive(false);
            }
        }

        public void CancelSkillSelection()
        {
            IsMulliganSelection = false;
            HideMulliganUI();
        }

        [Header("Mulligan UI Settings")]
        [SerializeField] private GameObject mulliganCanvas;
        private System.Collections.Generic.List<GameObject> hiddenUIs = new System.Collections.Generic.List<GameObject>();

        public void StartMulliganSelection()
        {
            IsMulliganSelection = true;
            ShowMulliganUI();
        }

        private RectTransform _lastMulliganOutSlotRt;

        public void OnMulliganTileSelected(int tileId, RectTransform slotRt)
        {
            IsMulliganSelection = false;
            HideMulliganUI();
            
            // アニメーション中の不意なRebuildを防ぐ
            uiManager.SetIsTransitioning(true);
            
            var wallTiles = BoardStateManager.Instance.OriginalWallTiles;
            if (wallTiles != null)
            {
                int targetIndex = wallTiles.IndexOf(tileId);
                if (targetIndex != -1)
                {
                    _lastMulliganOutTileId = tileId;
                    _lastMulliganTargetIndex = targetIndex;
                    _lastMulliganOutSlotRt = slotRt;
                    
                    // クリック直後には透明にしない。アニメーション開始時に透明にする。

                    uiManager.SendActionToServer("skill", new Network.ActionPayload { skill_type = "mulligan", target_hand_index = targetIndex });
                }
                else
                {
                    Debug.LogWarning("Mulligan failed: Selected tile not found in wall tiles.");
                    uiManager.SetIsTransitioning(false);
                }
            }
        }

        private void ShowMulliganUI()
        {
            if (mulliganCanvas != null) mulliganCanvas.SetActive(true);
            
            _sortingScope.BringToFront(uiManager.HandUI?.gameObject, UISortingOrders.MulliganFocusTiles);
            _sortingScope.BringToFront(uiManager.WallUI?.gameObject, UISortingOrders.MulliganFocusTiles);
            
            // Hide distracting/overlapping UI elements
            hiddenUIs.Clear();
            HideIfActive(uiManager.DialogueUI?.gameObject);
            HideIfActive(uiManager.PlayerInfoUI?.gameObject);
            HideIfActive(uiManager.EnemyInfoUI?.gameObject);
            HideIfActive(uiManager.YakuListUI?.gameObject);
        }

        private void HideIfActive(GameObject go)
        {
            if (go != null && go.activeSelf)
            {
                hiddenUIs.Add(go);
                go.SetActive(false);
            }
        }

        /// <summary>マリガン中の手牌/山UIの前面化と復元。
        /// プロジェクトルールに従い、対象のルートCanvasの overrideSorting のみを操作する。</summary>
        private readonly CanvasSortingScope _sortingScope = new CanvasSortingScope();

        private void HideMulliganUI()
        {
            if (mulliganCanvas != null) mulliganCanvas.SetActive(false);
            if (uiManager != null)
            {
                _sortingScope.Restore(uiManager.HandUI?.gameObject);
                _sortingScope.Restore(uiManager.WallUI?.gameObject);
            }
            
            foreach (var go in hiddenUIs)
            {
                if (go != null) go.SetActive(true);
            }
            hiddenUIs.Clear();
        }

        public void StartBoostHandSelection()
        {
            if (yakuSelectionUI == null)
            {
                yakuSelectionUI = gameObject.AddComponent<YakuSelectionUI>();
                if (yakuSelectionFont != null)
                {
                    yakuSelectionUI.customFont = yakuSelectionFont;
                }
            }

            yakuSelectionUI.Show(
                onSelected: (yakuName) => {
                    uiManager.SendActionToServer("skill", new Network.ActionPayload { skill_type = "boost_hand", yaku_name = yakuName });
                },
                onCanceled: () => {
                    Debug.Log("Boost hand cancelled");
                }
            );
        }

        public void HandleSkillCasted(SkillCastedData data)
        {
            StartCoroutine(HandleSkillCastedRoutine(data));
        }

        private System.Collections.IEnumerator HandleSkillCastedRoutine(SkillCastedData data)
        {
            uiManager.SetIsTransitioning(true); // ★ アニメーション中の非同期Rebuildを防ぐ

            string localPlayerId = KillingMahjong.Network.NetworkMessageHandler.Instance.LocalPlayerId;
            bool isLocalPlayer = (data.player_id == localPlayerId);
            string skillName = SkillNames.GetDisplayName(data.skillType);
            string subText = null;

            if (data.skillType == "boost_hand")
            {
                var oldLocalBonus = Managers.BoardStateManager.Instance.LocalBoostHandBonus != null ? 
                    new Dictionary<string, int>(Managers.BoardStateManager.Instance.LocalBoostHandBonus) : new Dictionary<string, int>();
                var oldEnemyBonus = Managers.BoardStateManager.Instance.EnemyBoostHandBonus != null ? 
                    new Dictionary<string, int>(Managers.BoardStateManager.Instance.EnemyBoostHandBonus) : new Dictionary<string, int>();

                bool statusReceived = false;
                System.Action<KillingMahjong.EngineData.StatusData> onStatus = (statusData) => { statusReceived = true; };
                NetworkMessageHandler.Instance.OnStatusReceived += onStatus;

                float timeout = 2.0f;
                while (!statusReceived && timeout > 0)
                {
                    timeout -= Time.deltaTime;
                    yield return null;
                }
                NetworkMessageHandler.Instance.OnStatusReceived -= onStatus;

                var newLocalBonus = Managers.BoardStateManager.Instance.LocalBoostHandBonus ?? new Dictionary<string, int>();
                var newEnemyBonus = Managers.BoardStateManager.Instance.EnemyBoostHandBonus ?? new Dictionary<string, int>();

                var targetOldBonus = isLocalPlayer ? oldLocalBonus : oldEnemyBonus;
                var targetNewBonus = isLocalPlayer ? newLocalBonus : newEnemyBonus;

                string boostedYakuName = "";
                foreach (var kvp in targetNewBonus)
                {
                    if (!targetOldBonus.ContainsKey(kvp.Key) || targetOldBonus[kvp.Key] < kvp.Value)
                    {
                        boostedYakuName = kvp.Key;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(boostedYakuName))
                {
                    subText = $"<color=yellow>{boostedYakuName}</color>";
                }
            }

            // --- プレ解析：透視スキルの場合の newlyExposed の抽出 ---
            // サーバーからのstatus上書き前に最新の追加分を計算する
            List<int> newlyExposed = new List<int>();
            if (data.skillType == "perspective" && isLocalPlayer)
            {
                if (data.exposedHandIndexes != null && data.exposedHandIndexes.Count > 0)
                {
                    List<int> targetIndexes = data.exposedHandIndexes;
                    if (data.exposedHandIndexesByPlayer != null)
                    {
                        foreach (var kvp in data.exposedHandIndexesByPlayer)
                        {
                            if (kvp.Key != localPlayerId)
                            {
                                targetIndexes = kvp.Value;
                                break;
                            }
                        }
                    }

                    foreach (int val in targetIndexes)
                    {
                        int wallIdx = val;
                        if (wallIdx >= 0 && wallIdx < 34)
                        {
                            if (!Managers.BoardStateManager.Instance.ExposedEnemyHandWallIndexes.Contains(wallIdx))
                            {
                                newlyExposed.Add(wallIdx);
                                Managers.BoardStateManager.Instance.ExposedEnemyHandWallIndexes.Add(wallIdx);
                            }
                        }
                    }
                }
            }
            // 1. 以前の大迫力カットイン演出（血飛沫＋立ち絵＋巨大テキスト）を再生する
            if (uiManager.PhaseTransitionUI != null)
            {
                CharacterData cData = isLocalPlayer ? uiManager.PlayerInfoUI.CurrentCharacterData : uiManager.EnemyInfoUI.CurrentCharacterData;
                yield return uiManager.PhaseTransitionUI.PlaySkillCutinAnimationRoutine(skillName, isLocalPlayer, cData, 2.0f, null, subText);
            }
            else if (uiManager.DialogueUI != null)
            {
                string castMessage = isLocalPlayer ? $"【あなた】がアビリティを発動！\n「{skillName}」" : $"【相手】がアビリティを発動！\n「{skillName}」";
                uiManager.DialogueUI.ShowText(castMessage);
                yield return new WaitForSeconds(2.0f);
            }

            // 2. HP（コスト）の支払い演出
            if (isLocalPlayer)
            {
                int currentLocalHp = Managers.BoardStateManager.Instance.LocalPlayerHp;
                int currentEnemyHp = Managers.BoardStateManager.Instance.EnemyPlayerHp;
                Managers.BoardStateManager.Instance.UpdateHp(currentLocalHp - data.cost, currentEnemyHp);
                if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.SetHP(Managers.BoardStateManager.Instance.LocalPlayerHp);
            }
            else
            {
                int currentLocalHp = Managers.BoardStateManager.Instance.LocalPlayerHp;
                int currentEnemyHp = Managers.BoardStateManager.Instance.EnemyPlayerHp;
                Managers.BoardStateManager.Instance.UpdateHp(currentLocalHp, currentEnemyHp - data.cost);
                if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetHP(Managers.BoardStateManager.Instance.EnemyPlayerHp);
            }

            // 体力が減る様子をしっかり見せるためのタメ（待機）
            yield return new WaitForSeconds(1.0f);

            // --- 以降、実際のアビリティ効果（透視以外も含む）を実行 ---

            if (data.skillType == "perspective")
            {
                if (isLocalPlayer)
                {
                    if (newlyExposed.Count > 0 && uiManager.VisualController != null)
                    {
                        // 演出を見せるため、アニメーション完了を待つ
                        yield return StartCoroutine(uiManager.VisualController.PlayPerspectiveAnimation(newlyExposed));
                        
                        // アニメーション完了後にUIロックを解除する
                        uiManager.SetIsTransitioning(false);
                        uiManager.VisualController?.RebuildAllTilesFromState();
                    }
                    else
                    {
                        uiManager.SetIsTransitioning(false);
                        uiManager.VisualController?.RebuildAllTilesFromState();
                    }
                }
                else
                {
                    // 敵プレイヤーの透視の場合は、ローカルプレイヤーの手牌が透視される
                    List<int> targetIndexes = data.exposedHandIndexes;
                    if (data.exposedHandIndexesByPlayer != null && data.exposedHandIndexesByPlayer.ContainsKey(localPlayerId))
                    {
                        targetIndexes = data.exposedHandIndexesByPlayer[localPlayerId];
                    }

                    if (targetIndexes != null)
                    {
                        foreach (int val in targetIndexes)
                        {
                            int wallIdx = val; // Python sends wall indices
                            
                            if (wallIdx >= 0 && wallIdx < 34)
                            {
                                Managers.BoardStateManager.Instance.ExposedLocalHandWallIndexes.Add(wallIdx);
                            }
                        }
                    }
                    uiManager.SetIsTransitioning(false);
                    uiManager.VisualController?.RebuildAllTilesFromState();
                }
            }
            else if (data.skillType == "mulligan")
            {
                if (isLocalPlayer)
                {
                    uiManager.ClearSelection();
                    
                    if (data.mulliganResult != null)
                    {
                        int oldTileId = data.mulliganResult.oldTile;
                        int newTileId = data.mulliganResult.newTile;
                        int targetHandIndex = data.mulliganResult.targetHandIndex;
                        
                        var stateMgr = Managers.BoardStateManager.Instance;
                        if (stateMgr.CurrentHandTiles.Contains(oldTileId))
                        {
                            stateMgr.CurrentHandTiles.Remove(oldTileId);
                            stateMgr.CurrentHandTiles.Add(newTileId);
                            stateMgr.SortTileIds(stateMgr.CurrentHandTiles);

                            int wallIdx = stateMgr.CurrentWallTiles.IndexOf(newTileId);
                            if (wallIdx >= 0)
                            {
                                stateMgr.CurrentWallTiles[wallIdx] = oldTileId;
                            }
                        }
                        else if (stateMgr.CurrentWallTiles.Contains(oldTileId))
                        {
                            // If oldTileId is not in hand, this might be an invalid state, but we swap it out safely
                            int wallIdx = stateMgr.CurrentWallTiles.IndexOf(oldTileId);
                            if (wallIdx >= 0)
                            {
                                stateMgr.CurrentWallTiles.RemoveAt(wallIdx);
                                stateMgr.CurrentWallTiles.Add(newTileId);
                                stateMgr.SortTileIds(stateMgr.CurrentWallTiles);
                            }
                        }

                        // アニメーション用のスロットを取得（直前の操作時の記録があればそれを使う、無ければデフォルト）
                        RectTransform targetSlot = _lastMulliganOutSlotRt;
                        if (targetSlot == null && uiManager.HandUI != null)
                        {
                            targetSlot = uiManager.HandUI.GetTileSlotRectTransform(oldTileId);
                        }

                        if (_mulliganSwapAnimator != null)
                        {
                            yield return _mulliganSwapAnimator.PlayRoutine(oldTileId, newTileId, targetSlot);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Mulligan animation failed: mulliganResult is null.");
                    }

                    _lastMulliganOutTileId = -1;
                    _lastMulliganTargetIndex = -1;
                    
                    // MulliganSwapAnimator側で IsTransitioning=false と RebuildAllTilesFromState が呼ばれるため、ここでは不要
                }
                else
                {
                    if (data.mulliganResult != null)
                    {
                        int oldTileId = data.mulliganResult.oldTile;
                        int newTileId = data.mulliganResult.newTile;
                        
                        var stateMgr = Managers.BoardStateManager.Instance;
                        if (stateMgr.CurrentEnemyHandTiles.Contains(oldTileId))
                        {
                            stateMgr.CurrentEnemyHandTiles.Remove(oldTileId);
                            stateMgr.CurrentEnemyHandTiles.Add(newTileId);
                            stateMgr.SortTileIds(stateMgr.CurrentEnemyHandTiles);

                            int wallIdx = stateMgr.CurrentEnemyWallTiles.IndexOf(newTileId);
                            if (wallIdx >= 0)
                            {
                                stateMgr.CurrentEnemyWallTiles[wallIdx] = oldTileId;
                            }
                        }
                        else if (stateMgr.CurrentEnemyWallTiles.Contains(oldTileId))
                        {
                            int wallIdx = stateMgr.CurrentEnemyWallTiles.IndexOf(oldTileId);
                            if (wallIdx >= 0)
                            {
                                stateMgr.CurrentEnemyWallTiles.RemoveAt(wallIdx);
                                stateMgr.CurrentEnemyWallTiles.Add(newTileId);
                                stateMgr.SortTileIds(stateMgr.CurrentEnemyWallTiles);
                            }
                        }
                    }
                    
                    uiManager.SetIsTransitioning(false);
                    uiManager.VisualController?.RebuildAllTilesFromState();
                }
            }
        }
    }
}
