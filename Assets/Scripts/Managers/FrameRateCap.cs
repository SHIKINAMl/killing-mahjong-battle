using UnityEngine;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// 描く回数に上限を付ける（2026-09-19、「ゲームが重い」への対処）。
    ///
    /// それまでは vSync も上限も無く、チュートリアルで**毎秒約390回**描いていた
    /// （1回あたりの処理は約2.6ms で、動きの見え方は60回と変わらない）。
    /// 余った回数ぶん CPU と GPU を回し続けるので、PC 全体が重くなり、熱で他の処理も遅れる。
    /// 60 に絞ると1回あたりも約1.9ms に下がった。
    ///
    /// **Quality 設定の vSync ではなくここで止める。** エディタの Game ビューは vSync を無視するため。
    /// </summary>
    public static class FrameRateCap
    {
        public const int TargetFps = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            Application.targetFrameRate = TargetFps;
        }
    }
}
