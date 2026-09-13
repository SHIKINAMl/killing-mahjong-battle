using System;
using System.Collections.Generic;
using KillingMahjong.Common;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// ボルテージ。**仕様書（Drive「じゃんぱいあ：ボルテージシステム」8/23）がこの実装の元。**
    /// 2026-09-13 に、仕様書どおりへ作り直した。
    ///
    /// 仕様:
    ///   - まだ川に出ていない牌を打つと**ボルテージポイントが1つ**たまる
    ///   - ポイントが一定に達すると段階が上がり、アガったときの獲得金に倍率がつく
    ///   - **ポイントを持った状態で既出の牌を打つと、ボーナスが無くなる**
    ///   - その後また未出牌を打ち始めると、**前回のボーナス段階より1つ下の段階から**
    ///     ポイントがたまり始める
    ///
    /// **段階は「1段階目＝ポイント0＝等倍」から数える。** 表示側は 0 始まりの
    /// <see cref="GetLevel"/>（＝段階-1）を使う。ゲージの4区画がちょうど
    /// 2〜5段階目にあたる。
    ///
    /// **「新しい牌か」の判定は両方の河を見る。** 相手が捨てた牌を自分が捨てても新しくない。
    /// ポイントと段階は自分と相手で別々に持つ。
    ///
    /// **ここは表示のためだけの計算。** 点数への反映はサーバー（`mahjong_engine`）の担当で、
    /// まだ実装されていない。**倍率が出ていても獲得金は変わらない。**
    /// </summary>
    public static class VoltageSystem
    {
        /// <summary>
        /// 段階ごとの倍率。添字が「段階-1」（0＝1段階目＝等倍）。
        /// **仕様書の表そのまま。** 変えるときは仕様書も直すこと。
        /// </summary>
        private static readonly float[] Multipliers = { 1.0f, 1.1f, 1.3f, 1.6f, 2.0f };

        /// <summary>その段階に上がるのに要るポイント。添字は <see cref="Multipliers"/> と同じ。</summary>
        private static readonly int[] StagePoints = { 0, 2, 5, 9, 14 };

        /// <summary>表示で使う最大の段（＝段階5）。</summary>
        public static int MaxLevel => Multipliers.Length - 1;

        /// <summary>どちらかが捨てた牌の種類。赤ドラ違いは同じ牌として扱う。</summary>
        private static readonly HashSet<int> SeenBaseIds = new HashSet<int>();

        private static int _localPoints;
        private static int _enemyPoints;

        // ボーナスが無くなっている最中か。**この間は倍率も等倍に戻る。**
        private static bool _localBroken;
        private static bool _enemyBroken;

        // 途切れたあと、次に未出牌を打ったときにポイントを再開する起点。
        // 仕様の「前回のボーナス段階より1つ下の段階から」がこれ。
        private static int _localResumePoints;
        private static int _enemyResumePoints;

        /// <summary>段数か途切れ状態が変わったときに呼ばれる。表示側はこれを見て描き直す。</summary>
        public static event Action Changed;

        /// <summary>いまのボルテージポイント。途切れている間は 0。</summary>
        public static int GetPoints(bool isEnemy) => isEnemy ? _enemyPoints : _localPoints;

        /// <summary>0 始まりの段（＝仕様書の段階 - 1）。途切れている間は 0。</summary>
        public static int GetLevel(bool isEnemy) => StageIndexFor(GetPoints(isEnemy));

        public static bool IsBroken(bool isEnemy) => isEnemy ? _enemyBroken : _localBroken;

        /// <summary>いまの段に上がったときのポイント。ゲージの途中塗りに使う。</summary>
        public static int GetPointsAtCurrentLevel(bool isEnemy) => StagePoints[GetLevel(isEnemy)];

        /// <summary>
        /// 次の段に上がるのに要るポイント。**最大段に居るときは、いまの段の値をそのまま返す**
        /// （もう伸びないので、ゲージは満タンのまま）。
        /// </summary>
        public static int GetPointsForNextLevel(bool isEnemy)
        {
            int level = GetLevel(isEnemy);
            return level >= MaxLevel ? StagePoints[MaxLevel] : StagePoints[level + 1];
        }

        public static float GetMultiplier(bool isEnemy)
        {
            return Multipliers[GetLevel(isEnemy)];
        }

        /// <summary>ポイントから段（0始まり）を求める。</summary>
        private static int StageIndexFor(int points)
        {
            int stage = 0;
            for (int i = 0; i < StagePoints.Length; i++)
            {
                if (points >= StagePoints[i]) stage = i;
            }
            return stage;
        }

        /// <summary>
        /// この種類の牌が、まだどちらの河にも無いか。**数えるだけで、何も変えない。**
        /// ポイントを動かすのは <see cref="NotifyDiscard"/> と <see cref="ApplyDiscardResult"/>。
        /// </summary>
        public static bool IsNewTileType(int encodedTileId)
        {
            return !SeenBaseIds.Contains(TileId.BaseId(encodedTileId));
        }

        /// <summary>局の切り替わりで呼ぶ。河が空になったらポイントも履歴も捨てる。</summary>
        public static void ResetAll()
        {
            SeenBaseIds.Clear();
            _localPoints = 0;
            _enemyPoints = 0;
            _localBroken = false;
            _enemyBroken = false;
            _localResumePoints = 0;
            _enemyResumePoints = 0;
            Changed?.Invoke();
        }

        /// <summary>
        /// 「この牌は出た」とだけ記録する。**ポイントは動かさない。**
        ///
        /// ボルテージへ光が飛ぶ演出（<see cref="KillingMahjong.UI.VoltageTileFlightEffect"/>）は、
        /// **光が届いてからポイントを足す**（2026-09-13 のユーザー指示）。
        /// 足すのを待つあいだに同じ牌がもう1枚捨てられても二重に数えないよう、
        /// 記録だけは捨てた瞬間に済ませておく。
        /// </summary>
        /// <returns>まだ出ていない牌だった場合に true。</returns>
        public static bool MarkSeen(int encodedTileId)
        {
            int baseId = TileId.BaseId(encodedTileId);
            bool isNew = !SeenBaseIds.Contains(baseId);
            SeenBaseIds.Add(baseId);
            return isNew;
        }

        /// <summary>
        /// <see cref="MarkSeen"/> の結果を、実際にポイントへ反映する。
        /// 光が着いた時に呼ぶ想定。
        /// </summary>
        public static void ApplyDiscardResult(bool isEnemy, bool isNew)
        {
            int points = isEnemy ? _enemyPoints : _localPoints;
            bool broken = isEnemy ? _enemyBroken : _localBroken;
            int resume = isEnemy ? _enemyResumePoints : _localResumePoints;

            if (isNew)
            {
                // 途切れていたら、**前回の段より1つ下の段**から数え直す
                if (broken) points = resume;
                points = UnityEngine.Mathf.Min(StagePoints[MaxLevel], points + 1);
                broken = false;
            }
            else
            {
                // **ボーナスを落とす。** 次に未出牌を打ったとき、どこから再開するかを覚えておく
                int level = StageIndexFor(points);
                resume = StagePoints[UnityEngine.Mathf.Max(0, level - 1)];
                points = 0;
                broken = true;
            }

            if (isEnemy)
            {
                _enemyPoints = points;
                _enemyBroken = broken;
                _enemyResumePoints = resume;
            }
            else
            {
                _localPoints = points;
                _localBroken = broken;
                _localResumePoints = resume;
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// 牌が1枚捨てられたときに呼ぶ。**河に並べる前に呼ぶこと。**
        /// 並べてから呼ぶと、自分が今捨てた牌を「すでに出ている」と数えてしまう。
        /// </summary>
        public static void NotifyDiscard(bool isEnemy, int encodedTileId)
        {
            ApplyDiscardResult(isEnemy, MarkSeen(encodedTileId));
        }
    }
}
