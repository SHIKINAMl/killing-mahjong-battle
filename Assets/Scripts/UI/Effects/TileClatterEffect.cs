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
        private const int TileCount = 18;

        private const float TileWidth = 30f;
        private const float TileHeight = 40f;
        private const float PileWidth = 250f;
        private const float PileHeight = 130f;
        private const float PileHalfWidth = 96f;
        private const float PileHalfHeight = 46f;

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
        private const float MaxStray = 70f;
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
                existing.RebuildTileListIfNeeded();
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
            Sprite back = FindBackSprite();
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
                if (back != null)
                {
                    img.sprite = back;
                    img.preserveAspect = true;
                }
                else
                {
                    // **牌の絵が取れない場面がある。** 対局シーン以外には
                    // `TileResourceManager` が居らず、裏牌が借りられない。
                    // そのときは、それらしい色の札に縁を付けて牌に見せる。
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
        private static Sprite FindBackSprite()
        {
            var manager = FindFirstObjectByType<TileResourceManager>();
            if (manager == null) return null;
            return manager.GetTileSprite(-1);
        }

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
