using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    public sealed partial class EffectPreviewPlayer
    {
        private IEnumerator PreviewHandConfirmation()
        {
            var reveal = HandWaitRevealUI.Ensure(rig.transform);
            var rank = HandRankCallUI.Create();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(rank.gameObject, previewScene);
            // 本編の保存済み待ち牌枠を借りる。通信・盤面管理は追加しない。
            var prefab = Resources.Load<GameObject>("Presentation/HandConfirmationWaitBox");
            if (prefab == null) throw new System.InvalidOperationException("待ち牌の試写枠がありません。試写舞台を更新してください。");
            var box = Instantiate(prefab, rig.transform).GetComponent<WaitUI>();
            box.BindTileResources(rig.tiles);
            var canvas = box.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = KillingMahjong.Common.UISortingOrders.ConfirmationDialog;
            box.gameObject.SetActive(false);
            scope.AddCleanup(() => { if (reveal != null) reveal.Hide(); if (box != null) box.Hide(); if (rank != null) Destroy(rank.gameObject); });
            var data = new[] {
                new WaitData { tile=1, han=8, mangan_or_more=true, multiplier=2, multiplier_label="倍満", yaku=new[]{"清一色","一盃口","立直"} },
                new WaitData { tile=2, han=9, mangan_or_more=true, multiplier=2, multiplier_label="倍満", yaku=new[]{"平和","清一色","一盃口","立直"} },
                new WaitData { tile=3, han=7, mangan_or_more=true, multiplier=1.5f, multiplier_label="跳満", yaku=new[]{"清一色","立直"} },
                new WaitData { tile=4, han=13, mangan_or_more=true, multiplier=4, multiplier_label="役満", yaku=new[]{"四暗刻"} },
                new WaitData { tile=5, han=9, mangan_or_more=true, multiplier=2, multiplier_label="倍満", yaku=new[]{"平和","清一色","一盃口","立直"} }
            };
            int[] hand = {0,0,0,1,1,1,2,2,2,3,3,3,4};
            for (int i = 0; i < rig.handTiles.Count; i++)
            {
                var rt = rig.handTiles[i]; rt.anchoredPosition = new Vector2(-148 + i * 27, -225);
                rt.sizeDelta = new Vector2(40, 54); rt.GetComponent<UnityEngine.UI.Image>().sprite = rig.tiles.GetTileSprite(hand[i]);
            }
            if (EffectId != "hand.wait_transfer") rank.ShowRank("役満");
            reveal.Show(data, rig.tiles, EffectId != "hand.wait_transfer");
            yield return new WaitForSeconds(2.5f);
            if (EffectId == "hand.wait_reveal") yield break;
            rank.HideImmediate();
            bool done = false;
            rig.phase.PlayCenterTextAnim("手牌決定！", 2f, () => done = true);
            yield return reveal.Transfer(box);
            yield return Until(() => done);
            yield return new WaitForSeconds(3.5f);
        }
    }
}
