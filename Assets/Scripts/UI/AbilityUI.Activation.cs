using UnityEngine;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public partial class AbilityUI
    {
        // 紹介中だけ台本側へ渡す。通常対局の通信経路には入れない。
        internal System.Func<string, bool> TutorialActivationHandler { get; set; }
        private void OnActivateClicked()
        {
            if (IsDisplayOnly || !isWindowVisible) return;

            if (currentSelection != null)
            {
                int index = _perspectiveMenu == PerspectiveMenu.None
                    ? currentSelection.AbilityIndex
                    : realAbilities.FindIndex(ability => ability.skillType == SkillNames.Perspective);
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
                            if (TutorialActivationHandler != null)
                            {
                                if (TutorialActivationHandler(data.skillType))
                                {
                                    DeselectAll();
                                    ToggleAbilityWindow(false);
                                }
                                return;
                            }
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
                        if (!currentSelection.IsAffordable || CurrentLocalHp <
                            GameRules.GetSkillCost(data.skillType, CurrentSpecialVictoryCount))
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

                        if (data.skillType == SkillNames.Perspective)
                        {
                            ActivatePerspectiveChoice(uiMgr, currentSelection.AbilityIndex);
                            return; // 選択中は巻物を閉じず、最終決定のときだけ送信する。
                        }
                        else if (data.skillType == "mulligan")
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
