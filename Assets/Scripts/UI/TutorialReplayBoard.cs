using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    /// <summary>本編の配置処理を使う独立盤面。サーバー・HP・ボルテージには触れない。</summary>
    public sealed class TutorialReplayBoard : MonoBehaviour
    {
        public GameObject tilePrefab;
        public RiverUI localRiver, enemyRiver;
        public WallUI wallUI;
        public EnemyWallUI enemyWallUI;
        public HandBaseUI handUI;
        private TileResourceManager tiles;
        private readonly List<RectTransform> wall = new List<RectTransform>(), enemyWall = new List<RectTransform>();
        private readonly List<int> wallIds = new List<int>();
        private readonly List<GameObject> ownedTiles = new List<GameObject>();
        private TutorialRoundData example;

        public void Initialize(TileResourceManager resources)
        {
            tiles = resources;
            var scenario = Resources.Load<TutorialScenario>("Tutorial/TutorialScenario");
            if (scenario == null) throw new InvalidOperationException("試写のチュートリアル台本がありません。");
            example = scenario.rounds[0];
            gameObject.SetActive(true);
            var ids = TutorialTiles.EncodeAll(example.wallBaseIds, example.doraBaseId);
            // 13枚を手牌に使い、16打済みなので、山牌は残り5枚。
            for (int i = 0; i < 5; i++) { wallIds.Add(ids[i]); wall.Add(Tile(ids[i])); }
            wallUI.LayoutWallTiles(wall, wallIds, null, true);
            for (int i = 0; i < 4; i++) enemyWall.Add(Tile(-1));
            enemyWallUI.LayoutEnemyWallTiles(enemyWall, new List<int> { -1, -1, -1, -1 }, true);
            for (int i = 0; i < 16; i++) localRiver.AddPresentationTile(Tile(TutorialTiles.Encode(9 + i % 9, false)), TutorialTiles.Encode(9 + i % 9, false));
            for (int i = 0; i < 17; i++) enemyRiver.AddPresentationTile(Tile(TutorialTiles.Encode(18 + i % 9, false)), TutorialTiles.Encode(18 + i % 9, false));
        }

        private RectTransform Tile(int id)
        {
            var go = Instantiate(tilePrefab, transform, false);
            ownedTiles.Add(go);
            var interaction = go.GetComponent<TileInteraction>() ?? go.AddComponent<TileInteraction>();
            interaction.TileId = id; interaction.enabled = false;
            if (id >= 0) go.GetComponent<TileVisual>().SetTile(id, tiles.GetTileSprite(id));
            else go.GetComponent<Image>().sprite = tiles.GetTileSprite(id);
            foreach (var graphic in go.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        public IEnumerator DiscardLastTile()
        {
            var tile = wall[0]; int id = wallIds[0];
            Vector3 from = tile.position;
            wall.RemoveAt(0); wallIds.RemoveAt(0);
            localRiver.AddPresentationTile(tile, id);
            Vector3 to = tile.position;
            wallUI.LayoutWallTiles(wall, wallIds, null, true);
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime * 2)
            {
                tile.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t)); yield return null;
            }
            tile.position = to;
        }

        public void ShowNewWall()
        {
            foreach (var go in ownedTiles) if (go != null) Destroy(go);
            ownedTiles.Clear(); wall.Clear(); wallIds.Clear(); enemyWall.Clear();
            localRiver.gameObject.SetActive(false); enemyRiver.gameObject.SetActive(false);
            foreach (int id in TutorialTiles.EncodeAll(example.wallBaseIds, example.doraBaseId)) { wallIds.Add(id); wall.Add(Tile(id)); }
            wallUI.LayoutWallTiles(wall, wallIds, null, false);
            for (int i = 0; i < 34; i++) enemyWall.Add(Tile(-1));
            enemyWallUI.LayoutEnemyWallTiles(enemyWall, new List<int>(new int[34]), false);
        }

        public IEnumerator BuildHand(Action completed)
        {
            foreach (int id in TutorialTiles.EncodeAll(example.manganHandBaseIds, example.doraBaseId))
            {
                int index = wallIds.IndexOf(id);
                if (index < 0) throw new InvalidOperationException("試写の手牌が山牌にありません。");
                var tile = wall[index]; Vector3 from = tile.position;
                wall.RemoveAt(index); wallIds.RemoveAt(index);
                handUI.AddTileToHand(tile, id);
                tile.position = from;
                wallUI.LayoutWallTiles(wall, wallIds, null, false);
                yield return new WaitForSecondsRealtime(.35f);
            }
            completed?.Invoke();
        }
    }
}
