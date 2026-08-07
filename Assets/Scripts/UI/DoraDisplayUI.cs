using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.Common;

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

        [Tooltip("卓上の「ドラ」ラベル。未設定なら名前 DoraText でシーンから検索する")]
        [SerializeField] private GameObject doraLabelObject;

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

                    var visual = obj.GetComponent<TileVisual>();
                    if (visual != null && tileResourceManager != null)
                    {
                        visual.SetTile(doraId, tileResourceManager.GetTileSprite(doraId));
                    }

                    // UIEffecterのみでシンプルにサイバー（ホログラム）風の調整を行う
                    var uiEffect = obj.GetComponent<Coffee.UIEffects.UIEffect>();
                    if (uiEffect == null) uiEffect = obj.AddComponent<Coffee.UIEffects.UIEffect>();
                    
                    // 虹色（RgbShift）や飛び跳ね（座標移動）をなくし、シンプルに青白く光らせる
                    uiEffect.samplingFilter = Coffee.UIEffects.SamplingFilter.BlurFast;
                    uiEffect.samplingIntensity = 0.2f;
                    uiEffect.colorFilter = Coffee.UIEffects.ColorFilter.Additive;
                    uiEffect.color = new Color(0f, 0.8f, 1f, 1f); // シアン
                    uiEffect.colorIntensity = 0.6f;
                    uiEffect.colorGlow = true;

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
            SetDoraLabelActive(isActive);

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
                        GameObject tileObj = tileTrans.gameObject;

                        // ----- 3D Mesh/Sprite を WorldSpace Canvas + Image に動的変換 -----
                        var meshRenderer = tileObj.GetComponent<MeshRenderer>();
                        var spriteRenderer = tileObj.GetComponent<SpriteRenderer>();
                        
                        if (meshRenderer != null || spriteRenderer != null)
                        {
                            if (meshRenderer != null) DestroyImmediate(meshRenderer);
                            if (spriteRenderer != null) DestroyImmediate(spriteRenderer);
                            
                            var meshFilter = tileObj.GetComponent<MeshFilter>();
                            if (meshFilter != null) DestroyImmediate(meshFilter);

                            // Canvasを追加すると元のTransformがRectTransformに置換（Destroy）されるため、
                            // 以降は tileTrans ではなく tileObj を使用する
                            var canvas = tileObj.AddComponent<Canvas>();
                            canvas.renderMode = RenderMode.WorldSpace;
                            canvas.sortingOrder = UISortingOrders.DoraTile;

                            var rt = tileObj.GetComponent<RectTransform>();
                            if (rt == null) rt = tileObj.AddComponent<RectTransform>();
                            
                            // Imageを追加
                            tileObj.AddComponent<UnityEngine.UI.Image>();

                            // TileVisualが古いキャッシュを持っている場合があるので再付与してリセット
                            var oldVisual = tileObj.GetComponent<TileVisual>();
                            if (oldVisual != null) DestroyImmediate(oldVisual);
                        }
                        // -------------------------------------------------------------

                        var visual = tileObj.GetComponent<TileVisual>();
                        if (visual == null) visual = tileObj.AddComponent<TileVisual>();
                        visual.SetTile(doraId, tileResourceManager.GetTileSprite(doraId));

                        // SpriteRendererの本来の表示サイズ（PPU考慮済みのサイズ）にRectTransformを合わせる
                        var image = tileObj.GetComponent<UnityEngine.UI.Image>();
                        if (image != null && image.sprite != null)
                        {
                            var rt = tileObj.GetComponent<RectTransform>();
                            if (rt != null) rt.sizeDelta = new Vector2(image.sprite.bounds.size.x, image.sprite.bounds.size.y);
                        }

                        // 正常なCanvasコンポーネントになったので、UIEffectがエラーなく適用できる
                        var uiEffect = tileObj.GetComponent<Coffee.UIEffects.UIEffect>();
                        if (uiEffect == null) uiEffect = tileObj.AddComponent<Coffee.UIEffects.UIEffect>();

                        // ホログラム感を出すための設定
                        // ブラーと加算合成で発光させる
                        uiEffect.samplingFilter = Coffee.UIEffects.SamplingFilter.BlurFast;
                        uiEffect.samplingIntensity = 0.2f;
                        uiEffect.colorFilter = Coffee.UIEffects.ColorFilter.MultiplyAdditive;
                        uiEffect.color = new Color(0f, 0.8f, 1f, 1f); // シアン
                        uiEffect.colorIntensity = 0.8f;
                        uiEffect.colorGlow = true;

                        if (image != null)
                        {
                            image.color = new Color(1f, 1f, 1f, 0.7f);
                        }
                    }
                    
                    // グランドライトの土台（ProjectorBase）にも色の明滅だけを追加（形はうねらせない）
                    Transform projectorBase = currentCyberEffectInstance.transform.Find("ProjectorBase");
                    if (projectorBase != null)
                    {
                        var auroraBase = projectorBase.GetComponent<KillingMahjong.Visuals.AuroraLightAnimator>();
                        if (auroraBase == null) auroraBase = projectorBase.gameObject.AddComponent<KillingMahjong.Visuals.AuroraLightAnimator>();
                        auroraBase.animateScale = false; // 土台はスケールを変えない
                    }

                    // グランドライトの光（LightPillarParticle）にオーロラのような太さと揺らぎを追加
                    Transform lightPillar = currentCyberEffectInstance.transform.Find("LightPillarParticle");
                    if (lightPillar != null)
                    {
                        var aurora = lightPillar.GetComponent<KillingMahjong.Visuals.AuroraLightAnimator>();
                        if (aurora == null) aurora = lightPillar.gameObject.AddComponent<KillingMahjong.Visuals.AuroraLightAnimator>();
                        aurora.animateScale = true; // 光の柱は太さをうねらせる
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



        /// <summary>
        /// 卓上の「ドラ」ラベルの表示を切り替える。
        /// シーン直下に置かれていてインスペクター未設定のことがあるため、
        /// DoraCyberEffect_MCP と同じく名前検索のフォールバックを持つ。
        /// </summary>
        private void SetDoraLabelActive(bool isActive)
        {
            if (doraLabelObject == null)
            {
                doraLabelObject = GameObject.Find("DoraText");
                if (doraLabelObject == null)
                {
                    var allObjs = Resources.FindObjectsOfTypeAll<GameObject>();
                    foreach (var go in allObjs)
                    {
                        if (go.name == "DoraText" && go.scene.isLoaded)
                        {
                            doraLabelObject = go;
                            break;
                        }
                    }
                }
            }

            if (doraLabelObject != null) doraLabelObject.SetActive(isActive);
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
