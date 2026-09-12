using UnityEngine;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// 体力からBGMの濃さを決める（2026-09-12）。
    ///
    /// **基準は「低い方の残り体力」。** 自分の体力だけで決めると、
    /// 相手を追い詰めて決着寸前という場面が静かなままになる。
    /// どちらかが死に近いとき、その局は決定的になる。そこを音で拾う。
    ///
    /// 段は <see cref="AudioManager.SetBgmIntensity"/> へ渡され、
    /// 層で鳴っているときは**曲を変えずに楽器の数だけ**が変わる。
    ///
    /// **しきい値は既存の尺度に合わせてある。** 開始体力は 20000 で、
    /// コード上すでに「残りわずか＝2000」「圧倒的＝差5000」という感覚が使われている
    /// （`ReactionController.GameFlow`）。ここだけ別の物差しを持ち込まない。
    /// </summary>
    public partial class AudioManager
    {
        /// <summary>対局開始時の体力。しきい値はこれに対する割合で決めている。</summary>
        private const int FullHp = 20000;

        /// <summary>
        /// 段の下端。**低い方の体力がこれを下回ったらその段へ上がる。**
        /// 添字が段（1〜4）。0段は対局外なので持たない。
        ///   12000 (60%) より上 … 1段
        ///    7000 (35%) より上 … 2段
        ///    3000 (15%) より上 … 3段
        ///    3000 以下         … 4段
        /// </summary>
        private static readonly int[] IntensityFloors = { 0, 12000, 7000, 3000 };

        /// <summary>
        /// 段を下げるときに必要な余裕。**これが無いとしきい値の上で行き来する。**
        /// 体力は勝つと増えるので、境目をまたぐたびに層が出たり消えたりしてしまう。
        /// </summary>
        private const int IntensityHysteresis = 1000;

        /// <summary>
        /// 体力から段を決めて反映する。**両方の体力が確定した時点で呼ぶこと。**
        /// 片方だけ新しい値で呼ぶと、一瞬だけ嘘の段になる。
        /// </summary>
        public void UpdateBgmIntensityFromHp(int playerHp, int enemyHp)
        {
            int lower = Mathf.Min(playerHp, enemyHp);
            if (lower < 0) lower = 0;

            int want = TierFor(lower);

            // 上げるのは即。下げるときだけ余裕を要求する。
            // 追い詰められたことはすぐ音に出したいが、少し回復しただけで
            // 緩むと「助かった」と誤解させる。
            if (want < bgmIntensity)
            {
                int needed = IntensityFloors[Mathf.Clamp(bgmIntensity - 1, 0, IntensityFloors.Length - 1)]
                             + IntensityHysteresis;
                if (lower < needed) return;
            }

            SetBgmIntensity(want);
        }

        private static int TierFor(int lowerHp)
        {
            if (lowerHp > IntensityFloors[1]) return 1;
            if (lowerHp > IntensityFloors[2]) return 2;
            if (lowerHp > IntensityFloors[3]) return 3;
            return 4;
        }

        /// <summary>対局を始めるときに呼ぶ。前の対局の段を持ち越さない。</summary>
        public void ResetBgmIntensity()
        {
            bgmIntensity = 1;
            if (AreLayersRunning) ApplyLayerMix(1, instant: false);
        }
    }
}
