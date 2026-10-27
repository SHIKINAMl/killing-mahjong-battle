using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public class WallUI : MonoBehaviour
    {
        [Header("Wall Configuration")]
        [SerializeField] private Transform wallContainer;
        
        [Header("Layout Settings")]
        [SerializeField] private Vector2 normalContainerPos = new Vector2(0, 0);       // 通常時のコンテナ位置
        [SerializeField] private Vector2 discardContainerPos = new Vector2(0, -350);   // 打牌フェイズ時のコンテナ位置 (より手前に)
        [SerializeField] private Vector2 startPosition = new Vector2(40, 150);         // コンテナ内での牌の基点
        [SerializeField] private float tileIntervalX = 55f;
        [SerializeField] private float gapIntervalX = 80f;
        [SerializeField] private float rowIntervalY = 95f;
        [SerializeField] private float maxWidthX = 1400f; // 画面端での折り返し幅

        private GameUIManager gameUIManager;

        public void Setup(GameUIManager manager)
        {
            this.gameUIManager = manager;
        }

        private Dictionary<RectTransform, Vector2> targetPositions = new Dictionary<RectTransform, Vector2>();
        [SerializeField] private float animationSpeed = 15f;

        private void Update()
        {
            foreach (var rt in wallSlots)
            {
                if (rt == null) continue;
                if (targetPositions.TryGetValue(rt, out Vector2 targetPos))
                {
                    rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, targetPos, Time.deltaTime * animationSpeed);
                }
            }
        }

        private List<RectTransform> wallSlots = new List<RectTransform>();
        public List<RectTransform> GetWallSlots() => wallSlots;

        /// <summary>牌を作り直さず、現在の絵柄で整列する。元の山の位置番号は維持する。</summary>
        public void SortCurrentTiles(bool isDiscardPhase)
        {
            var slots = new List<RectTransform>();
            var ids = new List<int>();
            var indexes = new List<int>();
            var originalIndexes = new Dictionary<TileInteraction, int>();
            foreach (var slot in wallSlots)
            {
                var tile = slot != null ? slot.GetComponent<TileInteraction>() : null;
                if (tile == null) continue;
                slots.Add(slot);
                ids.Add(tile.TileId);
                indexes.Add(tile.WallIndex);
                originalIndexes[tile] = tile.WallIndex;
            }
            LayoutWallTiles(slots, ids, null, isDiscardPhase, indexes);
            // 同一牌が複数ある場合も、各オブジェクトの通信上の位置番号を入れ替えない。
            foreach (var pair in originalIndexes) pair.Key.WallIndex = pair.Value;
        }

        // --- 「並べ直す」の動き（2026-10-15 のユーザーの要望） ---
        // 並びが変わらないとき（2回目以降）は牌が1枚も動かず、押せたのか分からなかった。
        // 押すたびに、山の牌がいったん中央へ寄り集まり（少しばらけて）、元の位置へ散って戻る。
        // 動かすのは Update の Lerp の行き先（targetPositions）だけで、位置そのものは触らない
        private const float ShuffleGatherSeconds = 0.16f;
        // 中央へどれだけ寄せるか（0〜1）。0.55 は「少し弱く」と言われて 0.35 にした（2026-10-15）
        private const float ShuffleGatherAmount = 0.35f;
        private const float ShuffleJitter = 10f;
        private Coroutine shuffleRoutine;
        private Dictionary<RectTransform, Vector2> shuffleHomes;

        /// <summary>今の並び（targetPositions）を行き先として、寄り集まって散る動きを1回見せる。</summary>
        public void PlayRearrangeShuffle()
        {
            if (shuffleRoutine != null)
            {
                StopCoroutine(shuffleRoutine);
                RestoreShuffleHomes(null);
            }
            shuffleRoutine = StartCoroutine(RearrangeShuffleRoutine());
        }

        private IEnumerator RearrangeShuffleRoutine()
        {
            shuffleHomes = new Dictionary<RectTransform, Vector2>();
            var gathered = new Dictionary<RectTransform, Vector2>();
            Vector2 center = Vector2.zero;
            foreach (var rt in wallSlots)
            {
                if (rt == null || !targetPositions.TryGetValue(rt, out Vector2 home)) continue;
                shuffleHomes[rt] = home;
                center += home;
            }
            if (shuffleHomes.Count == 0) { shuffleRoutine = null; yield break; }
            center /= shuffleHomes.Count;

            foreach (var pair in shuffleHomes)
            {
                Vector2 jitter = new Vector2(Random.Range(-ShuffleJitter, ShuffleJitter), Random.Range(-ShuffleJitter, ShuffleJitter) * 0.7f);
                Vector2 to = Vector2.Lerp(pair.Value, center, ShuffleGatherAmount) + jitter;
                gathered[pair.Key] = to;
                targetPositions[pair.Key] = to;
            }

            yield return new WaitForSecondsRealtime(ShuffleGatherSeconds);
            RestoreShuffleHomes(gathered);
            shuffleRoutine = null;
        }

        /// <summary>
        /// 寄せた行き先を元の並びへ戻す。途中で別の並べ替えが入って行き先が変わった牌は、その新しい行き先を残す。
        /// </summary>
        private void RestoreShuffleHomes(Dictionary<RectTransform, Vector2> gathered)
        {
            if (shuffleHomes == null) return;
            foreach (var pair in shuffleHomes)
            {
                if (pair.Key == null || !targetPositions.TryGetValue(pair.Key, out Vector2 now)) continue;
                if (gathered != null && (!gathered.TryGetValue(pair.Key, out Vector2 g) || now != g)) continue;
                targetPositions[pair.Key] = pair.Value;
            }
            shuffleHomes = null;
        }

        /// <summary>チュートリアルで山牌エリアを指し示すための実表示領域。</summary>
        public RectTransform GuideTargetRect => wallContainer as RectTransform;

        /// <summary>
        /// 山牌1枚ぶん。表示は種類順に並べ替えるので、
        /// 元の山での位置（WallIndex）を一緒に持ち回るために使う。
        /// </summary>
        private struct WallEntry
        {
            public TileData Data;
            public int WallIndex;
        }

        /// <summary>
        /// wallSlotsからtileIdが一致するRectTransformを取り出し、スロットから削除して返す
        /// </summary>
        // --- 相手の番のあいだ、山牌の段だけを静める（2026-09-20 のユーザー指示）---
        //
        // 打牌フェイズで触るのは**この段**（「山牌から1枚切る」）。自分の手牌は
        // 「手牌を見る」で覗く方式なので画面に出ていない。
        // **画面全体に暗幕は敷かない。** 牌が読みにくくなるより、触れる場所だけを落とす。

        /// <summary>
        /// 相手の番のときの濃さ。**0.72 では実機で3%しか変わらず、気づけなかった**ので 0.55 にした。
        /// 牌の絵柄はまだ読める（2026-09-20 に実測して決めた値）。
        /// </summary>
        private const float OpponentTurnQuietAlpha = 0.55f;

        /// <summary>濃さを移す時間。切り替わりが目で追える速さ。</summary>
        private const float QuietFadeSeconds = 0.25f;

        private CanvasGroup quietGroup;
        private Coroutine quietRoutine;

        /// <summary>山牌の段を静めるか。相手の番に true、自分の番に false。</summary>
        public void SetQuietForOpponentTurn(bool quiet)
        {
            if (wallContainer == null) return;

            if (quietGroup == null)
            {
                quietGroup = wallContainer.GetComponent<CanvasGroup>();
                if (quietGroup == null) quietGroup = wallContainer.gameObject.AddComponent<CanvasGroup>();
            }

            float to = quiet ? OpponentTurnQuietAlpha : 1f;
            if (Mathf.Approximately(quietGroup.alpha, to)) return;

            if (quietRoutine != null) StopCoroutine(quietRoutine);
            if (!gameObject.activeInHierarchy) { quietGroup.alpha = to; return; }
            quietRoutine = StartCoroutine(QuietFadeRoutine(to));
        }

        private IEnumerator QuietFadeRoutine(float to)
        {
            float from = quietGroup.alpha;
            for (float t = 0f; t < QuietFadeSeconds; t += Time.unscaledDeltaTime)
            {
                if (quietGroup == null) yield break;
                quietGroup.alpha = Mathf.Lerp(from, to, PresentationTiming.Sample(t) / QuietFadeSeconds);
                yield return null;
            }
            if (quietGroup != null) quietGroup.alpha = to;
            quietRoutine = null;
        }

        public RectTransform GrabTileById(int tileId)
        {
            for (int i = 0; i < wallSlots.Count; i++)
            {
                if (wallSlots[i] == null) continue;
                var interaction = wallSlots[i].GetComponent<TileInteraction>();
                if (interaction != null && interaction.TileId == tileId)
                {
                    if (currentActiveInteraction == interaction) SetActiveDiscardTile(null);
                    RectTransform t = wallSlots[i];
                    wallSlots.RemoveAt(i);
                    return t;
                }
            }
            return null;
        }

        public RectTransform GrabTileByWallIndex(int wallIndex)
        {
            if (wallIndex < 0) return null;
            var slot = wallSlots.Find(t => t != null && t.GetComponent<TileInteraction>() != null
                && t.GetComponent<TileInteraction>().WallIndex == wallIndex);
            return GrabTileInstance(slot);
        }

        public RectTransform GrabTileInstance(RectTransform slot)
        {
            if (slot == null || !wallSlots.Contains(slot)) return null;
            if (currentActiveInteraction == slot.GetComponent<TileInteraction>()) SetActiveDiscardTile(null);
            wallSlots.Remove(slot);
            return slot;
        }


        public void UpdateContainerPosition(bool isDiscardPhase)
        {
            if (wallContainer != null)
            {
                if (wallContainer is RectTransform rectTransform)
                {
                    rectTransform.anchoredPosition = isDiscardPhase ? discardContainerPos : normalContainerPos;
                }
                else
                {
                    wallContainer.localPosition = isDiscardPhase ? new Vector3(discardContainerPos.x, discardContainerPos.y, 0) 
                                                                 : new Vector3(normalContainerPos.x, normalContainerPos.y, 0);
                }
            }

            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = isDiscardPhase
                    ? UISortingOrders.WallDiscardPhase
                    : UISortingOrders.WallBase;
            }
        }


        public void LayoutWallTiles(List<RectTransform> generatedTiles, List<int> tileIds, List<int> waitTiles, bool isDiscardPhase, List<int> wallIndexes = null)
        {
            // Clear existing tracking list (but DO NOT destroy, GameUIManager manages their lifecycle)
            wallSlots.Clear();

            // コンテナ自体の位置をフェイズに応じて移動する
            UpdateContainerPosition(isDiscardPhase);

            // 1. Convert to TileData
            //    表示は種類順に並べ替えるが、山の何番目だったかは持ち回る。
            //    これを落とすと、同じ牌IDが複数ある山で index を引き直せなくなる。
            List<WallEntry> allTiles = new List<WallEntry>();
            for (int idx = 0; idx < tileIds.Count; idx++)
            {
                allTiles.Add(new WallEntry { Data = new TileData(tileIds[idx]), WallIndex = wallIndexes != null ? wallIndexes[idx] : idx });
            }

            // 2. Group by Category
            var manzu = new List<WallEntry>();
            var pinzu = new List<WallEntry>();
            var souzu = new List<WallEntry>();
            var honors = new List<WallEntry>();

            foreach (var t in allTiles)
            {
                switch (t.Data.Category)
                {
                    case TileCategory.Manzu: manzu.Add(t); break;
                    case TileCategory.Pinzu: pinzu.Add(t); break;
                    case TileCategory.Souzu: souzu.Add(t); break;
                    case TileCategory.Honor: honors.Add(t); break;
                }
            }

            // 3. 固定順で並べる: 萬子→ピンズ→索子→字牌
            var categoryLists = new List<List<WallEntry>> { manzu, pinzu, souzu, honors };
            categoryLists.Sort((a, b) => GetCategoryPriority(a).CompareTo(GetCategoryPriority(b)));

            // 4. Layout
            float currentY = startPosition.y;
            float currentX = startPosition.x;


            // generatedTiles を消費していくためのリスト
            List<RectTransform> remainingGeneratedTiles = new List<RectTransform>(generatedTiles);

            for (int i = 0; i < categoryLists.Count; i++)
            {
                var list = categoryLists[i];
                if (list.Count == 0) continue;

                // Sort inside category (Number順でソート。赤ドラも正しい位置に並ぶように)
                list.Sort((a, b) =>
                {
                    if (a.Data.Number != b.Data.Number) return a.Data.Number.CompareTo(b.Data.Number);
                    return a.Data.Id.CompareTo(b.Data.Id); // 同じNumberならencodedIdでタイブレーク
                });

                int j = 0;
                while (j < list.Count)
                {
                    int targetId = list[j].Data.Id;
                    
                    // 描画する前にmaxWidthX を超えるかチェック
                    if (currentX > startPosition.x + maxWidthX)
                    {
                        currentX = startPosition.x;
                        currentY -= rowIntervalY; // 元の下の段に切り返す設定に戻す
                    }

                    RectTransform slot = null;

                    // 該当のTileIdを持つオブジェクトを探す
                    for (int t = 0; t < remainingGeneratedTiles.Count; t++)
                    {
                        var tInteraction = remainingGeneratedTiles[t].GetComponent<TileInteraction>();
                        if (tInteraction != null && tInteraction.TileId == targetId)
                        {
                            slot = remainingGeneratedTiles[t];
                            remainingGeneratedTiles.RemoveAt(t);
                            break;
                        }
                    }

                    if (slot == null)
                    {
                        Debug.LogWarning("WallUI: Could not find generated tile for TileId: " + targetId);
                        if (remainingGeneratedTiles.Count > 0)
                        {
                            slot = remainingGeneratedTiles[0];
                            remainingGeneratedTiles.RemoveAt(0);
                        }
                        else
                        {
                            break;
                        }
                    }
                    slot.SetParent(wallContainer, false);
                    
                    // Reset scaling and anchors properly to avoid overlap/squishing
                    slot.localScale = Vector3.one;
                    slot.anchorMin = new Vector2(0.5f, 0.5f);
                    slot.anchorMax = new Vector2(0.5f, 0.5f);
                    slot.pivot = new Vector2(0.5f, 0.5f);
                    
                    float targetXPos = currentX;
                    float targetYPos = currentY;

                    Vector2 anchoredPos = new Vector2(targetXPos, targetYPos);
                    
                    // アニメーション用に目標位置を設定。初期配置の場合は即座に設定する
                    if (!targetPositions.ContainsKey(slot))
                    {
                        slot.anchoredPosition = anchoredPos;
                    }
                    targetPositions[slot] = anchoredPos;
                    
                    slot.localRotation = Quaternion.identity;

                    var interaction = slot.GetComponent<TileInteraction>();
                    if (interaction != null)
                    {
                        // anchoredPosition（アンカー基準座標）で保存する
                        interaction.OriginalWallPosition = new Vector3(anchoredPos.x, anchoredPos.y, 0);
                        // 表示順ではなく、山の何番目かを覚えさせる。
                        // サーバーへ index を送る操作はこれを使う。
                        interaction.WallIndex = list[j].WallIndex;
                    }
                    
                    var visual = slot.GetComponent<TileVisual>();
                    if (visual != null)
                    {
                        // 待ち牌リストに含まれていても赤枠を出さないように修正（ホバー時のみ赤枠にする）
                        visual.SetFuritenHighlight(false);
                    }

                    wallSlots.Add(slot);
                    slot.gameObject.SetActive(true);

                    // 同一カテゴリ内なら隙間なし、最後の牌ならカテゴリ間の隙間を追加
                    if (j < list.Count - 1)
                    {
                        currentX += tileIntervalX;
                    }
                    else if (i < categoryLists.Count - 1)
                    {
                        currentX += gapIntervalX;
                    }

                    j++;
                }
            }

            // 安全網: 配置されずに余った generatedTiles があれば、確実にプールに返す（幽霊オブジェクトによる西増殖バグ防止）
            if (remainingGeneratedTiles.Count > 0)
            {
                var poolManager = UnityEngine.Object.FindFirstObjectByType<TilePoolManager>();
                if (poolManager != null)
                {
                    foreach (var leftover in remainingGeneratedTiles)
                    {
                        if (leftover != null) poolManager.ReturnTileToPool(leftover.gameObject);
                    }
                }
                else
                {
                    foreach (var leftover in remainingGeneratedTiles)
                    {
                        if (leftover != null) Destroy(leftover.gameObject);
                    }
                }
            }
        }

        private int GetCategoryPriority(List<WallEntry> list)
        {
            if (list.Count == 0) return 99;
            var cat = list[0].Data.Category;
            switch (cat)
            {
                case TileCategory.Souzu: return 1;
                case TileCategory.Manzu: return 2;
                case TileCategory.Pinzu: return 3;
                case TileCategory.Honor: return 4;
                default: return 99;
            }
        }

        public RectTransform GrabTile(int tileId)
        {
            // WallSlotの中から該当IDを持つTransformを探し出して返す
            for (int i = 0; i < wallSlots.Count; i++)
            {
                if (wallSlots[i] == null) continue;
                var interaction = wallSlots[i].GetComponent<TileInteraction>();
                if (interaction != null && interaction.TileId == tileId)
                {
                    if (currentActiveInteraction == interaction) SetActiveDiscardTile(null);
                    RectTransform t = wallSlots[i];
                    wallSlots.RemoveAt(i);
                    return t; 
                }
            }
            return null;
        }

        public void ReturnTileToWall(RectTransform tileTransform, int tileId)
        {
            if (tileTransform == null) return;

            // 戻す（リストに追加して、親を設定）
            wallSlots.Add(tileTransform);
            tileTransform.SetParent(wallContainer, false);
            
            // Interactionリセットと初期座標の復元
            var interaction = tileTransform.GetComponent<TileInteraction>();
            if (interaction != null)
            {
                if (gameUIManager != null)
                {
                    Canvas canvas = GetComponentInParent<Canvas>();
                    interaction.Initialize(tileId, false, gameUIManager, canvas);
                }
                
                // アンカーとピボットをLayoutWallTilesと同じ基準（中心 0.5）に戻す
                tileTransform.anchorMin = new Vector2(0.5f, 0.5f);
                tileTransform.anchorMax = new Vector2(0.5f, 0.5f);
                tileTransform.pivot = new Vector2(0.5f, 0.5f);
                tileTransform.localScale = Vector3.one;
                tileTransform.localRotation = Quaternion.identity;
                
                // OriginalWallPositionを目標座標（targetPositions）に設定し、UpdateのLerpでアニメーションさせる
                Vector2 originalPos = new Vector2(interaction.OriginalWallPosition.x, interaction.OriginalWallPosition.y);
                targetPositions[tileTransform] = originalPos;
            }
        }

        public void UpdateWallHighlights(List<int> waitTiles, bool isDiscardPhase)
        {
            List<int> waitBaseIds = new List<int>();
            if (isDiscardPhase && waitTiles != null)
            {
                foreach (int waitId in waitTiles)
                {
                    waitBaseIds.Add(Common.TileId.BaseId(waitId));
                }
            }

            foreach (var slot in wallSlots)
            {
                if (slot == null) continue;
                var visual = slot.GetComponent<TileVisual>();
                var interaction = slot.GetComponent<TileInteraction>();
                if (visual != null && interaction != null)
                {
                    bool isFuritenAlert = waitBaseIds.Contains(Common.TileId.BaseId(interaction.TileId));
                    visual.SetFuritenHighlight(isFuritenAlert);
                }
            }
        }

        private RectTransform arrowIndicator;
        private TileInteraction currentActiveInteraction = null;

        public void SetActiveDiscardTile(TileInteraction interaction)
        {
            // 牌の座標・判定を動かさず、選択済みだけを固定の金枠で示す。
            if (currentActiveInteraction != null)
                UIInteractionFeedback.Attach(currentActiveInteraction.gameObject).SetSelected(false);
            currentActiveInteraction = interaction;
            if (currentActiveInteraction != null)
                UIInteractionFeedback.Attach(currentActiveInteraction.gameObject).SetSelected(true);
            if (arrowIndicator != null) arrowIndicator.gameObject.SetActive(false);
        }

        public void ClearActiveDiscardTile(TileInteraction interaction)
        {
            if (currentActiveInteraction == interaction)
            {
                SetActiveDiscardTile(null);
            }
        }

        public void UpdateDiscardTurnIndicator(bool isLocalTurn, bool isDiscardPhase)
        {
            if (isDiscardPhase && isLocalTurn)
            {
                // 未選択の先頭牌に、選択済みの印を付けない。
                if (currentActiveInteraction != null)
                {
                    // 既にセットされていれば再適用
                    SetActiveDiscardTile(currentActiveInteraction);
                }
            }
            else
            {
                SetActiveDiscardTile(null);
                foreach (var slot in wallSlots)
                    if (slot != null) UIInteractionFeedback.Attach(slot.gameObject).SetSelected(false);
            }
        }

        /// <summary>
        /// 矢印を山牌より手前に出す Canvas 設定を当て直す。
        ///
        /// **親が決まっていないうちに呼んではいけない。**
        /// 親 Canvas の無い GameObject の Canvas はルート扱いになり、
        /// overrideSorting を true にしても保持されない（あとから SetParent しても false のまま）。
        /// </summary>
        private void EnsureArrowSorting()
        {
            if (arrowIndicator == null) return;
            var canvas = arrowIndicator.GetComponent<Canvas>();
            if (canvas == null) canvas = arrowIndicator.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = UISortingOrders.WallFront;
        }

        private void CreateArrowIndicator()
        {
            GameObject arrowObj = new GameObject("DiscardArrowIndicator");
            arrowIndicator = arrowObj.AddComponent<RectTransform>();
            arrowIndicator.sizeDelta = new Vector2(50, 50);

            // **Canvas を付ける前に親を決める。**
            // 親無しで AddComponent<Canvas>() するとルート Canvas になり、
            // overrideSorting が落ちて山牌と同じ sortingOrder を引き継いでしまう。
            // 生成直後にwallContainerの子として登録するのは、牌のプール汚染を防ぐ目的も兼ねる
            Transform parent = wallContainer != null ? wallContainer : (Transform)this.transform;
            arrowIndicator.SetParent(parent, false);

            var text = arrowObj.AddComponent<UnityEngine.UI.Text>();
            text.text = "▼";
            text.fontSize = (int)KillingMahjong.Common.UITypography.BodyLarge;
            text.color = Color.red;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var outline = arrowObj.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2, -2);

            EnsureArrowSorting();
            arrowIndicator.gameObject.SetActive(false);
        }
    }
}
