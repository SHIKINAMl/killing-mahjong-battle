using System.Collections;
using UnityEngine;

namespace KillingMahjong.UI
{
    public sealed partial class TitleRankingBoardUI
    {
        private RankingRowsTransition rankingMotion;
        private Coroutine rankingRoutine;
        private bool rankingInTransition;

        private void StartRankingEntrance()
        {
            CancelRankingMotion();
            rankingMotion.PrepareEntrance();
            rankingInTransition = true;
            rankingRoutine = StartCoroutine(EnterRanking());
        }

        private IEnumerator EnterRanking()
        {
            yield return rankingMotion.Enter();
            pageStartedAt = Time.realtimeSinceStartup;
            rankingRoutine = null;
            rankingInTransition = false;
        }

        private void ChangeRankingPage(int nextPage)
        {
            rankingInTransition = true;
            rankingRoutine = StartCoroutine(ChangeRanking(nextPage));
        }

        private IEnumerator ChangeRanking(int nextPage)
        {
            yield return rankingMotion.Change(() => { page = nextPage; Render(); });
            pageStartedAt = Time.realtimeSinceStartup;
            rankingRoutine = null;
            rankingInTransition = false;
        }

        private void StartVideoTransition()
        {
            rankingInTransition = true;
            rankingRoutine = StartCoroutine(LeaveForVideo());
        }

        private IEnumerator LeaveForVideo()
        {
            yield return rankingMotion.Exit();
            rankingRoutine = null;
            rankingInTransition = false;
            ShowVideo();
        }

        private void CancelRankingMotion()
        {
            if (rankingRoutine != null) StopCoroutine(rankingRoutine);
            rankingRoutine = null;
            rankingInTransition = false;
            if (rankingMotion != null) rankingMotion.Cancel();
        }
    }
}
