namespace KillingMahjong.UI
{
    // 各コントローラ（PhaseController / SkillController / HandSelectionController）への
    // 単純な委譲だけをまとめた窓口。GameUIManager から分離（partial）。
    // クラス・namespace・[SerializeField] は変えていないのでシーン参照には影響しない。
    public partial class GameUIManager
    {
        public void ShowMatchmakingWaiting()
        {
            PhaseController?.ShowMatchmakingWaiting();
        }

        public void ShowDialogue(string text)
        {
            if (dialogueUI != null) dialogueUI.ShowText(text);
        }

        public void CancelSkillSelection()
        {
            SkillController?.CancelSkillSelection();
            HandUI?.UpdateLayout(currentPhaseStatus);
        }

        public void StartMulliganSelection()
        {
            SkillController?.StartMulliganSelection();
            HandUI?.UpdateLayout(currentPhaseStatus);
        }

        public void OnMulliganTileSelected(int tileId, UnityEngine.RectTransform slotRt)
        {
            SkillController?.OnMulliganTileSelected(tileId, slotRt);
            HandUI?.UpdateLayout(currentPhaseStatus);
        }

        public void StartBoostHandSelection()
        {
            SkillController?.StartBoostHandSelection();
        }

        public void CancelHandSelection()
        {
            HandSelectionController?.CancelHandSelection();
        }
    }
}
