using System.Collections;
using KillingMahjong.Common;
using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// マリガン（牌交換）スキルの交換演出。
    /// OUT牌が中央左へ拡大表示され、IN牌が上空から降ってきて入れ替わり、
    /// 元のスロットへ帰還する一連のコルーチンを担う（GameUISkillController から分離）。
    ///
    /// MonoBehaviour ではないプレーンなクラスのため、呼び出し側のコルーチンから
    /// yield return animator.PlayRoutine(...) の形で使用する。
    /// </summary>
    public class MulliganSwapAnimator
    {
        private readonly GameUIManager uiManager;

        public MulliganSwapAnimator(GameUIManager uiManager)
        {
            this.uiManager = uiManager;
        }

        /// <param name="outTileId">交換で出ていく牌のID</param>
        /// <param name="inTileId">交換で入ってくる牌のID</param>
        /// <param name="originalSlotRt">元の牌のUIスロット（選択時に保存したもの）。null可</param>
        public IEnumerator PlayRoutine(int outTileId, int inTileId, RectTransform originalSlotRt)
        {
            GameObject animContainer = new GameObject("MulliganAnimationContainer");
            Canvas canvas = animContainer.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UISortingOrders.MulliganSwapAnimation;
            animContainer.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;

            Vector2 startPos = new Vector2(0, -400); // 見つからなかった場合のデフォルト
            Vector2 initialSize = new Vector2(120, 180);
            Vector3 initialScale = Vector3.one;

            if (originalSlotRt != null)
            {
                RectTransform animCanvasRt = animContainer.GetComponent<RectTransform>();

                Camera uiCamera = null;
                Canvas parentCanvas = originalSlotRt.GetComponentInParent<Canvas>();
                if (parentCanvas != null)
                {
                    parentCanvas = parentCanvas.rootCanvas;
                    if (parentCanvas.renderMode == RenderMode.ScreenSpaceCamera || parentCanvas.renderMode == RenderMode.WorldSpace)
                    {
                        uiCamera = parentCanvas.worldCamera;
                    }
                }

                Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, originalSlotRt.position);
                Vector2 localPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(animCanvasRt, screenPos, null, out localPos);
                startPos = localPos;

                initialSize = originalSlotRt.rect.size;
                initialScale = originalSlotRt.localScale;

                // 元のスロットの画像を一時的に透明にする
                var cg = originalSlotRt.GetComponent<CanvasGroup>();
                if (cg == null) cg = originalSlotRt.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0;
            }

            // 背景の暗転
            GameObject bgObj = new GameObject("Bg");
            bgObj.transform.SetParent(animContainer.transform, false);
            var bgImg = bgObj.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0, 0, 0, 0.85f);
            bgImg.rectTransform.anchorMin = Vector2.zero;
            bgImg.rectTransform.anchorMax = Vector2.one;
            bgImg.rectTransform.offsetMin = Vector2.zero;
            bgImg.rectTransform.offsetMax = Vector2.zero;

            // テキストの用意
            GameObject outTextObj = new GameObject("OutText");
            outTextObj.transform.SetParent(animContainer.transform, false);
            var outText = outTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            outText.text = "OUT";
            outText.fontSize = KillingMahjong.Common.UITypography.Huge;
            outText.color = new Color(1f, 0.2f, 0.2f, 0f);
            outText.alignment = TMPro.TextAlignmentOptions.Center;
            outText.fontStyle = TMPro.FontStyles.Bold;
            outText.rectTransform.sizeDelta = new Vector2(400, 200);
            outText.rectTransform.anchoredPosition = new Vector2(-280, 0); // 画面内に見える位置

            // テキストを牌の後ろにするために先に生成したが、Canvasのソートは無いので後でSiblingIndexを調整
            outText.transform.SetAsFirstSibling();
            bgObj.transform.SetAsFirstSibling();

            GameObject inTextObj = new GameObject("InText");
            inTextObj.transform.SetParent(animContainer.transform, false);
            var inText = inTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            inText.text = "IN";
            inText.fontSize = KillingMahjong.Common.UITypography.Huge;
            inText.color = new Color(0.2f, 0.8f, 1f, 0f);
            inText.alignment = TMPro.TextAlignmentOptions.Center;
            inText.fontStyle = TMPro.FontStyles.Bold;
            inText.rectTransform.sizeDelta = new Vector2(400, 200);
            inText.rectTransform.anchoredPosition = new Vector2(280, 0); // 画面内に見える位置
            inText.transform.SetSiblingIndex(1); // 背景の次

            // OUT Tile
            GameObject outObj = new GameObject("OutTile");
            outObj.transform.SetParent(animContainer.transform, false);
            var outRt = outObj.AddComponent<RectTransform>();
            outRt.sizeDelta = initialSize;
            outRt.localScale = initialScale;
            var outImg = outObj.AddComponent<UnityEngine.UI.Image>();
            var outVis = outObj.AddComponent<TileVisual>();
            if (uiManager.TileResourceManager != null)
            {
                outVis.SetTile(outTileId, uiManager.TileResourceManager.GetTileSprite(outTileId), uiManager.TileResourceManager);
            }

            // IN Tile (最初は非表示)
            GameObject inObj = new GameObject("InTile");
            inObj.transform.SetParent(animContainer.transform, false);
            var inRt = inObj.AddComponent<RectTransform>();
            inRt.sizeDelta = initialSize;
            inRt.localScale = initialScale;
            var inImg = inObj.AddComponent<UnityEngine.UI.Image>();
            inImg.color = new Color(1, 1, 1, 0); // 初期は透明
            var inVis = inObj.AddComponent<TileVisual>();
            if (uiManager.TileResourceManager != null)
            {
                inVis.SetTile(inTileId, uiManager.TileResourceManager.GetTileSprite(inTileId), uiManager.TileResourceManager);
            }

            // --- アニメーション開始 ---
            Vector3 targetScale = new Vector3(3.5f, 3.5f, 1f); // 牌をさらに大きく拡大
            Vector2 outCenterPos = new Vector2(-80, 0); // 牌をさらに中央に寄せる
            Vector2 inCenterPos = new Vector2(80, 0); // 牌をさらに中央に寄せる

            // 1. 元の場所から左のOUT位置へ飛んでいく
            outRt.anchoredPosition = startPos;
            float t = 0;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float progress = Mathf.Sin((t / 0.3f) * Mathf.PI * 0.5f);
                outRt.anchoredPosition = Vector2.Lerp(startPos, outCenterPos, progress);
                outRt.localScale = Vector3.Lerp(initialScale, targetScale, progress);
                outText.color = new Color(1f, 0.2f, 0.2f, progress); // テキストフェードイン
                outText.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(-330, 0), new Vector2(-280, 0), progress); // 画面内に見える位置へ
                yield return null;
            }
            outRt.anchoredPosition = outCenterPos;
            outRt.localScale = targetScale;
            outText.color = new Color(1f, 0.2f, 0.2f, 1f);

            // 2. 右側に新しい牌(IN)が上空から降ってくる
            inRt.anchoredPosition = inCenterPos + new Vector2(0, 800);
            inRt.localScale = targetScale;
            t = 0;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float progress = Mathf.Sin((t / 0.3f) * Mathf.PI * 0.5f);
                inRt.anchoredPosition = Vector2.Lerp(inCenterPos + new Vector2(0, 800), inCenterPos, progress);
                inImg.color = new Color(1, 1, 1, progress);
                inText.color = new Color(0.2f, 0.8f, 1f, progress);
                inText.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(330, 0), new Vector2(280, 0), progress); // 画面内に見える位置へ
                yield return null;
            }
            inRt.anchoredPosition = inCenterPos;
            inImg.color = new Color(1, 1, 1, 1f);
            inText.color = new Color(0.2f, 0.8f, 1f, 1f);

            // 少し待機（左右に並んだ状態を見せる）
            yield return new WaitForSeconds(0.6f);

            // 3. OUTは上へ消え、INは元の場所へ戻る
            t = 0;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                float progress = Mathf.Sin((t / 0.4f) * Mathf.PI * 0.5f);

                // OUT退場
                outRt.anchoredPosition = Vector2.Lerp(outCenterPos, outCenterPos + new Vector2(0, 800), progress);
                outImg.color = new Color(1, 1, 1, 1f - progress);
                outText.color = new Color(1f, 0.2f, 0.2f, 1f - progress);

                // IN帰還
                inRt.anchoredPosition = Vector2.Lerp(inCenterPos, startPos, progress);
                inRt.localScale = Vector3.Lerp(targetScale, initialScale, progress);
                inText.color = new Color(0.2f, 0.8f, 1f, 1f - progress);

                yield return null;
            }
            inRt.anchoredPosition = startPos;
            inRt.localScale = initialScale;

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

            // アニメーション終了後に元のスロットの画像を復活し、先行して絵柄を更新する
            if (originalSlotRt != null)
            {
                var cg = originalSlotRt.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1;

                var interaction = originalSlotRt.GetComponent<TileInteraction>();
                var visual = originalSlotRt.GetComponent<TileVisual>();
                if (visual != null && uiManager.TileResourceManager != null)
                {
                    visual.SetTile(inTileId, uiManager.TileResourceManager.GetTileSprite(inTileId), uiManager.TileResourceManager);
                }
                if (interaction != null)
                {
                    // WallIndex はそのまま。位置は変わらず「その位置の牌が入れ替わった」だけなので、
                    // ここで消すと次の交換で位置を特定できなくなる。
                    interaction.TileId = inTileId;
                }

                // ここで交換ぶんの見た目はもう反映済み。
                // 差分リビルドのキャッシュを盤面状態に合わせておかないと、
                // 「oldId が newId になった」ともう一度判定され、
                // TileId で検索された**別の同じ牌**が巻き添えで書き換わる。
                // （状態は1枚しか変わっていないので、次の完全リビルドで戻る＝一時的に絵だけ化ける）
                uiManager.VisualController?.SyncRebuildCache();
            }

            // スライドアニメーションを実行（VisualControllerで定義）
            if (uiManager.VisualController != null)
            {
                yield return uiManager.VisualController.PlayHandSortAnimationRoutine();
            }

            // 特別ルールの牌（南、北、白、發、中）が来た場合のセリフ演出
            int baseId = TileId.BaseId(inTileId);
            if (baseId == 28 || baseId >= 30)
            {
                var reactionController = Managers.ReactionController.Instance;
                if (reactionController != null)
                {
                    var tileData = new TileData(inTileId);
                    string tileName = tileData.GetTileName();
                    reactionController.EnqueueCustomDialogue($"あっ！「{tileName}」が来たわね…！", "Idle", "Surprised");
                }
            }

            uiManager.SetIsTransitioning(false); // ★ここで解除してRebuildを許可する
            uiManager.VisualController?.RebuildAllTilesFromState(null);
        }
    }
}
