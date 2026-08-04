namespace KillingMahjong.Common
{
    /// <summary>
    /// サーバーから送られる牌IDのビットエンコードを扱うユーティリティ。
    /// コード中に散在していた & 0x1F / >> 5 等の生ビット演算を集約する。
    ///
    /// サーバー側のビット構造 (TileData.cs のドキュメントと同一):
    ///   bit 6 (0x40): 赤ドラ (五萬・五筒・五索 最初の1枚)
    ///   bit 5 (0x20): ドラ
    ///   bit 4-0 (0x1F): 基本牌種別 (0-28)
    /// </summary>
    public static class TileId
    {
        /// <summary>基本牌種別を取り出すマスク (下位5ビット)</summary>
        public const int BaseIdMask = 0x1F;

        /// <summary>ドラフラグのビット</summary>
        public const int DoraBit = 0x20;

        /// <summary>赤ドラフラグのビット</summary>
        public const int RedDoraBit = 0x40;

        /// <summary>ドラ関連フラグ部分を取り出す際のシフト量 (id >> DoraFlagShift)</summary>
        public const int DoraFlagShift = 5;

        /// <summary>エンコード済みIDから基本牌種別 (0-28) を取り出す</summary>
        public static int BaseId(int encodedId) => encodedId & BaseIdMask;

        /// <summary>ドラ関連フラグ部分 (0=通常, 1=ドラ, 2以上=赤ドラ含む) を取り出す。
        /// 値が大きいほど「価値の高い」牌として比較に使える。</summary>
        public static int DoraFlags(int encodedId) => encodedId >> DoraFlagShift;

        /// <summary>ドラ牌かどうか</summary>
        public static bool IsDora(int encodedId) => (encodedId & DoraBit) != 0;

        /// <summary>赤ドラ牌かどうか</summary>
        public static bool IsRedDora(int encodedId) => (encodedId & RedDoraBit) != 0;

        /// <summary>フラグを無視して同じ牌種別かどうかを判定する</summary>
        public static bool IsSameBase(int encodedIdA, int encodedIdB)
            => BaseId(encodedIdA) == BaseId(encodedIdB);

        /// <summary>伏せ牌を表す番兵。牌IDではないが不正でもない。</summary>
        public const int FaceDownId = -1;

        /// <summary>赤ドラが実在する牌種（五萬・五筒・五索）</summary>
        private static bool CanBeRedDora(int baseId)
            => baseId == 4 || baseId == 13 || baseId == 22;

        /// <summary>
        /// サーバーから届いたIDが、実在しうる牌かどうか。
        ///
        /// **この判定を通らないIDを、勝手に近い牌へ丸めてはいけない。**
        /// `&amp; 0x1F` はどんな整数でも 0〜31 に化けさせるため、壊れたIDでも
        /// もっともらしい牌の絵が出てしまい、不具合が画面から消える。
        /// 異常は異常として出す（TileVisual が紫に着色し、ログにも出す）。
        /// </summary>
        public static bool IsValid(int encodedId) => DescribeProblem(encodedId) == null;

        /// <summary>
        /// IDのどこがおかしいかを日本語で返す。正常なら null。
        /// ログにそのまま出せる形にしてある。
        /// </summary>
        public static string DescribeProblem(int encodedId)
        {
            if (encodedId == FaceDownId) return null;   // 伏せ牌

            if (encodedId < 0) return $"負のID ({encodedId})";
            if (encodedId > 0x7F) return $"7ビットに収まらないID ({encodedId})";

            int baseId = BaseId(encodedId);
            if (baseId > 28) return $"存在しない牌種 {baseId} (ID {encodedId})";

            // 0=通常 / 1=ドラ / 2=赤ドラ / **3=ドラかつ赤ドラ**。
            // tile_wall.py:47-48 の dora_flag は足し算なので、赤五がその局のドラを兼ねると 3 になる。
            // 実際に 100(赤五萬) / 109(赤五筒) / 118(赤五索) が正規の牌として山に入る。
            // ここを「ありえない」と弾くと本物の牌を異常扱いしてしまう。
            if (IsRedDora(encodedId) && !CanBeRedDora(baseId))
                return $"赤ドラになりえない牌種 {baseId} に赤ドラビット (ID {encodedId})";

            return null;
        }
    }
}
