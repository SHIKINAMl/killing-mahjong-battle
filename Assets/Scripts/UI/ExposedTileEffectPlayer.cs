using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 透視スキルで新たに公開された敵の牌の演出（GameUIVisualController から分離）。
    /// 対象スロットの拡大ポップと、演出終了後の発光アピールを行う。
    /// </summary>
    public class ExposedTileEffectPlayer
    {
        private readonly GameUIManager uiManager;
        private readonly GameUIVisualController visualController;

        public ExposedTileEffectPlayer(GameUIManager uiManager, GameUIVisualController visualController)
        {
            this.uiManager = uiManager;
            this.visualController = visualController;
        }

        /// <param name="newlyExposed">今回新たに公開された敵山のインデックス一覧</param>
        public IEnumerator PlayPerspectiveAnimation(List<int> newlyExposed)
        {
            if (uiManager.EnemyWallUI == null) yield break;

            visualController.RebuildAllTilesFromState(newlyExposed);

            yield return new WaitForSeconds(0.2f);

            List<UnityEngine.UI.Image> glowingImages = new List<UnityEngine.UI.Image>();

            foreach (int index in newlyExposed)
            {
                if (index >= 0 && index < uiManager.EnemyWallUI.GetEnemyWallSlots().Count)
                {
                    Transform slot = uiManager.EnemyWallUI.GetEnemyWallSlots()[index];
                    RectTransform rt = slot as RectTransform;
                    if (rt != null)
                    {
                        int actualTileId = -1;
                        if (index < Managers.BoardStateManager.Instance.OriginalEnemyWallTiles.Count)
                        {
                            actualTileId = Managers.BoardStateManager.Instance.OriginalEnemyWallTiles[index];
                            visualController.InitializeTileComponent(rt, actualTileId, false); // isHandTile = false (壁牌なので)
                        }

                        UnityEngine.UI.Image img = rt.GetComponent<UnityEngine.UI.Image>();
                        if (img != null) glowingImages.Add(img);

                        Vector3 origScale = rt.localScale;
                        float duration = 0.15f; // 拡大演出を短縮
                        for (float t = 0; t < duration; t += Time.deltaTime)
                        {
                            float s = Mathf.Lerp(1f, 1.3f, Mathf.PingPong(t * (1f / (duration / 2f)), 1f));
                            rt.localScale = origScale * s;
                            yield return null;
                        }
                        rt.localScale = origScale;
                    }
                }
            }

            // 全体で約2.0秒になるように、残りの時間を発光アピールにあてる
            float waitedSoFar = 0.2f + (0.15f * newlyExposed.Count);
            float glowDuration = 2.0f - waitedSoFar;
            if (glowDuration < 0.5f) glowDuration = 0.5f; // 最低でも0.5秒は光らせる

            Color originalColor = Color.white;
            Color glowColor = new Color(1.0f, 0.8f, 0.2f); // 黄色っぽく発光

            for (float t = 0; t < glowDuration; t += Time.deltaTime)
            {
                float pingPong = Mathf.PingPong(t * 3f, 1f);
                Color currentColor = Color.Lerp(originalColor, glowColor, pingPong);
                foreach (var img in glowingImages)
                {
                    if (img != null) img.color = currentColor;
                }
                yield return null;
            }

            // 元の色に戻す
            foreach (var img in glowingImages)
            {
                if (img != null) img.color = originalColor;
            }

            if (uiManager.PhaseController != null)
            {
                uiManager.PhaseController.HandlePhaseVisibility(uiManager.CurrentPhaseStatus);
            }
        }
    }
}
