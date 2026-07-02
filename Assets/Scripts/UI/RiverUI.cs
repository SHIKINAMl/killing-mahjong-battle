using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    public class RiverUI : MonoBehaviour
    {
        [Header("River Configuration")]
        [SerializeField] private Transform riverContainer;
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private TileResourceManager tileResourceManager;
        [SerializeField] private TMPro.TextMeshProUGUI turnText; // 左上の打目表示用UI

        [Header("Layout Settings")]
        [SerializeField] private float tileWidth = 50.0f;
        [SerializeField] private float tileHeight = 70.0f;
        [SerializeField] private float tileScale = 0.9f;
        [SerializeField] private int maxPerRow = 6;
        [SerializeField] private bool isEnemyRiver = false; // 相手の河として扱う場合、180度回転させる
        [SerializeField] private float enemyOffsetX = 350f; // 敵の河の位置手前寄せ調整X（ユーザー要望: 350）
        [SerializeField] private float enemyOffsetY = -200f; // 敵の河の位置手前寄せ調整Y（ユーザー要望: -200）

        private List<Transform> discardedTiles = new List<Transform>();

        // 既存のタイル(Wallなどから)をRiverに入れるための新しいメソッド
        public void AddExistingTile(RectTransform rt, int tileId)
        {
            if (rt == null || riverContainer == null) return;
            
            rt.SetParent(riverContainer, true);
            rt.SetAsLastSibling();

            ApplyRiverLayout(rt);

            // Visual Setup
            TileVisual visual = rt.GetComponent<TileVisual>();
            if (visual != null && tileResourceManager != null)
            {
                visual.SetTile(tileId, tileResourceManager.GetDiscardTileSprite(tileId, isEnemyRiver));
            }

            discardedTiles.Add(rt);
            UpdateTurnText();
            UpdateSiblingOrder();
        }

        public void AddTile(int tileId)
        {
            if (tilePrefab == null || riverContainer == null) return;

            GameObject obj = Instantiate(tilePrefab, riverContainer);
            RectTransform rt = obj.GetComponent<RectTransform>();
            if (rt == null) rt = obj.transform as RectTransform;

            ApplyRiverLayout(rt);
            
            // Visual
            TileVisual visual = obj.GetComponent<TileVisual>();
            if (visual != null && tileResourceManager != null)
            {
                visual.SetTile(tileId, tileResourceManager.GetDiscardTileSprite(tileId, isEnemyRiver));
            }

            discardedTiles.Add(obj.transform);
            UpdateTurnText();
            UpdateSiblingOrder();
        }

        private void ApplyRiverLayout(RectTransform rt)
        {
            int index = discardedTiles.Count;
            int row = index / maxPerRow;
            int col = index % maxPerRow;

            float targetX = col * tileWidth;
            float targetY = -row * tileHeight; 
            
            if (isEnemyRiver)
            {
                targetX = -targetX;
                targetY = -targetY;
                
                targetX += enemyOffsetX;
                targetY += enemyOffsetY;
            }

            if (rt != null)
            {
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(targetX, targetY);
                rt.localScale = new Vector3(tileScale, tileScale, 1f);

                rt.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// 行ごとのSiblingIndex（描画順）を再設定する。
        /// 手前（自分）: 2列目が1列目より前面（上レイヤー）
        /// 奥（敵）: 2列目が1列目より背面（下レイヤー）
        /// </summary>
        private void UpdateSiblingOrder()
        {
            if (discardedTiles.Count == 0) return;

            List<List<Transform>> rows = new List<List<Transform>>();

            for (int i = 0; i < discardedTiles.Count; i++)
            {
                int rowIdx = i / maxPerRow;
                while (rows.Count <= rowIdx)
                {
                    rows.Add(new List<Transform>());
                }
                rows[rowIdx].Add(discardedTiles[i]);
            }

            int sibIdx = 0;
            if (isEnemyRiver)
            {
                // 敵の河: 奥の行（rowIdxが大きい方）から先に描画する（背面にいく）
                // 3列目 -> 2列目 -> 1列目 の順で描画（1列目が一番上にくる）
                for (int i = rows.Count - 1; i >= 0; i--)
                {
                    foreach (var t in rows[i]) t.SetSiblingIndex(sibIdx++);
                }
            }
            else
            {
                // 自分の河: 手前の行（rowIdxが小さい方）から先に描画する（背面にいく）
                // 1列目 -> 2列目 -> 3列目 の順で描画（3列目が一番上にくる）
                for (int i = 0; i < rows.Count; i++)
                {
                    foreach (var t in rows[i]) t.SetSiblingIndex(sibIdx++);
                }
            }
        }

        public void UpdateTurnText()
        {
            if (turnText != null)
            {
                int turnCount = discardedTiles.Count;
                int displayTurn = turnCount + 1; // これから打つ牌が何巡目か
                
                bool isFirst = KillingMahjong.Managers.BoardStateManager.Instance.IsLocalPlayerFirstRound;
                string prefix = isFirst ? "先：" : "後：";
                
                turnText.text = $"{prefix}{ToKanji(displayTurn)}打目";
                turnText.gameObject.SetActive(true);
            }
        }

        private string ToKanji(int number)
        {
            string[] kanjiNumbers = { "", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
            if (number <= 0) return "零";
            if (number <= 10) return kanjiNumbers[number];
            if (number < 20) return "十" + kanjiNumbers[number % 10];
            if (number < 100)
            {
                int tens = number / 10;
                int ones = number % 10;
                return kanjiNumbers[tens] + "十" + kanjiNumbers[ones];
            }
            return number.ToString(); // Fallback for >= 100
        }

        public void Clear()
        {
            var uiManager = FindFirstObjectByType<GameUIManager>();
            foreach (var t in discardedTiles)
            {
                if (t != null && uiManager != null && uiManager.VisualController != null)
                {
                    uiManager.VisualController.poolManager.ReturnTileToPool(t.gameObject);
                }
                else if (t != null)
                {
                    Destroy(t.gameObject);
                }
            }
            discardedTiles.Clear();
        }

        public void SetRiver(List<int> tileIds)
        {
            Clear();
            if (tileIds != null)
            {
                foreach(int tileId in tileIds)
                {
                    AddTile(tileId);
                }
            }
            UpdateTurnText();
        }
    }
}
