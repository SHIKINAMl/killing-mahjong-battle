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
    public class TutorialScenario : ScriptableObject
    {
        public int playerStartHp = 20000;
        public int enemyStartHp = 20000;

        public List<TutorialRoundData> rounds = new List<TutorialRoundData>();

        [Header("● セリフを変える ─ 全局終了後")]
        public List<TutorialLine> endingLines = new List<TutorialLine>();
        public string titleSceneName = "タイトルシーン";

        /// <summary>
        /// 台本アセットが未設定のときに使われる既定シナリオ。
        ///
        /// 血の流れ。
        ///
        /// 賭け金は**確定した時点で両者の血から引かれ**、場に積まれる（賭けた分は先に払う）。
        /// 決着したときの増減はこれとは別に `GameRules` の式で決まる:
        ///
        ///   勝者が得る額 = 勝者自身の賭け金 × 勝者の役の倍率
        ///   敗者が失う額 = 敗者自身の賭け金 × 勝者の役の倍率（単騎で上がられたら2倍）
        ///   倍率: 満貫1 / 跳満1.5 / 倍満2 / 三倍満3 / 役満4 / ダブル役満8
        ///
        /// 満貫（1倍）は払った賭け金と同額が戻るだけなので、勝っても差し引き0になる。
        /// 流局では決着せず、賭け金は次の局へ積み増される（＝次の決着の元手が増える）。
        /// 敵は第4局で能力を3つ使い、そのコストぶん自分の血を失う。
        ///
        ///   開始                                              P20000 / E20000
        ///   第1局 賭け金2000ずつ引かれる                      P18000 / E18000
        ///         自分ロン 清一色6飜=跳満1.5倍
        ///           自分 +3000 / 相手 -3000                   P21000 / E15000
        ///   第2局 賭け金600ずつ                               P20400 / E14400
        ///         流局（場はそのまま持ち越し）                P20400 / E14400
        ///   第3局 同額600が自動で引かれ、賭け金は各1200        P19800 / E13800
        ///         相手ロン 四暗刻単騎13飜=役満4倍・単騎
        ///           相手 +4800 / 自分 -9600                   P10200 / E18600
        ///   第4局 能力コスト -12700（手牌フェイズ）           P10200 / E 5900
        ///         賭け金1000ずつ                              P 9200 / E 4900
        ///         自分ロン 対々和+混一色5飜=満貫1倍
        ///           自分 +1000 / 相手 -1000                   P10200 / E 3900
        ///   第5局 賭け金1000ずつ                              P 9200 / E 2900
        ///         自分ロン 純正九蓮宝燈26飜=ダブル役満8倍
        ///           自分 +8000 / 相手 -8000（残2900で死亡）   P17200 / E    0（決着）
        ///
        /// 数値を触るときの制約:
        ///   - 第3局で自分が死なないこと（単騎の2倍が効くので損失が跳ね上がる）
        ///   - 第4局の前に相手が能力コスト12700を払えること
        ///   - 賭け金の支払いで誰も死なないこと（第4局・第5局の相手の残り血が薄い）
        ///   - 第4局のあとも相手が生き残り、第5局で死ぬこと
        ///   - 全局とも満貫以上（制約『満貫手以下での開始は不可』）
        /// 数値を触ったら必ず最後まで通して確認すること。
        /// </summary>
        public static TutorialScenario BuildDefault()
        {
            var s = CreateInstance<TutorialScenario>();
            s.playerStartHp = 20000;
            s.enemyStartHp = 20000;

            // --- 共通の配牌 ---
            // 手牌13枚: 一萬×3 二三四萬 五六七萬 八萬×2 九萬×2
            //   → 111m 234m 567m 88m 99m の清一色シャンポン待ち（8m / 9m）
            var hand = new List<int>
            {
                TutorialTiles.Man(1), TutorialTiles.Man(1), TutorialTiles.Man(1),
                TutorialTiles.Man(2), TutorialTiles.Man(3), TutorialTiles.Man(4),
                TutorialTiles.Man(5), TutorialTiles.Man(6), TutorialTiles.Man(7),
                TutorialTiles.Man(8), TutorialTiles.Man(8),
                TutorialTiles.Man(9), TutorialTiles.Man(9),
            };
            // 待ちは 8m / 9m のシャンポンに加えて 7m の三面待ち。
            //   7m: 11m雀頭 + 123m 456m 789m 789m
            //   8m: 111m 234m 567m 888m 99m
            //   9m: 111m 234m 567m 999m 88m
            // いずれの和了形も萬子のみなので清一色が成立する。
            var waits = new List<int> { TutorialTiles.Man(7), TutorialTiles.Man(8), TutorialTiles.Man(9) };

            // 残り21枚: 一筒〜九筒 / 一索〜九索 / 東×2 / 西
            var rest = new List<int>();
            for (int n = 1; n <= 9; n++) rest.Add(TutorialTiles.Pin(n));
            for (int n = 1; n <= 9; n++) rest.Add(TutorialTiles.Sou(n));
            rest.Add(TutorialTiles.Ton);
            rest.Add(TutorialTiles.Ton);
            rest.Add(TutorialTiles.Sha);

            List<int> Wall()
            {
                var w = new List<int>(hand);
                w.AddRange(rest);
                return w;
            }

            // --- 第4局（能力）専用の配牌 ---
            // 第4局は敵が能力に12700もの血を払った直後なので、跳満12000で上がると
            // 敵の血が尽きて第5局（決着）が成立しない。ここは満貫ちょうどに抑えたい。
            //
            // ただし制約『満貫手以下での開始は不可』があるため、安くしすぎてもいけない。
            // 清一色（門前6飜＝跳満）ではなく、ちょうど5飜＝満貫になる構成にする。
            //   111p 444p 777p 99p 東東 → 9p で和了すると
            //   111p 444p 777p 999p 東東 = 対々和(2飜) + 混一色(門前3飜) = 5飜 満貫
            // 待ちは 9p / 東 のシャンポン。
            var abilityHand = new List<int>
            {
                TutorialTiles.Pin(1), TutorialTiles.Pin(1), TutorialTiles.Pin(1),
                TutorialTiles.Pin(4), TutorialTiles.Pin(4), TutorialTiles.Pin(4),
                TutorialTiles.Pin(7), TutorialTiles.Pin(7), TutorialTiles.Pin(7),
                TutorialTiles.Pin(9), TutorialTiles.Pin(9),
                TutorialTiles.Ton, TutorialTiles.Ton,
            };

            var abilityWaits = new List<int> { TutorialTiles.Pin(9), TutorialTiles.Ton };
            int abilityWinningTile = TutorialTiles.Pin(9);

            // 残り21枚。手牌の待ち（9p / 東）は1枚も含めないこと。
            // 含めるとプレイヤーが自分の待ちを打ててしまい、フリテンの説明が必要になる。
            List<int> AbilityWall()
            {
                var w = new List<int>(abilityHand);
                for (int n = 1; n <= 9; n++) w.Add(TutorialTiles.Man(n));
                for (int n = 1; n <= 9; n++) w.Add(TutorialTiles.Sou(n));
                w.Add(TutorialTiles.Sha);
                w.Add(TutorialTiles.Pin(2));
                w.Add(TutorialTiles.Pin(5)); // ドラ表示と同じ牌。手牌には入らないので打点は動かない
                return w;
            }

            // --- 第5局（決着）専用の配牌 ---
            // 決着局が第1局とまったく同じ「清一色 6飜 跳満 12000」だと、
            // 大逆転のはずの最後の一撃が開幕と同じ勝ち方にしか見えない。
            // そこで決着局だけ役を跳ね上げる。
            //
            // 1112345678999m は九面待ち（1〜9萬のどれでも和了）なので、
            // どの萬子で和了しても純正九蓮宝燈が成立する。
            var finalHand = new List<int>
            {
                TutorialTiles.Man(1), TutorialTiles.Man(1), TutorialTiles.Man(1),
                TutorialTiles.Man(2), TutorialTiles.Man(3), TutorialTiles.Man(4),
                TutorialTiles.Man(5), TutorialTiles.Man(6), TutorialTiles.Man(7),
                TutorialTiles.Man(8),
                TutorialTiles.Man(9), TutorialTiles.Man(9), TutorialTiles.Man(9),
            };

            var finalWaits = new List<int>();
            for (int n = 1; n <= 9; n++) finalWaits.Add(TutorialTiles.Man(n));

            List<int> FinalWall()
            {
                var w = new List<int>(finalHand);
                w.AddRange(rest);
                return w;
            }

            // アタリ牌は真ん中の五萬。プレイヤーが打てるのは山の残り21枚（筒子・索子・字牌）
            // なので、萬子を敵が捨てても牌が5枚目になることはない。
            int finalWinningTile = TutorialTiles.Man(5);

            int dora = TutorialTiles.Pin(5);

            // 敵の捨て牌に使う無難な牌（プレイヤーの待ち 8m/9m を含まない）
            int d1 = TutorialTiles.Ton;
            int d2 = TutorialTiles.Pin(1);
            int d3 = TutorialTiles.Sou(1);
            int d4 = TutorialTiles.Pin(9);
            int d5 = TutorialTiles.Sou(9);
            int d6 = TutorialTiles.Sha;

            // 第2局（流局の説明）用: 17手ぶんの敵の捨て牌。
            // プレイヤーの待ち 7m/8m/9m を含まないよう筒子・索子だけで組む。
            var drawDiscards = new List<int>();
            for (int n = 1; n <= 9; n++) drawDiscards.Add(TutorialTiles.Pin(n));
            for (int n = 1; n <= 8; n++) drawDiscards.Add(TutorialTiles.Sou(n));

            // 敵の役満手（単騎待ち・面子部分12枚）: 222m 333m 444m 555m
            //
            // 単騎のアタリ牌は「プレイヤーが実際に打った牌」になるため、どの牌で放銃しても
            // 破綻しない構成にする必要がある。プレイヤーが打てるのは山牌の残り21枚
            // （筒子・索子・字牌）なので、面子側を萬子だけで固めれば牌が5枚目にならない。
            //   例: 3p で放銃 → 222m 333m 444m 555m + 3p3p = 四暗刻単騎
            var ankoMelds = new List<int>
            {
                TutorialTiles.Man(2), TutorialTiles.Man(2), TutorialTiles.Man(2),
                TutorialTiles.Man(3), TutorialTiles.Man(3), TutorialTiles.Man(3),
                TutorialTiles.Man(4), TutorialTiles.Man(4), TutorialTiles.Man(4),
                TutorialTiles.Man(5), TutorialTiles.Man(5), TutorialTiles.Man(5),
            };

            // ================= 第1局: ロンの基本（手順①〜⑦） =================
            s.rounds.Add(new TutorialRoundData
            {
                label = "第1局 ロンの基本",
                wallBaseIds = Wall(),
                manganHandBaseIds = new List<int>(hand),
                waitBaseIds = new List<int>(waits),
                doraBaseId = dora,

                allowManualHandSelection = true,   // ① 適当に13枚選ばせる
                rejectFirstConfirm = true,         // ② 必ず弾く
                requireAutoManganToConfirm = true, // ④ オートへ誘導

                betAmount = 2000, // 各自2000払い、跳満1.5倍で自分+3000 / 相手-3000
                enemyDiscardBaseIds = new List<int> { d1, d2, d3, d4, TutorialTiles.Man(9) },
                outcome = TutorialOutcome.PlayerRon,
                playerWinningTileBaseId = TutorialTiles.Man(9),
                yakuList = new List<string> { "清一色" },
                formulaText = "6飜",
                rankText = "跳満",
                // 清一色6飜=跳満なので倍率1.5。自分 +2000×1.5=3000 / 相手 -3000

                betPromptText = "{0}円。それがあなたの言い値ね。",

                introLines = new List<TutorialLine>
                {
                    new TutorialLine("契約は済んだわね。"),
                    new TutorialLine("その契約書、まだ乾いていないでしょう。"),
                    new TutorialLine("……インク？ 違うわ。あなたの色よ。"),
                    new TutorialLine("名前を書いた指、まだ痛む？"),
                    new TutorialLine("ねえ、その血。あなたは \"払った\" つもり？"),
                    new TutorialLine("買い物なら、対価。詫びなら、誠意。契約なら、証。"),
                    new TutorialLine("どれも、自分で差し出したものよね。"),
                    new TutorialLine("それとも \"抜かれた\" と思っているのかしら。"),
                    new TutorialLine("献血なら、腕を出しただけ。虫になら、気づかぬうちに。"),
                    new TutorialLine("悪魔になら……断れなかったから。"),
                    new TutorialLine("ふふ。どちらでも、減った量は同じよ。"),
                    new TutorialLine("違うのは、誰が決めたかだけ。"),
                    new TutorialLine("さあ、始めましょう。"),
                    new TutorialLine("山牌から13枚選びなさい。それがあなたの命の値段になるわ。"),
                },
                onHandFilledLines = new List<TutorialLine>
                {
                    new TutorialLine("13枚ね。……その手、満貫にも届いていないわ。"),
                    new TutorialLine("安い手で座るのは許さない。死ぬ値打ちがないもの。"),
                    new TutorialLine("今回は組んであげる。『自動』を押しなさい。"),
                },
                onSelfManganLines = new List<TutorialLine>
                {
                    new TutorialLine("……あら。ちゃんと満貫に届いてる。"),
                    new TutorialLine("麻雀を知っている手ね。少し楽しくなってきたわ。"),
                    new TutorialLine("文句はないわ。『決定』を押しなさい。"),
                },
                beforeBetLines = new List<TutorialLine>
                {
                    new TutorialLine("次は賭け金よ。"),
                    new TutorialLine("言っておくけど、決めた分の血はその場で抜かれるわ。"),
                    new TutorialLine("勝ってから払うんじゃない。賭けた瞬間に、もう減っているの。"),
                    new TutorialLine("体力ゲージ、決めた瞬間に減るのを見ていなさい。"),
                    new TutorialLine("戻ってくるかどうかは、まだ何も決まっていないけれど。"),
                    new TutorialLine("勝てば役の倍率をかけて返る。負ければ、払った上にもっと持っていかれる。"),
                    new TutorialLine("いくら出す？ 自分で決めていいのよ。"),
                },
                onBattleStartLines = new List<TutorialLine>
                {
                    new TutorialLine("対局開始よ。"),
                    new TutorialLine("あなたの番。好きな牌を捨ててごらんなさい。"),
                    new TutorialLine("一枚捨てるたびに、どちらかが近づくの。"),
                },
                outroLines = new List<TutorialLine>
                {
                    new TutorialLine("ロン。あなたの上がりね。"),
                    new TutorialLine("獲得は『自分が賭けた額 × 役の倍率』。跳満なら1.5倍。"),
                    new TutorialLine("負けたほうは『自分が賭けた額 × 相手の倍率』を失う。"),
                    new TutorialLine("……それでね。満貫は、1倍なの。"),
                    new TutorialLine("勝っても、払った分が戻ってくるだけ。"),
                    new TutorialLine("気づいた？ 満貫で勝っても、あなたは1滴も増えていないの。"),
                    new TutorialLine("大きく賭けて、大きく獲る。それしか増える道はないわ。"),
                },
            });

            // ================= 第2局: 流局（手順⑧〜⑪） =================
            s.rounds.Add(new TutorialRoundData
            {
                label = "第2局 流局",
                wallBaseIds = Wall(),
                manganHandBaseIds = new List<int>(hand),
                waitBaseIds = new List<int>(waits),
                doraBaseId = dora,

                allowManualHandSelection = false,
                rejectFirstConfirm = false,
                requireAutoManganToConfirm = true,

                // 流局ぶんは次局へ積み増されるので少額にしておく。
                // 第3局は単騎の2倍が効くため、ここを大きくすると自分が死ぬ。
                betAmount = 600,
                // 17手ぶん。うち最初の15手は自動で流し、残り2手をプレイヤーに打たせる。
                enemyDiscardBaseIds = new List<int>(drawDiscards),
                autoDiscardTurns = 15,
                outcome = TutorialOutcome.Draw,
                // 流局では血が動かない。賭け金は決着していないので次の局へ積み増される
                // （持ち越しぶんだけ、次に決着したときの増減が大きくなる）。
                drawDamageToPlayer = 0,

                introLines = new List<TutorialLine>
                {
                    new TutorialLine("次は、誰も死なない局を見せてあげる。"),
                    new TutorialLine("『自動』を押しなさい。"),
                },
                onBattleStartLines = new List<TutorialLine>
                {
                    new TutorialLine("しばらく黙って見ていなさい。勝手に進めるわ。"),
                },
                beforeManualDiscardLines = new List<TutorialLine>
                {
                    new TutorialLine("お互い17牌捨てたら、その局は流れる。"),
                    new TutorialLine("あと2回よ。好きなのを捨てなさい。"),
                },
                outroLines = new List<TutorialLine>
                {
                    new TutorialLine("流局。誰も上がらないまま牌が尽きたわ。"),
                    new TutorialLine("誰も死ななかったわね。"),
                    new TutorialLine("それでも、抜かれた血は卓の上にあるのよ。"),
                    new TutorialLine("決着していないんだから、賭け金は誰のものにもならない。"),
                    new TutorialLine("だから次の局に積まれるの。"),
                    new TutorialLine("……いい？ 延ばした分だけ、次に決まるときが重くなるのよ。"),
                    new TutorialLine("流局は、助かったんじゃないわ。"),
                    new TutorialLine("支払いを、先に延ばしただけ。"),
                },
            });

            // ================= 第3局: 嘘の待ち牌と単騎（手順⑫〜⑰） =================
            s.rounds.Add(new TutorialRoundData
            {
                label = "第3局 フェイクと単騎",
                wallBaseIds = Wall(),
                manganHandBaseIds = new List<int>(hand),
                waitBaseIds = new List<int>(waits),
                doraBaseId = dora,

                allowManualHandSelection = false,
                rejectFirstConfirm = false,
                requireAutoManganToConfirm = true,

                betAmount = 1000,
                enemyDiscardBaseIds = new List<int> { d1, d2, d3, d4, d5 },
                lockedTileBaseId = TutorialTiles.Ton, // ⑭ 嘘の待ち牌。触れないようにする
                lockedTileMessage = "その牌は出しちゃダメって言ったでしょう！",

                outcome = TutorialOutcome.EnemyRon,
                enemyRonMeldBaseIds = ankoMelds,
                enemyRonOnPlayerDiscardTurn = 5,
                yakuList = new List<string> { "四暗刻単騎" },
                formulaText = "役満",
                rankText = "役満",
                enemyWinningHan = 13, // 役満 = 4倍
                isTankiWin = true,    // 単騎で上がられるので自分の失う額は2倍
                // 第2局の流局で決着しなかった賭け金が積み増されているため、
                // 双方の賭け金は各1200。相手 +1200×4=4800 / 自分 -1200×4×2=9600

                introLines = new List<TutorialLine>
                {
                    new TutorialLine("次の局よ。『自動』を押して。"),
                },
                inheritedBetLines = new List<TutorialLine>
                {
                    new TutorialLine("前の局は流れたわね。賭けたぶんは、まだ卓の上にあるの。"),
                    new TutorialLine("今回は{0}円が自動で積まれて、場には合計{1}円。改めて賭ける必要はないわ。"),
                    new TutorialLine("誰も勝っていないのに、卓の上の血だけが増えていくのよ。"),
                    new TutorialLine("積まれた分だけ、次に上がったほうの取り分が大きくなる。"),
                },
                onBattleStartLines = new List<TutorialLine>
                {
                    new TutorialLine("……ふふ。正直に言うとね、ちょっと怖いの。"),
                    new TutorialLine("このまま流局を続けていたいくらい。"),
                    new TutorialLine("だから教えてあげる。私の待ちは東よ。"),
                    new TutorialLine("東だけは絶対に出さないでね。"),
                    new TutorialLine("……ほら。ちゃんと目を見て言ったでしょう？"),
                },
                outroLines = new List<TutorialLine>
                {
                    new TutorialLine("ロン。"),
                    new TutorialLine("……ふふっ、信じたの？"),
                    new TutorialLine("待ちが東だなんて、本当だとは一度も言っていないわ。"),
                    new TutorialLine("さっき言ったわよね。自分で決めて払った、って。"),
                    new TutorialLine("違うわ。決めさせられていたの。"),
                    new TutorialLine("単騎待ちは、たった1枚を待つ代わりに――相手の失う額が2倍になるの。"),
                    new TutorialLine("役満は4倍。その2倍だから、積まれた血の8倍ね。"),
                    new TutorialLine("全部いただくわ。"),
                    new TutorialLine("あら、契約書に垂れちゃった。"),
                    new TutorialLine("署名の隣に、点がひとつ増えただけね。"),
                },
            });

            // ================= 第4局: 能力（手順⑱〜⑳）※対局あり =================
            s.rounds.Add(new TutorialRoundData
            {
                label = "第4局 能力",

                // この局だけ専用の配牌。理由は AbilityWall の定義を参照。
                wallBaseIds = AbilityWall(),
                manganHandBaseIds = new List<int>(abilityHand),
                waitBaseIds = new List<int>(abilityWaits),
                doraBaseId = dora,

                allowManualHandSelection = false,
                rejectFirstConfirm = false,
                requireAutoManganToConfirm = true,

                // 相手は直前に能力コスト12700を払っていて血が薄い。
                // 賭け金の支払いで相手が死ぬと第5局が成立しないので小さめにする。
                betAmount = 1000,

                // 2打目で放銃させる。1打目は待ちでない牌、2打目にプレイヤーの待ち(9p)を打たせる。
                enemyDiscardBaseIds = new List<int> { TutorialTiles.Man(1), abilityWinningTile },
                enemyUsesAbility = true, // ⑱

                outcome = TutorialOutcome.PlayerRon,
                playerWinningTileBaseId = abilityWinningTile,

                // 対々和(2飜) + 混一色(門前3飜) = 5飜 満貫 = 1倍。
                // 敵は直前に能力コスト12700を払っているので、ここで大きく削ると
                // 第5局（決着）が成立しなくなる。満貫の等倍がちょうどいい。
                yakuList = new List<string> { "対々和", "混一色" },
                formulaText = "5飜",
                rankText = "満貫",

                introLines = new List<TutorialLine>
                {
                    new TutorialLine("次は能力の話よ。『自動』で手牌を作りなさい。"),
                },

                // 能力は手牌フェイズでしか使えないので、手牌を決めたあと・賭け金の前に実演する
                abilityIntroLines = new List<TutorialLine>
                {
                    new TutorialLine("手牌が決まったわね。ここからが本番。"),
                    new TutorialLine("能力が使えるのはこの手牌フェイズの間だけ。打牌が始まったら、もう使えないわ。"),
                    new TutorialLine("見せてあげる。よく見ていなさい。"),
                },

                onBattleStartLines = new List<TutorialLine>
                {
                    new TutorialLine("さあ、打ちましょう。"),
                },

                // 手順⑱: 3つの能力を順に実演する
                abilityShowcases = new List<TutorialAbilityShowcase>
                {
                    new TutorialAbilityShowcase(
                        SkillNames.Perspective,
                        new TutorialLine("まずは『透視』。あなたの牌を3枚、勝手に覗くの。"),
                        new TutorialLine("ほら、印がついたでしょう。その3枚は私に丸見えよ。")),

                    new TutorialAbilityShowcase(
                        SkillNames.Mulligan,
                        new TutorialLine("次は『牌交換』。要らない牌を山と入れ替えるわ。"),
                        new TutorialLine("これで私の手はずいぶん良くなった。")),

                    new TutorialAbilityShowcase(
                        SkillNames.BoostHand,
                        new TutorialLine("最後は『役強化』。決めた役の翻数を+1するの。"),
                        new TutorialLine("『清一色』を選んだわ。同じ手でも打点が跳ね上がる。"))
                    {
                        boostYakuName = "清一色",
                        boostHan = 1,
                    },
                },

                // 手順⑲: 能力そのものの説明
                abilityExplainLines = new List<TutorialLine>
                {
                    new TutorialLine("これが能力よ。使えば対局を一気にひっくり返せる。"),
                    new TutorialLine("代償はあなたの血。体力そのものよ。"),
                    new TutorialLine("私のゲージ、見てごらんなさい。"),
                    new TutorialLine("……さっきの半分も残っていないでしょう？"),
                    new TutorialLine("なあに、その顔。"),
                    new TutorialLine("強くなるのに何も払わない方法があると思った？"),
                    new TutorialLine("使えるのは手牌フェイズの間だけ。使うなら今よ。"),
                    new TutorialLine("能力は画面の『能力』ボタンから確認できるわ。"),
                },

                // 手順⑳: 能力強化の説明 → 役一覧へ誘導
                enhanceExplainLines = new List<TutorialLine>
                {
                    new TutorialLine("それと、能力そのものを強化することもできるの。"),
                    new TutorialLine("『役強化』で積んだ翻数は、その役にずっと乗り続けるわ。"),
                    new TutorialLine("どの役がどれだけ育っているかは、役一覧で見なさい。"),
                },
                guideToYakuList = true,
                onYakuListOpenedLines = new List<TutorialLine>
                {
                    new TutorialLine("これが役一覧よ。役ごとの翻数と、強化された分が並んでいるわ。"),
                    new TutorialLine("さっき私が強化した『清一色』も乗っているでしょう？"),
                    new TutorialLine("狙う役を決めるときは、ここを見ること。"),
                },

                outroLines = new List<TutorialLine>
                {
                    new TutorialLine("……ロン？"),
                    new TutorialLine("うそ。あれだけ払って、こっちが振り込むの？"),
                    new TutorialLine("能力を使っても、放銃すれば意味がない。血だけ捨てたのと同じね。"),
                    new TutorialLine("……ねえ、気づいてる？"),
                    new TutorialLine("私のゲージ、あなたより短いのよ。"),
                    new TutorialLine("……次で最後にしましょう。"),
                },
            });

            // ================= 第5局: 決着（手順㉑〜㉕） =================
            s.rounds.Add(new TutorialRoundData
            {
                label = "第5局 決着",
                wallBaseIds = FinalWall(),
                manganHandBaseIds = new List<int>(finalHand),
                waitBaseIds = new List<int>(finalWaits),
                doraBaseId = dora,

                allowManualHandSelection = true,   // ㉑ 自分で組ませる
                rejectFirstConfirm = false,

                // 自力で組んで『決定』でも、『自動』に任せてもよい。矢印の誘導はしない。
                // requireAutoManganToConfirm は残す＝制約『満貫手以下での開始は不可』の担保。
                // 自力で台本の手を組めていれば決定を押した時点で通る（自動を押す必要はない）。
                freeHandBuilding = true,
                requireAutoManganToConfirm = true,

                // 相手の残りは2900程度。賭け金の支払いで死なせないこと。
                // ダブル役満8倍なので 1000 賭けても 8000 動き、決着には十分。
                betAmount = 1000,
                enemyDiscardBaseIds = new List<int> { d1, d2, d3, d4, finalWinningTile },
                outcome = TutorialOutcome.PlayerRon,
                playerWinningTileBaseId = finalWinningTile,

                // 純正九蓮宝燈は 26飜 = ダブル役満 = 8倍。
                // 1000 × 8 = 8000 で、残り2900の相手を倒し切る
                yakuList = new List<string> { "純正九蓮宝燈" },
                formulaText = "26飜",
                rankText = "役満",

                introLines = new List<TutorialLine>
                {
                    new TutorialLine("最後の局よ。"),
                    new TutorialLine("今度は自分で組みなさい。13枚、自分の手で選んで『決定』を押すの。"),
                    new TutorialLine("……どうしても組めないなら『自動』に頼ってもいいわ。好きになさい。"),
                },
                // 第4局は自分ロンで決着しているので流局の持ち越しは起きない。
                // inheritedBetLines は使われないため置いていない。
                onHandFilledLines = new List<TutorialLine>
                {
                    new TutorialLine("13枚そろったわね。その手で本当にいいの？"),
                    new TutorialLine("決めたなら『決定』を。迷うなら『自動』を。"),
                },
                // 決定して打牌フェイズに入った直後。命の賭け合いだと分からせる
                onBattleStartLines = new List<TutorialLine>
                {
                    new TutorialLine("手が決まった。もう引き返せないわ。"),
                    new TutorialLine("ここから先に賭かっているのは、お金じゃない。"),
                    new TutorialLine("一枚打つたびに、どちらかが減るのよ。"),
                    new TutorialLine("……ねえ。"),
                    new TutorialLine("ひとつだけ教えてあげる。"),
                    new TutorialLine("私も、あなたと同じだけ抜かれているのよ。"),
                    new TutorialLine("さあ、始めましょう。震える手で選びなさい。"),
                },
                outroLines = new List<TutorialLine>
                {
                    new TutorialLine("……九蓮宝燈。"),
                    new TutorialLine("しかも九面待ちですって……！？"),
                    new TutorialLine("きゃあああ……！"),
                    new TutorialLine("……ねえ。"),
                    new TutorialLine("この血は、\"払った\" のかしら。"),
                    new TutorialLine("それとも、\"抜かれた\" のかしら……"),
                    new TutorialLine("決めるのは……あなたなのね。"),
                },
            });

            // 第1〜3局は同じ清一色の手牌を使うので、聴牌チェックの応答も共通にしておく
            foreach (var r in s.rounds)
            {
                r.manganHandYaku = new List<string> { "清一色" };
                r.manganHandHan = 6;

                // 開幕は女の子とセリフだけ。1行目を送ったら盤面を出す。
                r.revealBoardAfterLineIndex = 0;
            }

            // プレイヤーが自分で牌を選ぶ局（第1局・第5局）は、イントロを全て送り終えてから盤面を出す。
            // 途中で出すと「13枚選んで」のセリフを送る前に牌が触れてしまい、
            // 説明を読む前に盤面が進んでしまう。-1 = イントロを全て流し終えたあと。
            foreach (var r in s.rounds)
            {
                if (r.allowManualHandSelection) r.revealBoardAfterLineIndex = -1;
            }

            // 第4局と第5局は手牌も役も違うので、共通設定のあとで上書きする
            if (s.rounds.Count > 3)
            {
                s.rounds[3].manganHandYaku = new List<string> { "対々和", "混一色" };
                s.rounds[3].manganHandHan = 5;
            }
            if (s.rounds.Count > 4)
            {
                s.rounds[4].manganHandYaku = new List<string> { "純正九蓮宝燈" };
                s.rounds[4].manganHandHan = 26;
            }

            // 相手が倒れたあとの沈黙。これを送るとタイトルへ戻る。
            // 話者を既定にしているので吹き出しに 「…………」 と出る。
            // （以前あった先輩の締めセリフは、倒れた直後に出すと空気が壊れるので外した）
            s.endingLines = new List<TutorialLine>
            {
                new TutorialLine("…………"),
            };
            s.titleSceneName = "タイトルシーン";

            // **セリフだけ、外の表で差し替えられるようにしてある。**
            // `Assets/Resources/TutorialLines.asset` があれば、ID が一致した行の文字列で
            // 上書きする。表が無い／IDが無い行は、ここに書いた文字列がそのまま使われる。
            // 表の作り方は Tools/チュートリアル/台本TSVの書き出し・取り込み。
            //
            // 構造（局数・実演・ボタンの開放順）はここが正で、文字だけ外。
            // 台本ごとアセットへ移すと BuildDefault と二重管理になる（A-7 と同じ罠）。
            ApplyLineTable(s);

            return s;
        }

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
