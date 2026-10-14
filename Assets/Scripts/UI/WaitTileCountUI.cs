using System.Collections.Generic;
using TMPro;
using UnityEngine;
using KillingMahjong.Common;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    /// <summary>自分の山牌・手牌の外にある同種牌の枚数。捨て牌とドラ表示牌は除かない。</summary>
    public sealed class WaitTileCountUI : MonoBehaviour
    {
        private TMP_Text label;
        private int tileId;
        private bool compact;

        public static void Bind(TMP_Text text, int id, bool compact = false)
        {
            var counter = text.gameObject.AddComponent<WaitTileCountUI>();
            counter.label = text;
            counter.tileId = id;
            counter.compact = compact;
            counter.Refresh();
        }

        public static int CountOutsideLocalTiles(int id, IReadOnlyList<int> wall, IReadOnlyList<int> hand)
        {
            int remaining = 4;
            if (wall != null)
                foreach (int tile in wall) if (tile >= 0 && TileId.BaseId(tile) == TileId.BaseId(id)) remaining--;
            if (hand != null)
                foreach (int tile in hand) if (tile >= 0 && TileId.BaseId(tile) == TileId.BaseId(id)) remaining--;
            return Mathf.Clamp(remaining, 0, 4);
        }

        private void LateUpdate() { Refresh(); }

        private void Refresh()
        {
            if (label == null) return;
            var board = BoardStateManager.Instance;
            // 盤面を持たないコレクションでは、根拠のない枚数を表示しない。
            label.enabled = board != null && board.CurrentWallTiles.Count + board.CurrentHandTiles.Count > 0;
            if (!label.enabled) return;
            string text = (compact ? "" : "あと") + CountOutsideLocalTiles(tileId, board.CurrentWallTiles, board.CurrentHandTiles) + "枚";
            if (label.text != text) label.text = text;
        }
    }
}
