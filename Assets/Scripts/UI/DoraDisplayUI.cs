using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// ドラ表示牌を画面上に表示するUIコンポーネント。
    /// WaitUIと同じ方式で、tilePrefabをInstantiateしてコンテナに配置する。
    /// </summary>
    public class DoraDisplayUI : MonoBehaviour
    {
        [Header("Dora Display Settings")]
        [SerializeField] private RectTransform doraContainer;
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private TileResourceManager tileResourceManager;

        private List<GameObject> activeDoraTiles = new List<GameObject>();
        private int currentDoraId = -1;

        /// <summary>
        /// ドラ表示牌をセットして表示する
        /// </summary>
        /// <param name="doraId">ドラ表示牌のエンコード済みID</param>
        public void ShowDora(int doraId)
        {
            ClearDoraTiles();
            currentDoraId = doraId;

            if (doraId < 0)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            if (tilePrefab == null || doraContainer == null) return;

            GameObject obj = Instantiate(tilePrefab, doraContainer);
            activeDoraTiles.Add(obj);

            TileVisual visual = obj.GetComponent<TileVisual>();
            if (visual != null && tileResourceManager != null)
            {
                visual.SetTile(doraId, tileResourceManager.GetTileSprite(doraId));
            }

            // ドラ表示用なのでクリック判定は不要
            var interaction = obj.GetComponent<TileInteraction>();
            if (interaction != null)
            {
                Destroy(interaction);
            }
        }

        /// <summary>
        /// ドラ表示を非表示にする
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            ClearDoraTiles();
            currentDoraId = -1;
        }

        private void ClearDoraTiles()
        {
            foreach (var t in activeDoraTiles)
            {
                if (t != null) Destroy(t);
            }
            activeDoraTiles.Clear();
        }

        /// <summary>
        /// 現在表示中のドラIDを返す
        /// </summary>
        public int CurrentDoraId => currentDoraId;
    }
}
