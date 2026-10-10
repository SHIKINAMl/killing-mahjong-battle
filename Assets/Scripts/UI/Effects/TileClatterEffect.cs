using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 待っているあいだ、牌をじゃらじゃらと鳴らす（2026-09-15 のユーザー指示）。
    ///
    /// **対戦相手を待っている画面が、文字だけで止まっていた。**
    /// 卓の上に牌を散らして置き、マウスで混ぜられるようにする。
    ///
    /// **音は鳴らさない。** 見た目だけで「じゃらじゃら」を出している。音を足すときは、
    /// マウスが牌を押した所（<see cref="ApplyHand"/>）か、牌どうしが押し合った所
    /// （<see cref="ResolveTileSeparation"/>）で鳴らすとよい。
    ///
    /// **シーンには置かない。** 対局シーンが2つあるので、置くと片方に入れ忘れる
    /// （`ScreenFlash` などと同じ理由）。待ち画面から呼んで作る。
    ///
    /// **動き方（2026-10-11 にユーザーの指示で作り直した）。**
    ///   ・牌は、自分が動かしたときだけ動く。ひとりでに揺れたり跳ねたりしない
    ///   ・動かしたあと、元の場所へ戻らない。止まった所に居る
    ///   ・牌はマウスを避ける。マウスの先の近くには居られず、外へ押し出される
    ///   ・卓の中だけを動く。卓の縁（と画面の端）から外へは出ない
    /// それまでは、常に小さく揺れ、ときどき1枚が跳ね、払っても元の山へばねで戻っていた。
    /// </summary>
    public class TileClatterEffect : MonoBehaviour
    {
        /// <summary>
        /// 手で混ぜるための裏牌の枚数。列ではなく小さな山にして、
        /// カーソルで払ったときに「じゃらじゃら」と崩れる量にする。
        /// </summary>
        private const int TileCount = 28;

        // **牌の絵は 320x320 の正方形**（`Assets/Resources/麻雀牌/`）。
        // 縦長の枠に入れると余白が出るので、枠も正方形にして
        // `preserveAspect` に任せる。
        private const float TileWidth = 42f;
        private const float TileHeight = 42f;

        // **卓の上へ広げる（2026-09-17 のユーザー指示）。**
        // 以前は 192x92 の小さな山だったので、卓の真ん中に固まって見えた。
        private const float PileWidth = 620f;
        private const float PileHeight = 200f;
        private const float PileHalfWidth = 250f;
        private const float PileHalfHeight = 72f;

        // --- マウスで押す ---

        /// <summary>
        /// マウスの先からこの距離より内側に、牌の中心は居られない（画面基準の単位）。
        /// 牌の半分（21）より大きくして、マウスが牌に触れる前に牌のほうがよけるようにする。
        /// </summary>
        private const float HandReach = 54f;

        /// <summary>入り込んだ牌を外へ押し出す速さの上限[単位/秒]。上限が無いと、一瞬で飛んで見える。</summary>
        private const float HandShoveSpeed = 1100f;

        /// <summary>静かに近づいたときでも、よけた牌が少し滑る速さ[単位/秒]。</summary>
        private const float HandNudgeSpeed = 60f;

        /// <summary>手の速さのうち、牌がもらう割合。払うと、そのぶん遠くへ滑る。</summary>
        private const float HandCarry = 0.6f;

        private const float HandSpeedCap = 1400f;

        /// <summary>横をかすめたときに牌が回る強さ[度/秒²]。</summary>
        private const float HandSpin = 1800f;

        /// <summary>滑りが止まる早さ。大きいほどすぐ止まる。</summary>
        private const float Damping = 4.5f;

        /// <summary>牌どうしがこれより近いと押し合う。牌の幅（42）より小さいので、少し重なって山に見える。</summary>
        private const float PairRadius = 30f;
        private const float PairSeparation = 780f;

        /// <summary>牌の傾きの上限[度]。ドット絵は大きく回すと崩れて見える。</summary>
        private const float MaxSpin = 24f;

        // --- 卓の形（2026-10-11 に待ち画面のスクリーンショットから測った） ---
        //
        // 座標は 800x600 の画面基準で、**画面の下端の真ん中が原点**、上が正。
        // 卓は台形で、上の辺は下から 208、そこから下へ行くほど左右に広がる（1 下がるごとに 1.414）。
        // 下から 58 より下では、卓の縁は画面の外へ出る。
        //
        //   実測（画面上端から y / 左端 / 右端）: 395 / 207 / 592、450 / 130 / 669、540 / 2 / 797
        //
        // 背景の絵を描き直して卓の形が変わったら、ここを測り直すこと。

        /// <summary>卓の上の辺の高さ。</summary>
        private const float TableTopY = 208f;

        /// <summary>卓の上の辺の、真ん中から端までの幅。</summary>
        private const float TableTopHalfWidth = 188f;

        /// <summary>1 下がるごとに、卓が片側へ広がる量。</summary>
        private const float TableSideSlope = 1.414f;

        /// <summary>画面の幅の半分。卓の縁が画面の外へ出たあとは、ここが端になる。</summary>
        private const float ScreenHalfWidth = 400f;

        /// <summary>牌の中心を、卓の上の辺からどれだけ内側に留めるか。牌の半分＋縁の線の太さ。</summary>
        private const float InsetTop = 30f;

        /// <summary>牌の中心を、卓の斜めの辺からどれだけ内側（横方向）に留めるか。</summary>
        private const float InsetSide = 46f;

        /// <summary>牌の中心を、画面の端からどれだけ内側に留めるか。</summary>
        private const float InsetScreen = 24f;

        private class Tile
        {
            public RectTransform Rect;
            /// <summary>最初に置いた場所。戻る先ではない（戻らない）。いまの場所は Home + Offset。</summary>
            public Vector2 Home;
            public float Phase;
            public Vector2 Offset;
            public Vector2 Velocity;
            public float Spin;
            public float SpinVelocity;
        }

        private readonly List<Tile> _tiles = new List<Tile>();
        private RectTransform _rect;
        private Canvas _canvas;
        private Vector2 _lastHandPosition;
        private Vector2 _handVelocity;
        private float _handSpeed01;
        private bool _hasHandPosition;

        /// <summary>
        /// 待ち画面へ牌を並べる。**すでに並べてあれば作り直さない。**
        /// `ShowWaiting` は待っているあいだ何度も呼ばれる。
        /// </summary>
        public static TileClatterEffect Attach(RectTransform parent, float offsetFromBottom)
        {
            if (parent == null) return null;

            var existing = parent.GetComponentInChildren<TileClatterEffect>(true);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);

                // **絵の種類が変わっていたら作り直す。** 使い回しのままだと
                // `Art` を切り替えても前の絵が出たままになる
                if (existing._builtArtGeneration != _artGeneration)
                {
                    existing.Rebuild();
                }
                else
                {
                    existing.RebuildTileListIfNeeded();
                }
                return existing;
            }

            var go = new GameObject("TileClatter", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            // **画面の下に貼る（2026-09-15 に直した）。**
            // 最初は待ち文字からの相対で置いたが、あの文字は 200x50 の枠に
            // 80pt を流し込んでいて**枠から大きくはみ出す**ため、
            // 文字を基準にすると必ず重なった。下端から測れば重ならない。
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, offsetFromBottom);
            rt.sizeDelta = new Vector2(PileWidth, PileHeight);

            var effect = go.AddComponent<TileClatterEffect>();
            effect.Build();
            return effect;
        }

        /// <summary>配牌完了など、演出の終わりで山を隠す。次回は作り直さず再利用する。</summary>
        public static void Hide(RectTransform parent)
        {
            if (parent == null) return;

            var existing = parent.GetComponentInChildren<TileClatterEffect>(true);
            if (existing != null) existing.gameObject.SetActive(false);
        }

        private void Awake()
        {
            _rect = transform as RectTransform;
            _canvas = GetComponentInParent<Canvas>();
        }

        private void OnEnable()
        {
            // 非表示のあいだのマウス位置を次回へ持ち込むと、一瞬で全牌が飛ぶ。
            _hasHandPosition = false;
            _handVelocity = Vector2.zero;
            _handSpeed01 = 0f;
        }

        private void Build()
        {
            // **1枚ずつ違う絵にする。** 同じ牌が並ぶと、卓に広げた感じが出ない
            Sprite[] faces = LoadTileSprites();
            var bag = new List<Sprite>(faces);
            for (int i = 0; i < TileCount; i++)
            {
                var go = new GameObject("Tile" + i, typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.sizeDelta = new Vector2(TileWidth, TileHeight);

                // 卓の中に散らして置く。**牌どうしが押し合わない間隔を空ける。**
                // 重ねて置くと、押し合いで牌がひとりでに動いてしまう（触るまで動かさない）
                Vector2 home = PickHome();
                rt.anchoredPosition = home;

                var img = go.GetComponent<Image>();
                img.raycastTarget = false;

                // 袋から1枚ずつ引く。空になったら詰め直す（枚数が絵の数を超えるため）
                if (bag.Count == 0) bag.AddRange(faces);
                if (bag.Count > 0)
                {
                    int pick = Random.Range(0, bag.Count);
                    img.sprite = bag[pick];
                    bag.RemoveAt(pick);
                    img.preserveAspect = true;
                }
                else
                {
                    // 絵が1枚も読めなかったときの保険
                    img.color = new Color32(238, 232, 214, 255);
                    KillingMahjong.Visuals.UIEdgeOutline.AddBehind(rt, 3f);
                }

                _tiles.Add(new Tile
                {
                    Rect = rt,
                    Home = home,
                    // **等間隔にずらす。** 乱数だと固まって、列が波に見えないことがある
                    Phase = i * 0.8f,
                    Offset = Vector2.zero,
                    Velocity = Vector2.zero,
                    Spin = Random.Range(-8f, 8f),
                    SpinVelocity = 0f,
                });
                rt.localRotation = Quaternion.Euler(0f, 0f, _tiles[_tiles.Count - 1].Spin);
            }

            _builtArtGeneration = _artGeneration;
        }

        /// <summary>
        /// 牌を置く場所を1つ決める。卓の中で、すでに置いた牌から離れた所。
        /// 何度か引いても空きが無ければ、いちばんましだった所にする。
        /// </summary>
        private Vector2 PickHome()
        {
            Vector2 origin = TableOrigin;
            Vector2 best = Vector2.zero;
            float bestGap = -1f;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                // 山の広がり（PileHalfWidth x PileHalfHeight）の中から引いて、卓の中へ収める
                Vector2 scatter = Random.insideUnitCircle;
                Vector2 candidate = ClampToTable(
                    new Vector2(scatter.x * PileHalfWidth, scatter.y * PileHalfHeight) + origin) - origin;

                float gap = float.MaxValue;
                for (int i = 0; i < _tiles.Count; i++)
                {
                    gap = Mathf.Min(gap, Vector2.Distance(candidate, _tiles[i].Home));
                }
                if (gap > bestGap) { bestGap = gap; best = candidate; }
                if (gap >= PairRadius + 2f) break;
            }
            return best;
        }

        /// <summary>
        /// 画面の下端の真ん中から見た、この山の原点の位置。
        /// 山は親の下端の真ん中に留めてあるので（<see cref="Attach"/>）、留めた位置がそのまま答えになる。
        /// </summary>
        private Vector2 TableOrigin
        {
            get { return _rect != null ? _rect.anchoredPosition : Vector2.zero; }
        }

        /// <summary>
        /// 点を卓の中へ収める。座標は画面基準（下端の真ん中が原点）。
        /// </summary>
        private static Vector2 ClampToTable(Vector2 p)
        {
            p.y = Mathf.Clamp(p.y, InsetScreen, TableTopY - InsetTop);
            float half = Mathf.Min(ScreenHalfWidth - InsetScreen,
                TableTopHalfWidth + (TableTopY - p.y) * TableSideSlope - InsetSide);
            p.x = Mathf.Clamp(p.x, -half, half);
            return p;
        }

        /// <summary>
        /// Unity のドメイン再読込後は、実行時に作った子オブジェクトだけ残り、
        /// 非シリアライズのリストが空になることがある。その場合も既存の牌を拾って動かす。
        /// </summary>
        private void RebuildTileListIfNeeded()
        {
            if (_tiles.Count > 0) return;

            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (!child.name.StartsWith("Tile") || child.name.EndsWith("_BlackEdge")) continue;

                var rect = child as RectTransform;
                if (rect == null) continue;

                _tiles.Add(new Tile
                {
                    Rect = rect,
                    Home = rect.anchoredPosition,
                    Phase = _tiles.Count * 0.8f,
                    Offset = Vector2.zero,
                    Velocity = Vector2.zero,
                    Spin = rect.localEulerAngles.z > 180f ? rect.localEulerAngles.z - 360f : rect.localEulerAngles.z,
                    SpinVelocity = 0f,
                });
            }

            if (_tiles.Count == 0) Build();
        }

        /// <summary>
        /// 裏向きの牌の絵を借りる。**取れなければ null**（呼ぶ側で色を塗る）。
        /// `GetTileSprite(-1)` が裏牌を返す約束になっている。
        /// </summary>
        /// <summary>じゃらじゃらに使う絵の種類。</summary>
        public enum TileArt
        {
            /// <summary>手牌の絵（立てた見た目）。</summary>
            Hand,

            /// <summary>河へ捨てた絵（`自身打`／寝かせた見た目）。</summary>
            Discard,
        }

        /// <summary>
        /// どちらの絵を使うか。**次に <see cref="Attach"/> するときから効く。**
        /// 見比べたいので切り替えられるようにしてある。
        /// </summary>
        public static TileArt Art
        {
            get => _art;
            set
            {
                if (_art == value) return;
                _art = value;
                _tileSprites = null;   // 読み直させる
                _artGeneration++;      // すでに出ている山も作り直させる
            }
        }

        /// <summary>
        /// 絵を切り替えた回数。使い回している山が古い絵のままかどうかを、
        /// これで見分ける。
        /// </summary>
        private static int _artGeneration;

        private int _builtArtGeneration = -1;

        /// <summary>牌を捨てて作り直す。絵の種類を変えたときに使う。</summary>
        private void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;

                // **消す前に隠す。** `Destroy` はフレームの終わりまで効かないので、
                // そのままだと新しい山と1フレームだけ二重に写る
                child.SetActive(false);
                Destroy(child);
            }
            _tiles.Clear();
            Build();
        }

        /// <summary>
        /// 牌の絵を読み込む（2026-09-17 のユーザー指示「もともとある牌の画像を使う」）。
        ///
        /// **シーンの `TileResourceManager` に頼らない。** あれは対局シーンにしか
        /// 居ないので、待ち画面では借りられず、白い札で代用していた。
        /// 絵は `Assets/Resources/麻雀牌/` にあるので、そこから直に読む。
        ///
        /// **切り抜けていない絵を弾く（2026-10-04 のユーザー指摘
        /// 「変なものが混じっています」）。** 同じフォルダには、牌のまわりが
        /// 白いままの絵が混ざっている:
        ///
        ///   南・北・中・撥・白 … 2048x2048・透明部分なし
        ///   赤筒5／赤索５／赤萬5 … 1024x1024 の JPG（そもそも透過が持てない）
        ///
        /// これらは白い四角として出るので、卓の上に紙が散らばって見えていた。
        /// ちゃんと切り抜けている絵は全部 320x320 なので、**候補の中で
        /// いちばん多い大きさだけを残す**という形で弾いている。
        /// 寸法を直書きしないので、絵を描き直して大きさが変わっても付いてくる。
        ///
        /// **外れた5種は、どれもこのゲームの牌ではない。**
        /// 牌は 29 種（萬子1-9・筒子1-9・索子1-9・字牌は**東と西だけ**）で、
        /// `mahjong_engine/engine/tile_wall.py` の `TOTAL_TILE_TYPES = 29` と
        /// `麻雀牌リスト.asset` の `tileSprites`（29枚・27番が東、28番が西）が
        /// どちらもそう言っている。南・北・中・撥・白 は使われていない絵。
        /// **東と西は 320x320 なので、ここでも今までどおり出る。**
        ///
        /// 『能力発動ボタン』は牌ではないので名前で弾く。裏牌も、表を向けて
        /// 並べたいので外す。
        /// </summary>
        private static Sprite[] LoadTileSprites()
        {
            if (_tileSprites != null) return _tileSprites;

            var all = Resources.LoadAll<Sprite>(TileFolder);

            // まず名前で絞る
            var candidates = new List<Sprite>();
            foreach (var sprite in all)
            {
                if (sprite == null) continue;
                string n = sprite.name;
                if (n.Contains("ボタン")) continue;
                if (n.Contains("裏牌")) continue;

                bool isDiscard = n.Contains("自身打");
                if (isDiscard != (_art == TileArt.Discard)) continue;

                candidates.Add(sprite);
            }

            _tileSprites = KeepMostCommonSize(candidates);
            if (_tileSprites.Length == 0) _tileSprites = candidates.ToArray();
            return _tileSprites;
        }

        /// <summary>
        /// いちばん多い大きさの絵だけを残す。**切り抜けていない絵は、
        /// 作り直しの元データのまま入っているので寸法が違う。**
        /// 絵の中身を読まずに（Read/Write を開けずに）仕分けられる。
        /// </summary>
        private static Sprite[] KeepMostCommonSize(List<Sprite> candidates)
        {
            var counts = new Dictionary<Vector2Int, int>();
            foreach (var sprite in candidates)
            {
                var size = new Vector2Int((int)sprite.rect.width, (int)sprite.rect.height);
                counts.TryGetValue(size, out int c);
                counts[size] = c + 1;
            }

            var best = Vector2Int.zero;
            int bestCount = 0;
            foreach (var pair in counts)
            {
                if (pair.Value <= bestCount) continue;
                best = pair.Key;
                bestCount = pair.Value;
            }

            var kept = new List<Sprite>();
            foreach (var sprite in candidates)
            {
                if ((int)sprite.rect.width == best.x && (int)sprite.rect.height == best.y)
                {
                    kept.Add(sprite);
                }
            }
            return kept.ToArray();
        }

        private const string TileFolder = "麻雀牌";
        private static Sprite[] _tileSprites;
        private static TileArt _art = TileArt.Hand;

        private void Update()
        {
            // **待ち画面は時間が止まっていることがある**ので、実時間で動かす
            float deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0f) return;
            // 画面が引っかかった直後に、牌が一気に飛ばないようにする
            deltaTime = Mathf.Min(deltaTime, 0.05f);

            UpdateHand(deltaTime);

            // マウスが押す。**牌を動かすのはこれだけ。** ひとりでに揺れたり跳ねたりはしない
            for (int i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i];
                if (tile.Rect == null) continue;
                ApplyHand(tile, deltaTime);
            }

            // 押された牌が、隣の牌を押す
            ResolveTileSeparation(deltaTime);

            Vector2 origin = TableOrigin;
            float damping = Mathf.Exp(-Damping * deltaTime);
            for (int i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i];
                if (tile.Rect == null) continue;

                // 滑って、止まる。**元の場所へは戻さない**
                tile.Velocity *= damping;
                tile.SpinVelocity *= damping;
                tile.Offset += tile.Velocity * deltaTime;
                tile.Spin = Mathf.Clamp(tile.Spin + tile.SpinVelocity * deltaTime, -MaxSpin, MaxSpin);
                KeepOnTable(tile, origin);

                tile.Rect.anchoredPosition = tile.Home + tile.Offset;
                tile.Rect.localRotation = Quaternion.Euler(0f, 0f, tile.Spin);
            }
        }

        private void UpdateHand(float deltaTime)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || _rect == null)
            {
                _hasHandPosition = false;
                _handVelocity = Vector2.zero;
                _handSpeed01 = 0f;
                return;
            }

            Camera eventCamera = null;
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                eventCamera = _canvas.worldCamera;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rect, mouse.position.ReadValue(), eventCamera, out Vector2 handPosition))
            {
                _hasHandPosition = false;
                return;
            }

            if (!_hasHandPosition)
            {
                _hasHandPosition = true;
                _lastHandPosition = handPosition;
                _handVelocity = Vector2.zero;
                _handSpeed01 = 0f;
                return;
            }

            _handVelocity = Vector2.ClampMagnitude((handPosition - _lastHandPosition) / deltaTime, HandSpeedCap);
            _handSpeed01 = Mathf.Clamp01(_handVelocity.magnitude / HandSpeedCap);
            _lastHandPosition = handPosition;
        }

        /// <summary>
        /// マウスが牌を押す。**牌は、マウスの先の近く（<see cref="HandReach"/>）には居られない。**
        /// 入り込んだ分だけ外へ押し出し、よける向きの速さを足す。マウスが止まっていても押し出す
        /// （止まっているマウスの下に牌が残ると、「よける」に見えない）。
        /// </summary>
        private void ApplyHand(Tile tile, float deltaTime)
        {
            if (!_hasHandPosition) return;

            Vector2 delta = tile.Home + tile.Offset - _lastHandPosition;
            float distance = delta.magnitude;
            if (distance >= HandReach) return;

            Vector2 away = distance > 0.001f
                ? delta / distance
                : new Vector2(Mathf.Cos(tile.Phase), Mathf.Sin(tile.Phase));
            float depth = HandReach - distance;

            tile.Offset += away * Mathf.Min(depth, HandShoveSpeed * deltaTime);

            // 手が速いほど遠くへ滑る。もう十分その向きへ動いていれば足さない
            float along = Mathf.Max(0f, Vector2.Dot(_handVelocity, away));
            float want = HandNudgeSpeed + along * HandCarry;
            float have = Vector2.Dot(tile.Velocity, away);
            if (have < want) tile.Velocity += away * (want - have);

            // 横をかすめると回る
            float cross = _handVelocity.x * away.y - _handVelocity.y * away.x;
            tile.SpinVelocity += Mathf.Clamp(cross / HandSpeedCap, -1f, 1f) * HandSpin * deltaTime;
        }

        private void ResolveTileSeparation(float deltaTime)
        {
            for (int i = 0; i < _tiles.Count; i++)
            {
                var a = _tiles[i];
                if (a.Rect == null) continue;

                for (int j = i + 1; j < _tiles.Count; j++)
                {
                    var b = _tiles[j];
                    if (b.Rect == null) continue;

                    Vector2 delta = (a.Home + a.Offset) - (b.Home + b.Offset);
                    float distance = delta.magnitude;
                    if (distance >= PairRadius) continue;

                    Vector2 direction = distance > 0.001f
                        ? delta / distance
                        : new Vector2(Mathf.Cos(a.Phase - b.Phase), Mathf.Sin(a.Phase - b.Phase));
                    float strength = (1f - distance / PairRadius) * PairSeparation * deltaTime;
                    a.Velocity += direction * strength;
                    b.Velocity -= direction * strength;
                }
            }
        }

        /// <summary>
        /// 牌を卓の中に留める。縁に当たったら、外へ向かう速さだけを消す（縁に沿っては滑れる）。
        /// </summary>
        private static void KeepOnTable(Tile tile, Vector2 origin)
        {
            Vector2 position = tile.Home + tile.Offset + origin;
            Vector2 kept = ClampToTable(position);
            if (kept == position) return;

            if (kept.x != position.x && Mathf.Sign(tile.Velocity.x) == Mathf.Sign(position.x - kept.x)) tile.Velocity.x = 0f;
            if (kept.y != position.y && Mathf.Sign(tile.Velocity.y) == Mathf.Sign(position.y - kept.y)) tile.Velocity.y = 0f;
            tile.Offset = kept - origin - tile.Home;
        }
    }
}
