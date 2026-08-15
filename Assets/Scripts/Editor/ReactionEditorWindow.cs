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
    public class ReactionEditorWindow : EditorWindow
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

        private void LoadAll()
        {
            LoadCsv();
            LoadCharacter();
        }

        private void LoadCharacter()
        {
            if (_character == null)
            {
                // シーンに置かれた EnemyInfoUI が指しているものを当てにできないので、
                // プロジェクト内で reactions がいちばん多いものを既定にする。
                // 空の残骸（Preafb/差配麻雀.asset）を掴んで「何も無い」と誤解しないため
                var guids = AssetDatabase.FindAssets("t:" + nameof(CharacterData));
                int best = -1;
                foreach (var g in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    var cd = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
                    if (cd == null) continue;
                    int n = cd.reactions == null ? 0 : cd.reactions.Count;
                    if (n > best) { best = n; _character = cd; }
                }
            }
            RefreshSpriteIds();
        }

        private void RefreshSpriteIds()
        {
            var faces = new List<string> { "" };
            var bodies = new List<string> { "" };
            if (_character != null)
            {
                if (_character.faceSprites != null)
                    foreach (var s in _character.faceSprites)
                    {
                        // blink は瞬き専用。候補に混ぜると誤用を誘う
                        if (s != null && !string.IsNullOrEmpty(s.id) && s.id != "blink") faces.Add(s.id);
                    }
                if (_character.bodySprites != null)
                    foreach (var s in _character.bodySprites)
                        if (s != null && !string.IsNullOrEmpty(s.id)) bodies.Add(s.id);
            }
            _faceIds = faces.ToArray();
            _bodyIds = bodies.ToArray();
        }

        private void LoadCsv()
        {
            _rows.Clear();
            _added.Clear();
            _csvDirty = false;
            _csvError = null;
            _rawLines = null;

            string full = Path.GetFullPath(CsvPath);
            if (!File.Exists(full))
            {
                _csvError = "CSV が見つかりません:\n" + CsvPath;
                return;
            }

            string text;
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(full);
                text = File.ReadAllText(full);
            }
            catch (IOException e) { _csvError = "読み込めませんでした: " + e.Message; return; }

            // BOM は ReadAllText が黙って剥がすので、生のバイトで見る。
            // 元のファイルに付いていなければ付けずに書き戻す（無駄な差分を出さないため）
            _hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            _eol = text.Contains("\r\n") ? "\r\n" : "\n";

            _rawLines = new List<string>(text.Split(new[] { _eol }, StringSplitOptions.None));

            for (int i = 0; i < _rawLines.Count; i++)
            {
                // DialogueManager と同じ切り方。カンマとタブのどちらでも列が割れる
                string[] cols = _rawLines[i].Split(',', '\t');
                for (int c = 0; c < cols.Length; c++) cols[c] = cols[c].Replace("﻿", "").Trim();
                if (cols.Length == 0) continue;

                if (_iCond < 0)
                {
                    if (cols[0] != "状況") continue;
                    for (int c = 0; c < cols.Length; c++)
                    {
                        if (cols[c] == "状況") _iCond = c;
                        else if (cols[c] == "体" || cols[c] == "ポーズ") _iPose = c;
                        else if (cols[c] == "表情") _iExpr = c;
                        else if (cols[c] == "セリフ" || cols[c] == "セリフ1") _iDlg1 = c;
                        else if (cols[c] == "セリフ2") _iDlg2 = c;
                    }
                    _headerCols = cols.Length;
                    continue;
                }

                if (_iCond >= cols.Length || string.IsNullOrEmpty(cols[_iCond])) continue;

                var row = new Row { lineIndex = i, condition = cols[_iCond] };
                if (_iPose >= 0 && _iPose < cols.Length) row.pose = cols[_iPose];
                if (_iExpr >= 0 && _iExpr < cols.Length) row.expression = cols[_iExpr];
                if (_iDlg1 >= 0 && _iDlg1 < cols.Length) row.dialogue1 = cols[_iDlg1];
                if (_iDlg2 >= 0 && _iDlg2 < cols.Length) row.dialogue2 = cols[_iDlg2];

                // 同じ状況名が2度出てきたら、先に書かれている方が勝つ。
                // DialogueManager.GetDialogueEntry が Find（先頭一致）で引いているため
                if (!_rows.ContainsKey(row.condition)) _rows[row.condition] = row;
            }

            if (_iCond < 0) _csvError = "見出し行（1列目が「状況」）が見つかりません。";
        }

        private bool SaveCsv()
        {
            if (_rawLines == null) return false;

            var problems = new List<string>();
            foreach (var r in AllRows())
            {
                // DialogueManager は引用符を解釈しない（Split するだけ）ので、
                // カンマやタブが1つでも混ざると列がずれて別のセリフに化ける
                foreach (var pair in new[] {
                    new KeyValuePair<string, string>("体", r.pose),
                    new KeyValuePair<string, string>("表情", r.expression),
                    new KeyValuePair<string, string>("セリフ", r.dialogue1),
                    new KeyValuePair<string, string>("セリフ2", r.dialogue2) })
                {
                    if (string.IsNullOrEmpty(pair.Value)) continue;
                    if (pair.Value.IndexOf(',') >= 0 || pair.Value.IndexOf('\t') >= 0)
                        problems.Add($"「{r.condition}」の{pair.Key}に , か Tab が入っています（列がずれるので使えません。読点は 、 を使ってください）");
                    if (pair.Value.IndexOf('\n') >= 0 || pair.Value.IndexOf('\r') >= 0)
                        problems.Add($"「{r.condition}」の{pair.Key}に改行が入っています");
                }
            }

            if (problems.Count > 0)
            {
                EditorUtility.DisplayDialog("保存できません",
                    string.Join("\n", problems.ToArray()), "閉じる");
                return false;
            }

            var outLines = new List<string>(_rawLines);
            foreach (var r in AllRows())
            {
                if (r.lineIndex < 0) continue;
                outLines[r.lineIndex] = BuildLine(r);
            }
            foreach (var r in _added)
            {
                if (r.lineIndex >= 0) continue;
                outLines.Add(BuildLine(r));
                r.lineIndex = outLines.Count - 1;
            }

            string body = string.Join(_eol, outLines.ToArray());
            try
            {
                File.WriteAllText(Path.GetFullPath(CsvPath), body, new UTF8Encoding(_hasBom));
            }
            catch (IOException e)
            {
                EditorUtility.DisplayDialog("保存できません", e.Message, "閉じる");
                return false;
            }

            AssetDatabase.ImportAsset(CsvPath);
            _rawLines = outLines;
            _csvDirty = false;
            return true;
        }

        private string BuildLine(Row r)
        {
            string[] cols;
            if (r.lineIndex >= 0 && r.lineIndex < _rawLines.Count)
                cols = _rawLines[r.lineIndex].Split(',');
            else
                cols = new string[_headerCols];

            int need = Mathf.Max(_headerCols, Mathf.Max(Mathf.Max(_iCond, _iPose), Mathf.Max(Mathf.Max(_iExpr, _iDlg1), _iDlg2)) + 1);
            if (cols.Length < need)
            {
                var grown = new string[need];
                for (int i = 0; i < grown.Length; i++) grown[i] = i < cols.Length ? cols[i] : "";
                cols = grown;
            }
            for (int i = 0; i < cols.Length; i++) if (cols[i] == null) cols[i] = "";

            if (_iCond >= 0) cols[_iCond] = r.condition;
            if (_iPose >= 0) cols[_iPose] = r.pose ?? "";
            if (_iExpr >= 0) cols[_iExpr] = r.expression ?? "";
            if (_iDlg1 >= 0) cols[_iDlg1] = r.dialogue1 ?? "";
            if (_iDlg2 >= 0) cols[_iDlg2] = r.dialogue2 ?? "";

            return string.Join(",", cols);
        }

        private IEnumerable<Row> AllRows()
        {
            foreach (var kv in _rows) yield return kv.Value;
            foreach (var r in _added) if (!_rows.ContainsKey(r.condition)) yield return r;
        }

        private Row FindRow(string condition)
        {
            Row r;
            if (_rows.TryGetValue(condition, out r)) return r;
            foreach (var a in _added) if (a.condition == condition) return a;
            return null;
        }

        private Row CreateRow(string condition)
        {
            var r = new Row { condition = condition, pose = "通常" };
            _added.Add(r);
            _rows[condition] = r;
            _csvDirty = true;
            return r;
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

        // ---- 対局中タブ ----

        private void DrawMatchTab()
        {
            foreach (var g in CsvGroups)
            {
                EditorGUILayout.LabelField(g.title, EditorStyles.boldLabel);
                if (!string.IsNullOrEmpty(g.note)) EditorGUILayout.LabelField(P(g.note), WrapMini);
                foreach (var spec in g.specs) DrawCsvSpec(spec);
                EditorGUILayout.Space();
            }

            DrawUnlistedRows();
        }

        /// <summary>
        /// 目次に載っていない状況名を最後にまとめて出す。
        /// **CSV に足したのに目次を更新し忘れた行を隠さないため。** 見えないと二重に書き足す事故になる。
        /// </summary>
        private void DrawUnlistedRows()
        {
            var known = new HashSet<string>();
            foreach (var g in CsvGroups) foreach (var s in g.specs) known.Add(s.condition);

            var rest = new List<Row>();
            foreach (var r in AllRows())
            {
                if (known.Contains(r.condition)) continue;
                if (r.condition.StartsWith("クリックされた時")) continue;   // クリックタブの担当
                rest.Add(r);
            }
            if (rest.Count == 0) return;

            rest.Sort((a, b) => a.lineIndex.CompareTo(b.lineIndex));

            EditorGUILayout.LabelField("目次に載っていない行", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "CSV にあるけれど、このウィンドウの目次に説明が書かれていない状況名です。"
                + "いつ出るのか（そもそも出るのか）はコードを読んで確かめてください。", WrapMini);

            foreach (var r in rest)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(r.condition, EditorStyles.miniBoldLabel);
                DrawRowFields(r);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawCsvSpec(CsvSpec spec)
        {
            var row = FindRow(spec.condition);
            bool dead = !string.IsNullOrEmpty(spec.dead);
            string key = "csv:" + spec.condition;
            bool collapsed = _collapsed.Contains(key);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            string head = dead
                ? spec.condition + "   ×  出ません"
                : spec.condition + (row == null ? "   （行がありません）" : "");

            bool open = EditorGUILayout.Foldout(!collapsed, head, true, dead ? RedFoldout : EditorStyles.foldoutHeader);
            if (open == collapsed) { if (open) _collapsed.Remove(key); else _collapsed.Add(key); }

            // 畳んでいても「出ません」だけは見せる。閉じた枠に書き足す事故を防ぐ
            if (dead && !open) DrawDeadBanner(spec.dead);

            if (!open) { EditorGUILayout.EndVertical(); return; }

            if (dead) DrawDeadBanner(spec.dead);
            EditorGUILayout.LabelField("いつ出るか: " + P(spec.when), WrapMini);

            if (row == null)
            {
                EditorGUILayout.LabelField("（この状況の行が CSV にありません。無言になります）", WrapMini);
                if (GUILayout.Button("この行を作る", GUILayout.Width(120f))) CreateRow(spec.condition);
                EditorGUILayout.EndVertical();
                return;
            }

            DrawRowFields(row);
            EditorGUILayout.EndVertical();
        }

        private void DrawRowFields(Row row)
        {
            EditorGUILayout.BeginHorizontal();
            row.pose = DrawIdPopup("体", row.pose, _bodyIds, 40f, 90f);
            row.expression = DrawIdPopup("表情", row.expression, _faceIds, 40f, 110f);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            DrawTextField("セリフ", ref row.dialogue1);
            DrawTextField("セリフ2", ref row.dialogue2);

            // セリフ2 だけ書かれていても DialogueManager は拾うが、
            // 1 が空だと最初の吹き出しが出ないまま2つ目が流れる。分かりにくいので注意を出す
            if (string.IsNullOrEmpty(row.dialogue1) && !string.IsNullOrEmpty(row.dialogue2))
            {
                EditorGUILayout.HelpBox("セリフ が空でセリフ2 だけ入っています。セリフ2 が単独で流れます。", MessageType.Warning);
            }

            WarnAboutText(row.dialogue1);
            WarnAboutText(row.dialogue2);
            WarnAboutId(row.pose, _bodyIds, "体");
            WarnAboutId(row.expression, _faceIds, "表情");
        }

        private void DrawTextField(string label, ref string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(48f));
            string next = EditorGUILayout.TextArea(value ?? "", GUILayout.MinHeight(18f));
            EditorGUILayout.EndHorizontal();
            if (next != (value ?? "")) { value = next; _csvDirty = true; }
        }

        private string DrawIdPopup(string label, string value, string[] ids, float labelW, float popupW)
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(labelW));

            int index = Array.IndexOf(ids, value ?? "");
            if (index < 0)
            {
                // アセットに無いIDが CSV に書かれている。勝手に消さず、そのまま選択肢に足して見せる
                var grown = new string[ids.Length + 1];
                Array.Copy(ids, grown, ids.Length);
                grown[ids.Length] = value;
                ids = grown;
                index = ids.Length - 1;
            }

            var labels = new string[ids.Length];
            for (int i = 0; i < ids.Length; i++) labels[i] = string.IsNullOrEmpty(ids[i]) ? "（変えない）" : ids[i];

            int next = EditorGUILayout.Popup(index, labels, GUILayout.Width(popupW));
            if (next != index) { _csvDirty = true; return ids[next]; }
            return value;
        }

        private static void WarnAboutText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (text.Length > DialogueSoftLimit)
                EditorGUILayout.HelpBox($"{text.Length} 字あります。{DialogueSoftLimit} 字を超えると吹き出しから見切れます。", MessageType.Warning);
            if (text.IndexOf(',') >= 0 || text.IndexOf('\t') >= 0)
                EditorGUILayout.HelpBox(", や Tab は列の区切りとして読まれてしまいます。読点は 、 を使ってください。", MessageType.Error);
        }

        private void WarnAboutId(string id, string[] known, string what)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (Array.IndexOf(known, id) >= 0) return;
            EditorGUILayout.HelpBox(
                $"{what}「{id}」は {(_character != null ? _character.name : "CharacterData")} に登録されていません。"
                + "絵が見つからないので、表情は変わらずキャラが跳ねるだけになります。", MessageType.Error);
        }

        // ---- クリックタブ ----

        private void DrawClickTab()
        {
            var sceneAreas = CollectSceneClickAreas();

            EditorGUILayout.HelpBox(
                "クリックの抽選は ClickableCharacter.OnClicked の Random.Range(1, 21)。\n"
                + "1〜20 の番号しか引かれません。21 番以降は何を書いても絶対に出ません。\n"
                + "部位をクリックすると、まず「クリックされた時_部位名＋番号」を探し、"
                + "無ければ同じ番号の「クリックされた時＋番号」に落ちます。",
                MessageType.Info);

            if (sceneAreas != null)
            {
                EditorGUILayout.LabelField(
                    sceneAreas.Count == 0
                        ? "いま開いているシーンに ClickableCharacter のクリック枠がありません。"
                        : "いま開いているシーンのクリック枠: " + string.Join(" / ", new List<string>(sceneAreas).ToArray()),
                    WrapMiniBold);
            }

            DrawClickGroup("", "全体（部位の枠に当たらなかったとき、および部位専用が無いとき）", sceneAreas);

            foreach (var area in CollectClickAreaNames(sceneAreas))
            {
                DrawClickGroup(area, "部位「" + area + "」", sceneAreas);
            }
        }

        /// <summary>
        /// いま開いているシーンの `ClickableCharacter` から部位名を集める。
        /// `clickAreas` は private なので `SerializedObject` 経由で読む。
        /// **シーンを開いていない／コンポーネントが無いときは null を返す**（「枠が0個」と区別する）。
        /// </summary>
        private static List<string> CollectSceneClickAreas()
        {
            var comps = UnityEngine.Object.FindObjectsByType<ClickableCharacter>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (comps == null || comps.Length == 0) return null;

            var names = new List<string>();
            foreach (var c in comps)
            {
                var so = new SerializedObject(c);
                var list = so.FindProperty("clickAreas");
                if (list == null || !list.isArray) continue;
                for (int i = 0; i < list.arraySize; i++)
                {
                    var nameProp = list.GetArrayElementAtIndex(i).FindPropertyRelative("areaName");
                    if (nameProp == null) continue;
                    string n = nameProp.stringValue;
                    // 同じ名前の枠が複数あっても、引くセリフは1種類なのでまとめて数える
                    if (!names.Contains(n)) names.Add(n);
                }
            }
            return names;
        }

        /// <summary>CSV に書かれている部位名と、シーンに置かれている部位名の和集合</summary>
        private List<string> CollectClickAreaNames(List<string> sceneAreas)
        {
            var areas = new List<string>();
            foreach (var r in AllRows())
            {
                if (!r.condition.StartsWith("クリックされた時_")) continue;
                string rest = r.condition.Substring("クリックされた時_".Length);
                int cut = rest.Length;
                while (cut > 0 && char.IsDigit(rest[cut - 1])) cut--;
                if (cut <= 0 || cut == rest.Length) continue;
                string area = rest.Substring(0, cut);
                if (!areas.Contains(area)) areas.Add(area);
            }
            if (sceneAreas != null)
                foreach (var a in sceneAreas)
                    if (!string.IsNullOrEmpty(a) && !areas.Contains(a)) areas.Add(a);
            areas.Sort(StringComparer.Ordinal);
            return areas;
        }

        private void DrawClickGroup(string area, string title, List<string> sceneAreas)
        {
            string prefix = string.IsNullOrEmpty(area) ? "クリックされた時" : "クリックされた時_" + area;

            // この部位で埋まっている番号と、抽選の外に出てしまっている番号を数える
            var present = new List<int>();
            int outOfRange = 0;
            for (int n = 1; n <= 200; n++)
            {
                var r = FindRow(prefix + n);
                if (r == null) continue;
                if (n <= ClickLotteryMax) present.Add(n); else outOfRange++;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            // 部位専用は、埋まっていない番号がそのまま全体側へ落ちる。
            // 「5本しか書いていないのに全然出ない」の正体がこれなので、割合を数字で出す
            if (!string.IsNullOrEmpty(area))
            {
                int hit = present.Count;
                EditorGUILayout.LabelField(
                    $"1〜{ClickLotteryMax} のうち {hit} 個が埋まっています → この部位を押したとき"
                    + $"{hit * 100 / ClickLotteryMax}% が専用のセリフ、"
                    + $"残り {(ClickLotteryMax - hit) * 100 / ClickLotteryMax}% は全体のセリフに落ちます。",
                    WrapMini);

                if (sceneAreas != null && !sceneAreas.Contains(area))
                {
                    DrawDeadBanner("いま開いているシーンの ClickableCharacter に「" + area + "」の枠がありません。"
                        + "枠が無いと OnClicked にこの部位名が渡らないので、ここのセリフは1度も出ません。");
                }
            }

            if (outOfRange > 0)
            {
                DrawDeadBanner($"{ClickLotteryMax + 1} 番以降が {outOfRange} 行あります。"
                    + $"抽選は Random.Range(1, {ClickLotteryMax + 1}) なので、この番号は永久に引かれません。"
                    + $"活かすなら {ClickLotteryMax} 番までの空き番号へ移すか、ClickableCharacter.OnClicked の範囲を広げてください。");
            }

            for (int n = 1; n <= ClickLotteryMax + outOfRange + 1; n++)
            {
                string cond = prefix + n;
                var row = FindRow(cond);
                bool beyond = n > ClickLotteryMax;

                if (row == null)
                {
                    // 空き番号は1つだけ「作る」ボタンを出す。全部並べても邪魔なので
                    if (n <= ClickLotteryMax || beyond)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(
                            beyond ? $"{n} 番（作っても抽選されません）" : $"{n} 番　（空き）",
                            beyond ? RedMini : WrapMini);
                        if (!beyond && GUILayout.Button("作る", GUILayout.Width(60f))) CreateRow(cond);
                        EditorGUILayout.EndHorizontal();
                        if (beyond) break;
                    }
                    continue;
                }

                string key = "click:" + cond;
                bool collapsed = _collapsed.Contains(key);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                string head = beyond
                    ? $"{n} 番   ×  抽選されません　{Excerpt(row.dialogue1)}"
                    : $"{n} 番　{Excerpt(row.dialogue1)}";

                bool open = EditorGUILayout.Foldout(!collapsed, head, true, beyond ? RedFoldout : EditorStyles.foldoutHeader);
                if (open == collapsed) { if (open) _collapsed.Remove(key); else _collapsed.Add(key); }

                if (open) DrawRowFields(row);
                EditorGUILayout.EndVertical();
            }
        }

        private static string Excerpt(string s)
        {
            if (string.IsNullOrEmpty(s)) return "（空）";
            return s.Length <= 24 ? s : s.Substring(0, 24) + "…";
        }

        // ---- トリガータブ ----

        private void DrawTriggerTab()
        {
            if (_character == null)
            {
                EditorGUILayout.HelpBox("CharacterData を選んでください。", MessageType.Info);
                return;
            }
            if (_character.reactions == null)
                _character.reactions = new List<CharacterReaction>();

            EditorGUILayout.LabelField(
                "漫符（manfu）・表示時間（duration）・ボイス（voiceClip）は"
                + "まだどこからも読まれていません。設定しても今は効きません。", WrapMiniBold);
            EditorGUILayout.LabelField(
                "EnemyInfoUI.PlayReaction は表情が設定されているときだけ絵を差し替えます。"
                + "体だけ設定しても無視されます。", WrapMini);

            foreach (var g in TriggerGroups)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(g.title, EditorStyles.boldLabel);
                foreach (var t in g.triggers) DrawTrigger(t);
            }

            DrawUnlistedTriggers();
        }

        /// <summary>
        /// `TriggerGroups` のどの分類にも入っていないトリガーを最後にまとめて出す。
        ///
        /// **これが無いと、enum に足したトリガーがエディタから見えない。**
        /// 「追加したのに出てこない」＝「追加できていない」と誤解して二重に足す事故になる。
        /// `ReactionTrigger` を正として引くので、分類への登録を忘れても必ずここに現れる。
        /// </summary>
        private void DrawUnlistedTriggers()
        {
            var known = new HashSet<ReactionTrigger>();
            foreach (var g in TriggerGroups)
                foreach (var t in g.triggers) known.Add(t);

            var rest = new List<ReactionTrigger>();
            foreach (ReactionTrigger t in Enum.GetValues(typeof(ReactionTrigger)))
                if (!known.Contains(t)) rest.Add(t);

            if (rest.Count == 0) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("分類に登録されていないトリガー", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "ReactionTrigger には在るけれど、このウィンドウの分類（TriggerGroups）に"
                + "書かれていないものです。セリフはここからそのまま書けます。\n"
                + "分類に足すと、上の適切な見出しの下へ移動します。", WrapMini);

            foreach (var t in rest) DrawTrigger(t);
        }

        private void DrawTrigger(ReactionTrigger trigger)
        {
            string when;
            bool live = LiveTriggers.TryGetValue(trigger, out when);

            var mine = new List<CharacterReaction>();
            foreach (var r in _character.reactions)
                if (r != null && r.trigger == trigger) mine.Add(r);

            string key = "trg:" + trigger;
            bool collapsed = _collapsed.Contains(key);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            string head = live
                ? $"{trigger}  ({mine.Count}件)"
                : $"{trigger}  ({mine.Count}件)   ×  呼ばれていません";
            bool open = EditorGUILayout.Foldout(!collapsed, head, true, live ? EditorStyles.foldoutHeader : RedFoldout);
            if (open == collapsed) { if (open) _collapsed.Remove(key); else _collapsed.Add(key); }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("＋行を足す", GUILayout.Width(90f)))
            {
                RecordCharacter("リアクションを追加");
                _character.reactions.Add(new CharacterReaction { trigger = trigger, dialogueText = "" });
                _collapsed.Remove(key);
                open = true;
            }
            EditorGUILayout.EndHorizontal();

            if (!live && !open)
            {
                DrawDeadBanner(UnwiredReason(trigger));
                EditorGUILayout.EndVertical();
                return;
            }
            if (!open) { EditorGUILayout.EndVertical(); return; }

            if (live)
            {
                EditorGUILayout.LabelField("いつ出るか: " + P(when), WrapMini);
            }
            else
            {
                DrawDeadBanner(UnwiredReason(trigger));
            }

            if (mine.Count == 0)
            {
                EditorGUILayout.LabelField("（このトリガーのセリフはありません）", WrapMini);
                EditorGUILayout.EndVertical();
                return;
            }
            if (mine.Count == 1)
            {
                EditorGUILayout.HelpBox("1件だけなので、毎回まったく同じセリフになります。", MessageType.Warning);
            }

            CharacterReaction remove = null;
            for (int i = 0; i < mine.Count; i++)
            {
                var r = mine[i];

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label((i + 1).ToString(), EditorStyles.miniLabel, GUILayout.Width(22f));

                string nextFace = DrawIdPopupForAsset("表情", r.faceExpressionId, _faceIds, 40f, 110f, r);
                if (nextFace != r.faceExpressionId) { RecordCharacter("表情を変更"); r.faceExpressionId = nextFace; }

                var nextManfu = (ManfuType)EditorGUILayout.EnumPopup(r.manfuType, GUILayout.Width(90f));
                if (nextManfu != r.manfuType) { RecordCharacter("漫符を変更"); r.manfuType = nextManfu; }

                string nextText = EditorGUILayout.TextArea(r.dialogueText ?? "", GUILayout.MinHeight(18f));
                if (nextText != (r.dialogueText ?? "")) { RecordCharacter("セリフを編集"); r.dialogueText = nextText; }

                if (GUILayout.Button("−", GUILayout.Width(24f))) remove = r;
                EditorGUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(r.dialogueText) && r.dialogueText.Length > DialogueSoftLimit)
                {
                    EditorGUILayout.HelpBox(
                        $"{r.dialogueText.Length} 字あります。{DialogueSoftLimit} 字を超えると吹き出しから見切れます。",
                        MessageType.Warning);
                }

                // `{0}` は EnemyDiscard にしか渡っていない。他で書くとそのまま画面に出る
                if (!string.IsNullOrEmpty(r.dialogueText) && r.dialogueText.Contains("{0}")
                    && r.trigger != ReactionTrigger.EnemyDiscard)
                {
                    EditorGUILayout.HelpBox(
                        "{0} に入れる値が渡されるのは EnemyDiscard だけです。"
                        + "ここでは {0} の文字がそのまま吹き出しに出ます。", MessageType.Error);
                }

                if (!string.IsNullOrEmpty(r.bodyExpressionId) && string.IsNullOrEmpty(r.faceExpressionId))
                {
                    EditorGUILayout.HelpBox(
                        "体だけ設定されています。PlayReaction は表情が空だと絵をまったく差し替えないので、"
                        + "この体は無視されます。", MessageType.Warning);
                }
            }

            if (remove != null)
            {
                RecordCharacter("リアクションを削除");
                _character.reactions.Remove(remove);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>配線されていない理由。個別の説明が無ければ一般的な文言に落とす</summary>
        private static string UnwiredReason(ReactionTrigger trigger)
        {
            string reason;
            if (UnwiredReasons.TryGetValue(trigger, out reason)) return reason;
            return "このトリガーを鳴らしているコードがありません。"
                 + "出すには発火させる側から ReactionController.Trigger() を呼ぶ実装が要ります。"
                 + "配線したら ReactionEditorWindow の LiveTriggers に条件を1行書き足すと、"
                 + "この赤帯が「いつ出るか」の説明に変わります。";
        }

        private string DrawIdPopupForAsset(string label, string value, string[] ids, float labelW, float popupW, CharacterReaction r)
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(labelW));

            int index = Array.IndexOf(ids, value ?? "");
            if (index < 0)
            {
                var grown = new string[ids.Length + 1];
                Array.Copy(ids, grown, ids.Length);
                grown[ids.Length] = value;
                ids = grown;
                index = ids.Length - 1;
            }

            var labels = new string[ids.Length];
            for (int i = 0; i < ids.Length; i++) labels[i] = string.IsNullOrEmpty(ids[i]) ? "（変えない）" : ids[i];

            int next = EditorGUILayout.Popup(index, labels, GUILayout.Width(popupW));
            return ids[next];
        }

        /// <summary>
        /// Undo に積みつつ dirty を立てる。
        /// **ScriptableObject の中の List を書き換えるだけでは Unity は保存すべきだと気づかない。**
        /// 触る直前に必ずこれを通す。
        /// </summary>
        private void RecordCharacter(string label)
        {
            if (_character == null) return;
            Undo.RecordObject(_character, label);
            EditorUtility.SetDirty(_character);
        }

        // ------------------------------------------------------------------
        // 見た目の小物
        // ------------------------------------------------------------------

        /// <summary>
        /// 説明文から Markdown の飾りを落とす。
        /// **IMGUI は ** も ` も解釈しない**ので、そのまま渡すと記号が画面に出てしまう。
        /// 目次の文字列は他の資料と読み比べるためにソース上は Markdown のまま置いておき、
        /// 画面に出す直前でここを通す。
        /// </summary>
        private static string P(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("**", "").Replace("`", "");
        }

        /// <summary>Unity のスキンによって読める赤が違うので、明暗で切り替える。</summary>
        private static Color DeadColor
        {
            get
            {
                return EditorGUIUtility.isProSkin
                    ? new Color(1f, 0.45f, 0.40f)
                    : new Color(0.72f, 0.11f, 0.08f);
            }
        }

        private static GUIStyle _wrapMini, _wrapMiniBold, _redMini, _redFoldout;

        private static GUIStyle WrapMini
        {
            get
            {
                if (_wrapMini == null) _wrapMini = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                return _wrapMini;
            }
        }

        private static GUIStyle WrapMiniBold
        {
            get
            {
                if (_wrapMiniBold == null) _wrapMiniBold = new GUIStyle(EditorStyles.miniBoldLabel) { wordWrap = true };
                return _wrapMiniBold;
            }
        }

        private static GUIStyle RedMini
        {
            get
            {
                // 色はスキンで変わるので毎回入れ直す。生成し直すと GC が増えるため style だけ使い回す
                if (_redMini == null) _redMini = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                var c = DeadColor;
                _redMini.normal.textColor = c;
                return _redMini;
            }
        }

        private static GUIStyle RedFoldout
        {
            get
            {
                if (_redFoldout == null) _redFoldout = new GUIStyle(EditorStyles.foldoutHeader);
                var c = DeadColor;
                _redFoldout.normal.textColor = c;
                _redFoldout.onNormal.textColor = c;
                _redFoldout.focused.textColor = c;
                _redFoldout.onFocused.textColor = c;
                _redFoldout.hover.textColor = c;
                _redFoldout.onHover.textColor = c;
                _redFoldout.active.textColor = c;
                _redFoldout.onActive.textColor = c;
                return _redFoldout;
            }
        }

        /// <summary>「出ません」の赤い帯。理由まで書かないと直しようがない。</summary>
        private static void DrawDeadBanner(string reason)
        {
            var prev = GUI.color;
            GUI.color = DeadColor;
            EditorGUILayout.LabelField("× ここに書いても画面には出ません。", WrapMiniBold);
            GUI.color = prev;
            EditorGUILayout.LabelField("理由: " + P(reason), WrapMini);
        }
    }
}
