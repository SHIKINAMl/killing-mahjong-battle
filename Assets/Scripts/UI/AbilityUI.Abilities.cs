using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public partial class AbilityUI
    {
        private void EnsureAbilities()
        {
            if (realAbilities != null) return;

            // スキルの種類（skillType）はサーバーの SkillType enum と対になっている。
            // 表示名は SkillNames に一本化してあるので、ここでは文字列を書かない。
            realAbilities = new System.Collections.Generic.List<AbilityData>
            {
                new AbilityData(SkillNames.Mulligan, SkillNames.GetDisplayName(SkillNames.Mulligan),
                    "手牌か山牌から不要な牌を選び、山札と交換する。"),
                new AbilityData(SkillNames.Perspective, SkillNames.GetDisplayName(SkillNames.Perspective),
                    "相手の手牌をランダムに3枚公開する。"),
                new AbilityData(SkillNames.BoostHand, SkillNames.GetDisplayName(SkillNames.BoostHand),
                    "指定した役の翻数を+1する。"),
                new AbilityData(SkillNames.Assault, SkillNames.GetDisplayName(SkillNames.Assault),
                    "この局は上がっても点を得ない。代わりに、得るはずだった額を相手への追加ダメージにする。1局1回。")

                // **特殊勝利は載せない（2026-08-14 に廃止の指示）。**
                // サーバー側の enum・HP_COST_TABLE・special_victory_won の処理は残っているので、
                // ここから外すだけでプレイヤーは選べなくなる。
                // `special_victory_count` はコスト表のどの段を使うかの添字として今も生きているため、
                // BoardStateManager / GameRules 側は触っていない。
                // 完全に消すなら Python 側の対応が要る（担当外）。
            };
        }

        /// <summary>現在の所持HP（＝スキルの支払い原資）。BoardStateManager が無い場合は 0 扱い。</summary>
        private int CurrentLocalHp =>
            KillingMahjong.Managers.BoardStateManager.Instance != null
                ? KillingMahjong.Managers.BoardStateManager.Instance.LocalPlayerHp
                : 0;

        private int CurrentSpecialVictoryCount =>
            KillingMahjong.Managers.BoardStateManager.Instance != null
                ? KillingMahjong.Managers.BoardStateManager.Instance.LocalPlayerSpecialVictoryCount
                : 0;

        /// <summary>
        /// HP以外の理由で今は撃てないなら、その説明を返す（撃てるなら null）。
        ///
        /// **強襲の「1局1回」はサーバー側にしか制限が無かった**（`game_engine.py:392`）。
        /// クライアントは何度でも送れてしまい、2発目は `error` しか返らないので
        /// 送信直前に立てた `IsTransitioning` が倒れず盤面が固まっていた
        /// （2026-08-23 に `GameUINetworkHandler.HandleError` 側でも倒すようにしたが、
        /// **そもそも送らせない**のがここの役目）。
        ///
        /// 使用済みかどうかは `skill_casted` を受けた時点で `BoardStateManager` が覚えている。
        /// </summary>
        private string GetUnusableReason(string skillType)
        {
            if (skillType != SkillNames.Assault) return null;

            var board = KillingMahjong.Managers.BoardStateManager.Instance;
            if (board == null || !board.LocalAssaultUsedThisRound) return null;

            return $"「{SkillNames.GetDisplayName(SkillNames.Assault)}はこの局でもう使ったわ。次の局まで待ちなさい」";
        }
        private void PopulateList()
        {
            EnsureAbilities();
            if (itemPrefab == null || contentContainer == null) return;

            // clear existing
            foreach(Transform child in contentContainer) Destroy(child.gameObject);
            instantiatedItems.Clear();

            // **選択は行と一緒に消える。** 行を作り直しているので、
            // 参照を残すと破棄済みのオブジェクトを掴んだままになる
            currentSelection = null;

            LayoutContentContainer();

            int svCount = CurrentSpecialVictoryCount;
            int currentHp = CurrentLocalHp;

            float currentY = 0f;
            for (int i = 0; i < realAbilities.Count; i++)
            {
                var data = realAbilities[i];
                int currentCost = GameRules.GetSkillCost(data.skillType, svCount);
                bool affordable = currentHp >= currentCost;
                string unusableReason = GetUnusableReason(data.skillType);

                var itemObj = Instantiate(itemPrefab, contentContainer);
                itemObj.Setup(this, i, data.name, currentCost, data.description, affordable, unusableReason);

                // 行の寸法と位置はここで決め切る。
                //
                // **プレハブの値は当てにしない。** 行の器は 138x40 なのに中の板は
                // 120x45 で、縦が 2.5 ずつはみ出して `RectMask2D` に切られていた。
                // 器そのものを板として使い、子は器いっぱいに張る（AbilityItemUI.BuildTile）。
                RectTransform rt = itemObj.GetComponent<RectTransform>();
                if (rt != null)
                {
                    // 行からはみ出した子が隣の行のクリック判定を奪うのを防ぐ
                    if (itemObj.GetComponent<RectMask2D>() == null)
                    {
                        itemObj.gameObject.AddComponent<RectMask2D>();
                    }

                    rt.localRotation = Quaternion.identity;
                    rt.localScale = Vector3.one;

                    // 上端そろえ。pivot も固定する（プレハブ任せにすると
                    // 位置の式が pivot に依存して読めなくなる）
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.sizeDelta = new Vector2(RowWidth, RowHeight);
                    rt.anchoredPosition3D = new Vector3(0f, -currentY, 0f);
                }

                currentY += RowHeight + RowSpacing;
                instantiatedItems.Add(itemObj);
            }
        }

        /// <summary>
        /// 一覧の器を、巻物の紙の面（内枠）の上側へ合わせる。
        ///
        /// シーンの値は 138x186 @(6.5,112) で、幅が内枠より 22 狭く、
        /// 上端が内枠より 4 だけ外に出ていた。両シーンあるのでコードから当てる。
        /// </summary>
        private void LayoutContentContainer()
        {
            var rect = contentContainer as RectTransform;
            if (rect == null) return;

            // 説明欄を巻物の外（右）へ出したので、一覧は内枠を上から下まで使える。
            float listHeight = PanelInnerHeight - ListTopMargin;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(PanelInnerWidth, listHeight);
            rect.anchoredPosition = new Vector2(
                PanelInnerCenter.x,
                PanelInnerCenter.y + PanelInnerHeight * 0.5f - ListTopMargin);
            rect.localScale = Vector3.one;
        }
    }
}
