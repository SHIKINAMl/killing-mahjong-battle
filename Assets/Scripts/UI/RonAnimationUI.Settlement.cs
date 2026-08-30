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
            const float fadeIn = 0.3f;
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
                yield return new WaitForSeconds(0.12f);
            }

            // ② 合計と倍率
            totalHanText.text = $"{s.TotalHan}翻";
            yield return new WaitForSeconds(0.25f);
            multiplierText.text = "×" + s.Multiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            yield return new WaitForSeconds(0.4f);

            // ③ 素点と倍率が左右に入る
            string multLabel = "×" + s.Multiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            myBetText.text = s.MyBet.ToString();
            theirBetText.text = s.TheirBet.ToString();
            yield return new WaitForSeconds(0.18f);
            myMultText.text = multLabel;
            theirMultText.text = multLabel;
            yield return new WaitForSeconds(0.18f);

            // 単騎で倍になるのは負けた側だけ。**今まで画面のどこにも出ていなかった行。**
            if (s.IsTankiWait && tankiMine != null && tankiTheirs != null)
            {
                // **ダッシュ `—`(U+2014) もフォントに無い。** ASCII のハイフンで代用する
                tankiMine.text = s.LocalWon ? "-" : "×2";
                tankiTheirs.text = s.LocalWon ? "×2" : "-";
                yield return new WaitForSeconds(0.25f);
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

            yield return new WaitForSeconds(0.8f);

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
