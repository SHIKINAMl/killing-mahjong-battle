using UnityEngine;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public partial class AbilityUI
    {
        private void OnActivateClicked()
        {
            if (IsDisplayOnly) return;

            if (currentSelection != null)
            {
                int index = currentSelection.AbilityIndex;
                if (index >= 0 && index < realAbilities.Count)
                {
                    var data = realAbilities[index];
                    Debug.Log($"Activting Ability: {data.name} " +
                              $"(Cost: {GameRules.GetSkillCost(data.skillType, CurrentSpecialVictoryCount)}, " +
                              $"Type: {data.skillType})");
                    
                    var uiMgr = FindFirstObjectByType<GameUIManager>();
                    if (uiMgr != null)
                    {
                        // チュートリアルはサーバーに接続しないため、発動要求を送っても無反応になる。
                        // 制約「チュートリアル中はプレイヤーの能力使用は不可」に合わせて明示的に弾く。
                        if (uiMgr.IsTutorialMode)
                        {
                            if (uiMgr.DialogueUI != null)
                                uiMgr.DialogueUI.ShowText("「今は見てるだけでいいわ。能力の使い方は後で教えてあげる」");
                            DeselectAll();
                            ToggleAbilityWindow(false);
                            return;
                        }

                        if (uiMgr.CurrentPhaseStatus != KillingMahjong.EngineData.RoundStatus.HandSelection)
                        {
                            if (uiMgr.DialogueUI != null) uiMgr.DialogueUI.ShowText("「今はスキルを使えないわ！」");
                            DeselectAll();
                            ToggleAbilityWindow(false);
                            return;
                        }

                        // 1局1回などHP以外の制限。**HP不足の判定より先に見る**
                        // （血が足りていても撃てないので、後ろに置くと嘘の理由が出る）
                        if (!string.IsNullOrEmpty(currentSelection.UnusableReason))
                        {
                            if (uiMgr.DialogueUI != null)
                                uiMgr.DialogueUI.ShowText(currentSelection.UnusableReason);
                            DeselectAll();
                            ToggleAbilityWindow(false);
                            return;
                        }

                        // HP不足のスキルは押せてしまうと無反応で終わるため、理由を示して弾く
                        if (!currentSelection.IsAffordable)
                        {
                            int requiredCost = GameRules.GetSkillCost(data.skillType, CurrentSpecialVictoryCount);
                            if (uiMgr.DialogueUI != null)
                            {
                                uiMgr.DialogueUI.ShowText(
                                    $"「{data.name}には{requiredCost}必要よ。今のあなたには{CurrentLocalHp}しかないわ」");
                            }
                            DeselectAll();
                            ToggleAbilityWindow(false);
                            return;
                        }

                        if (data.skillType == "mulligan")
                        {
                            uiMgr.StartMulliganSelection();
                        }
                        else if (data.skillType == "boost_hand")
                        {
                            uiMgr.StartBoostHandSelection();
                        }
                        else
                        {
                            // サーバーへスキル発動リクエストを直接送信
                            uiMgr.SendActionToServer("skill", new KillingMahjong.Network.ActionPayload { skill_type = data.skillType });
                        }
                    }
                }
                DeselectAll();
                ToggleAbilityWindow(false); // 発動後にウィンドウを閉じる (キャンセルはしない)
            }
            else
            {
                Debug.Log("No ability selected.");
            }
        }
    }
}
