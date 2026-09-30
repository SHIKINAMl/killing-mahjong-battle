using UnityEngine;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 立ち絵の**髪の黄色い部分だけ**を蛍光色に光らせる（2026-09-30 のユーザー指示）。
    ///
    /// 立ち絵と同じ絵をもう1枚、加算合成（<c>KillingMahjong/NeonKeyAdd</c>）で重ね、
    /// **髪の黄色に近い画素だけ**に光を足す。
    ///
    /// **絵を新しく作らない。** 元の立ち絵をそのまま使い、色で絞り込む。
    /// 髪だけを切り出した画像を別に用意する手もあるが、表情や体の差分が増えるたびに
    /// 作り直しになるうえ、絵が増えるとビルドにも載る。色で絞れば、
    /// スプライトが差し替わっても自動で付いてくる。
    ///
    /// 色は実際の絵（`tutorial_girl_body.png`, 3600x3600）から数えて決めた:
    ///
    ///   髪の黄色でいちばん多い色 … (240, 204, 63)
    ///   許容値 70（255階調）で 205,408 画素が一致し、**頭の範囲だけに収まる**
    ///   （x 1371-2378 / y 224-1203）。100 まで広げると肌まで拾って y=2337 まで伸びる
    ///
    /// **シーンには置かない。** 対局シーンが2つあるため（AGENTS.md §2）。
    /// </summary>
    public class HairNeonGlow : MonoBehaviour
    {
        /// <summary>髪の黄色。ここに近い画素だけが光る。</summary>
        private static readonly Color HairKeyColor = new Color32(240, 204, 63, 255);

        /// <summary>
        /// 一致とみなす距離（0〜1 に正規化した RGB 空間）。
        /// **255階調の 70 にあたる。** これ以上広げると肌まで光る。
        /// </summary>
        private const float HairTolerance = 70f / 255f;

        /// <summary>
        /// 光らせる色。**髪が黄色なので黄色で光らせる**（2026-09-30 のユーザー指示）。
        /// 白を混ぜすぎると蛍光色ではなく「白飛び」に見えるので、彩度を残した黄色にする。
        /// </summary>
        private static readonly Color NeonColor = new Color32(255, 233, 76, 255);

        /// <summary>
        /// 足す光の強さ。息をするようにこの幅で行き来する。
        /// **0.30〜0.55 では「少し明るい」程度で、蛍光色には見えなかった**（実機で確認）。
        /// </summary>
        private const float PowerMin = 0.60f;
        private const float PowerMax = 0.95f;

        /// <summary>明滅の速さ（1秒あたりの往復）。</summary>
        private const float PulsePerSecond = 0.45f;

        private SpriteRenderer _source;
        private SpriteRenderer _neon;
        private float _phase;

        /// <summary>
        /// <paramref name="source"/> の髪を光らせる。二重に呼んでも増えない。
        /// </summary>
        public static HairNeonGlow Attach(SpriteRenderer source)
        {
            if (!Application.isPlaying || source == null) return null;

            var existing = source.GetComponentInChildren<HairNeonGlow>(true);
            if (existing != null) return existing;

            var go = new GameObject("HairNeon", typeof(SpriteRenderer), typeof(HairNeonGlow));
            go.transform.SetParent(source.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var view = go.GetComponent<HairNeonGlow>();
            view.Build(source);
            return view;
        }

        private void Build(SpriteRenderer source)
        {
            _source = source;
            _neon = GetComponent<SpriteRenderer>();
            _neon.sharedMaterial = NeonMaterials.Create(NeonColor, HairKeyColor, HairTolerance);
            _phase = Random.Range(0f, Mathf.PI * 2f);
            ApplyPower((PowerMin + PowerMax) * 0.5f);
            SyncShape();
        }

        private void LateUpdate()
        {
            if (_source == null || _neon == null) { enabled = false; return; }

            SyncShape();

            _phase += Time.deltaTime * PulsePerSecond * Mathf.PI * 2f;
            float t = (Mathf.Sin(_phase) + 1f) * 0.5f;
            ApplyPower(Mathf.Lerp(PowerMin, PowerMax, t));
        }

        private void ApplyPower(float power)
        {
            // **元の絵の不透明度も掛ける。** 立ち絵はフェードインで薄く出てくるので、
            // これが無いと、まだ姿が見えないうちから髪だけ光って浮かび上がる
            float sourceAlpha = _source != null ? _source.color.a : 1f;
            _neon.color = new Color(1f, 1f, 1f, power * sourceAlpha);
        }

        /// <summary>
        /// 元の絵と同じスプライト・同じ並び順のすぐ手前にする。
        /// **毎フレーム写す。** 表情や姿勢でスプライトが差し替わるため。
        /// </summary>
        private void SyncShape()
        {
            _neon.sprite = _source.sprite;
            _neon.flipX = _source.flipX;
            _neon.flipY = _source.flipY;
            _neon.sortingLayerID = _source.sortingLayerID;
            _neon.sortingOrder = _source.sortingOrder + 1;
            _neon.enabled = _source.enabled && _source.sprite != null;
        }
    }
}
