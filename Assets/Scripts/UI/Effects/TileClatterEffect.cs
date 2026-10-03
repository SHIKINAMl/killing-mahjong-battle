using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 待っているあいだ、牌をじゃらじゃらと鳴らす（2026-09-15 のユーザー指示）。
    ///
    /// **対戦相手を待っている画面が、文字だけで止まっていた。**
    /// 裏向きの牌を並べて、卓の上で牌をかき混ぜているように動かす。
    ///
    /// **音は鳴らさない。** いまゲームは無音にしてある（AGENTS.md 第6項）ので、
    /// 見た目だけで「じゃらじゃら」を出している。音を戻すときは、
    /// <see cref="Hop"/> が起きた瞬間に牌がぶつかる音を当てるとよい。
    ///
    /// **シーンには置かない。** 対局シーンが2つあるので、置くと片方に入れ忘れる
    /// （`ScreenFlash` などと同じ理由）。待ち画面から呼んで作る。
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

        /// <summary>常に揺れている量。**小さく。** 大きいと壊れて見える。</summary>
        private const float IdleSwayDegrees = 2.5f;
        private const float IdleBobPixels = 2.0f;

        /// <summary>跳ねる高さ[px]と、跳ねている長さ[秒]。</summary>
        private const float HopHeight = 16f;
        private const float HopSeconds = 0.32f;

        /// <summary>次に誰かが跳ねるまでの間隔[秒]。ばらけさせる。</summary>
        private const float HopIntervalMin = 0.18f;
        private const float HopIntervalMax = 0.55f;

        // --- 手混ぜの物理量（4:3 のゲーム画面基準） ---
        private const float HandRadius = 120f;
        private const float HandPush = 1700f;
        private const float HandCarry = 0.55f;
        private const float HandSpeedCap = 1400f;
        private const float HandSpin = 520f;
        // 動画のように、払った直後は少し散らばったまま残る。
        // 収束を急ぎすぎると、牌を触った感触が出ない。
        private const float HomeSpring = 18f;
        private const float Damping = 4f;
        private const float SpinSpring = 14f;
        private const float PairRadius = 29f;
        private const float PairSeparation = 780f;
        // **押して広げられる距離（2026-09-17 に 70 -> 190 へ）。**
        // 「もっと卓上に広げたい」という指示。狭いと、手で払っても
        // すぐ引き戻されて散らばらなかった。
        private const float MaxStray = 190f;
        private const float MaxSpin = 24f;

        private class Tile
        {
            public RectTransform Rect;
            public Vector2 Home;
            public float Phase;
            public float HopStart;      // 負なら跳ねていない
            public float Tilt;
            public Vector2 Offset;
            public Vector2 Velocity;
            public float Spin;
            public float SpinVelocity;
        }

        private readonly List<Tile> _tiles = new List<Tile>();
        private float _nextHopAt;
        private float _elapsed;
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

                // 毎回同じ列に戻すのではなく、最初から少し重なった「牌の山」にする。
                // y を浅くして、画面下端でも局名や待ち文字にかぶらないようにする。
                Vector2 scatter = Random.insideUnitCircle;
                var home = new Vector2(scatter.x * PileHalfWidth, scatter.y * PileHalfHeight);
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
                    HopStart = -1f,
                    Tilt = 0f,
                    Offset = Vector2.zero,
                    Velocity = Vector2.zero,
                    Spin = Random.Range(-8f, 8f),
                    SpinVelocity = 0f,
                });
            }

            _nextHopAt = Random.Range(HopIntervalMin, HopIntervalMax);
            _builtArtGeneration = _artGeneration;
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
                    HopStart = -1f,
                    Tilt = 0f,
                    Offset = Vector2.zero,
                    Velocity = Vector2.zero,
                    Spin = rect.localEulerAngles.z > 180f ? rect.localEulerAngles.z - 360f : rect.localEulerAngles.z,
                    SpinVelocity = 0f,
                });
            }

            if (_tiles.Count == 0) Build();
            else _nextHopAt = _elapsed + Random.Range(HopIntervalMin, HopIntervalMax);
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
        ///   中・北・南・撥・東 … 2048x2048・透明部分なし
        ///   赤筒5／赤索５／赤萬5 … 1024x1024 の JPG（そもそも透過が持てない）
        ///
        /// これらは白い四角として出るので、卓の上に紙が散らばって見えていた。
        /// ちゃんと切り抜けている絵は全部 320x320 なので、**候補の中で
        /// いちばん多い大きさだけを残す**という形で弾いている。
        /// 寸法を直書きしないので、絵を描き直して大きさが変わっても付いてくる。
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

            _elapsed += deltaTime;
            UpdateHand(deltaTime);

            if (_elapsed >= _nextHopAt)
            {
                Hop();
                _nextHopAt = _elapsed + Random.Range(HopIntervalMin, HopIntervalMax);
            }

            // 手で押す力・元の山へ戻るばねを先に加え、牌どうしの反発を足す。
            // 元の「列」には戻らず、最初に作った散ったホーム位置へ収まる。
            for (int i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i];
                if (tile.Rect == null) continue;

                ApplyHandForce(tile, deltaTime);
                tile.Velocity += -tile.Offset * HomeSpring * deltaTime;
                tile.SpinVelocity += -tile.Spin * SpinSpring * deltaTime;
            }

            ResolveTileSeparation(deltaTime);

            for (int i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i];
                if (tile.Rect == null) continue;

                float damping = Mathf.Exp(-Damping * deltaTime);
                tile.Velocity *= damping;
                tile.SpinVelocity *= damping;
                tile.Offset += tile.Velocity * deltaTime;
                tile.Spin += tile.SpinVelocity * deltaTime;
                ConstrainMotion(tile);

                // いつもの小さな揺れ
                float sway = Mathf.Sin(_elapsed * 3.1f + tile.Phase) * IdleSwayDegrees;
                float bob = Mathf.Sin(_elapsed * 4.3f + tile.Phase * 1.7f) * IdleBobPixels;

                // 跳ねている最中はそこへ足す
                float lift = 0f;
                if (tile.HopStart >= 0f)
                {
                    float k = (_elapsed - tile.HopStart) / HopSeconds;
                    if (k >= 1f)
                    {
                        tile.HopStart = -1f;
                        tile.Tilt = 0f;
                    }
                    else
                    {
                        // 上がって落ちる。**落ち際を速くする**と牌らしくなる
                        lift = Mathf.Sin(k * Mathf.PI) * HopHeight * (1f - k * 0.3f);
                    }
                }

                tile.Rect.anchoredPosition = tile.Home + tile.Offset + new Vector2(0f, bob + lift);
                tile.Rect.localRotation = Quaternion.Euler(0f, 0f, sway + tile.Tilt + tile.Spin);
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

        private void ApplyHandForce(Tile tile, float deltaTime)
        {
            // 停止している手に吸い付かず、払った瞬間だけ流れるようにする。
            if (_handSpeed01 <= 0.01f) return;

            Vector2 tilePosition = tile.Home + tile.Offset;
            Vector2 delta = tilePosition - _lastHandPosition;
            float distance = delta.magnitude;
            if (distance >= HandRadius) return;

            Vector2 away = distance > 0.001f
                ? delta / distance
                : new Vector2(Mathf.Cos(tile.Phase), Mathf.Sin(tile.Phase));
            float influence = 1f - distance / HandRadius;
            influence *= influence;

            // 手の進行方向へ流す力と、指先から逃がす力を混ぜる。
            tile.Velocity += (away * HandPush + _handVelocity * HandCarry) * influence * deltaTime;

            float cross = _handVelocity.x * away.y - _handVelocity.y * away.x;
            tile.SpinVelocity += Mathf.Sign(cross) * HandSpin * influence * deltaTime;
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

        private static void ConstrainMotion(Tile tile)
        {
            if (tile.Offset.sqrMagnitude > MaxStray * MaxStray)
            {
                Vector2 outward = tile.Offset.normalized;
                tile.Offset = outward * MaxStray;
                float outwardSpeed = Vector2.Dot(tile.Velocity, outward);
                if (outwardSpeed > 0f) tile.Velocity -= outward * outwardSpeed;
            }

            tile.Spin = Mathf.Clamp(tile.Spin, -MaxSpin, MaxSpin);
        }

        /// <summary>牌を1枚はじく。**音を戻すときは、ここで鳴らすこと。**</summary>
        private void Hop()
        {
            if (_tiles.Count == 0) return;

            var tile = _tiles[Random.Range(0, _tiles.Count)];
            if (tile.HopStart >= 0f) return;     // もう跳ねている

            tile.HopStart = _elapsed;
            tile.Tilt = Random.Range(-14f, 14f);
        }
    }
}
