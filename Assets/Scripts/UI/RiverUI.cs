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
        [SerializeField] private int maxPerRow = 6;

        private List<Transform> discardedTiles = new List<Transform>();

        public void AddTile(int tileId)
        {
            if (tilePrefab == null || riverContainer == null) return;

            GameObject obj = Instantiate(tilePrefab, riverContainer);
            
            // Layout Logic (6 tiles per row)
            int index = discardedTiles.Count;
            
            // 3行（18枚）を超過した場合は3行目の末尾に重ねる等の対処も可能だが、
            // 現状は4行目以降もそのまま下に伸びるようにする
            int row = index / maxPerRow;
            int col = index % maxPerRow;

            float targetX = col * tileWidth;
            float targetY = -row * tileHeight; 
            
            obj.transform.localPosition = new Vector3(targetX, targetY, 0);
            obj.transform.localRotation = Quaternion.identity;
            
            // UI上のRectTransformの場合はリセット
            RectTransform rt = obj.GetComponent<RectTransform>();
            if (rt != null)
            {
                // 左上基準などにしている場合は環境に合わせる（親コンテナの左上にAnchorを置く前提）
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(targetX, targetY);
                rt.localScale = Vector3.one;
            }

            // Visual
            TileVisual visual = obj.GetComponent<TileVisual>();
            if (visual != null && tileResourceManager != null)
            {
                visual.SetTile(tileId, tileResourceManager.GetTileSprite(tileId));
            }

            discardedTiles.Add(obj.transform);
            UpdateTurnText();
        }

        private void UpdateTurnText()
        {
            if (turnText != null)
            {
                int turnCount = discardedTiles.Count;
                if (turnCount > 0)
                {
                    turnText.text = $"{ToKanji(turnCount)}打目";
                    turnText.gameObject.SetActive(true);
                }
                else
                {
                    turnText.gameObject.SetActive(false);
                }
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
            foreach (var t in discardedTiles)
            {
                if (t != null) Destroy(t.gameObject);
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
