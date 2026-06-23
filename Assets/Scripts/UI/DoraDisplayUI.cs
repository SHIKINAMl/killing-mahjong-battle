using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.UI
{
    public enum DoraDisplayMode
    {
        Canvas2D,
        GroundLight3D
    }

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

        [Header("Experimental Settings")]
        [Tooltip("実験的：ドラ表示を2D Canvasにするか、3Dグランドライトエフェクトにするかを選択")]
        public DoraDisplayMode displayMode = DoraDisplayMode.GroundLight3D;

        [Tooltip("グランドライトのオブジェクト（シーン上のオブジェクト、またはプレハブを指定）")]
        [SerializeField] private GameObject groundLightObject;

        [Tooltip("2D表示の際の背景パネル（3D表示の時は非表示にします）")]
        [SerializeField] private GameObject doraPanelObject;

        private List<GameObject> activeDoraTiles = new List<GameObject>();
        private int currentDoraId = -1;
        private GameObject currentCyberEffectInstance;

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
                Hide();
                return;
            }

            // スクリプトがアタッチされている大元は常にアクティブにしておく
            gameObject.SetActive(true);

            if (displayMode == DoraDisplayMode.Canvas2D)
            {
                // 2Dパネルを表示
                if (doraPanelObject != null) doraPanelObject.SetActive(true);

                if (tilePrefab != null && doraContainer != null)
                {
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
                
                SetCyberEffectActive(false, -1);
            }
            else if (displayMode == DoraDisplayMode.GroundLight3D)
            {
                // 3D表示の時は、2Dのドラ表示パネルを非表示にする
                if (doraPanelObject != null) doraPanelObject.SetActive(false);

                // Canvas側には何も作らず、3Dエフェクトだけを表示
                SetCyberEffectActive(true, doraId);
            }
        }

        private void SetCyberEffectActive(bool isActive, int doraId)
        {
            // インスペクターで設定されている場合の処理
            if (currentCyberEffectInstance == null && groundLightObject != null)
            {
                // プレハブがアサインされている場合は生成、シーン上のオブジェクトならそのまま使う
                if (groundLightObject.scene.rootCount == 0)
                {
                    currentCyberEffectInstance = Instantiate(groundLightObject);
                    currentCyberEffectInstance.name = "DoraCyberEffect_MCP";
                }
                else
                {
                    currentCyberEffectInstance = groundLightObject;
                }
            }

            // それでも見つからない場合（古い設定との後方互換）はシーンから名前検索する
            if (currentCyberEffectInstance == null)
            {
                currentCyberEffectInstance = GameObject.Find("DoraCyberEffect_MCP");
                if (currentCyberEffectInstance == null)
                {
                    var allObjs = Resources.FindObjectsOfTypeAll<GameObject>();
                    foreach (var go in allObjs)
                    {
                        if (go.name == "DoraCyberEffect_MCP" && go.scene.isLoaded)
                        {
                            currentCyberEffectInstance = go;
                            break;
                        }
                    }
                }
            }

            if (currentCyberEffectInstance != null)
            {
                currentCyberEffectInstance.SetActive(isActive);
                
                if (isActive && doraId >= 0 && tileResourceManager != null)
                {
                    Transform tileTrans = currentCyberEffectInstance.transform.Find("DoraTile");
                    if (tileTrans != null)
                    {
                        var visual = tileTrans.GetComponent<TileVisual>();
                        if (visual == null) visual = tileTrans.gameObject.AddComponent<TileVisual>();
                        visual.SetTile(doraId, tileResourceManager.GetTileSprite(doraId));
                    }
                }
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

            SetCyberEffectActive(false, -1);
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
