using KillingMahjong.Common;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    public partial class AbilityUI
    {
        private enum PerspectiveMenu { None, Mode, Suit }
        private PerspectiveMenu _perspectiveMenu;

        // サーバーのPERSPECTIVE_SUITSと対になる送信キー。牌の判定はサーバーに任せる。
        private static readonly string[] PerspectiveSuitKeys = { "manzu", "pinzu", "souzu", "jihai" };
        private static readonly string[] PerspectiveSuitLabels = { "萬子", "筒子", "索子", "字牌" };

        private void ShowPerspectiveChoices(PerspectiveMenu menu)
        {
            _perspectiveMenu = menu;
            var entries = new System.Collections.Generic.List<AbilityData>();
            if (menu == PerspectiveMenu.Mode)
            {
                entries.Add(new AbilityData(SkillNames.Perspective, "種別を選ぶ",
                    "萬子・筒子・索子・字牌から、透視する種別を選ぶ。"));
                entries.Add(new AbilityData(SkillNames.Perspective, "ランダム",
                    "相手の山の未公開牌を、種別を問わず最大3枚公開する。"));
            }
            else if (menu == PerspectiveMenu.Suit)
            {
                for (int i = 0; i < PerspectiveSuitLabels.Length; i++)
                    entries.Add(new AbilityData(SkillNames.Perspective, PerspectiveSuitLabels[i],
                        PerspectiveSuitLabels[i] + "の未公開牌を最大3枚公開する。該当する牌がなくてもコストはかかる。"));
            }
            PopulateEntries(entries);
            HideTooltip();
        }

        // OnActivateClickedのチュートリアル／フェイズ／HPチェックを通ってから呼ぶ。
        // 行を再クリックして決定する操作と、閉じるボタンによるキャンセルは従来のまま。
        private void ActivatePerspectiveChoice(GameUIManager uiMgr, int choice)
        {
            if (_perspectiveMenu == PerspectiveMenu.None)
            {
                ShowPerspectiveChoices(PerspectiveMenu.Mode);
                return;
            }
            if (_perspectiveMenu == PerspectiveMenu.Mode && choice == 0)
            {
                ShowPerspectiveChoices(PerspectiveMenu.Suit);
                return;
            }

            string suit;
            if (_perspectiveMenu == PerspectiveMenu.Mode && choice == 1) suit = null;
            else if (_perspectiveMenu == PerspectiveMenu.Suit && choice >= 0 && choice < PerspectiveSuitKeys.Length)
                suit = PerspectiveSuitKeys[choice];
            else return;

            // 決定前には何も送らない。HPや公開牌もサーバーの返事が来るまで変更しない。
            var payload = new ActionPayload { skill_type = SkillNames.Perspective, suit = suit };
            DeselectAll();
            CloseWindow(false); // 先に閉じて、旧行からの二重送信を防ぐ。
            uiMgr.SendActionToServer("skill", payload);
        }
    }
}
