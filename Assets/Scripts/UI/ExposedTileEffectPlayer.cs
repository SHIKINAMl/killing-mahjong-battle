using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 透視スキルで新たに公開された敵の牌の演出（GameUIVisualController から分離）。
    ///
    /// 2026-10-03 に、Canva の提案スライドのとおりへ作り直した。
    ///
    ///   ① 発動      … 画面を1枚撮って青く重ね、山牌以外を暗く落とし、集中線を集める。
    ///                  BGMが深い水の底に沈んだ音になる（<see cref="PerspectiveSkillEffect"/>）
    ///   ② 1枚ずつ    … 牌を1枚ずつめくる。重ねと集中線は出したまま、BGMも沈んだまま
    ///   ③ フラッシュ … 少し間を置いてから白く光らせ、その瞬間に重ねと集中線を消す
    ///   ④ もとに戻る … 心音とともにBGMが戻る。めくった牌はそのまま見えている
    ///
    /// **めくる間隔は演出の要。** 以前は3枚まとめて出していたので、
    /// 「何枚見えたのか」が分からなかった。1枚ずつ、間を置いて返す。
    /// </summary>
    public class ExposedTileEffectPlayer
    {
        /// <summary>1枚を返すときの拡大ポップの長さ（秒）。</summary>
        private const float PopDuration = 0.15f;

        /// <summary>次の1枚を返すまでの間（秒）。</summary>
        private const float TileRevealGap = 0.28f;

        /// <summary>めくり終わってからフラッシュまでの間（秒）。</summary>
        private const float HoldBeforeFlash = 0.45f;

        /// <summary>戻ったあと、透視できた牌を光らせてアピールする長さ（秒）。</summary>
        private const float GlowAppealDuration = 0.9f;

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

            // まだ返さないまま並べ直す。撮る画面は「返す前」でなければならない
            visualController.RebuildAllTilesFromState(newlyExposed);

            List<RectTransform> slots = uiManager.EnemyWallUI.GetEnemyWallSlots();

            // ---- ① 発動 ----
            PerspectiveSkillEffect effect = PerspectiveSkillEffect.Create();
            if (effect != null)
            {
                // 集中線の集まる先と、暗く落とさない穴は**実際の牌の位置**から決める。
                // 画面比が変わっても付いてくる
                Rect focus = PerspectiveSkillEffect.GetScreenRect(slots);
                yield return effect.Enter(focus);
            }

            // ---- ② 1枚ずつ透視する ----
            var glowingImages = new List<UnityEngine.UI.Image>();

            for (int n = 0; n < newlyExposed.Count; n++)
            {
                int index = newlyExposed[n];
                if (index < 0 || index >= slots.Count) continue;

                RectTransform rt = slots[index];
                if (rt == null) continue;

                var boardState = Managers.BoardStateManager.Instance;
                if (boardState != null && index < boardState.OriginalEnemyWallTiles.Count)
                {
                    int actualTileId = boardState.OriginalEnemyWallTiles[index];
                    visualController.InitializeTileComponent(rt, actualTileId, false); // 壁牌なので isHandTile = false
                }

                var img = rt.GetComponent<UnityEngine.UI.Image>();
                if (img != null) glowingImages.Add(img);

                yield return PopTile(rt);

                // 最後の1枚のあとは、フラッシュまでの間でまとめて待つ
                if (n < newlyExposed.Count - 1) yield return new WaitForSeconds(TileRevealGap);
            }

            yield return new WaitForSeconds(HoldBeforeFlash);

            // ---- ③ フラッシュ → ④ もとに戻る ----
            if (effect != null)
            {
                yield return effect.Release();
            }
            else
            {
                ScreenFlash.Play();
            }

            // 透視できた牌がどれだったかを、戻ったあとにもう一度見せる
            yield return GlowAppeal(glowingImages);

            if (uiManager.PhaseController != null)
            {
                uiManager.PhaseController.HandlePhaseVisibility(uiManager.CurrentPhaseStatus);
            }
        }

        /// <summary>1枚ぶんの拡大ポップ。返った瞬間を目に留まらせる。</summary>
        private IEnumerator PopTile(RectTransform rt)
        {
            Vector3 origScale = rt.localScale;

            for (float t = 0f; t < PopDuration; t += Time.deltaTime)
            {
                if (rt == null) yield break;
                float s = Mathf.Lerp(1f, 1.3f, Mathf.PingPong(t * (1f / (PopDuration / 2f)), 1f));
                rt.localScale = origScale * s;
                yield return null;
            }

            if (rt != null) rt.localScale = origScale;
        }

        /// <summary>透視できた牌を黄色く明滅させる。</summary>
        private IEnumerator GlowAppeal(List<UnityEngine.UI.Image> images)
        {
            if (images.Count == 0) yield break;

            Color originalColor = Color.white;
            Color glowColor = new Color(1.0f, 0.8f, 0.2f);

            for (float t = 0f; t < GlowAppealDuration; t += Time.deltaTime)
            {
                Color current = Color.Lerp(originalColor, glowColor, Mathf.PingPong(t * 3f, 1f));
                foreach (var img in images)
                {
                    if (img != null) img.color = current;
                }
                yield return null;
            }

            foreach (var img in images)
            {
                if (img != null) img.color = originalColor;
            }
        }
    }
}
