using System;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.Common;

namespace KillingMahjong.Managers
{
    /// <summary>局の結末。旧実装の日本語マジックストリング比較を置き換える。</summary>
    public enum TutorialOutcome
    {
        PlayerRon,
        EnemyRon,
        Draw
    }

    /// <summary>
    /// セリフの話者。立ち絵の出し分けに使う。
    ///
    /// **1 は欠番。** 以前あった「あずにゃん先輩」の跡。
    /// TutorialScenario.asset には地の文が `speaker: 2` で保存済みなので、
    /// System を 1 に詰めると保存された行が別の話者になってしまう。
    /// </summary>
    public enum TutorialSpeaker
    {
        Enemy = 0,   // 対戦相手の女の子
        System = 2   // 地の文・ルール説明
    }

    [Serializable]
    public class TutorialLine
    {
        public TutorialSpeaker speaker = TutorialSpeaker.Enemy;
        [TextArea(1, 3)] public string text;

        public TutorialLine() { }

        public TutorialLine(string text, TutorialSpeaker speaker = TutorialSpeaker.Enemy)
        {
            this.text = text;
            this.speaker = speaker;
        }
    }

    /// <summary>
    /// 敵が1つの能力を見せる単位（手順⑱〜⑲）。
    /// skillType は SkillNames の定数を使う。SEと能力欄の誘導はこの type から引く。
    /// </summary>
    [Serializable]
    public class TutorialAbilityShowcase
    {
        [Tooltip("SkillNames の type（mulligan / perspective / boost_hand）")]
        public string skillType;

        [Tooltip("発動前に流すセリフ")]
        public List<TutorialLine> beforeLines = new List<TutorialLine>();

        [Tooltip("発動後に流すセリフ")]
        public List<TutorialLine> afterLines = new List<TutorialLine>();

        [Tooltip("透視(perspective)のとき、プレイヤーの牌のうち何枚に透視マークを出すか")]
        public int perspectiveTileCount = 3;

        [Tooltip("役強化(boost_hand)のとき、敵が強化する役名。役一覧に敵の強化として表示される。")]
        public string boostYakuName = "";

        [Tooltip("役強化で上乗せする翻数")]
        public int boostHan = 1;

        public TutorialAbilityShowcase() { }

        public TutorialAbilityShowcase(string skillType, TutorialLine before, TutorialLine after)
        {
            this.skillType = skillType;
            if (before != null) beforeLines.Add(before);
            if (after != null) afterLines.Add(after);
        }
    }

    /// <summary>
    /// 1局分のチュートリアル台本。
    /// 牌は「牌種（0〜28）」で持ち、ドラフラグは TutorialTiles.EncodeAll が付与する。
    /// </summary>
    [Serializable]
    public class TutorialRoundData
    {
        [Header("■ この局の名前（Inspector の見出しになるだけ）")]
        public string label = "第N局";

        [Header("■ 牌を変える ─ 配られる牌・手牌・ドラ")]
        [Tooltip("この局で配られる34枚（牌種 0〜28）。重複あり。")]
        [TilePicker] public List<int> wallBaseIds = new List<int>();

        [Tooltip("オート満貫ボタンで組まれる13枚（牌種 0〜28）。wallBaseIds の部分集合であること。")]
        [TilePicker] public List<int> manganHandBaseIds = new List<int>();

        [Tooltip("上記手牌の待ち牌（牌種）。WaitUI の表示に使う。")]
        [TilePicker] public List<int> waitBaseIds = new List<int>();

        [Tooltip("オート満貫手の役名。聴牌チェック（is_tenpai）のモック応答に使う。")]
        public List<string> manganHandYaku = new List<string>();

        [Tooltip("オート満貫手の翻数。聴牌チェックのモック応答に使う。")]
        public int manganHandHan = 6;

        [Tooltip("この局のドラ（牌種）。-1 でドラなし。")]
        [TilePicker(allowNone: true)] public int doraBaseId = -1;

        [Header("■ 状況を変える ─ 手牌フェイズの進み方")]
        [Tooltip("手動で牌を動かせるか（手順①。第1局と第5局は true）")]
        public bool allowManualHandSelection = true;

        [Tooltip("13枚そろったら『自動』と『決定』の両方を出し、矢印の誘導もしない。\n" +
                 "自力で組んでも『自動』に任せてもよい局に使う（第5局）。")]
        public bool freeHandBuilding = false;

        [Tooltip("最初の決定を無条件で弾くか（手順②。満貫判定は持たずスクリプトで弾く）")]
        public bool rejectFirstConfirm = true;

        [Tooltip("オート満貫を使わないと決定できないか。制約『満貫手以下での開始は不可』を担保する。")]
        public bool requireAutoManganToConfirm = true;

