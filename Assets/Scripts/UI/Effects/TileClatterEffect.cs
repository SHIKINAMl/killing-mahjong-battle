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
        /// <summary>並べる枚数。多いと賑やかだが、待ち文字を邪魔しない程度に。</summary>
        private const int TileCount = 7;

        private const float TileWidth = 30f;
        private const float TileHeight = 40f;
        private const float TileGap = 6f;

        /// <summary>常に揺れている量。**小さく。** 大きいと壊れて見える。</summary>
        private const float IdleSwayDegrees = 2.5f;
        private const float IdleBobPixels = 2.0f;

        /// <summary>跳ねる高さ[px]と、跳ねている長さ[秒]。</summary>
        private const float HopHeight = 16f;
        private const float HopSeconds = 0.32f;

        /// <summary>次に誰かが跳ねるまでの間隔[秒]。ばらけさせる。</summary>
        private const float HopIntervalMin = 0.18f;
        private const float HopIntervalMax = 0.55f;

        private class Tile
        {
            public RectTransform Rect;
            public Vector2 Home;
            public float Phase;
            public float HopStart;      // 負なら跳ねていない
            public float Tilt;
        }

        private readonly List<Tile> _tiles = new List<Tile>();
        private float _nextHopAt;
        private float _elapsed;

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
            rt.sizeDelta = new Vector2(TileCount * (TileWidth + TileGap), TileHeight * 2f);

            var effect = go.AddComponent<TileClatterEffect>();
            effect.Build();
            return effect;
        }

        private void Build()
        {
            Sprite back = FindBackSprite();
            float step = TileWidth + TileGap;
            float left = -(TileCount - 1) * step * 0.5f;

            for (int i = 0; i < TileCount; i++)
            {
                var go = new GameObject("Tile" + i, typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.sizeDelta = new Vector2(TileWidth, TileHeight);

                var home = new Vector2(left + i * step, 0f);
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
                });
            }

            _nextHopAt = Random.Range(HopIntervalMin, HopIntervalMax);
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
            _elapsed += Time.unscaledDeltaTime;

            if (_elapsed >= _nextHopAt)
            {
                Hop();
                _nextHopAt = _elapsed + Random.Range(HopIntervalMin, HopIntervalMax);
            }

            for (int i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i];
                if (tile.Rect == null) continue;

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

                tile.Rect.anchoredPosition = tile.Home + new Vector2(0f, bob + lift);
                tile.Rect.localRotation = Quaternion.Euler(0f, 0f, sway + tile.Tilt);
            }
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
