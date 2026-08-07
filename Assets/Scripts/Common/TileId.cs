namespace KillingMahjong.Common
{
    /// <summary>
    /// 牌IDを扱う**唯一の場所**。
    ///
    /// ## 大原則
    ///
    /// **牌IDの意味を決めているのはサーバー（Python）で、Unity は決めない。**
    /// ここは Python の定義を写しただけの読み取り口であって、独自解釈を足す場所ではない。
    /// `&amp; 0x1F` や `&gt;&gt; 5` を**このファイルの外に書かないこと。**
    /// 散らばると、Python 側の採番が変わったときに追随できず、
    /// しかも壊れたIDが「もっともらしい牌」に化けて不具合が画面から消える。
    ///
    /// ## Python 側の定義（mahjong_engine/engine/tile_wall.py）
    ///
    /// <code>
    /// TOTAL_TILE_TYPES = 29   # 牌種は 0〜28
    /// TILES_PER_TYPE   = 4
    /// TOTAL_TILES      = 116  # ← これは「枚数」。IDの範囲ではない
    ///
    /// dora_flag  = 1 if tile_id == dora else 0
    /// dora_flag += 1 &lt;&lt; 1 if (tile_id in (4, 13, 22)) and i == 0 else 0
    /// self.tiles.append(tile_id | (dora_flag &lt;&lt; 5))
    /// </code>
    ///
    /// つまり:
    ///
    ///   bit 6 (0x40) 赤ドラ / bit 5 (0x20) ドラ / bit 4-0 (0x1F) 牌種 0〜28
    ///
    /// 牌種の割り当ては 0〜8=一萬〜九萬 / 9〜17=一筒〜九筒 / 18〜26=一索〜九索 / 27=東 / 28=西。
    ///
    /// **dora_flag は足し算**なので 0〜3 を取る。赤五がその局のドラを兼ねると 3 になり、
    /// 100(赤五萬) / 109(赤五筒) / 118(赤五索) が正規の牌として山に入る。
    /// ここを見落として「ドラと赤ドラの同時は不正」と判定すると、本物の牌を弾いてしまう。
    ///
    /// なお山は **115枚**（`_initialize_wall` がドラ表示牌を1枚抜くため）。116枚ではない。
    /// </summary>
    public static class TileId
    {
        // ---- Python 側の定数をそのまま写したもの ----

        /// <summary>牌種の数（tile_wall.py の TOTAL_TILE_TYPES）</summary>
        public const int TileTypeCount = 29;

        /// <summary>牌種の最大値（0 始まりなので 28 = 西）</summary>
        public const int MaxBaseId = TileTypeCount - 1;

        /// <summary>基本牌種別を取り出すマスク (下位5ビット)</summary>
        public const int BaseIdMask = 0x1F;

        /// <summary>ドラフラグのビット</summary>
        public const int DoraBit = 0x20;

        /// <summary>赤ドラフラグのビット</summary>
        public const int RedDoraBit = 0x40;

        /// <summary>ドラ関連フラグ部分を取り出す際のシフト量 (id >> DoraFlagShift)</summary>
        public const int DoraFlagShift = 5;

        /// <summary>牌種 27 = 東</summary>
        public const int Ton = 27;

        /// <summary>牌種 28 = 西</summary>
        public const int Sha = 28;

        /// <summary>伏せ牌を表す番兵。牌IDではないが不正でもない。</summary>
        public const int FaceDownId = -1;

        // ---- 読み取り ----

        /// <summary>エンコード済みIDから基本牌種別 (0-28) を取り出す</summary>
        public static int BaseId(int encodedId) => encodedId & BaseIdMask;

        /// <summary>ドラ関連フラグ部分 (0=通常 / 1=ドラ / 2=赤ドラ / 3=ドラかつ赤ドラ) を取り出す。
        /// 値が大きいほど「価値の高い」牌として比較に使える。</summary>
        public static int DoraFlags(int encodedId) => encodedId >> DoraFlagShift;

        /// <summary>ドラ牌かどうか</summary>
        public static bool IsDora(int encodedId) => (encodedId & DoraBit) != 0;

        /// <summary>赤ドラ牌かどうか</summary>
        public static bool IsRedDora(int encodedId) => (encodedId & RedDoraBit) != 0;

        /// <summary>ドラ・赤ドラのどちらかであるか</summary>
        public static bool IsAnyDora(int encodedId) => IsDora(encodedId) || IsRedDora(encodedId);

        /// <summary>フラグを無視して同じ牌種別かどうかを判定する</summary>
        public static bool IsSameBase(int encodedIdA, int encodedIdB)
            => BaseId(encodedIdA) == BaseId(encodedIdB);

        // ---- 組み立て・加工 ----

        /// <summary>牌種とフラグから牌IDを組み立てる（チュートリアル用。対局では使わない）</summary>
        public static int Encode(int baseId, bool isDora = false, bool isRedDora = false)
        {
            int id = baseId & BaseIdMask;
            if (isDora) id |= DoraBit;
            if (isRedDora) id |= RedDoraBit;
            return id;
        }

        /// <summary>ドラフラグだけ落とす。赤ドラフラグは残す（牌そのものの見た目が変わるため）</summary>
        public static int WithoutDora(int encodedId) => encodedId & ~DoraBit;

        /// <summary>赤ドラフラグを立てる</summary>
        public static int WithRedDora(int encodedId) => encodedId | RedDoraBit;

        /// <summary>
        /// 手牌・河の並び順。牌種で並べ、同じ牌種ならID順（＝ドラが後ろ）。
        /// 同じ比較が5か所に写経されていたのでここへ集約した。
        /// </summary>
        public static int CompareForDisplay(int a, int b)
        {
            int baseA = BaseId(a);
            int baseB = BaseId(b);
            if (baseA != baseB) return baseA.CompareTo(baseB);
            return a.CompareTo(b);
        }

        // ---- 検証 ----

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
        ///
        /// なお**通っても正しいとは限らない。** 「本来その牌は山に無いはず」という
        /// 情報はサーバーしか持っていないため、ここで分かるのは形の異常だけ。
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
            if (baseId > MaxBaseId) return $"存在しない牌種 {baseId} (ID {encodedId})";

            if (IsRedDora(encodedId) && !CanBeRedDora(baseId))
                return $"赤ドラになりえない牌種 {baseId} に赤ドラビット (ID {encodedId})";

            return null;
        }
    }
}
