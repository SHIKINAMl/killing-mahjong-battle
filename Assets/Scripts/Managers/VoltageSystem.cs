using System;
using System.Collections.Generic;
using KillingMahjong.Common;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// ボルテージ。ユーザーの指示（2026-09-08）。
    ///
    /// **自分と相手のどちらの河にも無い牌**を捨てると1段上がる。
    /// すでに出ている牌を捨てると途切れ、**次に新しい牌を出したとき1段下から再開する。**
    ///
    /// **段数は自分と相手で別々に持つ。** ただし「新しい牌か」の判定は
    /// **両方の河**を見る（相手が捨てた牌を自分が捨てても新しくない）。
    ///
    /// **ここは表示のためだけの計算。** 点数への反映はサーバー（`mahjong_engine`）の担当で、
    /// まだ実装されていない。**倍率が出ていても点数は変わらない。**
    /// サーバー側が入ったら <see cref="Multipliers"/> を差し替える。
    /// </summary>
    public static class VoltageSystem
    {
        /// <summary>
        /// 段ごとの倍率。**後でサーバー側の値に差し替える前提で、ここ1箇所にまとめてある。**
        /// 添字が段数（0段＝ボルテージ無し＝等倍）。
        /// </summary>
        private static readonly float[] Multipliers = { 1.0f, 1.2f, 1.4f, 1.8f, 2.0f };

        /// <summary>上限の段数。倍率表から決まるので、別に定数を置かない。</summary>
        public static int MaxLevel => Multipliers.Length - 1;

        /// <summary>どちらかが捨てた牌の種類。赤ドラ違いは同じ牌として扱う。</summary>
        private static readonly HashSet<int> SeenBaseIds = new HashSet<int>();

        private static int _localLevel;
        private static int _enemyLevel;

        // 途切れている最中か。途切れた直後は段数を落とさず、
        // **次に新しい牌が出た時点で1段下から再開する。**
        // 落としてから再開すると差し引き元に戻ってしまい、途切れた意味がなくなる。
        private static bool _localBroken;
        private static bool _enemyBroken;

        /// <summary>段数か途切れ状態が変わったときに呼ばれる。表示side はこれを見て描き直す。</summary>
        public static event Action Changed;

        public static int GetLevel(bool isEnemy) => isEnemy ? _enemyLevel : _localLevel;

        public static bool IsBroken(bool isEnemy) => isEnemy ? _enemyBroken : _localBroken;

        public static float GetMultiplier(bool isEnemy)
        {
            int level = UnityEngine.Mathf.Clamp(GetLevel(isEnemy), 0, MaxLevel);
            return Multipliers[level];
        }

        /// <summary>局の切り替わりで呼ぶ。河が空になったら段数も履歴も捨てる。</summary>
        public static void ResetAll()
        {
            SeenBaseIds.Clear();
            _localLevel = 0;
            _enemyLevel = 0;
            _localBroken = false;
            _enemyBroken = false;
            Changed?.Invoke();
        }

        /// <summary>
        /// 牌が1枚捨てられたときに呼ぶ。**河に並べる前に呼ぶこと。**
        /// 並べてから呼ぶと、自分が今捨てた牌を「すでに出ている」と数えてしまう。
        /// </summary>
        public static void NotifyDiscard(bool isEnemy, int encodedTileId)
        {
            int baseId = TileId.BaseId(encodedTileId);
            bool isNew = !SeenBaseIds.Contains(baseId);
            SeenBaseIds.Add(baseId);

            int level = isEnemy ? _enemyLevel : _localLevel;
            bool broken = isEnemy ? _enemyBroken : _localBroken;

            if (isNew)
            {
                if (broken)
                {
                    // 1段下から再開する。上げ直しではなく、落ちた位置から。
                    level = UnityEngine.Mathf.Max(0, level - 1);
                    broken = false;
                }
                else
                {
                    level = UnityEngine.Mathf.Min(MaxLevel, level + 1);
                }
            }
            else
            {
                // すでに出ている牌。段数はまだ落とさず、途切れた印だけ付ける。
                broken = true;
            }

            if (isEnemy)
            {
                _enemyLevel = level;
                _enemyBroken = broken;
            }
            else
            {
                _localLevel = level;
                _localBroken = broken;
            }

            Changed?.Invoke();
        }
    }
}
