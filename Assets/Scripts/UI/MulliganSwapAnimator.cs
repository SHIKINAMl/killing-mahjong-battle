using System.Collections;
using System.Collections.Generic;
using KillingMahjong.Common;
using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// マリガン（牌交換）スキルの交換演出。
    /// OUT牌が中央左へ拡大表示され、IN牌が上空から降ってきて入れ替わり、
    /// 元のスロットへ帰還する一連のコルーチンを担う（GameUISkillController から分離）。
    /// 何枚かを替えるときは、全部を一度に並べて一斉に入れ替える。
    ///
    /// MonoBehaviour ではないプレーンなクラスのため、呼び出し側のコルーチンから
    /// yield return animator.PlayRoutine(...) の形で使用する。
    /// </summary>
    public class MulliganSwapAnimator
    {
        private readonly GameUIManager uiManager;
        private readonly TileResourceManager resources;
        private readonly PresentationScopeSet presentations = new PresentationScopeSet(Debug.LogException);
        public void CancelPresentation() { presentations.CancelAll(); }

        public MulliganSwapAnimator(GameUIManager uiManager)
        {
            this.uiManager = uiManager;
            resources = uiManager.TileResourceManager;
        }

        /// <summary>演出の試写用。盤面の更新や手牌ソートを実行しない。</summary>
        public MulliganSwapAnimator(TileResourceManager resources) { this.resources = resources; }

        /// <summary>交換1組。出ていく牌・入ってくる牌・元の牌のUIスロット（null可）。</summary>
        public struct Swap
        {
            public int OutTileId, InTileId;
            public RectTransform Slot;
            public Swap(int outTileId, int inTileId, RectTransform slot) { OutTileId = outTileId; InTileId = inTileId; Slot = slot; }
        }

        /// <param name="outTileId">交換で出ていく牌のID</param>
        /// <param name="inTileId">交換で入ってくる牌のID</param>
        /// <param name="originalSlotRt">元の牌のUIスロット（選択時に保存したもの）。null可</param>
        public IEnumerator PlayRoutine(int outTileId, int inTileId, RectTransform originalSlotRt)
        {
            return PlayRoutine(new[] { new Swap(outTileId, inTileId, originalSlotRt) });
        }

        /// <summary>
        /// 何枚かを**一度に**入れ替える（2026-10-22 ユーザー指示「3枚指定した時、一気に3枚交換してほしい」）。
        /// 出ていく牌は左に、入ってくる牌は右に縦に並べ、全部そろってから一斉に入れ替える。
        /// </summary>
        public IEnumerator PlayRoutine(IList<Swap> swaps)
        {
            if (swaps == null || swaps.Count == 0) yield break;
            using (var scope = presentations.Begin())
                yield return scope.Run(PlayVisualsRoutine(scope, swaps));
        }

        /// <summary>演出中に動かす1組ぶんの絵。</summary>
        private sealed class Pair
        {
            public Swap Swap;
            public RectTransform OutRt, InRt;
            public UnityEngine.UI.Image OutImg, InImg;
            public Vector2 StartPos, OutCenter, InCenter;
            public Vector3 InitialScale;
        }

        /// <summary>
        /// 拡大率。1枚のときは今までどおり 3.5 倍。2〜3枚は縦に並べても画面に収まるよう小さくする
        /// （牌の高さ 40 × 2.2 × 3 段で 264 と、段の間）。
        /// </summary>
        private static float ZoomFor(int count) { return count <= 1 ? 3.5f : count == 2 ? 2.6f : 2.2f; }

        private IEnumerator PlayVisualsRoutine(PresentationScope scope, IList<Swap> swaps)
        {
            // **音は、透視と同じ段取りを掛ける**（2026-10-09 のユーザー指示）。
            // 入れ替えのあいだ BGM が水の中のように沈み、牌が戻った所で白く光って抜け、
            // そのあと心音とともに元へ戻る。途中で打ち切られたら、沈めた音はここで戻す
            var trance = Effects.SkillTranceAudio.Begin(0.3f);
            scope.AddCleanup(() => { if (trance != null && !trance.IsReleased) trance.Dispose(); });

            GameObject animContainer = scope.Own(new GameObject("MulliganAnimationContainer"));
            Canvas canvas = animContainer.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UISortingOrders.MulliganSwapAnimation;
            var scaler = animContainer.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);
            scaler.matchWidthOrHeight = 0.5f;
            Canvas.ForceUpdateCanvases();
            RectTransform animCanvasRt = animContainer.GetComponent<RectTransform>();

            // 背景の暗転
            GameObject bgObj = new GameObject("Bg");
            bgObj.transform.SetParent(animContainer.transform, false);
            var bgImg = bgObj.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0, 0, 0, 0.85f);
            bgImg.rectTransform.anchorMin = Vector2.zero;
            bgImg.rectTransform.anchorMax = Vector2.one;
            bgImg.rectTransform.offsetMin = Vector2.zero;
            bgImg.rectTransform.offsetMax = Vector2.zero;

            // テキストの用意（背景の次＝牌より奥に描く）
            var outText = MakeLabel(animContainer.transform, "OutText", "OUT", new Color(1f, 0.2f, 0.2f, 0f), new Vector2(-280, 0));
            var inText = MakeLabel(animContainer.transform, "InText", "IN", new Color(0.2f, 0.8f, 1f, 0f), new Vector2(280, 0));

            Vector3 targetScale = Vector3.one * ZoomFor(swaps.Count);
            float rowStep = 40f * targetScale.y + 14f;
            var pairs = new List<Pair>();
            for (int i = 0; i < swaps.Count; i++)
            {
                var swap = swaps[i];
                var pair = new Pair { Swap = swap };
                Vector2 initialSize = new Vector2(45, 40); // 元のスロットが無い場合も通常の牌サイズを使う
                pair.StartPos = new Vector2(0, -400);      // 見つからなかった場合のデフォルト
                pair.InitialScale = Vector3.one;
                if (swap.Slot != null)
                {
                    pair.StartPos = SlotPosition(swap.Slot, animCanvasRt);
                    initialSize = swap.Slot.rect.size;
                    pair.InitialScale = swap.Slot.localScale;

                    // 元のスロットの画像を一時的に透明にする
                    var cg = swap.Slot.GetComponent<CanvasGroup>();
                    if (cg == null) cg = swap.Slot.gameObject.AddComponent<CanvasGroup>();
                    float before = cg.alpha;
                    scope.AddCleanup(() => { if (cg != null) cg.alpha = before; });
                    cg.alpha = 0;
                }
                // 上から順に並べる。1枚なら真ん中（今までどおり）
                float y = ((swaps.Count - 1) * 0.5f - i) * rowStep;
                pair.OutCenter = new Vector2(-80, y);
                pair.InCenter = new Vector2(80, y);
                pair.OutRt = MakeTile(animContainer.transform, "OutTile" + i, swap.OutTileId, initialSize, pair.InitialScale, out pair.OutImg);
                pair.InRt = MakeTile(animContainer.transform, "InTile" + i, swap.InTileId, initialSize, pair.InitialScale, out pair.InImg);
                pair.InImg.color = new Color(1, 1, 1, 0); // 初期は透明
                pair.OutRt.anchoredPosition = pair.StartPos;
                pairs.Add(pair);
            }

            // --- アニメーション開始 ---
            // 1. 元の場所から左のOUT位置へ、全部いっしょに飛んでいく
            float t = 0;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float progress = Mathf.Sin(Mathf.Clamp01(t / 0.3f) * Mathf.PI * 0.5f);
                foreach (var p in pairs)
                {
                    p.OutRt.anchoredPosition = Vector2.Lerp(p.StartPos, p.OutCenter, progress);
                    p.OutRt.localScale = Vector3.Lerp(p.InitialScale, targetScale, progress);
                }
                outText.color = new Color(1f, 0.2f, 0.2f, progress); // テキストフェードイン
                outText.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(-330, 0), new Vector2(-280, 0), progress); // 画面内に見える位置へ
                yield return null;
            }

            // 2. 右側に新しい牌(IN)が上空から降ってくる
            foreach (var p in pairs) p.InRt.localScale = targetScale;
            t = 0;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float progress = Mathf.Sin(Mathf.Clamp01(t / 0.3f) * Mathf.PI * 0.5f);
                foreach (var p in pairs)
                {
                    p.InRt.anchoredPosition = Vector2.Lerp(p.InCenter + new Vector2(0, 800), p.InCenter, progress);
                    p.InImg.color = new Color(1, 1, 1, progress);
                }
                inText.color = new Color(0.2f, 0.8f, 1f, progress);
                inText.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(330, 0), new Vector2(280, 0), progress); // 画面内に見える位置へ
                yield return null;
            }

            // 少し待機（左右に並んだ状態を見せる）
            yield return new WaitForSeconds(0.6f);

            // 3. OUTは上へ消え、INは元の場所へ戻る（全部いっしょに）
            t = 0;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                float progress = Mathf.Sin(Mathf.Clamp01(t / 0.4f) * Mathf.PI * 0.5f);
                foreach (var p in pairs)
                {
                    // OUT退場
                    p.OutRt.anchoredPosition = Vector2.Lerp(p.OutCenter, p.OutCenter + new Vector2(0, 800), progress);
                    p.OutImg.color = new Color(1, 1, 1, 1f - progress);
                    // IN帰還
                    p.InRt.anchoredPosition = Vector2.Lerp(p.InCenter, p.StartPos, progress);
                    p.InRt.localScale = Vector3.Lerp(targetScale, p.InitialScale, progress);
                }
                outText.color = new Color(1f, 0.2f, 0.2f, 1f - progress);
                inText.color = new Color(0.2f, 0.8f, 1f, 1f - progress);
                yield return null;
            }

            // 4. 全体がフェードアウト
            t = 0;
            var canvasGroup = animContainer.AddComponent<CanvasGroup>();
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(1, 0, t / 0.2f);
                yield return null;
            }

            UnityEngine.Object.Destroy(animContainer);

            // 牌が戻った。白く光って、沈んでいた音が抜ける。
            // 心音と BGM の戻りは向こうが鳴らしきるので、ここでは待たない
            if (trance != null) trance.ReleaseDetached();

            // アニメーション終了後に元のスロットの画像を復活し、先行して絵柄を更新する
            bool updatedSlot = false;
            foreach (var p in pairs)
            {
                var slot = p.Swap.Slot;
                if (slot == null) continue;
                var cg = slot.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1;

                var interaction = slot.GetComponent<TileInteraction>();
                var visual = slot.GetComponent<TileVisual>();
                if (visual != null && resources != null)
                {
                    visual.SetTile(p.Swap.InTileId, resources.GetTileSprite(p.Swap.InTileId), resources);
                }
                if (interaction != null)
                {
                    // WallIndex はそのまま。位置は変わらず「その位置の牌が入れ替わった」だけなので、
                    // ここで消すと次の交換で位置を特定できなくなる。
                    interaction.TileId = p.Swap.InTileId;
                }
                updatedSlot = true;
            }
            if (updatedSlot)
            {
                // ここで交換ぶんの見た目はもう反映済み。
                // 差分リビルドのキャッシュを盤面状態に合わせておかないと、
                // 「oldId が newId になった」ともう一度判定され、
                // TileId で検索された**別の同じ牌**が巻き添えで書き換わる。
                // （状態は交換した枚数しか変わっていないので、次の完全リビルドで戻る＝一時的に絵だけ化ける）
                uiManager?.VisualController?.SyncRebuildCache();
            }

            // スライドアニメーションを実行（VisualControllerで定義）
            if (uiManager != null && uiManager.VisualController != null)
            {
                yield return uiManager.VisualController.PlayHandSortAnimationRoutine();
            }

            // 特別ルールの牌（南、北、白、發、中）が来た場合のセリフ演出。何枚来ても、セリフは最初の1枚ぶんだけ
            foreach (var p in pairs)
            {
                int baseId = TileId.BaseId(p.Swap.InTileId);
                if (baseId != 28 && baseId < 30) continue;
                var reactionController = Managers.ReactionController.Instance;
                if (uiManager != null && reactionController != null)
                {
                    var tileData = new TileData(p.Swap.InTileId);
                    string tileName = tileData.GetTileName();
                    reactionController.EnqueueCustomDialogue($"あっ！「{tileName}」が来たわね…！", "Idle", "Surprised");
                }
                break;
            }

            // 親のスキル処理が、自分のロックを解除してから再構築する。
        }

        /// <summary>元のスロットの位置を、演出用の Canvas の中の位置へ直す。</summary>
        private static Vector2 SlotPosition(RectTransform slot, RectTransform animCanvasRt)
        {
            Camera uiCamera = null;
            Canvas parentCanvas = slot.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                parentCanvas = parentCanvas.rootCanvas;
                if (parentCanvas.renderMode == RenderMode.ScreenSpaceCamera || parentCanvas.renderMode == RenderMode.WorldSpace)
                {
                    uiCamera = parentCanvas.worldCamera;
                }
            }

            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, slot.position);
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(animCanvasRt, screenPos, null, out localPos);
            return localPos;
        }

        private RectTransform MakeTile(Transform parent, string name, int tileId, Vector2 size, Vector3 scale,
                                       out UnityEngine.UI.Image image)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.localScale = scale;
            image = obj.AddComponent<UnityEngine.UI.Image>();
            var vis = obj.AddComponent<TileVisual>();
            if (resources != null)
            {
                vis.SetTile(tileId, resources.GetTileSprite(tileId), resources);
            }
            return rt;
        }

        private static TMPro.TextMeshProUGUI MakeLabel(Transform parent, string name, string label, Color color, Vector2 position)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<TMPro.TextMeshProUGUI>();
            text.text = label;
            text.fontSize = KillingMahjong.Common.UITypography.Huge;
            text.color = color;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.fontStyle = TMPro.FontStyles.Bold;
            text.rectTransform.sizeDelta = new Vector2(400, 200);
            text.rectTransform.anchoredPosition = position; // 画面内に見える位置
            return text;
        }
    }
}
