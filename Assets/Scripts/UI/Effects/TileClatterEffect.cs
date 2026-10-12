using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>待機中の牌を指先で混ぜる共通演出。音・通信・進行判定は持たない。</summary>
    public sealed class TileClatterEffect : MonoBehaviour
    {
        public const string EffectId = "tile.clatter";
        private const int Count = 28;
        [SerializeField] private TileArt builtArt = TileArt.Discard;
        private readonly List<RectTransform> tiles = new List<RectTransform>();
        private RectTransform rect;
        private Canvas canvas;
        private TileClatterPhysics simulation;
        private bool previewDriven;
        private int builtArtGeneration;
        private static int _artGeneration;
        public enum TileArt { Hand, Discard }
        public static TileArt Art
        {
            get => _art;
            set { if (_art == value) return; _art = value; _tileSprites = null; _artGeneration++; }
        }
        public int TileCount => tiles.Count;
        public bool HasPhysics => simulation != null;
        public string PhysicsSceneName => simulation != null ? simulation.SceneName : null;
        public float OpaqueOverlapPixels => simulation != null ? simulation.ReadOpaqueOverlap() : 0f;

        public static TileClatterEffect Attach(RectTransform parent, float offsetFromBottom)
        {
            if (parent == null) return null;
            var existing = parent.GetComponentInChildren<TileClatterEffect>(true);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                if (existing.builtArt != _art || existing.builtArtGeneration != _artGeneration) existing.Rebuild();
                else existing.RestoreTiles();
                existing.EnsurePhysics(false);
                return existing;
            }
            // 牌の山は待機中だけの可変演出で、同じ親の子を再利用する。
            var go = new GameObject("TileClatter", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, 0);
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = new Vector2(0, offsetFromBottom);
            rt.sizeDelta = new Vector2(620, 200);
            var effect = go.AddComponent<TileClatterEffect>();
            effect.Build();
            return effect;
        }
        public static void Hide(RectTransform parent)
        {
            if (parent == null) return;
            var existing = parent.GetComponentInChildren<TileClatterEffect>(true);
            if (existing != null) existing.gameObject.SetActive(false);
        }
        private void Awake() { rect = transform as RectTransform; canvas = GetComponentInParent<Canvas>(); }
        private void OnEnable() { previewDriven = false; RestoreTiles(); if (tiles.Count > 0) EnsurePhysics(false); }
        private void OnDisable() { DisposePhysics(); previewDriven = false; }
        private void OnDestroy() { DisposePhysics(); }
        private void DisposePhysics() { simulation?.Dispose(); simulation = null; }
        private void Build()
        {
            var sprites = LoadTileSprites();
            var bag = new List<Sprite>(sprites);
            var random = new System.Random(GetInstanceID());
            for (int i = 0; i < Count; i++)
            {
                var go = new GameObject("Tile" + i, typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false); rt.sizeDelta = Vector2.one * 42f;
                var image = go.GetComponent<Image>(); image.raycastTarget = false; image.preserveAspect = true;
                if (bag.Count == 0) bag.AddRange(sprites);
                if (bag.Count > 0) { int pick = random.Next(bag.Count); image.sprite = bag[pick]; bag.RemoveAt(pick); }
                else image.color = new Color32(238, 232, 214, 255);
                tiles.Add(rt);
            }
            builtArt = _art; builtArtGeneration = _artGeneration;
            EnsurePhysics(true);
        }
        private void RestoreTiles()
        {
            if (tiles.Count > 0) return;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i) as RectTransform;
                if (child != null && child.name.StartsWith("Tile") &&
                    child.GetComponent<Image>() != null && child.gameObject.activeSelf) tiles.Add(child);
            }
        }
        private void Rebuild()
        {
            DisposePhysics();
            for (int i = transform.childCount - 1; i >= 0; i--)
            { var child = transform.GetChild(i).gameObject; child.SetActive(false); Destroy(child); }
            tiles.Clear(); Build();
        }
        private void EnsurePhysics(bool scatter)
        {
            if (simulation != null || tiles.Count == 0 || !Application.isPlaying) return;
            simulation = new TileClatterPhysics(tiles, rect.anchoredPosition, builtArt, scatter);
            simulation.Render();
        }
        private void Update()
        {
            if (previewDriven) return;
            Vector2? pointer = null;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && rect != null)
            {
                var eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, mouse.position.ReadValue(),
                    eventCamera, out var local)) pointer = local + rect.anchoredPosition;
            }
            Advance(Time.unscaledDeltaTime, pointer);
        }
        private void Advance(float deltaTime, Vector2? pointer)
        {
            if (!isActiveAndEnabled || deltaTime <= 0) return;
            EnsurePhysics(false); simulation?.Step(Mathf.Min(deltaTime, .05f), pointer);
        }
        /// <summary>試写・検査の入力だけを差し替える。動きは本編と同じ処理を通る。</summary>
        public void PreviewStep(float deltaTime, Vector2? tablePointer)
        { previewDriven = true; Advance(deltaTime, tablePointer); }
        public void ReleasePreviewInput() { previewDriven = false; simulation?.ResetPointer(); }

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
        private static TileArt _art = TileArt.Discard;

    }
}
