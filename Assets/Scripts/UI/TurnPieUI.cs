using UnityEngine;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 左上の打牌数を、文字（「後：三打目」）ではなく円グラフで見せる（2026-11-08 ユーザー指示
    /// 「左上の先攻、後攻は円グラフにしてみようかな。先攻、後攻は色で示す感じで」）。
    ///
    /// 1局で打てるのは1人 <see cref="DiscardsPerPlayer"/> 打。円を同じ数に切り、
    /// 打ち終えたぶんを12時から時計回りに塗る。これから打つ1つは明るい色で示す。
    /// **先攻か後攻かは塗りの色で分ける**（先攻＝金、後攻＝青）。
    ///
    /// ドット絵に合わせ、2単位の升目ごとに色を決めて並べる（滑らかな扇形は描かない）。
    /// 置き場所はシーン（RiverUI の turnPie）。編集中も見本の値で描くので、シーンで位置を合わせられる。
    /// </summary>
    public class TurnPieUI : PixelShapeGraphic
    {
        /// <summary>1人が1局で打てる数。サーバーの DISCARDS_PER_PLAYER（mahjong_engine/engine/game_engine.py）と同じ。</summary>
        public const int DiscardsPerPlayer = 17;

        [SerializeField] private float radius = 22f;
        [SerializeField] private Color firstColor = new Color32(232, 180, 64, 255);
        [SerializeField] private Color firstNextColor = new Color32(255, 232, 160, 255);
        [SerializeField] private Color secondColor = new Color32(72, 132, 220, 255);
        [SerializeField] private Color secondNextColor = new Color32(170, 210, 255, 255);
        [SerializeField] private Color emptyColor = new Color32(26, 14, 16, 220);
        [SerializeField] private Color edgeColor = new Color32(12, 6, 8, 255);

        // 編集中に描く見本（先攻で5打済み）
        private int _discarded = 5;
        private bool _first = true;

        /// <summary>打ち終えた数と、自分が先攻か。</summary>
        public void Show(int discarded, bool first)
        {
            _discarded = Mathf.Clamp(discarded, 0, DiscardsPerPlayer);
            _first = first;
            Redraw();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Redraw();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            Redraw();
        }
#endif

        private void Redraw()
        {
            const float cell = 2f; // 1ドット＝2単位
            float slice = 360f / DiscardsPerPlayer;
            Color done = _first ? firstColor : secondColor;
            Color next = _first ? firstNextColor : secondNextColor;
            float outer = radius + cell; // 縁の1ドット

            Begin();
            for (float y = -outer; y < outer; y += cell)
            {
                // 横に同じ色が続く升目はまとめて1枚にする
                float runStart = 0f;
                Color runColor = Color.clear;
                bool inRun = false;
                for (float x = -outer; x <= outer; x += cell)
                {
                    Color c = CellColor(x + cell * .5f, y + cell * .5f, slice, done, next);
                    if (inRun && c == runColor) continue;
                    if (inRun) Box(runStart, y, x, y + cell, runColor);
                    inRun = c.a > 0f;
                    runStart = x;
                    runColor = c;
                }
                if (inRun) Box(runStart, y, outer + cell, y + cell, runColor);
            }
            End();
        }

        private Color CellColor(float x, float y, float slice, Color done, Color next)
        {
            float d = Mathf.Sqrt(x * x + y * y);
            if (d > radius + 2f) return Color.clear;
            if (d > radius) return edgeColor;
            // 12時から時計回りの角度
            float angle = Mathf.Atan2(x, y) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            int index = Mathf.FloorToInt(angle / slice);
            if (index < _discarded) return done;
            if (index == _discarded) return next;
            return emptyColor;
        }
    }
}
