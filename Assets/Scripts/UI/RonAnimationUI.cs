using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public partial class RonAnimationUI : MonoBehaviour
    {
        // ロンの決着は、カットイン・清算・血の移動を順番に見せて因果を読ませる。
        // 同じ状態を渡しながら各段階を独立して読めるように、責務ごとに partial を分けている。
        //
        // - RonAnimationUI.Sequence.cs: カットイン、役の帯、手牌、清算への接続
        // - RonAnimationUI.Settlement.cs: 清算パネルの数値を入れる順序
        // - RonAnimationUI.BloodTransfer.cs: 素点からHPへ落とす演出
        // - RonAnimationUI.BloodTransferLabels.cs: 着弾後の注記、増減表示、SE
        // - RonAnimationUI.SettlementPanel.cs: 清算パネルの枠と行の組み立て

        // **血しぶきの設定4つと bloodSplatterSprite は 2026-08-29 に消した。**
        // 巨大スコアと同時に飛ばすものだったので、旧経路を消した時点で参照が全て無くなった。
        // インスペクタに刺さっていた値はシーンを保存した時点で落ちる。
        [SerializeField] private TMP_FontAsset customFont;

        [Header("Player Ron Bubble (Pre-Animation)")]
        [Tooltip("自分がロンした瞬間に盤面上に出す吹き出し")]
        [SerializeField] private GameObject playerRonBubbleContainer;
        
        [Header("Hand Display Layout")]
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private TileResourceManager tileResourceManager;
        [SerializeField] private float tileSpacing = 115f;
        [SerializeField] private float tileScale = 1.5f;

        private static readonly Color PanelLine  = new Color32(0x3A, 0x44, 0x68, 0xFF);
        private static readonly Color PanelBg    = new Color32(0x0F, 0x13, 0x26, 0xF2);
        private static readonly Color PanelInk   = new Color32(0xE8, 0xE4, 0xF0, 0xFF);
        private static readonly Color PanelFaint = new Color32(0x97, 0xA0, 0xC0, 0xFF);
        private static readonly Color AccentGold = new Color32(0xFF, 0xD3, 0x4D, 0xFF); // 翻数
        private static readonly Color AccentMine = new Color32(0x57, 0xC7, 0xE8, 0xFF); // 自分
        private static readonly Color AccentThem = new Color32(0xF2, 0x70, 0x5A, 0xFF); // 相手

        private const float PanelWidth = 560f;
        private const float ValueColumn = 150f;

        // ============================================================
        //  血が動く瞬間
        //
        //  2026-08-29 の指示（**それぞれ非対称な表示方法で**）:
        //   ① 素点の数字が画面中央へ、大きくなりながら移動する
        //   ② 「満貫」表示くらいの大きさになったら、数値が変化する
        //   ③ 変化した数値がHPへ向かって収縮しながら移動し、HPの数値が動く
        //   ④ 同時に、自分と相手のHPの隣にこの局の増減を出す
        //
        //  **飛ぶのは片側だけ。これが「非対称」の中身であり、同時に嘘を防いでいる。**
        //  勝者の獲得と敗者の損失は母数が違う（単騎なら負けた側だけ2倍、強襲なら勝者は0）ので、
        //  両側から数字を飛ばすと「血が相手から自分へ移った」ように見えてしまう。
        //  旧経路の SpawnAbsorbParticles（中央 → 勝者）がまさにその絵で、新経路には持ってきていない。
        //
        //  尺: 離陸0.35 → 変化0.25 → 静止0.25 → 着弾0.30 → HP0.80 → 静止0.50 ＝ 約2.45秒。
        //  **パネルを読ませる 0.8秒（SettlementRoutine 側）を別に取ってある。**
        //  最後の静止は 2026-08-29 の実機確認のあと 1.00 → 0.50 に詰めた（見ていて一番余っていた場所）。
        //  **中央の静止 0.25 は逆に短いくらいなので、削るならここではない。**
        // ============================================================

        /// <summary>
        /// 中央で止まるときの文字の大きさ。**ゲーム内の「満貫」表示と同じ寸法にする**という指示。
        /// 拡大は fontSize ではなく localScale でやる（毎フレーム fontSize を動かすと再レイアウトが走る）ので、
        /// 文字は最初からこの大きさで作って縮めた状態から始める。
        /// </summary>
        private const float BloodPeakFontSize = UITypography.Huge;

        /// <summary>着弾したときの大きさ。HPの数字と同じくらいに収める。</summary>
        private const float BloodLandFontSize = 28f;

        private void Start()
        {
            PrepareForPreDialogue();
        }

        public void PrepareForPreDialogue()
        {
            if (playerRonBubbleContainer != null) playerRonBubbleContainer.SetActive(false);
        }

        public bool HasPlayerRonBubble()
        {
            return playerRonBubbleContainer != null;
        }

        public void ShowPlayerRonBubble(bool show)
        {
            if (playerRonBubbleContainer != null)
            {
                playerRonBubbleContainer.SetActive(show);
            }
        }

        /// <param name="formula">「6飜」のような飜数の文字列。表示には使わず、安手かどうかの判定に使う</param>
        /// <param name="scoreFormula">
        /// 「200 × 1.5」のような計算式。**サーバーの liquidation から作って渡す。**
        /// 渡さなかった場合は式を出さず、獲得額だけを見せる。
        /// </param>
        /// <param name="settlement">
        /// 清算パネルの内容。**渡すと「式 → ランク → 巨大な数字」の代わりに1枚のパネルを出す。**
        /// null なら従来の見せ方に落ちる（チュートリアルなど内訳を持たない経路のため）。
        /// </param>
        public void PlayRonSequence(List<int> handTiles, int ronTile, List<string> yakuList, string formula, string rankName, int score, bool isLocalPlayerWin,
            PlayerInfoUI playerInfo, EnemyInfoUI enemyInfo, int prevLocalHp, int newLocalHp, int prevEnemyHp, int newEnemyHp, System.Action onComplete,
            string scoreFormula = null, RonSettlementInfo settlement = null)
        {
            StartCoroutine(SequenceRoutine(handTiles, ronTile, yakuList, formula, rankName, score, isLocalPlayerWin, playerInfo, enemyInfo, prevLocalHp, newLocalHp, prevEnemyHp, newEnemyHp, onComplete, scoreFormula, settlement));
        }
    }
}
