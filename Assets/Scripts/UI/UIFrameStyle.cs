using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>操作の意味ではなく、フレームの見た目だけを定義する追加コンテンツ。</summary>
    [CreateAssetMenu(menuName = "じゃんぱいあ/UIフレーム", fileName = "FrameStyle")]
    public sealed class UIFrameStyle : ScriptableObject
    {
        [SerializeField] private string id = UIFrameCatalog.DefaultId;
        [SerializeField] private string displayName = "標準ドット枠";
        public string Id => id;
        public string DisplayName => displayName;
        public Color hoverColor = new Color32(255, 244, 214, 255);
        public Color selectedColor = new Color32(255, 211, 77, 255);
        public Color edgeColor = new Color32(36, 24, 34, 255);
        public Color pressedColor = new Color(.14f, .07f, .12f, .30f);
        [Min(1)] public float hoverWidth = 1;
        [Min(1)] public float tileHoverWidth = 2;
        [Min(1)] public float selectedWidth = 2;
        [Tooltip("任意の9スライス枠。未指定なら共通のドット描画を使う。入力矩形は変更しない。")]
        public Sprite hoverSprite;
        public Sprite selectedSprite;
        [Header("役条件の枠")]
        public Color panelBackground = new Color32(37, 27, 35, 238);
        public Color panelBorder = new Color32(239, 238, 216, 255);
        public Color panelText = new Color32(239, 238, 216, 255);
        [Min(0)] public float panelSkew = 14;
        [Min(1)] public float panelBorderWidth = 2;
    }
}
