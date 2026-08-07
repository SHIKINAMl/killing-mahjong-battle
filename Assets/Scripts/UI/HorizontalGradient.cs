using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// Image / Text の頂点色を左右で変えて横グラデーションにする。
    ///
    /// Unity の Image 単体ではグラデーションが作れないため、
    /// タイトル画面の「右へ行くほど暗くする幕」に使っている。
    /// 画像アセットを増やさずに済み、色をインスペクタで詰められる。
    ///
    /// 頂点が4つしかない普通の Image が対象。Sliced / Tiled のように
    /// 頂点が増える描画では中間の頂点が補間されないので、Simple のまま使うこと。
    /// </summary>
    [AddComponentMenu("UI/Effects/Horizontal Gradient")]
    [RequireComponent(typeof(Graphic))]
    public class HorizontalGradient : BaseMeshEffect
    {
        [SerializeField] public Color left = new Color(0f, 0f, 0f, 0f);
        [SerializeField] public Color right = new Color(0f, 0f, 0f, 0.8f);

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            // 左右の端を知るために、まず全頂点の x の範囲を取る
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            var vert = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                if (vert.position.x < minX) minX = vert.position.x;
                if (vert.position.x > maxX) maxX = vert.position.x;
            }

            float width = maxX - minX;
            if (width <= Mathf.Epsilon) return;

            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                float t = (vert.position.x - minX) / width;
                vert.color = Color.Lerp(left, right, t);
                vh.SetUIVertex(vert, i);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (graphic != null) graphic.SetVerticesDirty();
        }
#endif
    }
}
