using UnityEngine;
using TMPro;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public partial class PlayerInfoUI
    {
        /// <summary>
        /// 「準備完了」の札。シーンの ReadyBoxContainer を実行時に組み直して使う。
        /// スマホ（zoomTarget）の真下に置く。
        /// </summary>
        private ReadyBadge EnsureReadyBadge()
        {
            return ReadyBoxUtil.EnsureBadge(
                ref readyBadge, readyBoxContainer, readyCheckImage,
                zoomTarget as RectTransform, isSelf: true);
        }

        /// <summary>
        /// **「準備完了」の札は出さない（2026-08-14 の指示）。**
        ///
        /// 相手側は透視を狙い撃ちできてしまうため先に消していた。
        /// 自分側も、確定したことは手牌が伏せられて操作を受け付けなくなることで分かるので、
        /// 札まで出すと画面の情報が増えるだけだった。
        ///
        /// **シーンの `ReadyBoxContainer` を直しても意味がない。** 見た目は
        /// <see cref="ReadyBadge"/> が実行時に作り直す（文字も大きさも位置も定数で上書きする）。
        /// 出す出さないの判断だけがここにある。
        ///
        /// 呼び出し元は <c>GameUIPhaseController</c> に10箇所以上あるので、
        /// 個々の呼び出しを消すのではなくここで受け止めて常に伏せる。
        /// 戻したくなったらこの2つのメソッドの中身だけ復元すればよい。
        /// </summary>
        public void ShowReadyBox(bool show)
        {
            ReadyBoxUtil.HideReadyBox(EnsureReadyBadge(), readyBoxContainer, readyCheckImage);
        }

        /// <summary>札を出さないので、チェックの受け口も何もしない。</summary>
        public void SetReadyCheck(bool isReady)
        {
        }

        /// <summary>
        /// スマホ（体力表示）だけ出し入れする。**立ち絵は触らない。**
        /// チュートリアル第1局で「牌を選ぶUI以外を伏せる」のに使う。
        /// </summary>
        public void SetVitalsVisible(bool visible)
        {
            if (zoomTarget != null) zoomTarget.gameObject.SetActive(visible);
        }

        /// <summary>ベット中のスマホ拡大に隠れるので、拡大している間だけ札を伏せる。</summary>
        public void SetReadyBoxSuppressed(bool suppressed)
        {
            var badge = EnsureReadyBadge();
            if (badge != null) badge.SetSuppressed(suppressed);
        }

        /// <summary>
        /// 自分の手番のときスマホを青く脈打たせる。
        /// スマホ（zoomTarget = HPPanel）は FloatingAnimator で揺れているので、
        /// その中に影絵を敷いて揺れごと追従させる。
        /// </summary>
        public void SetTurnGlow(bool on)
        {
            if (turnGlow == null)
            {
                turnGlow = TurnGlow.Attach(zoomTarget as RectTransform, isSelf: true);
            }
            if (turnGlow != null) turnGlow.SetOn(on);
        }
    }
}
