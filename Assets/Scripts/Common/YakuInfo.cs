using System.Collections.Generic;

namespace KillingMahjong.Common
{
    /// <summary>
    /// 役の成立条件を1行で説明するための辞書。
    ///
    /// 役一覧・役選択UIは役名しか出しておらず、麻雀を知らないプレイヤーは
    /// 「強化する役」を実質ランダムに選ぶしかなかった。表示用の説明文をここに集約する。
    ///
    /// このゲームの字牌は東・西の2種だけなので、字牌が絡む役の説明もそれに合わせている。
    /// </summary>
    public static class YakuInfo
    {
        private static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>
        {
            // --- 1飜 ---
            { "立直",       "聴牌したら勝手に付く" },
            { "断么九",     "2から8の牌だけで作る" },
            { "平和",       "順子だけで作って、両面で待つ" },
            { "一盃口",     "同じ順子が2つ" },
            { "東",         "東が3枚" },
            { "西",         "西が3枚" },
            { "一発",       "相手の1打目でロン" },
            { "河底撈魚",   "最後の捨て牌でロン" },
            // 表示されている牌そのものがドラ（次の牌ではない。サーバーの game_engine.py は dora_id と同じ牌にドラを付ける）
            // 後半は軽口（ユーザーの指示「一枚で1飜よりも、軽口、ジョークを」）。役カードの幅に収まる長さにする
            { "ドラ",       "表示の牌がドラ。どんどん重ねていこう" },
            { "赤ドラ",     "赤い5。1枚で1翻" },

            // --- 2飜 ---
            { "三色同順",   "萬子・筒子・索子で同じ数の順子" },
            { "三色同刻",   "萬子・筒子・索子で同じ数の刻子" },
            { "三暗刻",     "暗刻が3つ" },
            { "対々和",     "刻子だけで4つ" },
            { "混老頭",     "1・9と字牌だけ" },
            { "混全帯么九", "どの面子にも1・9か字牌。雀頭も" },
            { "七対子",     "対子を7つ" },
            // **抜けていた（2026-10-09）。** サーバーの yaku.py と GameRules.GetBaseHan には在るのに、
            // 説明だけ無かった。役一覧でカーソルを合わせても何も出なかった
            { "一気通貫",   "ひとつの柄で123・456・789" },

            // --- 3飜 ---
            { "二盃口",     "一盃口が2つ" },
            { "混一色",     "ひとつの柄と字牌だけ" },
            { "純全帯么九", "どの面子にも1か9が入る。字牌はなし" },

            // --- 6飜 ---
            { "清一色",     "ひとつの柄だけ。字牌もなし" },

            // --- 役満 ---
            { "九蓮宝燈",   "ひとつの柄で1112345678999、あと1枚" },
            { "緑一色",     "2・3・4・6・8索だけ" },
            { "清老頭",     "1と9だけ" },
            { "四暗刻",     "暗刻が4つ" },

            // --- ダブル役満 ---
            { "純正九蓮宝燈", "九蓮宝燈の9面待ちでアガる" },
        };

        /// <summary>役の説明を返す。未登録の役は空文字。</summary>
        public static string GetDescription(string yakuName)
        {
            if (string.IsNullOrEmpty(yakuName)) return "";
            return Descriptions.TryGetValue(yakuName, out string desc) ? desc : "";
        }

        public static bool Has(string yakuName)
        {
            return !string.IsNullOrEmpty(yakuName) && Descriptions.ContainsKey(yakuName);
        }
    }
}
