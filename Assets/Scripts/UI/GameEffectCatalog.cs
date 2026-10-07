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
            public Entry(string id, string group, string name, string source)
            { Id = id; Group = group; Name = name; Source = source; }
        }

        private static readonly Entry[] entries = {
            new Entry("skill.perspective", "スキル", "透視：集中 → 3枚公開 → 解除", "ExposedTileEffectPlayer.PlayReveal"),
            new Entry("skill.mulligan", "スキル", "牌交換：OUT / IN", "MulliganSwapAnimator.PlayRoutine"),
            new Entry("skill.boost_hand", "スキル", "役強化：清一色＋1", "PhaseTransitionUI.PlaySkillCutinAnimationRoutine"),
            new Entry("skill.assault", "スキル", "強襲：能力カットイン", "PhaseTransitionUI.PlaySkillCutinAnimationRoutine"),
            new Entry("skill.cutin.enemy", "スキル", "相手の能力カットイン", "PhaseTransitionUI.PlaySkillCutinAnimationRoutine"),
            new Entry("phase.dealing", "フェイズ", "配牌：暗転 → 牌の山 → 明転", "PhaseTransitionUI.PlayRoundStartDarken / PlayRoundStartFadeOut"),
            new Entry("phase.betting", "フェイズ", "掛け金パネル：入る → 出る", "BettingUI.ShowFixedBettingPhase / HideBettingPhase"),
            new Entry("phase.battle_start", "フェイズ", "対局開始：黒帯・市松・掛け金", "PhaseTransitionUI.PlayTransition"),
            new Entry("phase.local_turn", "フェイズ", "先行の表示", "PhaseTransitionUI.PlayCenterTextAnimRoutine"),
            new Entry("phase.enemy_turn", "フェイズ", "後攻の表示", "PhaseTransitionUI.PlayCenterTextAnimRoutine"),
            new Entry("phase.prompt", "フェイズ", "手牌選択の案内", "PhaseTransitionUI.PlayPromptText"),
            new Entry("phase.draw", "フェイズ", "流局の切り替え", "PhaseTransitionUI.PlayDrawTransition"),
            new Entry("ron.chance", "ロン・清算", "あたり牌：暗幕・牌の点滅", "RonChanceEffect.Show"),
            new Entry("ron.impact", "ロン・清算", "ロン入力：手振り・吹き出し", "RonImpactEffect.Play"),
            new Entry("ron.local", "ロン・清算", "自分のロン：カットイン・清算・血移動", "RonAnimationUI.PlayRonSequence"),
            new Entry("ron.enemy", "ロン・清算", "相手のロン：カットイン・清算・血移動", "RonAnimationUI.PlayRonSequence"),
            new Entry("settlement.local", "ロン・清算", "自分が得る血の表示", "PhaseTransitionUI.PlayScoreSettlementAnimation"),
            new Entry("settlement.enemy", "ロン・清算", "相手が得る血の表示", "PhaseTransitionUI.PlayScoreSettlementAnimation"),
            new Entry("ending.normal_win", "勝敗・ED", "通常勝利（現在は共通EDの仮内容）", "EndingSequenceUI.Show"),
            new Entry("ending.normal_lose", "勝敗・ED", "通常敗北：本文 → DEAD END → クレジット", "EndingSequenceUI.Show"),
            new Entry("ending.special_win", "勝敗・ED", "点数勝利（現在は共通EDの仮内容）", "EndingSequenceUI.Show"),
            new Entry("ending.special_lose", "勝敗・ED", "点数敗北：共通ED", "EndingSequenceUI.Show"),
            new Entry("screen.flash", "画面効果", "白フラッシュ", "ScreenFlash.Play"),
            new Entry("screen.pixel_tone", "画面効果", "細かなドット・ディザ", "PixelToneUI.Attach"),
            new Entry("screen.scene_break", "画面効果", "場面の切れ目：白飛ばし", "ScreenFlash.PlaySceneBreak"),
            new Entry("screen.quake", "画面効果", "画面の揺れ", "ScreenQuake.Play"),
            new Entry("screen.tint", "画面効果", "赤い色かぶり", "ScreenTint.Set / Clear"),
            new Entry("screen.flicker", "画面効果", "赤い明滅", "ScreenTint.Flicker"),
            new Entry("screen.wake", "画面効果", "目を開ける導入", "BlinkEffectUI.PlayWakeUpEffect"),
            new Entry("tile.sparkle", "牌・血・ゲージ", "牌のきらめき", "TileSparkleEffect.Attach"),
            new Entry("tile.rays", "牌・血・ゲージ", "牌から立ち上がる光", "TileRisingRayEffect.Attach"),
            new Entry("tile.clatter", "牌・血・ゲージ", "暗転中の牌の山", "TileClatterEffect.Attach"),
            new Entry("blood.burst", "牌・血・ゲージ", "ドットの血しぶき", "PixelBloodEffect.Play"),
            new Entry("blood.transfer", "牌・血・ゲージ", "血が相手から自分へ移る", "BloodTransferEffect.Play"),
            new Entry("blood.glow", "牌・血・ゲージ", "血のメーターの発光", "BloodGlowUI.Attach"),
            new Entry("hp.damage", "牌・血・ゲージ", "ダメージ：メーター・数値", "PlayerInfoUI.SetHP / EnemyInfoUI.SetHP"),
            new Entry("hp.heal", "牌・血・ゲージ", "回復：メーター・数値", "PlayerInfoUI.SetHP"),
            new Entry("hp.heartbeat", "牌・血・ゲージ", "瀕死：心音・暗い縁", "HeartbeatEffect.UpdateHeartbeat"),
            new Entry("hp.glitch", "牌・血・ゲージ", "被ダメージ時のノイズ（本編では停止中）", "HpDamageGlitch.Play"),
            new Entry("hp.zoom_self", "牌・血・ゲージ", "自分の体力表示の拡大", "PlayerInfoUI.ZoomInRoutine"),
            new Entry("hp.zoom_enemy", "牌・血・ゲージ", "相手の体力表示の拡大", "EnemyInfoUI.ZoomInRoutine"),
            new Entry("tile.dora", "牌・血・ゲージ", "ドラ牌の発光", "TileVisual.SetTile / DoraShine"),
            new Entry("gauge.absorb", "牌・血・ゲージ", "掛け金の吸収・獲得ゲージ", "ScoreGaugeUI.AbsorbStakesIntoGauge"),
            new Entry("voltage.flight", "牌・血・ゲージ", "牌がボルテージへ吸い込まれる", "VoltageTileFlightEffect.TryPlay"),
            new Entry("character.death", "キャラクター・背景", "相手が倒れる", "EnemyInfoUI.PlayDeathRoutine"),
            new Entry("character.bounce", "キャラクター・背景", "立ち絵のバウンド", "EnemyInfoUI.PlayBounceAnimation"),
            new Entry("character.turn", "キャラクター・背景", "手番の発光", "EnemyInfoUI.SetTurnGlow / PlayerInfoUI.SetTurnGlow"),
            new Entry("character.breathe", "キャラクター・背景", "呼吸の動き", "BreathingAnimator"),
            new Entry("character.float", "キャラクター・背景", "浮遊の動き", "FloatingAnimator"),
            new Entry("character.blink", "キャラクター・背景", "キャラクターのまばたき", "EnemyInfoUI.SetFaceExpression"),
            new Entry("character.talk", "キャラクター・背景", "喋るときの上下の動き", "TalkBobAnimator / DialogueUI.ShowText"),
            new Entry("camera.zoom", "キャラクター・背景", "カメラが寄って戻る", "CameraZoomController.ZoomToTargetRoutine"),
            new Entry("character.hair", "キャラクター・背景", "髪のネオン", "HairNeonGlow.Attach"),
            new Entry("scene.atmosphere", "キャラクター・背景", "背景の粒子・暗い縁", "SceneAtmosphere.Attach"),
            new Entry("scene.room", "キャラクター・背景", "部屋の待機：浮遊・ホタル", "RoomAmbience.Attach"),
            new Entry("scene.battle", "キャラクター・背景", "対局の雰囲気", "BattleAtmosphere.SetVisible"),
            new Entry("scene.momentum", "キャラクター・背景", "HP推移のモメンタムグラフ", "MatchMomentumUI.ShowMomentum"),
            new Entry("dora.float", "キャラクター・背景", "ドラ牌の浮遊・回転", "DoraFloatAnimator"),
            new Entry("dialogue.typewriter", "チュートリアル", "セリフの文字送り", "DialogueUI.ShowText"),
            new Entry("tutorial.highlight", "チュートリアル", "注目する場所の枠", "TutorialHighlightUI.Show"),
            new Entry("tutorial.arrow", "チュートリアル", "操作する場所の矢印", "TutorialArrowUI.ShowAt"),
            new Entry("tutorial.mask", "チュートリアル", "注目する場所だけを明るく", "TutorialMaskUI.Show"),
            new Entry("tutorial.button_intro", "チュートリアル", "ボタンを初めて見せる", "TutorialButtonIntroUI.Show"),
            new Entry("tile.sort", "牌・血・ゲージ", "手牌が並び替わる", "TileMoveAnimator.AnimatePositions"),
            new Entry("ron.cutin", "ロン・清算", "ロンのカットイン：満貫級", "CutinAnimationUI.PlayCutin"),
            new Entry("ron.cutin_max", "ロン・清算", "ロンのカットイン：役満級", "CutinAnimationUI.PlayCutin"),
            new Entry("scene.aurora", "キャラクター・背景", "光のゆらぎ・色の変化", "AuroraLightAnimator"),
            new Entry("ui.button_hover", "画面効果", "ボタンの拡大・押し込み", "UIButtonHoverEffect"),
            new Entry("ui.menu_hover", "画面効果", "メニューの発光・目印", "MenuButtonHover"),
            new Entry("unused.red_defeat", "没案・旧演出", "赤い目の敗北（本編EDでは未使用）", "RedDefeatPrototypeUI.Play"),
            new Entry("unused.special_victory", "没案・旧演出", "旧・特殊勝利の能力カットイン", "PhaseTransitionUI.PlaySkillCutinAnimationRoutine")
        };
        public static IReadOnlyList<Entry> Entries { get; } = Array.AsReadOnly(entries);
        public static Entry Find(string id)
        {
            foreach (var item in entries) if (item.Id == id) return item;
            throw new ArgumentException("演出IDが見つかりません: " + id, nameof(id));
        }
    }
}
