using System;
using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.Managers
{
    /// <summary>局の結末。旧実装の日本語マジックストリング比較を置き換える。</summary>
    public enum TutorialOutcome
    {
        PlayerRon,
        EnemyRon,
        Draw
    }

    /// <summary>セリフの話者。立ち絵の出し分けに使う。</summary>
    public enum TutorialSpeaker
    {
        Enemy,   // 対戦相手の女の子
        Senpai,  // あずにゃん先輩
        System   // 地の文・ルール説明
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
    /// 1局分のチュートリアル台本。
    /// 牌は「牌種（0〜28）」で持ち、ドラフラグは TutorialTiles.EncodeAll が付与する。
    /// </summary>
    [Serializable]
    public class TutorialRoundData
    {
        [Header("識別")]
        public string label = "第N局";

        [Header("配牌")]
        [Tooltip("この局で配られる34枚（牌種 0〜28）。重複あり。")]
        public List<int> wallBaseIds = new List<int>();

        [Tooltip("オート満貫ボタンで組まれる13枚（牌種 0〜28）。wallBaseIds の部分集合であること。")]
        public List<int> manganHandBaseIds = new List<int>();

        [Tooltip("上記手牌の待ち牌（牌種）。WaitUI の表示に使う。")]
        public List<int> waitBaseIds = new List<int>();

        [Tooltip("オート満貫手の役名。聴牌チェック（is_tenpai）のモック応答に使う。")]
        public List<string> manganHandYaku = new List<string>();

        [Tooltip("オート満貫手の翻数。聴牌チェックのモック応答に使う。")]
        public int manganHandHan = 6;

        [Tooltip("この局のドラ（牌種）。-1 でドラなし。")]
        public int doraBaseId = -1;

        [Header("手牌構築フェイズ")]
        [Tooltip("手動で牌を動かせるか（手順①。第1局と第5局は true）")]
        public bool allowManualHandSelection = true;

        [Tooltip("最初の決定を無条件で弾くか（手順②。満貫判定は持たずスクリプトで弾く）")]
        public bool rejectFirstConfirm = true;

        [Tooltip("オート満貫を使わないと決定できないか。制約『満貫手以下での開始は不可』を担保する。")]
        public bool requireAutoManganToConfirm = true;

        [Header("賭け金フェイズ")]
        [Tooltip("固定の賭け金。TODO: 実際の値は要決定。")]
        public int betAmount = 1000;

        [Tooltip("賭け金を促すセリフ。{0} に betAmount が入る。")]
        [TextArea(1, 2)] public string betPromptText = "{0}円賭けてちょうだい。";

        [Header("打牌フェイズ")]
        [Tooltip("敵が順番に捨てる牌（牌種）。要素数がそのまま手数になる。")]
        public List<int> enemyDiscardBaseIds = new List<int>();

        [Tooltip("開始直後に自動で進める手数。ここまでは自分も相手も自動で捨てる。0 なら最初から手動。")]
        public int autoDiscardTurns = 0;

        [Tooltip("プレイヤーが打てない牌（牌種）。-1 でなし。手順⑭の嘘の待ち牌。")]
        public int lockedTileBaseId = -1;

        [TextArea(1, 2)] public string lockedTileMessage = "その牌は出しちゃダメって言ったでしょ！";

        [Tooltip("敵が能力を使う演出を挟むか（手順⑱）")]
        public bool enemyUsesAbility = false;

        [Header("結末")]
        public TutorialOutcome outcome = TutorialOutcome.Draw;

        [Tooltip("PlayerRon 時、敵が捨てるアタリ牌（牌種）。enemyDiscardBaseIds の末尾と一致させること。")]
        public int playerWinningTileBaseId = -1;

        [Tooltip("EnemyRon 時、敵の手牌のうち面子部分12枚（牌種）。実際のアタリ牌は単騎待ちとして末尾に追加される。")]
        public List<int> enemyRonMeldBaseIds = new List<int>();

        [Tooltip("EnemyRon 時、何手目のプレイヤー打牌で放銃するか（1始まり）")]
        public int enemyRonOnPlayerDiscardTurn = 5;

        [Header("結末の表示内容")]
        public List<string> yakuList = new List<string>();
        public string formulaText = "";
        public string rankText = "";
        public int score = 0;

        [Header("ダメージ（局をまたいでHPに反映される）")]
        [Tooltip("流局によるプレイヤーへのダメージ。手順⑯の『流局のダメージ』。")]
        public int drawDamageToPlayer = 0;

        [Header("セリフ")]
        public List<TutorialLine> introLines = new List<TutorialLine>();

        [Tooltip("イントロの何行目のあとに盤面（山牌・手牌・ドラ・HP）を出すか。0始まり。" +
                 "-1 ならイントロを全て流し終えたあと。それまでは女の子とセリフだけが見える。")]
        public int revealBoardAfterLineIndex = -1;
        [Tooltip("13枚そろったあと、『自動』ボタンを開放する直前のセリフ。空なら既定文が使われる。")]
        public List<TutorialLine> onHandFilledLines = new List<TutorialLine>();

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

        [Header("終了時")]
        public List<TutorialLine> endingLines = new List<TutorialLine>();
        public string titleSceneName = "タイトルシーン";

        /// <summary>
        /// 台本アセットが未設定のときに使われる既定シナリオ。
        ///
        /// HPの流れ（手順⑦⑯㉔に対応）:
        ///   開始      P20000 / E20000
        ///   第1局 ロン E-12000 → E 8000
        ///   第2局 流局 P -1000 → P19000
        ///   第3局 流局+役満 P -1000 -16000 → P 2000
        ///   第4局 流局 P -1000 → P 1000
        ///   第5局 ロン E-32000 → E 0（敗北・死亡。0でクランプされるので過剰打点でも破綻しない）
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

                betAmount = 1000,
                enemyDiscardBaseIds = new List<int> { d1, d2, d3, d4, TutorialTiles.Man(9) },
                outcome = TutorialOutcome.PlayerRon,
                playerWinningTileBaseId = TutorialTiles.Man(9),
                yakuList = new List<string> { "清一色" },
                formulaText = "6飜",
                rankText = "跳満",
                score = 12000,

                introLines = new List<TutorialLine>
                {
                    new TutorialLine("ふふっ、無事に契約完了ね。"),
                    new TutorialLine("これでお前は私のモノ……と言いたいところだけど、まずはその腕前を見せてもらうわ。"),
                    new TutorialLine("デス麻雀のルール、ちゃんと覚えてる？"),
                    new TutorialLine("まずは山牌から好きな13枚を選んで、手牌を作ってみなさい。"),
                },
                onHandFilledLines = new List<TutorialLine>
                {
                    new TutorialLine("13枚そろったわね。……でも、その手じゃ満貫にも届かないわ。"),
                    new TutorialLine("今回は自動で選んであげるわ。"),
                },
                beforeBetLines = new List<TutorialLine>
                {
                    new TutorialLine("手牌が決まったなら、次は賭け金よ。"),
                },
                onBattleStartLines = new List<TutorialLine>
                {
                    new TutorialLine("いよいよ対局スタートよ。"),
                    new TutorialLine("あなたの番ね。まずは好きな牌をタップして捨ててみて。"),
                },
                outroLines = new List<TutorialLine>
                {
                    new TutorialLine("ロン！あなたの上がりね。"),
                    new TutorialLine("上がると、相手からダメージを奪えるのよ。"),
                    new TutorialLine("賭けた金額に応じて獲得金も手に入るわ。これが基本ルールよ。"),
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

                betAmount = 1000,
                // 17手ぶん。うち最初の15手は自動で流し、残り2手をプレイヤーに打たせる。
                enemyDiscardBaseIds = new List<int>(drawDiscards),
                autoDiscardTurns = 15,
                outcome = TutorialOutcome.Draw,
                drawDamageToPlayer = 1000,

                introLines = new List<TutorialLine>
                {
                    new TutorialLine("次は流局について教えるわ。"),
                    new TutorialLine("とりあえず『自動』ボタン（オート満貫）を押してね。"),
                },
                onBattleStartLines = new List<TutorialLine>
                {
                    new TutorialLine("しばらく黙って見ていなさい。勝手に打ち進めるわ。"),
                },
                beforeManualDiscardLines = new List<TutorialLine>
                {
                    new TutorialLine("お互い17牌捨てたら流局して、次の局に移るわ。"),
                    new TutorialLine("あと2回よ。好きな牌を捨ててみなさい。"),
                },
                outroLines = new List<TutorialLine>
                {
                    new TutorialLine("流局よ。誰も上がらずに牌が尽きると流局になるわ。"),
                    new TutorialLine("流局しても、お互い少しずつダメージを受けるの。覚えておきなさい。"),
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
                lockedTileMessage = "その牌は出しちゃダメって言ったでしょ！",

                outcome = TutorialOutcome.EnemyRon,
                enemyRonMeldBaseIds = ankoMelds,
                enemyRonOnPlayerDiscardTurn = 5,
                yakuList = new List<string> { "四暗刻単騎" },
                formulaText = "役満",
                rankText = "役満",
                score = 16000,
                drawDamageToPlayer = 1000, // ⑯ 流局のダメージも同時に受ける

                introLines = new List<TutorialLine>
                {
                    new TutorialLine("次の局よ。とりあえず『自動』ボタンを押してね。"),
                },
                onBattleStartLines = new List<TutorialLine>
                {
                    new TutorialLine("フフフ…私、ちょっとビビっちゃったかも。"),
                    new TutorialLine("このまま延々と流局を続けようかしら。"),
                    new TutorialLine("そうそう、私の待ち牌は東よ。東だけは絶対に出さないでね。"),
                },
                outroLines = new List<TutorialLine>
                {
                    new TutorialLine("ロン！ふふっ、騙されたわね！"),
                    new TutorialLine("待ち牌が東だなんて、一言も本当だとは言ってないわ。"),
                    new TutorialLine("単騎待ちは、たった1枚の牌を待つ代わりに打点が跳ね上がるの。"),
                    new TutorialLine("流局のダメージと、単騎待ちのダメージ。まとめて食らいなさい！"),
                },
            });

            // ================= 第4局: 能力（手順⑱〜⑳）※対局あり =================
            s.rounds.Add(new TutorialRoundData
            {
                label = "第4局 能力",
                wallBaseIds = Wall(),
                manganHandBaseIds = new List<int>(hand),
                waitBaseIds = new List<int>(waits),
                doraBaseId = dora,

                allowManualHandSelection = false,
                rejectFirstConfirm = false,
                requireAutoManganToConfirm = true,

                betAmount = 1000,
                enemyDiscardBaseIds = new List<int> { d1, d2, d3, d4, d5, d6 },
                enemyUsesAbility = true, // ⑱

                outcome = TutorialOutcome.Draw,
                drawDamageToPlayer = 1000,

                introLines = new List<TutorialLine>
                {
                    new TutorialLine("次は能力の使用についての説明よ。"),
                    new TutorialLine("まずは『自動』ボタンで手牌を作りなさい。"),
                },
                onBattleStartLines = new List<TutorialLine>
                {
                    new TutorialLine("ふふっ、私の能力を見せてあげるわ！"),
                },
                outroLines = new List<TutorialLine>
                {
                    new TutorialLine("これが能力よ。使えば対局を有利に進められるの。"),
                    new TutorialLine("能力は強化することもできるわ。"),
                    new TutorialLine("強化の条件は役一覧から確認してね。", TutorialSpeaker.System),
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
                requireAutoManganToConfirm = true, // 制約『満貫手以下での開始は不可』

                betAmount = 1000,
                enemyDiscardBaseIds = new List<int> { d1, d2, d3, d4, finalWinningTile },
                outcome = TutorialOutcome.PlayerRon,
                playerWinningTileBaseId = finalWinningTile,

                // 第3局で食らった役満（16000）を、その倍の打点で返して決着する
                yakuList = new List<string> { "純正九蓮宝燈" },
                formulaText = "26飜",
                rankText = "役満",
                score = 32000,

                introLines = new List<TutorialLine>
                {
                    new TutorialLine("さあ、これが最後の対局よ！"),
                    // 実際には requireAutoManganToConfirm が立っていて自動ボタンが必須なので、
                    // 「自動を使ってもいい」という言い回しは実挙動と食い違っていた。
                    new TutorialLine("好きに牌を並べてみなさい。仕上げは『自動』ボタンよ。"),
                },
                onHandFilledLines = new List<TutorialLine>
                {
                    new TutorialLine("13枚そろったわね。最後だもの、今回も自動で選んであげるわ。"),
                },
                outroLines = new List<TutorialLine>
                {
                    new TutorialLine("……九蓮宝燈？しかも九面待ちですって……！？"),
                    new TutorialLine("きゃあああ！やられたわ……！"),
                },
            });

            // 第1〜4局は同じ清一色の手牌を使うので、聴牌チェックの応答も共通にしておく
            foreach (var r in s.rounds)
            {
                r.manganHandYaku = new List<string> { "清一色" };
                r.manganHandHan = 6;

                // 開幕は女の子とセリフだけ。1行目を送ったら盤面を出す。
                r.revealBoardAfterLineIndex = 0;
            }

            // 第1局は導入が長いので、「山牌から13枚選んで」と促す直前で盤面を出す
            if (s.rounds.Count > 0) s.rounds[0].revealBoardAfterLineIndex = 2;

            // 決着局だけは手牌も役も違うので、共通設定のあとで上書きする
            if (s.rounds.Count > 4)
            {
                s.rounds[4].manganHandYaku = new List<string> { "純正九蓮宝燈" };
                s.rounds[4].manganHandHan = 26;
            }

            s.endingLines = new List<TutorialLine>
            {
                new TutorialLine("これでチュートリアルは終了だよ！", TutorialSpeaker.Senpai),
                new TutorialLine("お疲れ様！次はマルチモードで対戦してみよう！", TutorialSpeaker.Senpai),
            };
            s.titleSceneName = "タイトルシーン";

            return s;
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
