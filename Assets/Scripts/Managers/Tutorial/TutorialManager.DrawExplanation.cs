using System.Collections;
using System.Collections.Generic;
using KillingMahjong.UI;

namespace KillingMahjong.Managers
{
    public partial class TutorialManager
    {
        private IEnumerator RunDrawVideoExplanation()
        {
            yield return PlayLines(new List<TutorialLine> {
                new TutorialLine("ここにはこの局で打った牌の数が示されてる"),
                new TutorialLine("ここでお互いが17牌ずつ打ち終わったらゲームは終了\n流局ってのが起きるよ"),
                new TutorialLine("後輩ちゃんは今後攻だから…"),
                new TutorialLine("後輩ちゃんが17ターン目にロンされなかったら流局\nってことかな"),
            });
            yield return PlaySceneBreakAndWait();
            yield return PlayLines(new List<TutorialLine> {
                new TutorialLine("んで…流局が起こるとどうなるかというと……"),
                new TutorialLine("アタシの昔の対戦動画があるからこれで見よっか"),
            });
            // 差し出すポーズの専用素材は未受領のため、現在の立ち絵を保持する。
            ClearGuide();
            var replay=TutorialDrawReplayUI.Create(transform);
            try { yield return replay.Play(); }
            finally { if(replay!=null) replay.Close(); }
        }
    }
}
