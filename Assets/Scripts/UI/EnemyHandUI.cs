using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    public class EnemyHandUI : HandBaseUI
    {
        // 敵の本来の牌IDを記録する（見た目は0のダミーでも、後々の公開イベントで使うため）
        private List<int> realTileIds = new List<int>();

        public void AddEnemyTile(RectTransform tileTransform, int visualId, int realId)
        {
            // まずは共通のUI追加処理を呼び出す
            base.AddTileToHand(tileTransform, visualId);
            
            // プレイヤーと同じ操作用のInteractionがあると誤作動するため無効化・削除
            var interaction = tileTransform.GetComponent<TileInteraction>();
            if (interaction != null) Destroy(interaction);

            // 代わりにクリック公開用のButtonを追加
            var btn = tileTransform.gameObject.GetComponent<UnityEngine.UI.Button>();
            if (btn == null) btn = tileTransform.gameObject.AddComponent<UnityEngine.UI.Button>();
            
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => {
                int index = handSlots.IndexOf(tileTransform);
                if (index != -1) RevealTileByIndex(index);
            });

            // 本当のIDをリストに追加して記憶する
            realTileIds.Add(realId);
        }

        public override void RemoveTileFromHand(RectTransform tileTransform, int tileId)
        {
            if (handSlots.Contains(tileTransform))
            {
                int index = handSlots.IndexOf(tileTransform);
                if (index >= 0 && index < realTileIds.Count)
                {
                    realTileIds.RemoveAt(index);
                }
            }
            base.RemoveTileFromHand(tileTransform, tileId);
        }

        public void ClearHand()
        {
            foreach (var t in GetHandSlots())
            {
                if (t != null) Destroy(t.gameObject);
            }
            GetHandSlots().Clear();
            realTileIds.Clear();
        }

        /// <summary>
        /// 指定したインデックスの牌だけ、ダミー画像から本物の画像にひっくり返して公開する
        /// </summary>
        public void RevealTileByIndex(int index)
        {
            if (index < 0 || index >= handSlots.Count || index >= realTileIds.Count) return;

            int realId = realTileIds[index];
            RectTransform targetTile = handSlots[index];

            Debug.Log($"Revealing enemy tile at index {index}. Real ID: {realId}");

            if (targetTile != null && tileResourceManager != null)
            {
                var visual = targetTile.GetComponent<TileVisual>();
                if (visual != null)
                {
                    // For EnemyHandUI, SetTile needs to know we want to show the real tile, so we pass realId.
                    // Also pass the resource manager so it can fetch the correct sprite.
                    visual.SetTile(realId, tileResourceManager.GetTileSprite(realId));
                }
            }
        }
    }
}
