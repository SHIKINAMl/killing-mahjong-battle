using UnityEngine;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 表情や体の絵が差し替わる SpriteRenderer で、いま出ている絵に合うノーマルマップを KM/LitArt に渡す。
    /// 一覧に無い絵（白く光らせる絵など）のときは平らな面として照らす。
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(SpriteRenderer))]
    public sealed class LitArtNormalSwap : MonoBehaviour
    {
        [System.Serializable]
        public struct Pair
        {
            public Texture2D art;
            public Texture2D normal;
        }

        [SerializeField] private Pair[] pairs = new Pair[0];

        private static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");
        private SpriteRenderer spriteRenderer;
        private MaterialPropertyBlock block;
        private Texture currentArt;
        private bool applied;

        private void OnEnable()
        {
            applied = false;
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            var art = spriteRenderer.sprite != null ? spriteRenderer.sprite.texture : null;
            if (applied && art == currentArt) return;

            currentArt = art;
            applied = true;
            Texture normal = Texture2D.normalTexture;
            foreach (var pair in pairs)
            {
                if (pair.art != null && pair.art == art && pair.normal != null) { normal = pair.normal; break; }
            }

            block ??= new MaterialPropertyBlock();
            spriteRenderer.GetPropertyBlock(block);
            block.SetTexture(NormalMapId, normal);
            spriteRenderer.SetPropertyBlock(block);
        }
    }
}
