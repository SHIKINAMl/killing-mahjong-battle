using System.Collections;
using UnityEngine.SceneManagement;
using KillingMahjong.UI;

namespace KillingMahjong.Managers
{
    public partial class TutorialManager
    {
        private TutorialRevivalUI _revivalUI;

        /// <summary>ページ3FoKlrMHXmniJ9uaKRHV。No1の選択肢復帰とは別の、復活してメニューへ進む経路。</summary>
        private IEnumerator RunRevivalFinale()
        {
            ClearGuide();
            SetFirstRoundChromeVisible(false);
            SetBoardVisible(false);
            VoltageUI.SetCanvasVisible(false);
            yield return PlayLines(_scenario.revival.enemyLines);
            if (_aborted) yield break;
            if (dialogueUI != null)
            {
                dialogueUI.HideText();
                dialogueUI.HideAdvanceOnAnyClick();
            }
            Tutorial.TutorialAudioDirector.ResetVisuals();
            _revivalUI = TutorialRevivalUI.Create(transform);
            try
            {
                yield return _revivalUI.Play(_scenario.revival, BorrowJapaneseFont());
                if (_aborted) yield break;
                Tutorial.TutorialAudioDirector.ResetVisuals();
                TutorialNavigation.Complete(TutorialNavigation.RevivalToMatchMenu);
                SceneManager.LoadScene(_scenario.titleSceneName);
            }
            finally
            {
                CloseRevivalFinale();
            }
        }

        private void CloseRevivalFinale()
        {
            if (_revivalUI != null) _revivalUI.Close();
            _revivalUI = null;
        }
    }
}
