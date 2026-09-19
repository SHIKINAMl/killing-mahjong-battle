using UnityEngine;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 対局画面とチュートリアル画面に空気の層を敷く（2026-09-17 のユーザー指示
    /// 「画面に邪魔にならないような感じで空気層を表現したい」）。
    ///
    /// 中身は部屋の待機画面と同じ <see cref="SceneAtmosphere"/>。違うのは
    /// **置く高さと濃さ**だけなので、絵の作り方はあちらに一本化してある。
    ///
    /// **この画面は Canvas が何十枚も重なっている。** 背景・卓・牌・数字・セリフが
    /// それぞれ別の Canvas で、`sortingOrder` だけが上下を決めている。
    /// だから全画面に1枚かぶせると、**体力や賭け金の数字まで一緒に暗くなる。**
    /// </summary>
    public static class BattleAtmosphere
    {
        /// <summary>
        /// 空気層を差し込む高さ。**読む物より下、見る物より上。**
        ///
        /// 実際に並んでいる順（2026-09-17 に実機で数えた）:
        ///   -3 背景 / -1 麻雀卓・敵情報 / 0 河・壁・ロンボタン・賭け /
        ///    1 手牌・チュートリアル / 2 ロン演出 / 3 敵の手牌
        ///   → ここ(4) ←
        ///    7 摸打確認 / 10 自分の体力 / 14 場の血 / 15 能力 / 16 セリフ /
        ///   18 ボルテージ / 19 フェイズ切替 / 20 メニュー / 55 役一覧・合成 /
        ///   60 チュートリアルの覆い / 91 鼓動 / 99 カーソル
        ///
        /// つまり**卓・牌・キャラには空気がかかり、数字とセリフは素のまま**になる。
        /// 上げすぎると読めなくなり、下げすぎると牌だけ浮いて見える。
        /// </summary>
        private const int SortingOrder = 4;

        /// <summary>
        /// 四隅の暗さ。
        ///
        /// 最初 0.26 にしていたが、**弱すぎて言われないと気づかなかった**
        /// （四隅で -7.4階調 = -7%。中心は 107.2→107.0 とほぼ無変化）。
        /// 0.26 / 0.45 / 0.65 を並べて見比べ、2026-09-18 に 0.45 へ上げた。
        /// 0.65 まで行くと四隅の河や手牌の端が沈んで、牌が見づらくなる。
        /// </summary>
        private const float Vignette = 0.45f;

        /// <summary>ざらつきの濃さ。牌の柄と喧嘩しないよう部屋(0.07)より控えめ。</summary>
        private const float Grain = 0.06f;

        /// <summary>
        /// 粒の明るさの中心。**対局画面の地の明るさに合わせる。**
        ///
        /// 画面ぜんぶの平均を実際に測ったら 255階調中 92 だったので 92/255 = 0.36。
        /// 当てずっぽうで 0.20 を入れていたころは、粒が画面全体を
        /// 1.2階調ぶん暗くしていた（濃くするほどこのズレも大きくなる）。
        /// ここを白(0.5)にすると、逆に画面全体が白っぽく浮く。
        /// </summary>
        private const float GrainMean = 0.36f;

        private static SceneAtmosphere _instance;

        /// <summary>
        /// まだ無ければ作る。**何度呼んでもよい。**
        /// 対局の開始経路が複数あるので、作る側ではなく使う側から呼ぶ。
        /// </summary>
        public static void EnsureCreated()
        {
            if (_instance != null) return;

            var go = new GameObject("BattleAtmosphere", typeof(RectTransform), typeof(Canvas));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            // **`GraphicRaycaster` は付けない。** 付けると全画面の板が
            // クリックを拾いにいく。子の `raycastTarget` も切ってあるが、
            // そもそも受け口を作らないほうが確実。

            _instance = SceneAtmosphere.Attach(go.transform, Vignette, Grain, GrainMean);
        }

        /// <summary>出し入れ。演出中に一時的に消したくなったとき用。</summary>
        public static void SetVisible(bool visible)
        {
            if (_instance == null) return;
            _instance.transform.parent.gameObject.SetActive(visible);
        }
    }
}
