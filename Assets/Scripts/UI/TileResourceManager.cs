using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    [CreateAssetMenu(fileName = "TileResourceManager", menuName = "Mahjong/TileResourceManager")]
    public class TileResourceManager : ScriptableObject
    {
        [Header("Tile Sprites (Order: Manzu 1-9, Pinzu 1-9, Souzu 1-9, Honors: East, West)")]
        [Tooltip("Ensure exactly 29 sprites are assigned in standard order.")]
        [SerializeField] private List<Sprite> tileSprites;

        [Header("Red Dora Sprites (赤ドラ)")]
        [Tooltip("赤五萬, 赤五筒, 赤五索 の順に3枚アサイン")]
        [SerializeField] private List<Sprite> redDoraSprites;

        [Header("Prefabs")]
        [SerializeField] private GameObject tilePrefab;

        /// <summary>
        /// エンコード済みIDからスプライトを取得する。
        /// 赤ドラ（bit6）の場合は redDoraSprites から、その他は tileSprites から返す。
        /// </summary>
        public Sprite GetTileSprite(int encodedId)
        {
            var tile = new TileData(encodedId);
            int baseId = encodedId & 0x1F;

            // 赤ドラ（五萬=4, 五筒=13, 五索=22）のスプライト差し替え
            if (tile.IsRedDora && redDoraSprites != null && redDoraSprites.Count == 3)
            {
                if (baseId == 4)  return redDoraSprites[0]; // 赤五萬
                if (baseId == 13) return redDoraSprites[1]; // 赤五筒
                if (baseId == 22) return redDoraSprites[2]; // 赤五索
            }

            // 通常スプライト（ドラフラグは無視して牌種別で引く）
            if (baseId < 0 || baseId >= tileSprites.Count)
            {
                Debug.LogWarning($"[TileResourceManager] Tile base ID {baseId} (encoded: {encodedId}) is out of range.");
                return null;
            }
            return tileSprites[baseId];
        }

        /// <summary>
        /// エンコード済みIDがドラか赤ドラかどうか返す
        /// </summary>
        public bool IsDora(int encodedId)
        {
            var tile = new TileData(encodedId);
            return tile.IsDora || tile.IsRedDora;
        }

        public GameObject GetTilePrefab()
        {
            return tilePrefab;
        }
    }
}
