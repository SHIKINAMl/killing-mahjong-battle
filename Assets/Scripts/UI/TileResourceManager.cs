using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    [CreateAssetMenu(fileName = "TileResourceManager", menuName = "Mahjong/TileResourceManager")]
    public class TileResourceManager : ScriptableObject
    {
        [Header("Back Tile Sprite (裏牌)")]
        [Tooltip("伏せ牌（ID: 0）として表示される画像")]
        [SerializeField] private Sprite backTileSprite;

        [Header("Tile Sprites (Order: Manzu 1-9, Pinzu 1-9, Souzu 1-9, Honors: East, West)")]
        [Tooltip("Ensure exactly 29 sprites are assigned in standard order.")]
        [SerializeField] private List<Sprite> tileSprites;

        [Header("Red Dora Sprites (赤ドラ)")]
        [Tooltip("赤五萬, 赤五筒, 赤五索 の順に3枚アサイン")]
        [SerializeField] private List<Sprite> redDoraSprites;

        [Header("Discard Tile Sprites (河の画像)")]
        [Tooltip("通常の牌と同じ順番でアサイン")]
        [SerializeField] private List<Sprite> discardTileSprites;

        [Header("Discard Red Dora Sprites (河の赤ドラ)")]
        [Tooltip("赤五萬, 赤五筒, 赤五索 の順に3枚アサイン")]
        [SerializeField] private List<Sprite> discardRedDoraSprites;

        [Header("Enemy Discard Tile Sprites (敵の河の画像)")]
        [Tooltip("通常の牌と同じ順番でアサイン")]
        [SerializeField] private List<Sprite> enemyDiscardTileSprites;

        [Header("Enemy Discard Red Dora Sprites (敵の河の赤ドラ)")]
        [Tooltip("赤五萬, 赤五筒, 赤五索 の順に3枚アサイン")]
        [SerializeField] private List<Sprite> enemyDiscardRedDoraSprites;

        [Header("Prefabs")]
        [SerializeField] private GameObject tilePrefab;

        /// <summary>
        /// エンコード済みIDからスプライトを取得する。
        /// 赤ドラ（bit6）の場合は redDoraSprites から、その他は tileSprites から返す。
        /// </summary>
        public Sprite GetTileSprite(int encodedId)
        {
            // IDが -1 の場合は専用の裏牌画像を返す
            if (encodedId == -1 && backTileSprite != null)
            {
                return backTileSprite;
            }

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
        /// 河に捨てられた時用のエンコード済みIDからスプライトを取得する。
        /// 設定されていない場合は自動的に通常のスプライトを返す。
        /// </summary>
        public Sprite GetDiscardTileSprite(int encodedId, bool isEnemy = false)
        {
            if (encodedId == -1 && backTileSprite != null)
            {
                return backTileSprite;
            }

            List<Sprite> targetSprites = isEnemy ? enemyDiscardTileSprites : discardTileSprites;
            List<Sprite> targetRedDoraSprites = isEnemy ? enemyDiscardRedDoraSprites : discardRedDoraSprites;

            if (targetSprites == null || targetSprites.Count == 0)
            {
                // 用意されていなければプレイヤー側のものでフォールバック
                if (isEnemy && discardTileSprites != null && discardTileSprites.Count > 0)
                {
                    targetSprites = discardTileSprites;
                    targetRedDoraSprites = discardRedDoraSprites;
                }
                else
                {
                    return GetTileSprite(encodedId);
                }
            }

            var tile = new TileData(encodedId);
            int baseId = encodedId & 0x1F;

            // 赤ドラの差し替え
            if (tile.IsRedDora && targetRedDoraSprites != null && targetRedDoraSprites.Count == 3)
            {
                if (baseId == 4 && targetRedDoraSprites[0] != null)  return targetRedDoraSprites[0];
                if (baseId == 13 && targetRedDoraSprites[1] != null) return targetRedDoraSprites[1];
                if (baseId == 22 && targetRedDoraSprites[2] != null) return targetRedDoraSprites[2];
            }

            if (baseId < 0 || baseId >= targetSprites.Count || targetSprites[baseId] == null)
            {
                // フォールバック
                if (isEnemy && discardTileSprites != null && baseId < discardTileSprites.Count)
                    return discardTileSprites[baseId];
                else
                    return GetTileSprite(encodedId);
            }
            
            return targetSprites[baseId];
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
