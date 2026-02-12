using UnityEngine;
using UnityEngine.UI; // For Image if UI
// If using SpriteRenderer for 2D/3D object
// using UnityEngine; 

namespace KillingMahjong.UI
{
    public class TileVisual : MonoBehaviour
    {
        [Header("Visual Components (Assign one)")]
        [SerializeField] private SpriteRenderer spriteRenderer; // 2D Sprite
        [SerializeField] private Image uiImage;                 // UI Image
        [SerializeField] private MeshRenderer meshRenderer;     // 3D Object (Cube/Quad)

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

        public void SetTile(int id, Sprite sprite)
        {
            _currentId = id;
            if (sprite == null) return;

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = sprite;
            }
            else if (uiImage != null)
            {
                uiImage.sprite = sprite;
            }
            else if (meshRenderer != null)
            {
                // For 3D objects, we set the texture.
                // Note: This modifies the shared material instance in Editor, or instance in runtime.
                // Allow Texture based on Sprite
                meshRenderer.material.mainTexture = sprite.texture;
            }
        }
        
        public int GetId() => _currentId;
    }
}
