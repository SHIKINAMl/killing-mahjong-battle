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
    }
}
