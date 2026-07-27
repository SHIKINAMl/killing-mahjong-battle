using UnityEngine;

namespace KillingMahjong
{
    public enum TileCategory
    {
        Manzu,
        Pinzu,
        Souzu,
        Honor // 字牌
    }

    [System.Serializable]
    public struct TileData
    {
        public int Id;
        public TileCategory Category;
        public int Number; // 1-9 for suits, ID offset for honors
        
        public TileData(int encodedId)
        {
            Id = encodedId;
            
            // サーバー側のビット構造:
            //   bit 6 (0x40): 赤ドラ (五萬・五筒・五索 最初の1枚)
            //   bit 5 (0x20): ドラ
            //   bit 4-0 (0x1F): 基本牌種別 (0-28)
            IsRedDora = (encodedId & 0x40) != 0;
            IsDora    = (encodedId & 0x20) != 0;
            
            int id = encodedId & 0x1F; // 下位5ビットが牌種別

            if (id >= 0 && id <= 8)
            {
                Category = TileCategory.Manzu;
                Number = id + 1;
            }
            else if (id >= 9 && id <= 17)
            {
                Category = TileCategory.Pinzu;
                Number = id - 9 + 1;
            }
            else if (id >= 18 && id <= 26)
            {
                Category = TileCategory.Souzu;
                Number = id - 18 + 1;
            }
            else
            {
                Category = TileCategory.Honor;
                Number = id - 27 + 1;
            }
        }

        /// <summary>ドラ牌かどうか</summary>
        public bool IsDora { get; private set; }
        /// <summary>赤ドラ牌かどうか（五萬・五筒・五索）</summary>
        public bool IsRedDora { get; private set; }

        public string GetTileName()
        {
            string[] numbers = { "", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
            switch (Category)
            {
                case TileCategory.Manzu: return numbers[Number] + "萬";
                case TileCategory.Pinzu: return numbers[Number] + "筒";
                case TileCategory.Souzu: return numbers[Number] + "索";
                case TileCategory.Honor:
                    // このゲームの牌種は 29 種（萬子9・筒子9・索子9・字牌2）。
                    // 字牌は id 27 = 東, id 28 = 西 の 2 種類のみ（tile_wall.py のドラ計算 55-hyouji が
                    // 東↔西 でループすることに対応）。それ以外の Number は範囲外の不正な牌ID。
                    string[] honors = { "", "東", "西" };
                    if (Number >= 1 && Number <= 2) return honors[Number];
                    return "不明な字牌";
                default:
                    return "不明な牌";
            }
        }
    }
}
