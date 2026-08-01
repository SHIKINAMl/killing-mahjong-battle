using UnityEngine;
using UnityEngine.UI; // For Image if UI

namespace KillingMahjong.UI
{
    public class TileVisual : MonoBehaviour
    {
        [Header("Visual Components (Assign one)")]
        [SerializeField] private SpriteRenderer spriteRenderer; // 2D Sprite
        [SerializeField] private Image uiImage;                 // UI Image
        [SerializeField] private MeshRenderer meshRenderer;     // 3D Object (Cube/Quad)

        [Header("Dora Overlay")]
        [Tooltip("ドラ牌の時に表示するオーバーレイImage（子オブジェクトのImageを指定）")]
        [SerializeField] private Image doraOverlayImage;

        [Header("Exposed Overlay")]
        [Tooltip("透視されている時に表示するオーバーレイImage（子オブジェクトのImageを指定）")]
        [SerializeField] private Image exposedOverlayImage;

        [Header("Furiten Alert Overlay")]
        [Tooltip("フリテン（待ち牌と同じ牌）の時に表示するアラートImage")]
        [SerializeField] private Image alertOverlayImage;

        private void OnValidate()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (uiImage == null) uiImage = GetComponent<Image>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        }

        private void Awake()
        {
            // Runtime fallback if not assigned in Inspector
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (uiImage == null) uiImage = GetComponent<Image>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            
            // デフォルトでアウトラインはオフにする（Prefab設定漏れ対策）
            SetFuritenHighlight(false);
            SetHoverHighlight(false);
            SetExposed(false);
        }

        private int _currentId = -1;

        public void SetTile(int encodedId, Sprite sprite, TileResourceManager resourceManager = null)
        {
            _currentId = encodedId;

            if (sprite != null)
            {
                if (spriteRenderer != null)    spriteRenderer.sprite = sprite;
                else if (uiImage != null)      uiImage.sprite = sprite;
                else if (meshRenderer != null) meshRenderer.material.mainTexture = sprite.texture;
            }

            // ドラ枠オーバーレイはスプライトの有無に関わらず常に更新する（プール再利用時の状態リーク防止）
            bool isDoraTile = sprite != null && encodedId >= 0 && (resourceManager != null
                ? resourceManager.IsDora(encodedId)
                : new TileData(encodedId).IsDora || new TileData(encodedId).IsRedDora);

            if (doraOverlayImage != null)
            {
                doraOverlayImage.gameObject.SetActive(isDoraTile);
            }

            SetDoraShine(isDoraTile);
        }

        /// <summary>
        /// ドラ牌のきらめきを入り切りする。
        /// 牌はプールで使い回されるので、ドラでなくなったら必ず止めること。
        /// </summary>
        private void SetDoraShine(bool on)
        {
            var shine = GetComponent<DoraShine>();
            if (shine == null)
            {
                if (!on) return;              // 要らないなら作らない
                shine = gameObject.AddComponent<DoraShine>();
            }
            shine.enabled = on;
        }
        
        public int GetId() => _currentId;

        public void SetExposed(bool isExposed)
        {
            if (isExposed)
            {
                Debug.Log($"[TileVisual] SetExposed true for Tile ID: {_currentId}");
            }
            if (exposedOverlayImage != null)
            {
                exposedOverlayImage.gameObject.SetActive(isExposed);
                if (isExposed)
                {
                    exposedOverlayImage.transform.SetAsLastSibling();
                    exposedOverlayImage.color = new Color(1f, 1f, 1f, 1f);
                }
            }
            else if (isExposed)
            {
                Debug.LogWarning($"[TileVisual] exposedOverlayImage is NULL for Tile ID: {_currentId}. Did you assign it in the Inspector?");
            }
            UpdateOverlayPositions();
        }

        private bool _isFuriten = false;
        private bool _isHovered = false;

