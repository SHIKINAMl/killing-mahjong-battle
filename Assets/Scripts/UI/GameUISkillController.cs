using UnityEngine;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;

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

        public void Setup(GameUIManager manager)
        {
            this.uiManager = manager;
        }

        public void CancelSkillSelection()
        {
            IsMulliganSelection = false;
        }

        public void StartMulliganSelection()
        {
            IsMulliganSelection = true;
            if (uiManager.PhaseTransitionUI != null)
            {
                uiManager.PhaseTransitionUI.PlayCenterTextAnim("交換する牌を選んでください", 1.5f, null);
            }
            else if (uiManager.DialogueUI != null)
            {
                uiManager.DialogueUI.ShowText("交換する牌を選んでください");
            }
        }

        public void OnMulliganTileSelected(int tileId)
        {
            IsMulliganSelection = false;
            
            var wallTiles = BoardStateManager.Instance.OriginalWallTiles;
            if (wallTiles != null)
            {
                int targetIndex = wallTiles.IndexOf(tileId);
                if (targetIndex != -1)
                {
                    uiManager.SendActionToServer("skill", new Network.ActionPayload { skill_type = "mulligan", target_hand_index = targetIndex });
                }
                else
                {
                    Debug.LogWarning("Mulligan failed: Selected tile not found in wall tiles.");
                }
            }
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

        private string GetSkillName(string skillType)
        {
            switch (skillType)
            {
                case "mulligan": return "手牌交換";
                case "perspective": return "透視";
                case "boost_hand": return "役強化";
                case "special_victory": return "特殊勝利";
                default: return skillType;
            }
        }

        public void HandleSkillCasted(SkillCastedData data)
        {
            StartCoroutine(HandleSkillCastedRoutine(data));
        }

        private System.Collections.IEnumerator HandleSkillCastedRoutine(SkillCastedData data)
        {
            string localPlayerId = KillingMahjong.Network.NetworkMessageHandler.Instance.LocalPlayerId;
            bool isLocalPlayer = (data.player_id == localPlayerId);
            string skillName = GetSkillName(data.skillType);
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

            // 1. 以前の大迫力カットイン演出（血飛沫＋立ち絵＋巨大テキスト）を再生する
            if (uiManager.PhaseTransitionUI != null)
            {
                yield return uiManager.PhaseTransitionUI.PlaySkillCutinAnimationRoutine(skillName, isLocalPlayer, 2.0f, null, subText);
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

            // --- 以降、実際のアビリティ効果（透視など）を実行 ---

            if (data.skillType == "perspective")
            {
                if (data.exposedHandIndexes != null && data.exposedHandIndexes.Count > 0)
                {
                    // localPlayerId is already declared at the top of the method
                    List<int> targetIndexes = data.exposedHandIndexes;
                    
                    if (data.exposedHandIndexesByPlayer != null)
                    {
                        if (isLocalPlayer)
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
                        else
                        {
                            if (data.exposedHandIndexesByPlayer.ContainsKey(localPlayerId))
                            {
                                targetIndexes = data.exposedHandIndexesByPlayer[localPlayerId];
                            }
                        }
                    }

                    if (isLocalPlayer)
                    {
                        List<int> newlyExposed = new List<int>();
                        foreach (int val in targetIndexes)
                        {
                            int wallIdx = val; // Python sends wall indices
                            
                            if (wallIdx >= 0 && wallIdx < 34)
                            {
                                if (!Managers.BoardStateManager.Instance.ExposedEnemyHandWallIndexes.Contains(wallIdx))
                                {
                                    newlyExposed.Add(wallIdx);
                                    Managers.BoardStateManager.Instance.ExposedEnemyHandWallIndexes.Add(wallIdx);
                                }
                            }
                        }
                        
                        Debug.Log($"[Skill] Local player cast perspective. targetIndexes: {string.Join(",", targetIndexes)}. newlyExposed: {string.Join(",", newlyExposed)}");
                        
                        if (newlyExposed.Count > 0)
                        {
                            if (uiManager.VisualController != null)
                            {
                                yield return uiManager.VisualController.PlayPerspectiveAnimation(newlyExposed);
                            }
                        }
                        else
                        {
                            uiManager.VisualController?.RebuildAllTilesFromState();
                        }
                    }
                    else
                    {
                        Debug.Log($"[Skill] Enemy player cast perspective. targetIndexes for local player: {string.Join(",", targetIndexes)}");
                        foreach (int val in targetIndexes)
                        {
                            int wallIdx = val; // Python sends wall indices
                            
                            if (wallIdx >= 0 && wallIdx < 34)
                            {
                                Managers.BoardStateManager.Instance.ExposedLocalHandWallIndexes.Add(wallIdx);
                            }
                        }
                        uiManager.VisualController?.RebuildAllTilesFromState();
                    }
                }
            }
            else if (data.skillType == "mulligan")
            {
                if (isLocalPlayer)
                {
                    uiManager.ClearSelection();
                }
            }
        }
    }
}