        [Header("▲ 数値注意 ─ 賭け金（全局のHP収支に連鎖する）")]
        [Tooltip("固定の賭け金。\n" +
                 "※ここを変えると以降の全局のHPが変わり、途中で誰かが死ぬと進行不能になります。\n" +
                 "　 変更する場合は TutorialScenario.cs 冒頭の収支表を必ず確認してください。")]
        public int betAmount = 1000;

        [Tooltip("賭け金を促すセリフ。{0} に betAmount が入る。")]
        [TextArea(1, 2)] public string betPromptText = "{0}円賭けてちょうだい。";

        [Tooltip("前局が流局で賭け金が持ち越されたときのセリフ。{0}=持ち越し額 {1}=場の総額。" +
                 "空なら既定文が使われる。流局の次の局は賭け金を指示してはいけない（自動で同額が賭けられる仕様）。")]
        public List<TutorialLine> inheritedBetLines = new List<TutorialLine>();

        [Header("■ 状況を変える ─ 打牌フェイズの進み方")]
        [Tooltip("敵が順番に捨てる牌（牌種）。要素数がそのまま手数になる。")]
        [TilePicker] public List<int> enemyDiscardBaseIds = new List<int>();

        [Tooltip("開始直後に自動で進める手数。ここまでは自分も相手も自動で捨てる。0 なら最初から手動。")]
        public int autoDiscardTurns = 0;

        [Tooltip("プレイヤーが打てない牌（牌種）。-1 でなし。手順⑭の嘘の待ち牌。")]
        [TilePicker(allowNone: true)] public int lockedTileBaseId = -1;

        [TextArea(1, 2)] public string lockedTileMessage = "その牌は出しちゃダメって言ったでしょ！";

        [Tooltip("敵が能力を使う演出を挟むか（手順⑱）")]
        public bool enemyUsesAbility = false;

        [Tooltip("能力の実演を始める前のセリフ。能力は手牌フェイズ専用なので、このフェイズ中に流れる。")]
        public List<TutorialLine> abilityIntroLines = new List<TutorialLine>();

        [Tooltip("敵が順に見せる能力（手順⑱〜⑲）。enemyUsesAbility が true のときだけ使われる。")]
        public List<TutorialAbilityShowcase> abilityShowcases = new List<TutorialAbilityShowcase>();

        [Tooltip("手順⑲: 能力そのものの説明。能力デモのあとに流す。")]
        public List<TutorialLine> abilityExplainLines = new List<TutorialLine>();

        [Tooltip("手順⑳: 能力強化の説明。役一覧への誘導の前に流す。")]
        public List<TutorialLine> enhanceExplainLines = new List<TutorialLine>();

        [Tooltip("手順⑳: 役一覧（役表）を実際に開かせる誘導を行うか。")]
        public bool guideToYakuList = false;

        [Tooltip("役一覧を開いたあとのセリフ")]
        public List<TutorialLine> onYakuListOpenedLines = new List<TutorialLine>();

        [Header("■ 状況を変える ─ この局の決着のしかた")]
        public TutorialOutcome outcome = TutorialOutcome.Draw;

        [Tooltip("PlayerRon 時、敵が捨てるアタリ牌（牌種）。enemyDiscardBaseIds の末尾と一致させること。")]
        [TilePicker(allowNone: true)] public int playerWinningTileBaseId = -1;

        [Tooltip("EnemyRon 時、敵の手牌のうち面子部分12枚（牌種）。実際のアタリ牌は単騎待ちとして末尾に追加される。")]
        [TilePicker] public List<int> enemyRonMeldBaseIds = new List<int>();

        [Tooltip("EnemyRon 時、何手目のプレイヤー打牌で放銃するか（1始まり）")]
        public int enemyRonOnPlayerDiscardTurn = 5;

        [Header("▲ 数値注意 ─ 決着時の役と飜数（HP収支に連鎖する）")]
        public List<string> yakuList = new List<string>();
        public string formulaText = "";
        public string rankText = "";

        [Tooltip("EnemyRon のとき、敵の役の飜数。倍率は GameRules.GetMultiplier がここから決める。" +
                 "PlayerRon のときはプレイヤーの手（manganHandHan）を使うのでこの値は見ない。")]
        public int enemyWinningHan = 13;

        [Tooltip("勝者が単騎待ちで上がったか。true だと敗者の失う額が2倍になる。")]
        public bool isTankiWin = false;

        [Header("▲ 数値注意 ─ 流局ダメージ（局をまたいでHPに反映）")]
        [Tooltip("流局によるプレイヤーへのダメージ。手順⑯の『流局のダメージ』。")]
        public int drawDamageToPlayer = 0;

        [Header("● セリフを変える ─ ここは自由に書き換えてOK")]
        public List<TutorialLine> introLines = new List<TutorialLine>();

        [Tooltip("イントロの何行目のあとに盤面（山牌・手牌・ドラ・HP）を出すか。0始まり。" +
                 "-1 ならイントロを全て流し終えたあと。それまでは女の子とセリフだけが見える。")]
        public int revealBoardAfterLineIndex = -1;
        [Tooltip("13枚そろったあと、『自動』ボタンを開放する直前のセリフ。空なら既定文が使われる。")]
        public List<TutorialLine> onHandFilledLines = new List<TutorialLine>();