        public void SetFuritenHighlight(bool isFuriten)
        {
            _isFuriten = isFuriten;
            UpdateOutline(); // 既存の赤枠アウトラインは維持（ホバー時など）
            
            if (spriteRenderer != null)
            {
                if (_isFuriten) spriteRenderer.color = Color.red;
                else spriteRenderer.color = Color.white;
            }

            // 新たに追加した「アラート画像」の表示処理
            if (_isFuriten)
            {
                if (alertOverlayImage == null)
                {
                    // フォールバック: Inspectorで未設定の場合、動的に生成
                    GameObject obj = new GameObject("FuritenAlertOverlay");
                    obj.transform.SetParent(this.transform, false);
                    alertOverlayImage = obj.AddComponent<Image>();
                    alertOverlayImage.color = new Color(1f, 0f, 0f, 0.4f); 
                    RectTransform rt = alertOverlayImage.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(50, 50);
                    rt.anchoredPosition = Vector2.zero;

                    GameObject textObj = new GameObject("FuritenText");
                    textObj.transform.SetParent(obj.transform, false);
                    var text = textObj.AddComponent<UnityEngine.UI.Text>();
                    text.text = "⚠";
                    text.fontSize = (int)KillingMahjong.Common.UITypography.BodyLarge;
                    text.alignment = TextAnchor.MiddleCenter;
                    text.color = Color.white;
                    var textRt = textObj.GetComponent<RectTransform>();
                    textRt.anchorMin = Vector2.zero;
                    textRt.anchorMax = Vector2.one;
                    textRt.sizeDelta = Vector2.zero;
                    textRt.anchoredPosition = Vector2.zero;
                }

                alertOverlayImage.gameObject.SetActive(true);
                alertOverlayImage.transform.SetAsLastSibling();
            }
            else
            {
                if (alertOverlayImage != null)
                {
                    alertOverlayImage.gameObject.SetActive(false);
                }
            }
            UpdateOverlayPositions();
        }

        private void UpdateOverlayPositions()
        {
            bool hasExposed = exposedOverlayImage != null && exposedOverlayImage.gameObject.activeSelf;
            bool hasAlert = alertOverlayImage != null && alertOverlayImage.gameObject.activeSelf;

            float yPos = 15f; // 牌の上部付近により近づける（元は40fで高すぎた）
            float xOffset = 12f; // 並べた時の中心からのズレ（30fだと離れすぎていたため狭める）

            if (hasExposed && hasAlert)
            {
                // 両方表示の場合は並べる
                var expRt = exposedOverlayImage.GetComponent<RectTransform>();
                if (expRt != null) expRt.anchoredPosition = new Vector2(-xOffset, yPos);

                var alertRt = alertOverlayImage.GetComponent<RectTransform>();
                if (alertRt != null) alertRt.anchoredPosition = new Vector2(xOffset, yPos);
            }
            else if (hasExposed)
            {
                // 透視マークのみ
                var expRt = exposedOverlayImage.GetComponent<RectTransform>();
                if (expRt != null) expRt.anchoredPosition = new Vector2(0f, yPos);
            }
            else if (hasAlert)
            {
                // アラートのみ
                var alertRt = alertOverlayImage.GetComponent<RectTransform>();
                if (alertRt != null) alertRt.anchoredPosition = new Vector2(0f, yPos);
            }
        }

        public void SetHoverHighlight(bool isHovered)
        {
            _isHovered = isHovered;
            UpdateOutline();
        }

        private void UpdateOutline()
        {
            if (uiImage != null)
            {
                var outline = uiImage.GetComponent<UnityEngine.UI.Outline>();
                if (_isFuriten)
                {
                    if (outline == null)
                    {
                        outline = uiImage.gameObject.AddComponent<UnityEngine.UI.Outline>();
                        outline.effectDistance = new Vector2(3, 3);
                    }
                    outline.effectColor = Color.red; // フリテン時のみ赤枠
                    outline.enabled = true;
                }
                else
                {
                    if (outline != null) outline.enabled = false;
                }
            }
        }

        public void SetAlpha(float alpha)
        {
            if (uiImage != null)
            {
                var c = uiImage.color;
                c.a = alpha;
                uiImage.color = c;
            }
            else if (spriteRenderer != null)
            {
                var c = spriteRenderer.color;
                c.a = alpha;
                spriteRenderer.color = c;
            }
        }
    }
}
