using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 手番の側の体力表示の後ろに敷く、少し大きい影絵。黒と持ち主の色を行き来して脈打つ。
    ///
    /// 「YOUR TURN / ENEMY TURN」の文字は同じ位置に同じ大きさで出るため、
    /// 英字を読むまでどちらの番か分からなかった。**位置そのものを手がかりにする**のがこれ。
    /// 相手の番は点滴が赤く、自分の番はスマホが青く縁取られる。
    ///
    /// **枠線ではなく元の絵の複製を使う。** スマホも点滴も `FloatingAnimator` で
    /// 揺れているため、固定した矩形の枠だと絵とずれる。絵と同じ親の下に置けば
    /// 揺れにそのまま追従する。血袋の形も絵そのものなので、実測した矩形も要らない。
    ///
    /// 専用の画像は使わず実行時に組み立てる。**対局シーンが2つ（UIテストシーン /
    /// OpeningScene）あるので、調整値は SerializeField にせずここの定数で持つ。**
    /// </summary>
    public class TurnGlow : MonoBehaviour
    {
        // ---- 調整値（シーンではなくここを触る）----

        /// <summary>元の絵に対する拡大率。縁取りの太さになる</summary>
        private const float ScaleUp = 1.10f;

        /// <summary>脈の速さ。TurnIndicatorUI の明滅(3.0)と揃えてある</summary>
        private const float PulseSpeed = 3.0f;

        private static readonly Color SelfColor = new Color32(70, 150, 255, 255);
        private static readonly Color EnemyColor = new Color32(235, 45, 40, 255);

        /// <summary>行き来する片方の色。真っ黒だと背景に沈むので少しだけ浮かせる</summary>
        private static readonly Color DarkColor = new Color(0.02f, 0.02f, 0.04f, 1f);

        private Image[] _images;
        private Color _tint;

        /// <summary>
        /// 影絵を作る。すでに作ってあればそれを返す。
        /// </summary>
        /// <param name="container">FloatingAnimator が付いた枠（HPPanel / EnemyPanel）</param>
        /// <param name="isSelf">自分側なら青、相手側なら赤</param>
        public static TurnGlow Attach(RectTransform container, bool isSelf)
        {
            if (container == null) return null;

            var existing = container.Find("TurnGlow");
            if (existing != null) return existing.GetComponent<TurnGlow>();

            Image cover = FindCover(container);
            if (cover == null)
            {
                Debug.LogWarning("[TurnGlow] 影絵にする絵が見つかりません: " + container.name);
                return null;
            }

            // container の直下まで遡る。影絵は container の最初の子として敷くので、
            // 位置合わせの基準も container 直下の要素に揃える必要がある。
            // （相手側は EnemyPanel > メーター > cover の入れ子。メーターと cover は
            //   同じ 100x320 なので、メーターの値をそのまま使ってよい）
            RectTransform src = cover.rectTransform;
            while (src != null && src.parent != container) src = src.parent as RectTransform;
            if (src == null) src = cover.rectTransform;

            // **1枚の絵だけを複製しても影絵にならない。** 体力表示は「本体＋ハート＋手」
            // のように複数の絵の重なりでできており、そのうち1枚を後ろに敷いても
            // 元の絵とは形も位置も合わない（実際 HPUCOVER 1枚では何も見えなかった）。
            // まるごと複製して全部を単色に塗り、影絵にする。
            var clone = Instantiate(src.gameObject, src.parent);
            clone.name = "TurnGlow";

            // スクリプト・テキスト・Canvas を落として、絵だけの抜け殻にする。
            // 残すと元の要素と二重に動いたり、数字が影絵の中に出たりする
            foreach (var c in clone.GetComponentsInChildren<Component>(true))
            {
                if (c is RectTransform || c is Transform || c is CanvasRenderer || c is Image) continue;
                Destroy(c);
            }

            var images = clone.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                img.color = DarkColor;
                img.raycastTarget = false;   // 盤面の操作を邪魔しない
            }

            var rt = clone.GetComponent<RectTransform>();
            // 元の要素の直前へ入れる。UGUI は階層順に描くので、これで後ろに回る
            rt.SetSiblingIndex(src.GetSiblingIndex());
            rt.localScale = src.localScale * ScaleUp;

            var glow = clone.AddComponent<TurnGlow>();
            glow._images = images;
            glow._tint = isSelf ? SelfColor : EnemyColor;

            clone.SetActive(false);
            return glow;
        }

        /// <summary>
        /// いちばん外側の絵を探す。スマホは HPUCOVER、点滴は HPCOVER という名前で
        /// どちらも輪郭を持っている。見つからなければ、いちばん面積の大きい絵で代用する。
        /// </summary>
        private static Image FindCover(RectTransform container)
        {
            Image fallback = null;
            float maxArea = -1f;

            foreach (var img in container.GetComponentsInChildren<Image>(true))
            {
                if (img.sprite == null) continue;
                if (img.name == "TurnGlow") continue;

                if (img.sprite.name.IndexOf("COVER", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return img;
                }

                var size = img.rectTransform.sizeDelta;
                float area = size.x * size.y;
                if (area > maxArea)
                {
                    maxArea = area;
                    fallback = img;
                }
            }
            return fallback;
        }

        /// <summary>手番かどうか。true の間だけ脈打つ。</summary>
        public void SetOn(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        private void Update()
        {
            if (_images == null) return;

            float t = (Mathf.Sin(Time.time * PulseSpeed) + 1f) * 0.5f;
            var c = Color.Lerp(DarkColor, _tint, t);
            for (int i = 0; i < _images.Length; i++)
            {
                if (_images[i] != null) _images[i].color = c;
            }
        }
    }
}
