using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// チュートリアル用の牌ID ユーティリティ。
    ///
    /// このゲームの牌IDは TileData.cs と同じビット構造を持つ:
    ///   bit 6 (0x40): 赤ドラ
    ///   bit 5 (0x20): ドラ
    ///   bit 4-0 (0x1F): 牌種 0〜28
    ///
    /// 牌種は 29 種:
    ///   0〜8   : 一萬〜九萬
    ///   9〜17  : 一筒〜九筒
    ///   18〜26 : 一索〜九索
    ///   27     : 東
    ///   28     : 西
    ///
    /// 旧 TutorialManager は 1〜34 を牌IDとして使っていたため、
    /// 32/33/34 が「ドラの一萬/二萬/三萬」として描画され、30/31 は範囲外だった。
    /// 牌IDを直書きする箇所は必ずこのクラス経由で組み立て、Validate に通すこと。
    /// </summary>
    public static class TutorialTiles
    {
        public const int BaseMask = 0x1F;
        public const int DoraBit = 0x20;
        public const int RedDoraBit = 0x40;

        /// <summary>牌種の最大値（29種なので 0〜28）</summary>
        public const int MaxKind = 28;

        // --- 牌種のショートハンド ---
        /// <summary>萬子。number は 1〜9。</summary>
        public static int Man(int number) => number - 1;
        /// <summary>筒子。number は 1〜9。</summary>
        public static int Pin(int number) => 9 + (number - 1);
        /// <summary>索子。number は 1〜9。</summary>
        public static int Sou(int number) => 18 + (number - 1);
        /// <summary>東</summary>
        public const int Ton = 27;
        /// <summary>西</summary>
        public const int Sha = 28;

        /// <summary>牌種とフラグから実際の牌IDを組み立てる。</summary>
        public static int Encode(int baseId, bool isDora = false, bool isRedDora = false)
        {
            int id = baseId & BaseMask;
            if (isDora) id |= DoraBit;
            if (isRedDora) id |= RedDoraBit;
            return id;
        }

        /// <summary>牌IDから牌種（0〜28）を取り出す。</summary>
        public static int BaseOf(int tileId) => tileId & BaseMask;

        /// <summary>
        /// 指定した牌種リストに対し、dora と一致する牌へドラフラグを立てて牌IDリストを返す。
        /// </summary>
        public static List<int> EncodeAll(IReadOnlyList<int> baseIds, int doraBaseId)
        {
            var result = new List<int>(baseIds.Count);
            for (int i = 0; i < baseIds.Count; i++)
            {
                int b = baseIds[i];
                result.Add(Encode(b, isDora: b == doraBaseId));
            }
            return result;
        }

        /// <summary>
        /// 牌種が 0〜28 の範囲に収まっているかを検証する。
        /// 範囲外があれば LogError して false を返す（チュートリアルのデータ流し込み前に呼ぶ）。
        /// </summary>
        public static bool Validate(IReadOnlyList<int> baseIds, string label)
        {
            if (baseIds == null)
            {
                Debug.LogError($"[TutorialTiles] {label}: リストが null です。");
                return false;
            }

            bool ok = true;
            for (int i = 0; i < baseIds.Count; i++)
            {
                int b = baseIds[i];
                if (b < 0 || b > MaxKind)
                {
                    Debug.LogError(
                        $"[TutorialTiles] {label}[{i}] = {b} は牌種の範囲外です（有効値 0〜{MaxKind}）。" +
                        " 1〜34 の旧採番が残っていないか確認してください。");
                    ok = false;
                }
            }
            return ok;
        }
    }
}
