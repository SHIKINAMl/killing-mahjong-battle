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
        }

        private int _currentId = -1;

        public void SetTile(int encodedId, Sprite sprite, TileResourceManager resourceManager = null)
        {
            _currentId = encodedId;
            if (sprite == null) return;

            if (spriteRenderer != null)        spriteRenderer.sprite = sprite;
            else if (uiImage != null)          uiImage.sprite = sprite;
            else if (meshRenderer != null)     meshRenderer.material.mainTexture = sprite.texture;

            // ドラ枠オーバーレイ
            if (doraOverlayImage != null)
            {
                bool isDora = resourceManager != null
                    ? resourceManager.IsDora(encodedId)
                    : new TileData(encodedId).IsDora || new TileData(encodedId).IsRedDora;
                doraOverlayImage.gameObject.SetActive(isDora);
            }
        }
        
        public int GetId() => _currentId;

        private bool _isFuriten = false;
        private bool _isHovered = false;

        public void SetFuritenHighlight(bool isFuriten)
        {
            _isFuriten = isFuriten;
            UpdateOutline();
            
            if (spriteRenderer != null)
            {
                if (_isFuriten) spriteRenderer.color = Color.red;
                else spriteRenderer.color = Color.white;
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
                if (_isFuriten || _isHovered)
                {
                    if (outline == null)
                    {
                        outline = uiImage.gameObject.AddComponent<UnityEngine.UI.Outline>();
                        outline.effectDistance = new Vector2(3, 3);
                    }
                    outline.effectColor = _isHovered ? Color.yellow : Color.red;
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
