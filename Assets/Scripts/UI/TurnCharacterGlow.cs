using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 相手の手番の間だけ、女の子（敵キャラ）自身を赤く染めて脈打たせる。
    ///
    /// 以前は画面のふちを光らせていた（TurnVignette）が、盤面が狭く見えるのでやめた。
    ///
    /// **体力表示の <see cref="TurnGlow"/> と同じ「後ろに影絵を敷いて縁を光らせる」方式は
    /// この立ち絵では使えない。** 実際に作って比べた結果:
    /// 立ち絵は画面の大半を占め、輪郭のほとんどが「黒髪と赤い壁の境」になる。
    /// 拡大率 1.03〜1.08 のどれでも縁は数pxにしかならず、赤い壁に埋もれてほぼ読めなかった
    /// （色を橙・淡赤に振っても同じ。紫だけは見えたが、敵＝赤という配色から外れる）。
    /// 体力表示であれが効くのは、小さくて暗い背景の上に載っているため。
    ///
    /// 絵そのものを染めれば背景に左右されない。SpriteRenderer.color は元の絵に乗算されるので、
    /// 白へ戻せば元通りになる。**元の色は Init で控えておき、消すときに必ず戻す。**
    ///
    /// 調整値は SerializeField にせずここの定数で持つ。
    /// **対局シーンが2つ（UIテストシーン / OpeningScene）あるため。**
    /// </summary>
    public class TurnCharacterGlow : MonoBehaviour
    {
        // ---- 調整値（シーンではなくここを触る）----

        /// <summary>
        /// いちばん濃いときの染め色。元の絵に乗算される。
        /// これより赤くする（緑青を 0.4 以下にする）と、絵の描き込みが潰れて別人になる。
        /// </summary>
        private static readonly Color TintColor = new Color(1f, 0.62f, 0.58f, 1f);

        /// <summary>脈の速さ。TurnIndicatorUI の明滅(3.0)と揃えてある</summary>
        private const float PulseSpeed = 3.0f;

        private SpriteRenderer[] _renderers;
        private Color[] _originals;
        private bool _on;

        /// <summary>
        /// 立ち絵に取り付ける。すでに付いていればそれを返す。
        /// </summary>
        /// <param name="body">立ち絵の本体（EnemyInfoUI の characterRenderer）</param>
        public static TurnCharacterGlow Attach(SpriteRenderer body)
        {
            if (body == null) return null;

            var existing = body.GetComponent<TurnCharacterGlow>();
            if (existing != null) return existing;

            var glow = body.gameObject.AddComponent<TurnCharacterGlow>();
            glow.Init(body);
            return glow;
        }

        private void Init(SpriteRenderer body)
        {
            // 顔は別のレイヤーなので、身体だけ染めると顔が浮く。子まで含めて全部染める
            _renderers = body.GetComponentsInChildren<SpriteRenderer>(true);
            _originals = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _originals[i] = _renderers[i].color;
            }
        }

        /// <summary>相手の手番かどうか。true の間だけ脈打つ。</summary>
        public void SetOn(bool on)
        {
            if (_on == on) return;
            _on = on;
            if (!on) Restore();
        }

        private void Restore()
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null) _renderers[i].color = _originals[i];
            }
        }

        private void Update()
        {
            if (!_on || _renderers == null) return;

            float t = (Mathf.Sin(Time.time * PulseSpeed) + 1f) * 0.5f;
            var mul = Color.Lerp(Color.white, TintColor, t);
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null) _renderers[i].color = _originals[i] * mul;
            }
        }

        /// <summary>消し忘れ防止。手番のまま画面が切り替わっても元の色に戻す</summary>
        private void OnDisable()
        {
            Restore();
        }
    }
}
