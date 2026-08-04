using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 自分の手番の間だけ、画面のふちを持ち主の色で光らせる。
    ///
    /// 体力表示の影絵（TurnGlow）だけでは気づきにくい、という指摘への対応。
    /// **画面のどこを見ていても視界に入る**のがこれの狙い。
    ///
    /// 瀕死時の赤ビネット（HeartbeatEffect / sortingOrder 91）とは重なりうるが、
    /// **あちらの方が手前に出る**ようにしてある（こちらは 89）。
    /// 危険の表示が手番の表示に負けてはいけないため。
    /// 見た目も競合しにくいよう、こちらは細く鮮明な縁、あちらは広く淡い霧、と質を分けている。
    ///
    /// 専用の画像は使わず実行時に組み立てる。**対局シーンが2つ（UIテストシーン /
    /// OpeningScene）あるので、調整値は SerializeField にせずここの定数で持つ。**
    /// </summary>
    public class TurnVignette : MonoBehaviour
    {
        // ---- 調整値（シーンではなくここを触る）----

        /// <summary>ふちの太さ。画面の短辺に対する割合</summary>
        private const float ThicknessRatio = 0.065f;

        /// <summary>脈の速さ。TurnIndicatorUI の明滅(3.0)と揃えてある</summary>
        private const float PulseSpeed = 3.0f;

        /// <summary>いちばん薄いときの濃さ</summary>
        private const float MinAlpha = 0.18f;

        /// <summary>
        /// いちばん濃いときの濃さ。
        /// **四隅では縦帯と横帯が重なって実効的に濃くなる**ので、見た目の上限はこの値より上になる。
        /// 0.85 まで上げたら盤面の隅が沈んだため落とした。
        /// </summary>
        private const float MaxAlpha = 0.55f;

        private static readonly Color SelfColor = new Color32(70, 150, 255, 255);
        private static readonly Color EnemyColor = new Color32(235, 45, 40, 255);

        private Graphic[] _edges;
        private Color _tint;

        private static TurnVignette _instance;

        /// <summary>
        /// 画面のふちを作る。すでに作ってあればそれを返す。
        /// 盤面のどの UI にもぶら下げず、専用の Canvas を1枚持つ。
        /// </summary>
        public static TurnVignette Ensure()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("TurnVignette", typeof(RectTransform));
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UISortingOrders.TurnVignette;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800, 600);
            scaler.matchWidthOrHeight = 0f;

            // GraphicRaycaster は付けない。付けると画面全体のクリックを吸って
            // 牌もボタンも押せなくなる

            _instance = go.AddComponent<TurnVignette>();
            _instance.Build();
            go.SetActive(false);
            return _instance;
        }

        private void Build()
        {
            float shortSide = 600f; // referenceResolution の短辺
            float t = shortSide * ThicknessRatio;

            _edges = new Graphic[4];
            // 外側が濃く、内側へ向かって消える。4辺を貼り合わせて額縁にする
            _edges[0] = CreateEdge("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -t), EdgeDir.Down);
            _edges[1] = CreateEdge("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, t), EdgeDir.Up);
            _edges[2] = CreateEdge("Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(t, 0f), EdgeDir.Right);
            _edges[3] = CreateEdge("Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-t, 0f), EdgeDir.Left);
        }

        private enum EdgeDir { Up, Down, Left, Right }

        private Graphic CreateEdge(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 extend, EdgeDir fadeToward)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(Mathf.Min(0f, extend.x), Mathf.Min(0f, extend.y));
            rt.offsetMax = new Vector2(Mathf.Max(0f, extend.x), Mathf.Max(0f, extend.y));

            var img = go.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = false;

            var grad = go.AddComponent<EdgeGradient>();
            grad.fadeToward = (int)fadeToward;
            return img;
        }

        /// <summary>手番かどうか。true の間だけ光る。</summary>
        public void SetOn(bool on, bool isSelf)
        {
            _tint = isSelf ? SelfColor : EnemyColor;
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        private void Update()
        {
            if (_edges == null) return;

            float t = (Mathf.Sin(Time.unscaledTime * PulseSpeed) + 1f) * 0.5f;
            var c = _tint;
            c.a = Mathf.Lerp(MinAlpha, MaxAlpha, t);
            for (int i = 0; i < _edges.Length; i++)
            {
                if (_edges[i] != null) _edges[i].color = c;
            }
        }
    }

    /// <summary>
    /// 1辺ぶんの帯を、指定した向きへ向かって透明にする。
    ///
    /// `HorizontalGradient` は左右にしか効かないので、4辺で使えるよう向きを持たせた版。
    /// Image 単体ではグラデーションが作れないため、頂点色を書き換えている。
    /// 画像アセットを増やさずに済む。
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class EdgeGradient : BaseMeshEffect
    {
        /// <summary>TurnVignette.EdgeDir と同じ並び（0=Up 1=Down 2=Left 3=Right）</summary>
        public int fadeToward;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            var vert = new UIVertex();

            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                if (vert.position.x < minX) minX = vert.position.x;
                if (vert.position.x > maxX) maxX = vert.position.x;
                if (vert.position.y < minY) minY = vert.position.y;
                if (vert.position.y > maxY) maxY = vert.position.y;
            }

            float w = maxX - minX;
            float h = maxY - minY;
            if (w <= Mathf.Epsilon || h <= Mathf.Epsilon) return;

            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vert, i);

                // t = 0 で元の色、1 で透明
                float t;
                switch (fadeToward)
                {
                    case 0:  t = (vert.position.y - minY) / h; break;        // Up へ消える
                    case 1:  t = 1f - (vert.position.y - minY) / h; break;   // Down へ消える
                    case 2:  t = (vert.position.x - minX) / w; break;        // Left へ消える
                    default: t = 1f - (vert.position.x - minX) / w; break;   // Right へ消える
                }

                var c = vert.color;
                c.a = (byte)Mathf.Clamp(c.a * (1f - t), 0f, 255f);
                vert.color = c;
                vh.SetUIVertex(vert, i);
            }
        }
    }
}
