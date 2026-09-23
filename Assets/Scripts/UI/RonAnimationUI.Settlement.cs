using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public partial class RonAnimationUI
    {
        // ============================================================
        //  清算パネル
        //
        //  考え方は「**枠を先に完成させ、中の数字だけ後から入れる**」。
        //  以前は 式 → ランク → 巨大な数字 を順に「生成」していたので、後から出た大きい文字が
        //  先の文字を物理的に覆い、**4つが揃った瞬間には役名がもう読めなかった**
        //  （2026-08-27 の計測で、因果を目で追える時間は 0 秒）。
        //  枠が動かなければ、覆う問題そのものが起きない。
        // ============================================================

        // ------------------------------------------------------------
        //  表が出ていく速さ（2026-09-18 に MnoA さんの指示で遅くした）
        //
        //  「役計算の表の演出が早すぎる」。**数字が出た瞬間に次が出るので、
        //  読み終わる前に次へ行ってしまう**のが原因だった。
        //  全体でおよそ 1.7 倍に伸ばしてある。
        //
        //  **速さを変えるときはここだけ触ればよい。** 以前はコードの中に
        //  0.12f や 0.8f が直接書いてあり、どれがどの間なのか分からなかった。
        //
        //  とくに効くのは次の2つ:
        //    - RowInterval … 役が1行ずつ入る間隔。ここが詰まると表が一瞬で埋まる
        //    - ReadHold    … 最後の静止。**パネルはこの直後に消える**ので、
        //                    表を読める最後の時間がここ
        // ------------------------------------------------------------

        /// <summary>枠がフェードインする時間。0.30 → 0.45</summary>
        private const float PanelFadeIn = 0.45f;

        /// <summary>役の行が1行ずつ入る間隔。0.12 → 0.22</summary>
        private const float RowInterval = 0.22f;

        /// <summary>合計翻数を出したあとの間。0.25 → 0.45</summary>
        private const float AfterTotalHan = 0.45f;

        /// <summary>倍率を出したあとの間。0.40 → 0.65</summary>
        private const float AfterMultiplier = 0.65f;

        /// <summary>素点・倍率ラベルを出したあとの間。0.18 → 0.30</summary>
        private const float AfterSideValue = 0.30f;

        /// <summary>単騎待ちの行を出したあとの間。0.25 → 0.45</summary>
        private const float AfterTanki = 0.45f;

        /// <summary>表を読み切らせる最後の静止。0.80 → 1.50</summary>
        private const float ReadHold = 1.5f;

        // チュートリアル第1局の説明用。通常のロン演出とは別の順番で同じ清算パネルを読ませるため、
        // パネルそのものと既存の血移動に渡す値だけを一時的に保持する。
        private GameObject _tutorialSettlementContainer;
        private RectTransform _tutorialSettlementPanel;
        private RectTransform _tutorialRefundFormulaTarget;
        private RectTransform _tutorialDamageFormulaTarget;
        private TextMeshProUGUI _tutorialMyBetText;
        private TextMeshProUGUI _tutorialTheirBetText;
        private TextMeshProUGUI _tutorialMyMultText;

        /// <summary>第1局の「点数計算表」を指し示すための実体。</summary>
        public RectTransform TutorialSettlementGuideTarget => _tutorialSettlementPanel;

        /// <summary>払い戻しの説明で、素点と倍率の行をまとめて指し示すための実体。</summary>
        public RectTransform TutorialRefundFormulaGuideTarget => _tutorialRefundFormulaTarget;

        /// <summary>負けた場合の説明で、同じ計算欄を別の手順として指し示すための実体。</summary>
        public RectTransform TutorialDamageFormulaGuideTarget => _tutorialDamageFormulaTarget;

        /// <summary>
        /// チュートリアル第1局専用に、既存の清算パネルを静止表示する。
        /// ロン演出の <see cref="SettlementRoutine"/> は表を読ませた直後に血を動かすため、
        /// 台本の説明を挟む第1局ではここで同じ枠を先に出す。
        /// </summary>
        public void ShowTutorialSettlement(RonSettlementInfo settlement)
        {
            HideTutorialSettlement();
            if (settlement == null) return;

            _tutorialSettlementContainer = new GameObject("TutorialSettlementCanvas", typeof(RectTransform));
            _tutorialSettlementContainer.transform.SetParent(transform, false);
            _tutorialSettlementContainer.transform.SetAsLastSibling();

            var containerRt = _tutorialSettlementContainer.GetComponent<RectTransform>();
            containerRt.anchorMin = Vector2.zero;
            containerRt.anchorMax = Vector2.one;
            containerRt.offsetMin = Vector2.zero;
            containerRt.offsetMax = Vector2.zero;

            var canvas = _tutorialSettlementContainer.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            // セリフ送りは表の上からでも効かなければならないので、GraphicRaycaster は付けない。
            canvas.sortingOrder = UISortingOrders.ResultPanel;

            var scaler = _tutorialSettlementContainer.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);
            scaler.matchWidthOrHeight = 0f;

            var hanTexts = new List<TextMeshProUGUI>();
            TextMeshProUGUI totalHanText, multiplierText;
            TextMeshProUGUI myBetText, theirBetText, myMultText, theirMultText;
            TextMeshProUGUI tankiMine, tankiTheirs;
            TextMeshProUGUI myDeltaText, theirDeltaText, myHpText, theirHpText;

            CanvasGroup panelGroup = BuildSettlementPanel(containerRt, settlement,
                hanTexts, out totalHanText, out multiplierText,
                out myBetText, out theirBetText, out myMultText, out theirMultText,
                out tankiMine, out tankiTheirs,
                out myDeltaText, out theirDeltaText, out myHpText, out theirHpText);

            FillTutorialSettlementValues(settlement, hanTexts, totalHanText, multiplierText,
                myBetText, theirBetText, myMultText, theirMultText, tankiMine, tankiTheirs);
            panelGroup.alpha = 1f;

            _tutorialSettlementPanel = panelGroup.transform as RectTransform;
            _tutorialMyBetText = myBetText;
            _tutorialTheirBetText = theirBetText;
            _tutorialMyMultText = myMultText;

            Canvas.ForceUpdateCanvases();
            if (_tutorialSettlementPanel != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_tutorialSettlementPanel);
            RebuildTutorialFormulaTargets();
        }

        /// <summary>表を女の子の顔と重ならない左側へ寄せる。</summary>
        public void MoveTutorialSettlementLeft()
        {
            if (_tutorialSettlementPanel == null) return;
            _tutorialSettlementPanel.anchoredPosition = new Vector2(-115f, _tutorialSettlementPanel.anchoredPosition.y);
            // 誘導対象は表とは別の透明Rectなので、表を寄せた直後の位置で作り直す。
            RebuildTutorialFormulaTargets();
        }

        /// <summary>表だけを閉じる。第1局の説明を抜けたあとに本編のUIを残さないために呼ぶ。</summary>
        public void HideTutorialSettlement()
        {
            if (_tutorialSettlementContainer != null) Destroy(_tutorialSettlementContainer);
            ClearTutorialSettlementReferences();
        }

        /// <summary>
        /// 表を消しながら、通常の清算と同じ血移動・増減表示を出す。
        /// これで第1局だけ「説明を読んでから獲得を見せる」順にでき、本編の清算順は変えない。
        /// </summary>
        public IEnumerator PlayTutorialSettlementTransfer(RonSettlementInfo settlement,
            PlayerInfoUI playerInfo, EnemyInfoUI enemyInfo,
            int prevLocalHp, int newLocalHp, int prevEnemyHp, int newEnemyHp)
        {
            if (_tutorialSettlementContainer == null || settlement == null ||
                _tutorialMyBetText == null || _tutorialTheirBetText == null)
            {
                if (playerInfo != null) playerInfo.SetHP(newLocalHp);
                if (enemyInfo != null) enemyInfo.SetHP(newEnemyHp);
                yield break;
            }

            yield return BloodTransferRoutine(_tutorialSettlementContainer, settlement,
                _tutorialMyBetText, _tutorialTheirBetText,
                playerInfo, enemyInfo, prevLocalHp, newLocalHp, prevEnemyHp, newEnemyHp);
            ClearTutorialSettlementReferences();
        }

        /// <summary>
        /// 負けた場合の説明では実際のHPを変えず、既存の増減ラベルだけで相手の被ダメージ例を見せる。
        /// この局はプレイヤー勝利済みなので、説明のために相手の状態を書き換えてはいけない。
        /// </summary>
        public void ShowTutorialDamagePreview(EnemyInfoUI enemyInfo, int damage)
        {
            if (enemyInfo == null || enemyInfo.HpAnchor == null || damage <= 0) return;

            GameObject stage = new GameObject("TutorialDamagePreview");
            stage.transform.SetParent(transform, false);
            stage.transform.SetAsLastSibling();
            RectTransform stageRt = stage.AddComponent<RectTransform>();
            stageRt.anchorMin = Vector2.zero;
            stageRt.anchorMax = Vector2.one;
            stageRt.offsetMin = Vector2.zero;
            stageRt.offsetMax = Vector2.zero;

            Canvas canvas = stage.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = UISortingOrders.RonAnimation;

            SpawnHpDeltaLabel(stageRt, enemyInfo.HpAnchor, -damage, AccentThem, placeLeft: false);
            StartCoroutine(DestroyTutorialDamagePreview(stage));
        }

        private static IEnumerator DestroyTutorialDamagePreview(GameObject stage)
        {
            yield return new WaitForSeconds(1f);
            if (stage != null) Destroy(stage);
        }

        private void FillTutorialSettlementValues(RonSettlementInfo settlement, List<TextMeshProUGUI> hanTexts,
            TextMeshProUGUI totalHanText, TextMeshProUGUI multiplierText,
            TextMeshProUGUI myBetText, TextMeshProUGUI theirBetText,
            TextMeshProUGUI myMultText, TextMeshProUGUI theirMultText,
            TextMeshProUGUI tankiMine, TextMeshProUGUI tankiTheirs)
        {
            for (int i = 0; i < hanTexts.Count && i < settlement.Rows.Count; i++)
                hanTexts[i].text = settlement.ShowPerRowHan ? $"{settlement.Rows[i].Han}翻" : "";

            string multiplier = "×" + settlement.Multiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            if (totalHanText != null) totalHanText.text = $"{settlement.TotalHan}翻";
            if (multiplierText != null) multiplierText.text = multiplier;
            if (myBetText != null) myBetText.text = settlement.MyBet.ToString();
            if (theirBetText != null) theirBetText.text = settlement.TheirBet.ToString();
            if (myMultText != null) myMultText.text = multiplier;
            if (theirMultText != null) theirMultText.text = multiplier;

            if (settlement.IsTankiWait && tankiMine != null && tankiTheirs != null)
            {
                tankiMine.text = settlement.LocalWon ? "-" : "×2";
                tankiTheirs.text = settlement.LocalWon ? "×2" : "-";
            }
        }

        private RectTransform CreateTutorialFormulaTarget(string name, RectTransform firstRow, RectTransform lastRow)
        {
            if (_tutorialSettlementContainer == null || firstRow == null || lastRow == null) return null;

            Vector3[] corners = new Vector3[4];
            firstRow.GetWorldCorners(corners);
            Vector3 min = corners[0];
            Vector3 max = corners[2];
            lastRow.GetWorldCorners(corners);
            min = Vector3.Min(min, corners[0]);
            max = Vector3.Max(max, corners[2]);

            RectTransform containerRt = _tutorialSettlementContainer.GetComponent<RectTransform>();
            Vector2 localMin = containerRt.InverseTransformPoint(min);
            Vector2 localMax = containerRt.InverseTransformPoint(max);

            GameObject target = new GameObject(name, typeof(RectTransform));
            target.transform.SetParent(_tutorialSettlementContainer.transform, false);
            RectTransform targetRt = target.GetComponent<RectTransform>();
            targetRt.anchorMin = targetRt.anchorMax = new Vector2(0.5f, 0.5f);
            targetRt.pivot = new Vector2(0.5f, 0.5f);
            targetRt.sizeDelta = localMax - localMin;
            targetRt.anchoredPosition = (localMin + localMax) * 0.5f;
            return targetRt;
        }

        private void RebuildTutorialFormulaTargets()
        {
            if (_tutorialRefundFormulaTarget != null) Destroy(_tutorialRefundFormulaTarget.gameObject);
            if (_tutorialDamageFormulaTarget != null) Destroy(_tutorialDamageFormulaTarget.gameObject);

            RectTransform betRow = _tutorialMyBetText != null ? _tutorialMyBetText.rectTransform.parent as RectTransform : null;
            RectTransform multRow = _tutorialMyMultText != null ? _tutorialMyMultText.rectTransform.parent as RectTransform : null;
            _tutorialRefundFormulaTarget = CreateTutorialFormulaTarget("RefundFormulaGuideTarget", betRow, multRow);
            _tutorialDamageFormulaTarget = CreateTutorialFormulaTarget("DamageFormulaGuideTarget", betRow, multRow);
        }

        private void ClearTutorialSettlementReferences()
        {
            _tutorialSettlementContainer = null;
            _tutorialSettlementPanel = null;
            _tutorialRefundFormulaTarget = null;
            _tutorialDamageFormulaTarget = null;
            _tutorialMyBetText = null;
            _tutorialTheirBetText = null;
            _tutorialMyMultText = null;
        }

        private IEnumerator SettlementRoutine(RectTransform containerRt, GameObject container, RonSettlementInfo s,
            PlayerInfoUI playerInfo, EnemyInfoUI enemyInfo, int prevLocalHp, int newLocalHp, int prevEnemyHp, int newEnemyHp)
        {
            // 役の帯は役目を終えている。パネルが同じ役名を翻数つきで出し直すので、
            // 情報を落とさずに場所を空けられる。**宣言そのもの（1つずつ出る所）は上でやり終えている。**
            var ribbon = containerRt.Find("YakuRibbon");
            CanvasGroup ribbonGroup = null;
            if (ribbon != null)
            {
                ribbonGroup = ribbon.gameObject.GetComponent<CanvasGroup>();
                if (ribbonGroup == null) ribbonGroup = ribbon.gameObject.AddComponent<CanvasGroup>();
            }

            var hanTexts = new List<TextMeshProUGUI>();
            TextMeshProUGUI totalHanText, multiplierText;
            TextMeshProUGUI myBetText, theirBetText, myMultText, theirMultText;
            TextMeshProUGUI tankiMine, tankiTheirs;
            TextMeshProUGUI myDeltaText, theirDeltaText, myHpText, theirHpText;

            CanvasGroup panelGroup = BuildSettlementPanel(containerRt, s,
                hanTexts, out totalHanText, out multiplierText,
                out myBetText, out theirBetText, out myMultText, out theirMultText,
                out tankiMine, out tankiTheirs,
                out myDeltaText, out theirDeltaText, out myHpText, out theirHpText);

            // 枠がフェードインする。ここではまだ数字は入っていない
            const float fadeIn = PanelFadeIn;
            for (float t = 0; t < fadeIn; t += Time.deltaTime)
            {
                float p = t / fadeIn;
                panelGroup.alpha = p;
                if (ribbonGroup != null) ribbonGroup.alpha = 1f - p;
                yield return null;
            }
            panelGroup.alpha = 1f;
            if (ribbonGroup != null) ribbonGroup.alpha = 0f;

            // ① 役ごとの翻数が上から入る
            for (int i = 0; i < hanTexts.Count && i < s.Rows.Count; i++)
            {
                // **`飜`(U+98BB) は PixelMplus に入っていない。** 使うと □ になる。
                // ゲームの他の表示テキスト（AbilityUI・チュートリアル）は `翻`(U+7FFB) を使っているので揃える。
                // コード中のコメントや Tooltip には `飜` が残っているが、あれは画面に出ない。
                hanTexts[i].text = s.ShowPerRowHan ? $"{s.Rows[i].Han}翻" : "";
                yield return new WaitForSeconds(RowInterval);
            }

            // ② 合計と倍率
            totalHanText.text = $"{s.TotalHan}翻";
            yield return new WaitForSeconds(AfterTotalHan);
            multiplierText.text = "×" + s.Multiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            yield return new WaitForSeconds(AfterMultiplier);

            // ③ 素点と倍率が左右に入る
            string multLabel = "×" + s.Multiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            myBetText.text = s.MyBet.ToString();
            theirBetText.text = s.TheirBet.ToString();
            yield return new WaitForSeconds(AfterSideValue);
            myMultText.text = multLabel;
            theirMultText.text = multLabel;
            yield return new WaitForSeconds(AfterSideValue);

            // 単騎で倍になるのは負けた側だけ。**今まで画面のどこにも出ていなかった行。**
            if (s.IsTankiWait && tankiMine != null && tankiTheirs != null)
            {
                // **ダッシュ `—`(U+2014) もフォントに無い。** ASCII のハイフンで代用する
                tankiMine.text = s.LocalWon ? "-" : "×2";
                tankiTheirs.text = s.LocalWon ? "×2" : "-";
                yield return new WaitForSeconds(AfterTanki);
            }

            // ④ 表を読み切る間。**パネルはこのあと消える**ので、ここが表を見られる最後の時間。
            //
            // **`myDeltaText` / `theirDeltaText` / `myHpText` / `theirHpText` には何も入れない。**
            // 血の増減はこの下の演出が答えとして出すもので、先にパネルへ書くと山が消える。
            // 行そのものも `BuildSettlementPanel` で非アクティブにしてある（out は署名維持のために残している）。
            if (KillingMahjong.Managers.AudioManager.Instance != null)
            {
                KillingMahjong.Managers.AudioManager.Instance.PlayRankVoice(s.RankName);
            }

            yield return new WaitForSeconds(ReadHold);

            // ⑤ 血が動く。**パネルを消しながら**素点の数字を持ち出す
            yield return BloodTransferRoutine(container, s, myBetText, theirBetText,
                playerInfo, enemyInfo, prevLocalHp, newLocalHp, prevEnemyHp, newEnemyHp);
        }

        private static string FormatDelta(int v)
        {
            return v > 0 ? "+" + v : v.ToString(); // 負号は int の表記がそのまま使える
        }
    }
}
