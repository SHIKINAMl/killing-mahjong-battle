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
                // 怖い曲を A から C へ**ゆっくり**入れ替える（2026-10-30 プランナー「画面が暗くなったらCの曲に切り替えて」
                // 「チュートリアルの流れに沿い ゆっくり遷移VERを見せて」）。赤い縁が閉じていく間（3.5秒）に A を消し、
                // 真っ黒になったところから C を 3 秒かけて上げる。すぱっと切り替える版は onDarkened で PlayTutorialBgm を呼ぶ
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayTutorialBgmSlow(TutorialRevivalBgm, RevivalBgmFadeOutSeconds, RevivalBgmFadeInSeconds);
                yield return _revivalUI.Play(_scenario.revival, BorrowJapaneseFont());
                if (_aborted) yield break;
                Tutorial.TutorialAudioDirector.ResetVisuals();
                TutorialNavigation.Complete(TutorialNavigation.RevivalToMatchMenu);
                // タイトルではなくデバッグ部屋へ移し、そこで対局を選ぶ画面まで自動で開く
                // （2026-10-23 ユーザー指示。部屋シーンの TitleUIManager が帰還の印を読んで開く）
                SceneManager.LoadScene(TutorialNavigation.DebugRoomSceneName);
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