        [Tooltip("プレイヤーが自力で満貫手を組めたときのセリフ。この場合『自動』は出さず決定へ進ませる。空なら既定文。")]
        public List<TutorialLine> onSelfManganLines = new List<TutorialLine>();

        [Tooltip("手牌決定後・賭け金フェイズ前")]
        public List<TutorialLine> beforeBetLines = new List<TutorialLine>();
        [Tooltip("対局開始直後")]
        public List<TutorialLine> onBattleStartLines = new List<TutorialLine>();

        [Tooltip("自動打牌が終わり、プレイヤーが自分で打つ番になる直前のセリフ")]
        public List<TutorialLine> beforeManualDiscardLines = new List<TutorialLine>();
        [Tooltip("結末演出のあと")]
        public List<TutorialLine> outroLines = new List<TutorialLine>();
    }

    /// <summary>
    /// チュートリアル全体の台本。Inspector で差し替えられるように ScriptableObject にしている。
    /// TutorialManager に未設定の場合は BuildDefault() の内容が使われる。
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialScenario", menuName = "KillingMahjong/Tutorial Scenario")]
    public partial class TutorialScenario : ScriptableObject
    {
        public int playerStartHp = 20000;
        public int enemyStartHp = 20000;

        public List<TutorialRoundData> rounds = new List<TutorialRoundData>();

        [Header("● セリフを変える ─ 全局終了後")]
        public List<TutorialLine> endingLines = new List<TutorialLine>();
        public string titleSceneName = "タイトルシーン";


        /// <summary>
        /// `Resources/TutorialLines` があればセリフを差し替える。無ければ何もしない。
        /// </summary>
        private static void ApplyLineTable(TutorialScenario s)
        {
            var table = Resources.Load<Tutorial.TutorialLineTable>(
                Tutorial.TutorialLineTable.ResourcePath);
            if (table == null) return;

            table.ApplyTo(s);
            Debug.Log($"[TutorialScenario] セリフ表を適用しました（{table.rows.Count} 行）。");
        }

        /// <summary>
        /// 台本中の牌IDがすべて牌種の範囲（0〜28）に収まっているか検証する。
        /// </summary>
        public bool Validate()
        {
            bool ok = true;
            for (int i = 0; i < rounds.Count; i++)
            {
                var r = rounds[i];
                string tag = $"rounds[{i}] ({r.label})";
                ok &= TutorialTiles.Validate(r.wallBaseIds, $"{tag}.wallBaseIds");
                ok &= TutorialTiles.Validate(r.manganHandBaseIds, $"{tag}.manganHandBaseIds");
                ok &= TutorialTiles.Validate(r.waitBaseIds, $"{tag}.waitBaseIds");
                ok &= TutorialTiles.Validate(r.enemyDiscardBaseIds, $"{tag}.enemyDiscardBaseIds");
                ok &= TutorialTiles.Validate(r.enemyRonMeldBaseIds, $"{tag}.enemyRonMeldBaseIds");

                if (r.wallBaseIds.Count != 34)
                {
                    Debug.LogWarning($"[TutorialScenario] {tag}: 山牌が {r.wallBaseIds.Count} 枚です（想定34枚）。");
                }
                if (r.manganHandBaseIds.Count != 13)
                {
                    Debug.LogWarning($"[TutorialScenario] {tag}: 手牌が {r.manganHandBaseIds.Count} 枚です（想定13枚）。");
                }

                // 手牌が山牌の部分集合になっているか（重複枚数込み）
                var pool = new List<int>(r.wallBaseIds);
                foreach (int t in r.manganHandBaseIds)
                {
                    int idx = pool.IndexOf(t);
                    if (idx < 0)
                    {
                        Debug.LogError($"[TutorialScenario] {tag}: 手牌の牌 {t} が山牌に足りません。");
                        ok = false;
                    }
                    else
                    {
                        pool.RemoveAt(idx);
                    }
                }

                if (r.outcome == TutorialOutcome.PlayerRon)
                {
                    if (r.enemyDiscardBaseIds.Count == 0 ||
                        r.enemyDiscardBaseIds[r.enemyDiscardBaseIds.Count - 1] != r.playerWinningTileBaseId)
                    {
                        Debug.LogError(
                            $"[TutorialScenario] {tag}: PlayerRon なのに敵の最終打牌が playerWinningTileBaseId " +
                            $"({r.playerWinningTileBaseId}) と一致していません。");
                        ok = false;
                    }
                    if (!r.waitBaseIds.Contains(r.playerWinningTileBaseId))
                    {
                        Debug.LogWarning($"[TutorialScenario] {tag}: アタリ牌が待ち牌リストに含まれていません。");
                    }
                }
            }
            return ok;
        }
    }
}
