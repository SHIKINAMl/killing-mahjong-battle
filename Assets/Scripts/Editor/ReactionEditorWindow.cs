using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KillingMahjong.UI;
using UnityEditor;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    /// <summary>
    /// 対局中（通常のマルチプレイ）とクリックの反応を編集する専用ウィンドウ。
    ///
    /// **チュートリアル台本（`TutorialScenarioEditorWindow`）とは編集先が別。**
    /// あちらは `TutorialScenario.asset`、こちらは
    /// `Assets/Resources/MajangGameTaskBoard - セリフ一覧.csv` と `CharacterData` アセット。
    /// OpeningScene の会話をここから直すことはできない。
    ///
    /// **なぜ Inspector や Excel で済ませないのか。**
    /// 反応の出どころが3つに分かれていて、しかも**書いても出ないセリフが大量にある**:
    ///   1. CSV（`DialogueManager`）… 状況名の文字列で引く。対局中の反応の本体
    ///   2. CSV のクリック行 … `Random.Range(1, 21)` で番号を引く。21番以降は当たらない
    ///   3. `CharacterData.reactions`（ScriptableObject）… `ReactionTrigger` で引く。
    ///      **48種のうち5種しかゲームから呼ばれていない**
    /// 状況名を見ても、それがいつ出るのか・そもそも出るのかが分からない。
    /// ここでは**呼び出し元のコードを写した条件**を添えて、出ないものは赤で潰す。
    ///
    /// **写し元（向こうを直したらここも直す）:**
    ///   `ReactionController`(401-682) / `ClickableCharacter.OnClicked`(142-223) /
    ///   `DialogueManager.ParseCSV`(69-138) / `EnemyInfoUI.PlayReaction`(238-272)
    /// </summary>
    public partial class ReactionEditorWindow : EditorWindow
    {
        public const string CsvPath = "Assets/Resources/MajangGameTaskBoard - セリフ一覧.csv";

        /// <summary>吹き出しは3行までなので、これを超えると見切れる（`ReactionLineImporter` と同じ基準）</summary>
        private const int DialogueSoftLimit = 40;

        /// <summary>`ClickableCharacter.OnClicked` の `Random.Range(1, 21)`。1〜20 しか出ない</summary>
        private const int ClickLotteryMax = 20;

        // ------------------------------------------------------------------
        // 対局中（CSV）の目次
        // ------------------------------------------------------------------

        /// <summary>CSV の1状況ぶんの説明。`dead` に理由が入っていたら画面に出ない</summary>
        private class CsvSpec
        {
            public string condition;
            public string when;
            public string dead;

            public CsvSpec(string condition, string when, string dead = null)
            {
                this.condition = condition;
                this.when = when;
                this.dead = dead;
            }
        }

        private class CsvGroup
        {
            public string title;
            public string note;
            public CsvSpec[] specs;
        }

        /// <summary>`HandleEnemyHandSelection` を呼んでいる場所が無いことの説明。3件で使い回す</summary>
        private const string DeadEnemyHand =
            "`ReactionController.HandleEnemyHandSelection()` を呼んでいる場所がコード内に1つもありません。"
            + "相手の手の高さで反応する仕組み自体が繋がっていないので、書いても出ません。";

        /// <summary>
        /// 対局中に出る反応の一覧。**実際に流れる順**（局の開始 → 賭け金 → 手牌 → 打牌 → 決着）で並べる。
        /// `when` は呼び出し元の分岐をそのまま日本語にしたもの。**嘘を書くと直しようがなくなる**ので、
        /// 迷ったら条件を丸ごと書く。
        /// </summary>
        private static readonly CsvGroup[] CsvGroups =
        {
            new CsvGroup {
                title = "① 局のはじまり",
                note = "`ReactionController.HandleRoundStart()`。上から順に判定して、最初に当たった1つだけが流れる",
                specs = new[] {
                    new CsvSpec("1局目のゲーム開始時", "第1局が始まったとき。2局目以降は下の5つから選ばれる"),
                    new CsvSpec("プレイヤーのHPが残りわずかな時の開幕", "2局目以降。自分のHPが 2000 以下（開始時は 20000）"),
                    new CsvSpec("敵のHPが残りわずかな時の開幕", "2局目以降。自分は 2000 超で、相手のHPが 2000 以下"),
                    new CsvSpec("プレイヤーが圧倒的有利な時の開幕", "2局目以降。どちらも瀕死ではなく、自分のHPが相手より 5000 以上多い"),
                    new CsvSpec("敵が圧倒的有利な時の開幕", "2局目以降。どちらも瀕死ではなく、相手のHPが自分より 5000 以上多い"),
                    new CsvSpec("2局目以降の開幕時", "2局目以降で、上の4つのどれにも当てはまらないとき"),
                    new CsvSpec("山牌構築中のセリフ", "山牌を積んでいる間",
                        "`DialogueManager.ParseCSV` がこの状況名を**見出し行として読み飛ばして**います"
                        + "（除外リストに直書きされている）。`PlayDealingReaction()` は呼ばれていますが、"
                        + "引いた結果が必ず null になるので画面には出ません。"),
                },
            },

            new CsvGroup {
                title = "② 賭け金",
                note = "`ReactionController.CheckAndPlayBetReaction()`。**賭け金が 501〜4999 のときは何も出ない**。\n"
                     + "※「トリガー」タブの Bet_* に1行でも書いてあると、そちらが優先されてここは出ません",
                specs = new[] {
                    new CsvSpec("プレイヤーが即座に限度額を賭けた時", "自分が 5000 以上を、賭けフェイズ開始から 2 秒未満で確定した"),
                    new CsvSpec("プレイヤーが前の局で負けたのに限度額を賭けた時", "自分が 5000 以上（2 秒以上かけた）＋ 前の局に負けている"),
                    new CsvSpec("初めて限度額いっぱいまで賭けた時の開幕のセリフ",
                        "自分が 5000 以上。上の2つに当てはまらず、この対局で初めて。**2回目以降は無言になる**"),
                    new CsvSpec("初めて最小単位で賭けた時の開幕のセリフ", "自分が 500 以下を、この対局で初めて賭けた"),
                    new CsvSpec("プレイヤーが少額しか賭けなかった時", "自分が 500 以下。2回目以降はこちら"),
                    new CsvSpec("自分が限度額を賭けた時", "**相手（女の子）が** 5000 以上を賭けた。状況名の「自分」は女の子のこと"),
                },
            },

            new CsvGroup {
                title = "③ 手牌を組む",
                note = "`ReactionController.StopHandSelectionTimer()`。3〜15 秒で決めたときは何も出ない",
                specs = new[] {
                    new CsvSpec("プレイヤーが手牌決定に時間をかけている時", "自分が手牌を確定するまでに 15 秒より長くかかった"),
                    new CsvSpec("プレイヤーが手牌を即決した時", "自分が手牌を 3 秒未満で確定した"),
                    new CsvSpec("敵の手が役満の時", "相手の手が役満だったとき", DeadEnemyHand),
                    new CsvSpec("敵の手が満貫以上の時", "相手の手が満貫以上だったとき", DeadEnemyHand),
                    new CsvSpec("敵の手が安い時", "相手の手が安かったとき", DeadEnemyHand),
                },
            },

            new CsvGroup {
                title = "④ 打牌（第1局だけの特別枠）",
                note = "`ReactionController.CheckDiscardConditions()` の前半。**第1局のあいだしか判定されない**",
                specs = new[] {
                    new CsvSpec("相手が第１局目で先行", "第1局の1手目が相手の打牌だった"),
                    new CsvSpec("相手が第一局目で後攻", "第1局の2手目が相手の打牌だった"),
                    new CsvSpec("自分が第一局目で初めて合わせを行う", "第1局で、直前の打牌と同じ牌を自分が切った（対局中1回だけ）"),
                    new CsvSpec("相手が第一局目で合わせ(敵の直前の打牌同じ牌を打つこと)を行う", "第1局で、直前の打牌と同じ牌を相手が切った（対局中1回だけ）"),
                },
            },

            new CsvGroup {
                title = "⑤ 打牌（自分が切ったとき）",
                note = "`CheckDiscardConditions()`。上から順に判定して**最初に当たった1つだけ**。"
                     + "ただし「字牌を連続で切った時」だけは別枠で、重ねて流れることがある。\n"
                     + "※スジ牌を除き、「トリガー」タブの Discard_* に書いてあるとそちらが優先されます",
                specs = new[] {
                    new CsvSpec("プレイヤーが赤ドラを切った時", "赤ドラを切った（最優先）"),
                    new CsvSpec("プレイヤーがオタ風を切った時", "字牌の 1〜4（東南西北）を切った"),
                    new CsvSpec("プレイヤーが役牌を切った時", "字牌の 5〜7（白發中）を切った"),
                    new CsvSpec("プレイヤーがド真ん中の牌を切った時", "数牌の 4・5・6 を切った"),
                    new CsvSpec("プレイヤーが前の捨て牌と同じ牌を切った時", "自分の直前の捨て牌と同じ牌を切った"),
                    new CsvSpec("プレイヤーがスジ牌を切った時", "自分がすでに切った牌の ±3 にあたる牌を切った"),
                    new CsvSpec("プレイヤーが字牌を連続で切った時", "字牌を3回続けて切った。**上の枠とは別に判定される**ので重なることがある"),
                },
            },

            new CsvGroup {
                title = "⑥ 打牌（相手が切ったとき・その他）",
                note = "`CheckDiscardConditions()` の後半。「初めて〜」は**上の特別枠が何も出なかったときだけ**流れる",
                specs = new[] {
                    new CsvSpec("敵が赤ドラを切る時", "相手が赤ドラを切った"),
                    new CsvSpec("初めて一九字牌を切った時のセリフ", "この対局で初めて 1・9・字牌が切られた（自分・相手どちらでも）"),
                    new CsvSpec("初めて2-8の牌をを切った時のセリフ", "この対局で初めて 2〜8 の数牌が切られた（自分・相手どちらでも）"),
                    new CsvSpec("敵が手出しを続けている時", "相手がツモ切りせず手出しを続けているとき",
                        "この状況名をコードのどこからも引いていません。ツモ切りの判定自体が"
                        + "`CheckDiscardConditions()` の中で計算されたまま捨てられています"
                        + "（`isTsumogiri` が未使用）。サーバーからツモ切り情報が来ないと実装できません。"),
                },
            },

            new CsvGroup {
                title = "⑦ 打牌（ふつうのとき）",
                note = "`EnqueueDiscardReaction()`。上の⑤⑥が何も出なかったときの受け皿。**1〜5からランダムに1つ**。"
                     + "`{0}` と書くと切った牌の名前に置き換わる",
                specs = new[] {
                    new CsvSpec("相手が打牌した時1", "相手の打牌でランダムに選ばれる（1〜5）"),
                    new CsvSpec("相手が打牌した時2", "同上"),
                    new CsvSpec("相手が打牌した時3", "同上"),
                    new CsvSpec("相手が打牌した時4", "同上"),
                    new CsvSpec("相手が打牌した時5", "同上"),
                    new CsvSpec("プレイヤーが打牌した時1", "自分の打牌でランダムに選ばれる（1〜5）"),
                    new CsvSpec("プレイヤーが打牌した時2", "同上"),
                    new CsvSpec("プレイヤーが打牌した時3", "同上"),
                    new CsvSpec("プレイヤーが打牌した時4", "同上"),
                    new CsvSpec("プレイヤーが打牌した時5", "同上"),
                },
            },

            new CsvGroup {
                title = "⑧ 決着",
                note = "`HandleAgari()` / `CheckAndPlayDrawReaction()` / `HandleGameEnd()`。\n"
                     + "※役満・ドラ爆・HP0 は「トリガー」タブの Result_* / Win / Lose が優先されます",
                specs = new[] {
                    new CsvSpec("敵が役満に放銃した時", "自分が役満で和了した"),
                    new CsvSpec("ドラ爆でアガった時", "自分がドラ爆で和了した（役満ではない）"),
                    new CsvSpec("敵が安い手に放銃した時", "自分が安手で和了した"),
                    new CsvSpec("敵が放銃した時", "自分が和了した。上の3つに当てはまらないとき"),
                    new CsvSpec("プレイヤーが役満に放銃した時", "相手が役満で和了した"),
                    new CsvSpec("プレイヤーが安い手に放銃した時", "相手が安手で和了した"),
                    new CsvSpec("プレイヤーが放銃した時", "相手が和了した。上の2つに当てはまらないとき"),
                    new CsvSpec("初めて流局した時の最後のセリフ", "この対局で1回目の流局"),
                    new CsvSpec("流局が2回以上続いた時", "この対局で通算2回目以降の流局。**連続でなくても出る**（数えているだけ）"),
                    new CsvSpec("敵のHPが0になった時", "相手のHPが 0 になって対局が終わった"),
                    new CsvSpec("プレイヤーのHPが0になった時", "自分のHPが 0 になって対局が終わった"),
                },
            },
        };

        // ------------------------------------------------------------------
        // トリガー（CharacterData）の目次
        // ------------------------------------------------------------------

        /// <summary>
        /// 実際にゲームから鳴らしているトリガーと、その条件。**ここに無いものは書いても出ない。**
        ///
        /// 2026-08-15 に 41 種を配線した（それまでは最初の5種だけだった）。
        /// `Bet_*` / `Discard_*` / `Result_*` は**トリガーを先に試して、
        /// セリフが無ければ CSV へ落ちる**（`ReactionController.PlayOrFallback`）。
        /// つまりここに1行でも書けば、対応する CSV のセリフより優先される。
        ///
        /// 写し元: `ReactionController`(CheckAndPlayBetReaction / HandleSkillCast /
        /// CheckDiscardConditions / HandleAgari / HandleGameEnd) と `PlayerActivityWatcher`。
        /// 向こうの条件を変えたらここも直す。
        /// </summary>
        private static readonly Dictionary<ReactionTrigger, string> LiveTriggers =
            new Dictionary<ReactionTrigger, string>
            {
                // ---- 元から鳴っていた5種 ----
                { ReactionTrigger.GameStart,
                  "マッチングが成立して対局画面に入った瞬間。空なら \"Match Found! Game Starting...\" が出る" },
                { ReactionTrigger.HandSelection,
                  "自分の手牌選択がサーバーに受理された直後。空なら \"相手の手牌選択を待っています...\" が出る" },
                { ReactionTrigger.Click,
                  "女の子をクリックしたとき。**CSV に該当行が無いときだけ**の最終フォールバック。"
                  + "「クリック」タブが埋まっている限りここには来ない" },
                { ReactionTrigger.EnemyDiscard,
                  "相手の打牌。**CSV の「相手が打牌した時1〜5」が全部空のときだけ**。`{0}` は牌の名前" },
                { ReactionTrigger.PlayerDiscard,
                  "自分の打牌。**CSV の「プレイヤーが打牌した時1〜5」が全部空のときだけ**" },

                { ReactionTrigger.Win,
                  "対局が終わって**自分（プレイヤー）が負けた**とき。空なら CSV の「プレイヤーのHPが0になった時」" },
                { ReactionTrigger.Lose,
                  "対局が終わって女の子が負けたとき。**Result_EnemyKO が空のときだけ**ここに来る" },

                // ---- 賭け金 ----
                { ReactionTrigger.Bet_BluffMax,
                  "自分が限度額（既定 5000 以上）を賭けた。仕返しでも迷った末でもなく、**テンパイしていない**とき" },
                { ReactionTrigger.Bet_TenpaiMax,
                  "自分が限度額を賭けた。**テンパイしている**とき（サーバーの is_tenpai が基準）" },
                { ReactionTrigger.Bet_HesitateMax,
                  "自分が限度額を賭けた。賭け金を**4回以上いじった**あとのとき" },
                { ReactionTrigger.Bet_RevengeMax,
                  "自分が限度額を賭けた。**前の局に負けている**とき（いちばん優先される）" },
                { ReactionTrigger.Bet_TenpaiMin,
                  "自分が最小額（既定 500 以下）を賭けた。テンパイしているとき" },
                { ReactionTrigger.Bet_NoTenMin,
                  "自分が最小額を賭けた。テンパイしていないとき" },
                { ReactionTrigger.Bet_FidgetSpam,
                  "賭けフェイズ中に賭け金を**8回いじった**瞬間。確定を待たずに出る" },
                { ReactionTrigger.Bet_ZeroGiveUp,
                  "賭け金 0 で確定したとき。**BettingUI は最小でも1単位を賭けるので、いまは来ない。**"
                  + "サーバーが 0 を許すようになったとき用に配線だけしてある" },

                // ---- スキル ----
                { ReactionTrigger.Skill_PlayerClairvoyance, "自分が「透視」を発動した" },
                { ReactionTrigger.Skill_PlayerEnhance, "自分が「強化」を発動した" },
                { ReactionTrigger.Skill_PlayerSpecialWin, "自分が「特殊勝利」を発動した" },
                { ReactionTrigger.Skill_EnemyClairvoyance,
                  "女の子が「透視」を発動した。**下2つに当てはまらないときだけ**" },
                { ReactionTrigger.Skill_HighCostPaid,
                  "女の子がスキルに**3000 以上の血**を払った。種類は問わない" },
                { ReactionTrigger.Skill_NearDeathByCost,
                  "女の子がスキルを撃った結果、血が**3000 以下**になった（最優先）" },

                // ---- 打牌 ----
                { ReactionTrigger.Discard_RedDora, "自分が赤ドラを切った" },
                { ReactionTrigger.Discard_RawYakuhai, "自分が役牌（字牌の5〜7＝白發中）を切った" },
                { ReactionTrigger.Discard_SafeTile, "自分がオタ風（字牌の1〜4＝東南西北）を切った" },
                { ReactionTrigger.Discard_CenterTile, "自分が数牌の 4・5・6 を切った" },
                { ReactionTrigger.Discard_SameTileStreak, "自分が直前の捨て牌と同じ牌を切った" },
                { ReactionTrigger.Discard_HonorStreak, "自分が字牌を3回続けて切った" },

                // ---- 決着 ----
                { ReactionTrigger.Result_EnemyHitYakuman, "**女の子が役満に放銃した**（自分が役満で和了）" },
                { ReactionTrigger.Result_EnemyDoraBomb, "**女の子がドラ爆で和了した**（自分が被弾）" },
                { ReactionTrigger.Result_PlayerHitYakuman, "**女の子が役満で和了した**（自分が被弾）" },
                { ReactionTrigger.Result_PlayerNearDeath,
                  "自分の血が 3000 以下になった瞬間。**1対局に1回だけ**" },
                { ReactionTrigger.Result_EnemyKO, "対局が終わって女の子が負けた" },

                // ---- メタ操作（PlayerActivityWatcher）----
                { ReactionTrigger.Meta_ClickHead,
                  "頭をクリックしたが、その番号の「クリックされた時_Head」が CSV に無かったとき" },
                { ReactionTrigger.Meta_ClickChest,
                  "胸をクリックしたが、その番号の「クリックされた時_Chest」が CSV に無かったとき" },
                { ReactionTrigger.Meta_ClickSpam,
                  "女の子を**5秒で5回**クリックした。部位のセリフより優先される" },
                { ReactionTrigger.Meta_ScreenClickSpam,
                  "女の子でも牌でもない**画面の余白を5秒で6回**クリックした" },
                { ReactionTrigger.Meta_Idle20s, "マウスもキーも**20秒**動かさなかった" },
                { ReactionTrigger.Meta_Idle60s, "マウスもキーも**60秒**動かさなかった（20秒の方は出ない）" },
                { ReactionTrigger.Meta_MuteAudio,
                  "音を消した瞬間。マスターが 0、または BGM と SE が両方 0 になったとき" },
                { ReactionTrigger.Meta_WindowRefocus,
                  "**5秒以上**ウィンドウから離れて戻ってきたとき" },

                // ---- 牌の操作（PlayerActivityWatcher）----
                { ReactionTrigger.Tile_HoverHesitation,
                  "自分の手番の打牌フェイズで、牌の上にカーソルを**6秒で10回**乗せた" },
                { ReactionTrigger.Tile_WallPoke,
                  "打牌フェイズで**何も起きない牌を5秒で3回**押した（手牌側をつついた）" },
                { ReactionTrigger.Tile_InstantDiscard,
                  "自分の手番が来てから**1.5秒未満**で切った" },
                { ReactionTrigger.Tile_SpamClick, "牌を**5秒で8回**クリックした" },
                { ReactionTrigger.Tile_PeekHold, "「手牌を見る」を**20秒で3回**開いた" },
                { ReactionTrigger.Tile_ThinkTimeout,
                  "自分の手番が来てから**7.5秒**経っても切っていない（持ち時間は10秒）" },
            };

        /// <summary>
        /// 配線したくてもできていないトリガーと、その理由。
        /// 一律の「呼ばれていません」より、なぜ無理なのかが分かる方が直しようがある。
        /// </summary>
        private static readonly Dictionary<ReactionTrigger, string> UnwiredReasons =
            new Dictionary<ReactionTrigger, string>
            {
                { ReactionTrigger.Damage,
                  "被弾そのものを鳴らす場所を用意していません。血が減る場面は決着（Result_*）と"
                  + "スキルのコスト（Skill_*）で埋まっていて、ここを足すと同じ場面で二重に喋ります。"
                  + "セリフも0件のままです。" },
                { ReactionTrigger.Tile_HoverDora,
                  "ドラ表示（DoraDisplayUI）にカーソル判定がありません。付けるには当たり判定用の"
                  + "オブジェクトを足すことになり、見た目に手を入れる作業なので保留しています。" },
            };

        private class TriggerGroup
        {
            public string title;
            public ReactionTrigger[] triggers;
        }

        /// <summary>`ReactionTrigger` の宣言順にまとめたもの。enum のコメントの区切りに合わせている</summary>
        private static readonly TriggerGroup[] TriggerGroups =
        {
            new TriggerGroup { title = "基本（旧システム）", triggers = new[] {
                ReactionTrigger.GameStart, ReactionTrigger.Click, ReactionTrigger.HandSelection,
                ReactionTrigger.EnemyDiscard, ReactionTrigger.PlayerDiscard,
                ReactionTrigger.Win, ReactionTrigger.Lose, ReactionTrigger.Damage } },
            new TriggerGroup { title = "② ベッティング・心理戦", triggers = new[] {
                ReactionTrigger.Bet_BluffMax, ReactionTrigger.Bet_TenpaiMax, ReactionTrigger.Bet_HesitateMax,
                ReactionTrigger.Bet_RevengeMax, ReactionTrigger.Bet_TenpaiMin, ReactionTrigger.Bet_NoTenMin,
                ReactionTrigger.Bet_FidgetSpam, ReactionTrigger.Bet_ZeroGiveUp } },
            new TriggerGroup { title = "③ スキル発動・コスト消費", triggers = new[] {
                ReactionTrigger.Skill_PlayerClairvoyance, ReactionTrigger.Skill_PlayerEnhance,
                ReactionTrigger.Skill_PlayerSpecialWin, ReactionTrigger.Skill_EnemyClairvoyance,
                ReactionTrigger.Skill_HighCostPaid, ReactionTrigger.Skill_NearDeathByCost } },
            new TriggerGroup { title = "④ 打牌・河の状況", triggers = new[] {
                ReactionTrigger.Discard_RedDora, ReactionTrigger.Discard_RawYakuhai, ReactionTrigger.Discard_SafeTile,
                ReactionTrigger.Discard_CenterTile, ReactionTrigger.Discard_SameTileStreak, ReactionTrigger.Discard_HonorStreak } },
            new TriggerGroup { title = "⑥ 勝敗・被弾・瀕死", triggers = new[] {
                ReactionTrigger.Result_EnemyHitYakuman, ReactionTrigger.Result_EnemyDoraBomb,
                ReactionTrigger.Result_PlayerHitYakuman, ReactionTrigger.Result_PlayerNearDeath,
                ReactionTrigger.Result_EnemyKO } },
            new TriggerGroup { title = "⑦ メタ操作・UIつつき・放置", triggers = new[] {
                ReactionTrigger.Meta_ClickHead, ReactionTrigger.Meta_ClickChest, ReactionTrigger.Meta_ClickSpam,
                ReactionTrigger.Meta_ScreenClickSpam, ReactionTrigger.Meta_Idle20s, ReactionTrigger.Meta_Idle60s,
                ReactionTrigger.Meta_MuteAudio, ReactionTrigger.Meta_WindowRefocus } },
            new TriggerGroup { title = "① 牌・手牌・山牌操作", triggers = new[] {
                ReactionTrigger.Tile_HoverDora, ReactionTrigger.Tile_HoverHesitation, ReactionTrigger.Tile_WallPoke,
                ReactionTrigger.Tile_InstantDiscard, ReactionTrigger.Tile_SpamClick, ReactionTrigger.Tile_PeekHold,
                ReactionTrigger.Tile_ThinkTimeout } },
        };

        // ------------------------------------------------------------------
        // CSV の読み書き
        // ------------------------------------------------------------------

        /// <summary>CSV の1行。`lineIndex` が -1 のものはまだファイルに無い新規行</summary>
        private class Row
        {
            public int lineIndex = -1;
            public string condition = "";
            public string pose = "";
            public string expression = "";
            public string dialogue1 = "";
            public string dialogue2 = "";
        }

        // ファイルの見た目をなるべく壊さないため、生の行をそのまま抱えておく。
        // 触った行だけ組み立て直して差し替える。全部を書き出し直すと、
        // 空行や末尾のカンマといった既存の細かい差異が消えて git の差分が膨れる
        private List<string> _rawLines;
        private string _eol = "\r\n";
        private bool _hasBom;
        private int _iCond = -1, _iPose = -1, _iExpr = -1, _iDlg1 = -1, _iDlg2 = -1;
        private int _headerCols = 5;

        private readonly Dictionary<string, Row> _rows = new Dictionary<string, Row>();
        private readonly List<Row> _added = new List<Row>();
        private bool _csvDirty;
        private string _csvError;

        // ------------------------------------------------------------------
        // 画面の状態
        // ------------------------------------------------------------------

        private enum Tab { Match, Click, Trigger }
        private Tab _tab = Tab.Match;
        private Vector2 _scroll;
        private readonly HashSet<string> _collapsed = new HashSet<string>();

        private CharacterData _character;
        private string[] _faceIds = new string[0];
        private string[] _bodyIds = new string[0];

        [MenuItem("Tools/リアクション/対局中とクリックの反応を編集")]
        public static void Open()
        {
            var w = GetWindow<ReactionEditorWindow>("対局中の反応");
            w.minSize = new Vector2(820f, 480f);
            w.LoadAll();
            w.Show();
        }

        private void OnEnable()
        {
            if (_rawLines == null) LoadAll();
        }

        // ------------------------------------------------------------------
        // 描画
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            DrawToolbar();

            if (!string.IsNullOrEmpty(_csvError) && _tab != Tab.Trigger)
            {
                EditorGUILayout.HelpBox(_csvError, MessageType.Error);
                if (GUILayout.Button("読み込み直す")) LoadAll();
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case Tab.Match: DrawMatchTab(); break;
                case Tab.Click: DrawClickTab(); break;
                case Tab.Trigger: DrawTriggerTab(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var next = (Tab)GUILayout.Toolbar((int)_tab,
                new[] { "対局中（マルチプレイ）", "クリック", "トリガー（CharacterData）" },
                EditorStyles.toolbarButton, GUILayout.Width(420f));
            if (next != _tab) { _tab = next; GUI.FocusControl(null); }

            GUILayout.FlexibleSpace();

            if (_tab == Tab.Trigger)
            {
                using (new EditorGUI.DisabledScope(_character == null))
                {
                    if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                    {
                        EditorUtility.SetDirty(_character);
                        AssetDatabase.SaveAssets();
                        ShowNotification(new GUIContent("保存しました"));
                    }
                }
            }
            else
            {
                GUILayout.Label(_csvDirty ? "● 未保存" : "", EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(!_csvDirty))
                {
                    if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                    {
                        if (SaveCsv()) ShowNotification(new GUIContent("保存しました"));
                    }
                }
                if (GUILayout.Button("読み込み直す", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                {
                    if (!_csvDirty || EditorUtility.DisplayDialog("確認",
                        "保存していない編集があります。破棄して読み込み直しますか？", "破棄する", "やめる"))
                    {
                        LoadCsv();
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            if (_tab == Tab.Trigger)
            {
                EditorGUILayout.HelpBox(
                    "CharacterData.reactions を直接編集します。トリガーごとに複数書くとランダムに1つ選ばれます。\n"
                    + "Bet_/Discard_/Result_ はここに1行でも書くと、同じ場面の CSV のセリフより優先されます"
                    + "（空にすれば CSV に戻ります）。赤い枠に書いても画面には出ません。",
                    MessageType.Info);
                _character = (CharacterData)EditorGUILayout.ObjectField(
                    "編集するキャラ", _character, typeof(CharacterData), false);
                if (GUI.changed) RefreshSpriteIds();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    CsvPath + " を編集します。\n"
                    + "表示中の Play には反映されません。DialogueManager は Awake で1度だけ読むので、"
                    + "保存したあと Play をやり直してください。",
                    MessageType.Info);
            }
        }

    }
}
