using System;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    /// <summary>演出の名前・固定ID・実装の所在。コレクションと開発時の指定で共用する。</summary>
    public static class GameEffectCatalog
    {
        public sealed class Entry
        {
            public string Id { get; }
            public string Group { get; }
            public string Name { get; }
            public string Source { get; }

            /// <summary>
            /// 没案か。コレクションでは「演出」ではなく「没案」タブに並べる。
            ///
            /// **没にするときは、下の表でその行の最後に `unused: true` を足すだけ。**
            /// 分類は書き換えない。没案タブでも元の分類（画面効果、牌・血・ゲージ…）が
            /// 読めたほうが、何の演出だったか分かる。
            /// どれを没とするかはユーザーが決める（2026-10-08 に18件の指定を受けた）。
            /// </summary>
            public bool IsUnused { get; }

            public Entry(string id, string group, string name, string source, bool unused = false)
            { Id = id; Group = group; Name = name; Source = source; IsUnused = unused; }
        }

        /// <summary>最初から没案として一覧に入れたものの分類名。没案の判定には使わない。</summary>
        public const string UnusedGroup = "没案・旧演出";

        private static readonly Entry[] entries = {
            new Entry("title.ranking_rows", "画面効果", "ランキング：上から点灯・ページ切替", "RankingRowsTransition / TitleRankingBoardUI"),
            new Entry("skill.perspective", "スキル", "透視：集中 → 3枚公開 → 解除", "ExposedTileEffectPlayer.PlayReveal"),
            new Entry("skill.mulligan", "スキル", "牌交換：OUT / IN", "MulliganSwapAnimator.PlayRoutine"),
            new Entry("skill.boost_hand", "スキル", "役強化：カットイン → 右上の強化枠を短く強調", "GameUISkillController / BoostHandSkillEffect.PlayAfterCutin"),
            new Entry("skill.boost_hand.tutorial", "スキル", "役強化：チュートリアル用・ベルルを呼んで力を借りる", "GameUISkillController (Tutorial) / BoostHandSkillEffect.Play"),
            new Entry("skill.assault", "スキル", "強襲：自分の血を溜める → 相手へ撃ち込む → 札が貼り付く", "AssaultSkillEffect.Play"),
            new Entry("skill.assault_received_mark", "スキル", "強襲を受けた自分HPの札", "GameUISkillController.HandleSkillCasted / AssaultMarkUI.Show"),
            new Entry("skill.cutin.enemy", "スキル", "相手の能力カットイン", "PhaseTransitionUI.PlaySkillCutinAnimationRoutine"),
            new Entry(Effects.EnemySkillBatchPresentation.EffectId, "スキル", "相手の能力連打：透視・透視・役強化・透視", "EnemySkillBatchPresentation.Play"),
            new Entry(Effects.EnemySkillBatchPresentation.SummaryEffectId, "スキル", "旧・相手の能力まとめ：透視 × 2・牌交換", "EnemySkillBatchPresentation.PlaySummary", unused: true),
            new Entry("ui.ability_bell", "スキル", "能力を開くベルの音", "AbilityUI.OnTriggerClicked / AudioManager.PlayAbilityBellSE"),
            new Entry("phase.dealing", "フェイズ", "配牌：暗転 → 局名がその場で短くフェード → 牌の山 → 明転", "PhaseTransitionUI.PlayRoundStartDarken / PlayRoundStartFadeOut"),
            new Entry("phase.betting", "フェイズ", "掛け金パネル：入る → 出る", "BettingUI.ShowFixedBettingPhase / HideBettingPhase"),
            new Entry(Effects.BetStakeFx.EffectId, "フェイズ", "掛け金の重さ：枠に血が溜まる・心音・赤い縁・揺れ", "BettingUI / BetStakeFx"),
            new Entry(Effects.BetStakeFx.CardEffectId, "フェイズ", "賭けたあとの血：勝ち・負け・単騎の札", "BettingUI / BetOutcomePreviewUI", unused: true),
            new Entry("phase.battle_start", "フェイズ", "対局開始：黒帯と文字は60fps基準のフェードと数値表示", "PhaseTransitionUI.PlayTransition"),
            new Entry("phase.local_turn", "フェイズ", "先行の表示", "PhaseTransitionUI.PlayCenterTextAnimRoutine"),
            new Entry("phase.enemy_turn", "フェイズ", "後攻の表示", "PhaseTransitionUI.PlayCenterTextAnimRoutine"),
            new Entry("phase.prompt", "フェイズ", "手牌選択の案内", "PhaseTransitionUI.PlayPromptText"),
            new Entry("hand.wait_reveal", "牌・血・ゲージ", "13枚成立：役名・待ち牌と着弾SE", "HandRankCallUI.ShowRank / HandWaitRevealUI.Show / HandSelectionFeedbackAudio.PlayRankImpact"),
            new Entry("hand.wait_transfer", "牌・血・ゲージ", "手牌決定：光の移動・着地と効果音", "HandWaitRevealUI.Transfer / WaitUI.RevealArrivalTile / HandSelectionFeedbackAudio"),
            new Entry(HandRoleConditionsUI.EffectId, "牌・血・ゲージ", "役条件：順次登場のシャッ音・ホバー解除", "HandRoleConditionsUI / HandSelectionFeedbackAudio.PlayRoleEntry"),
            new Entry(HandRoleTileSupport.EffectId, "牌・血・ゲージ", "役ホバー：条件を満たす牌本体の発光", "HandRoleTileSupport / TileRoleGlow"),
            new Entry("hand.confirm", "フェイズ", "役名・待ち牌 → 手牌決定 → 光の着地", "GameUIHandSelectionController / HandWaitRevealUI"),
            new Entry("hand.confirm_below_mangan", "フェイズ", "満貫未満の待ち → 追加確認 → 手牌決定", "HandWeakConfirmationUI / HandWaitRevealUI"),
            new Entry("hand.confirm_no_waits", "フェイズ", "待ちなし：なしの枠 → 追加確認 → 手牌決定", "HandWeakConfirmationUI / HandWaitRevealUI"),
            new Entry("phase.draw", "フェイズ", "流局の切り替え", "PhaseTransitionUI.PlayDrawTransition"),
            new Entry(TutorialDrawReplayUI.EffectId, "チュートリアル", "流局解説：スマホから拡大・独立字幕・再生停止", "TutorialDrawReplayUI.Play"),
            new Entry(TutorialDrawReplayUI.ContinuationEffectId, "チュートリアル", "動画後半：スマホから拡大・決着・閉じる", "TutorialDrawReplayUI.PlayContinuation"),
            new Entry(EnemyInfoUI.TutorialRevealEffectId, "チュートリアル", "正体判明：先輩から女の子へ", "EnemyInfoUI.RevealTutorialCharacter"),
            new Entry(TutorialDeadEndUI.EffectId, "チュートリアル", "DEADEND No1：赤い遷移・本文・結果・クレジット・復帰", "TutorialManager.RunTutorialDeadEnd / TutorialDeadEndUI.Play"),
            new Entry(TutorialRevivalUI.EffectId, "チュートリアル", "復活：暗転・ベル・ベルル・メニューへの導入", "TutorialManager.RunRevivalFinale / TutorialRevivalUI.Play"),
            new Entry("ron.chance", "ロン・清算", "あたり牌：暗幕・牌の点滅", "RonChanceEffect.Show"),
            new Entry("ron.impact", "ロン・清算", "ロン入力：手振り・吹き出し", "RonImpactEffect.Play"),
            new Entry(Effects.RonAuraUI.LocalEffectId, "ロン・清算", "自分のロン：吹き出し背面の青いオーラ", "RonImpactEffect.Play / RonAuraUI.BehindCallout"),
            new Entry(Effects.RonAuraUI.EnemyEffectId, "ロン・清算", "相手のロン：立ち絵背面の赤いオーラ", "CutinAnimationUI.PlayCutin / RonAuraUI.BehindCharacter"),
            new Entry("ron.local", "ロン・清算", "自分のロン：斜めの役表示 → 清算表（青） → 血の移動", "RonAnimationUI.PlayRonSequence / ShowYakuRibbon / SettlementRoutine"),
            new Entry("ron.enemy", "ロン・清算", "相手のロン：斜めの役表示 → 清算表（赤） → 血の移動", "RonAnimationUI.PlayRonSequence / ShowYakuRibbon / SettlementRoutine"),
            // 2026-10-19 に本編が霧（全身から血が霧散）になったので、それまでのホースを通る筋を没案に置く
            new Entry("ron.stream_local", "ロン・清算", "自分のロン：卓の血だまりからホースで血が流れ込む", "RonAnimationUI.PreviewStreamDrain", unused: true),
            new Entry("ron.stream_enemy", "ロン・清算", "相手のロン：卓の血だまりからホースで血が流れ込む", "RonAnimationUI.PreviewStreamDrain", unused: true),
            new Entry("settlement.local", "ロン・清算", "自分が得る血の表示", "PhaseTransitionUI.PlayScoreSettlementAnimation", unused: true),
            new Entry("settlement.enemy", "ロン・清算", "相手が得る血の表示", "PhaseTransitionUI.PlayScoreSettlementAnimation", unused: true),
            new Entry("ending.normal_win", "勝敗・ED", "通常勝利（現在は共通EDの仮内容）", "EndingSequenceUI.Show"),
            new Entry("ending.normal_lose", "勝敗・ED", "通常敗北：本文 → DEAD END → クレジット", "EndingSequenceUI.Show"),
            new Entry("ending.special_win", "勝敗・ED", "点数勝利（現在は共通EDの仮内容）", "EndingSequenceUI.Show"),
            new Entry("ending.special_lose", "勝敗・ED", "点数敗北：共通ED", "EndingSequenceUI.Show"),
            new Entry("screen.flash", "画面効果", "白フラッシュ", "ScreenFlash.Play"),
            new Entry("screen.pixel_tone", "画面効果", "細かなドット・ディザ", "PixelToneUI.Attach", unused: true),
            new Entry("screen.scene_break", "画面効果", "場面の切れ目：白飛ばし", "ScreenFlash.PlaySceneBreak"),
            new Entry("screen.quake", "画面効果", "画面の揺れ", "ScreenQuake.Play"),
            new Entry("screen.tint", "画面効果", "赤い色かぶり", "ScreenTint.Set / Clear", unused: true),
            new Entry("screen.flicker", "画面効果", "赤い明滅", "ScreenTint.Flicker", unused: true),
            new Entry("screen.wake", "画面効果", "目を開ける導入", "BlinkEffectUI.PlayWakeUpEffect"),
            new Entry("tile.sparkle", "牌・血・ゲージ", "牌のきらめき", "TileSparkleEffect.Attach"),
            new Entry("tile.rays", "牌・血・ゲージ", "牌から立ち上がる光", "TileRisingRayEffect.Attach", unused: true),
            new Entry("tile.clatter", "牌・血・ゲージ", "暗転中の牌の山", "TileClatterEffect.Attach"),
            new Entry("blood.burst", "牌・血・ゲージ", "ドットの血しぶき", "PixelBloodEffect.Play", unused: true),
            new Entry("blood.transfer", "牌・血・ゲージ", "血が相手から自分へ移る", "BloodTransferEffect.Play", unused: true),
            new Entry("blood.glow", "牌・血・ゲージ", "血のメーターの発光", "BloodGlowUI.Attach", unused: true),
            new Entry(DiscardFeedbackDirector.EffectId, "牌・血・ゲージ", "一打の反応：立ち絵の小反応・終盤の静けさ", "RiverUI / DiscardFeedbackDirector / EnemyInfoUI.PlayDiscardAttention"),
            new Entry("hp.damage", "牌・血・ゲージ", "ダメージ：メーター・増減の数値", "PlayerInfoUI.SetHP / EnemyInfoUI.SetHP / DamagePopupUI"),
            new Entry("hp.heal", "牌・血・ゲージ", "回復：メーター・増減の数値", "PlayerInfoUI.SetHP / DamagePopupUI"),
            new Entry("hp.heartbeat", "牌・血・ゲージ", "瀕死：心音・暗い縁", "HeartbeatEffect.UpdateHeartbeat"),
            new Entry("hp.glitch", "牌・血・ゲージ", "被ダメージ時のノイズ（本編では停止中）", "HpDamageGlitch.Play", unused: true),
            new Entry("hp.zoom_self", "牌・血・ゲージ", "自分の体力表示の拡大", "PlayerInfoUI.ZoomInRoutine", unused: true),
            new Entry("hp.zoom_enemy", "牌・血・ゲージ", "相手の体力表示の拡大", "EnemyInfoUI.ZoomInRoutine", unused: true),
            new Entry("tile.dora", "牌・血・ゲージ", "ドラ牌の発光", "TileVisual.SetTile / DoraShine"),
            new Entry("gauge.absorb", "牌・血・ゲージ", "掛け金の吸収・獲得ゲージ", "ScoreGaugeUI.AbsorbStakesIntoGauge"),
            new Entry("voltage.flight", "牌・血・ゲージ", "牌がボルテージへ吸い込まれる", "VoltageTileFlightEffect.TryPlay"),
            new Entry(VoltageUI.RevealEffectId, "牌・血・ゲージ", "ボルテージ登場：ブロック → 炎の点火", "VoltageUI.RevealRoutine / TutorialManager.RunVoltageIntroduction"),
            new Entry("character.death", "キャラクター・背景", "相手が倒れる", "EnemyInfoUI.PlayDeathRoutine"),
            new Entry("character.bounce", "キャラクター・背景", "立ち絵のバウンド", "EnemyInfoUI.PlayBounceAnimation"),
            new Entry("character.turn", "キャラクター・背景", "手番の発光", "EnemyInfoUI.SetTurnGlow / PlayerInfoUI.SetTurnGlow", unused: true),
            new Entry("character.breathe", "キャラクター・背景", "呼吸の動き", "BreathingAnimator", unused: true),
            new Entry("character.float", "キャラクター・背景", "浮遊の動き", "FloatingAnimator", unused: true),
            // 2026-10-22 に本編から外した（とくしゅの CPU 戦だけのお試しだった）
            new Entry("character.beat_dance", "キャラクター・背景", "曲の拍に乗って弾む・揺れる・笑顔と交互", "BeatDanceAnimator / EnemyInfoUI.SetBeatDance", unused: true),
            new Entry("character.blink", "キャラクター・背景", "キャラクターのまばたき", "EnemyInfoUI.SetFaceExpression"),
            new Entry("character.senior_bounce", "キャラクター・背景", "先輩の待機中の跳ね：60fps", "TutorialManager.SetSeniorIdleBounce / SlimeBounceAnimator"),
            new Entry("character.talk", "キャラクター・背景", "喋るときの上下の動き", "TalkBobAnimator / DialogueUI.ShowText"),
            new Entry("camera.zoom", "キャラクター・背景", "カメラが寄って戻る", "CameraZoomController.ZoomToTargetRoutine", unused: true),
            new Entry("character.hair", "キャラクター・背景", "髪のネオン", "HairNeonGlow.Attach", unused: true),
            new Entry("scene.atmosphere", "キャラクター・背景", "背景の粒子・暗い縁", "SceneAtmosphere.Attach"),
            new Entry("scene.room", "キャラクター・背景", "部屋の待機：浮遊・ホタル", "RoomAmbience.Attach"),
            new Entry("scene.battle", "キャラクター・背景", "対局の雰囲気", "BattleAtmosphere.SetVisible"),
            new Entry("scene.momentum", "キャラクター・背景", "HP推移のモメンタムグラフ", "MatchMomentumUI.ShowMomentum"), // 2026-10-15 に採用（没案から戻した）
            // 2026-10-25 にドラの表示（左の壁ぎわの「ドラ」と光る牌）を本編から外した。ドラは牌の発光（tile.dora）だけで示す
            new Entry("dora.float", "キャラクター・背景", "ドラ牌の浮遊・回転（左の壁ぎわのドラの表示）", "DoraFloatAnimator", unused: true),
            new Entry("dialogue.typewriter", "チュートリアル", "セリフの文字送り（音の高さは相手で変わる）", "DialogueUI.ShowText"),
            new Entry("dialogue.log", "チュートリアル", "セリフのログ：過去の吹き出しが並ぶ", "DialogueUI.OpenLog"),
            new Entry("tutorial.highlight", "チュートリアル", "注目する場所の枠", "TutorialHighlightUI.Show"),
            new Entry("tutorial.arrow", "チュートリアル", "操作する場所の矢印", "TutorialArrowUI.ShowAt"),
            new Entry("tutorial.mask", "チュートリアル", "注目する場所だけを明るく", "TutorialMaskUI.Show"),
            new Entry("tutorial.button_intro", "チュートリアル", "ボタンを初めて見せる", "TutorialButtonIntroUI.Show"),
            new Entry(TutorialReadingPresentation.EffectId, "チュートリアル", "資料：暗転とBGMの静まり → ボードを置く → 読了して戻る", "TutorialReadingPresentation.Enter / Exit"),
            new Entry("tile.sort", "牌・血・ゲージ", "手牌が並び替わる", "TileMoveAnimator.AnimatePositions"),
            new Entry("ron.cutin", "ロン・清算", "ロンのカットイン：満貫級", "CutinAnimationUI.PlayCutin"),
            new Entry("ron.cutin_max", "ロン・清算", "ロンのカットイン：役満級", "CutinAnimationUI.PlayCutin"),
            new Entry("scene.aurora", "キャラクター・背景", "光のゆらぎ・色の変化", "AuroraLightAnimator"),
            new Entry("ui.button_hover", "画面効果", "ボタンのホバー枠・押下反応", "UIInteractionFeedback / UIButtonHoverEffect"),
            new Entry("ui.menu_hover", "画面効果", "メニューのホバー枠・押下反応", "UIInteractionFeedback / MenuButtonHover"),
            new Entry(UIFrameCatalog.DefaultId, "画面効果", "標準ドット枠：ホバー・押下・選択", "UIFrameCatalog / UIInteractionFeedback"),
            new Entry(UIFrameCatalog.RoleConditionsId, "画面効果", "役条件の半透明・斜め枠", "UIFrameCatalog / HandRoleConditionsUI"),
            new Entry("unused.red_defeat", UnusedGroup, "赤い目の敗北（本編EDでは未使用）", "RedDefeatPrototypeUI.Play", unused: true),
            new Entry("unused.special_victory", UnusedGroup, "旧・特殊勝利の能力カットイン", "PhaseTransitionUI.PlaySkillCutinAnimationRoutine", unused: true)
        };
        public static IReadOnlyList<Entry> Entries { get; } = Array.AsReadOnly(entries);

        /// <summary>
        /// コレクションのフェイズ別タブ（2026-10-17「演出が多くなってきたので、フェイズごとにタブ付けして」）。
        /// 1局の流れの順に並べる。先頭の「すべて」は絞り込まない。
        /// </summary>
        public const string AllPhases = "すべて";
        public static readonly string[] Phases =
        {
            AllPhases, "手牌選択", "掛け金", "対局", "ロン・流局", "勝敗・ED", "常時・画面", "チュートリアル"
        };

        /// <summary>
        /// 演出がどのフェイズで出るか。**表の行には書かず、ID から決める**
        /// （行ごとに足すと、新しい演出で書き忘れる）。決まらないものは「常時・画面」。
        /// 新しい演出の ID は、ここで拾える頭（`hand.` `phase.bet` `ron.` …）で付けること。
        /// </summary>
        public static string PhaseOf(Entry entry)
        {
            string id = entry.Id;
            if (entry.Group == "チュートリアル" || Starts(id, "tutorial.", "dialogue.")) return "チュートリアル";
            if (Starts(id, "skill.", "hand.", "phase.dealing", "phase.prompt", "tile.sort", "tile.clatter",
                    "ui.ability_bell", "unused.special_victory")) return "手牌選択";
            if (Starts(id, "phase.bet", "phase.battle_start", "gauge.absorb")) return "掛け金";
            if (Starts(id, "phase.local_turn", "phase.enemy_turn", "voltage.", "tile.dora", "dora.", "ron.chance"))
                return "対局";
            if (Starts(id, "ron.", "phase.draw", "settlement.", "hp.damage", "hp.heal", "hp.glitch", "hp.zoom",
                    "blood.", "scene.momentum")) return "ロン・流局";
            if (Starts(id, "ending.", "character.death", "unused.red_defeat")) return "勝敗・ED";
            return "常時・画面";
        }

        private static bool Starts(string id, params string[] heads)
        {
            foreach (var h in heads) if (id.StartsWith(h, StringComparison.Ordinal)) return true;
            return false;
        }
        public static Entry Find(string id)
        {
            foreach (var item in entries) if (item.Id == id) return item;
            throw new ArgumentException("演出IDが見つかりません: " + id, nameof(id));
        }
    }
}
