using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>画面の上下から閉じるまぶた。中央の開口を少し湾曲させる。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class EyelidClosureGraphic : MaskableGraphic
    {
        private float amount;
        public float Amount
        {
            get { return amount; }
            set { amount = Mathf.Clamp01(value); SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            for (int i = 0; i < 48; i++)
            {
                float x0 = Mathf.Lerp(r.xMin, r.xMax, i / 48f);
                float x1 = Mathf.Lerp(r.xMin, r.xMax, (i + 1) / 48f);
                float h0 = Opening(i / 48f, r.height);
                float h1 = Opening((i + 1) / 48f, r.height);
                Quad(vh, x0, x1, r.center.y + h0, r.center.y + h1, r.yMax);
                Quad(vh, x0, x1, r.center.y - h0, r.center.y - h1, r.yMin);
            }
        }

        private float Opening(float x, float height)
        {
            float distance = x * 2f - 1f;
            return Mathf.Max(0f, height * .5f * (1f - amount)
                - height * .10f * Mathf.Sin(amount * Mathf.PI) * distance * distance);
        }

        private void Quad(VertexHelper vh, float x0, float x1, float y0, float y1, float edge)
        {
            int index = vh.currentVertCount;
            vh.AddVert(new Vector3(x0, edge), color, Vector2.zero);
            vh.AddVert(new Vector3(x1, edge), color, Vector2.zero);
            vh.AddVert(new Vector3(x1, y1), color, Vector2.zero);
            vh.AddVert(new Vector3(x0, y0), color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
