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
        
        public TileData(int id)
        {
            Id = id;
            // Standard MJ ID mapping assumption:
            // 0-8: Manzu 1-9
            // 9-17: Pinzu 1-9
            // 18-26: Souzu 1-9
            // 27-33: East, South, West, North, White, Green, Red
            
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

        public string GetTileName()
        {
            string[] numbers = { "", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
            switch (Category)
            {
                case TileCategory.Manzu: return numbers[Number] + "萬";
                case TileCategory.Pinzu: return numbers[Number] + "筒";
                case TileCategory.Souzu: return numbers[Number] + "索";
                case TileCategory.Honor:
                    string[] honors = { "", "東", "南", "西", "北", "白", "發", "中" };
                    if (Number >= 1 && Number <= 7) return honors[Number];
                    return "不明な字牌";
                default:
                    return "不明な牌";
            }
        }
    }
}
