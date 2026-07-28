namespace KillingMahjong.Common
{
    /// <summary>
    /// Canvas / SpriteRenderer の sortingOrder を一元管理する定数群。
    /// 数値の大小関係がそのまま画面上の重なり順（大きいほど手前）を表す。
    /// 値を変更する際は、前後のレイヤーとの相対関係を必ず確認すること。
    /// </summary>
    public static class UISortingOrders
    {
        // ---- ゲーム内 常設UI (0 - 99) ----

        /// <summary>DoraDisplayUI: ドラ表示牌 (WorldSpace Canvas)</summary>
        public const int DoraTile = 10;

        /// <summary>PhaseTransitionUI: フェーズ演出の基本レイヤー</summary>
        public const int PhaseTransitionBase = 19;

        /// <summary>AbilityUI ルート / PlayerInfoUI 強調表示時の共通レイヤー</summary>
        public const int InfoPanelHighlight = 20;

        /// <summary>AbilityUI: ツールチップ (InfoPanelHighlight より手前)</summary>
        public const int AbilityTooltip = 25;

        /// <summary>LoadingManager: ローディング画面</summary>
        public const int LoadingScreen = 30;

        /// <summary>GameUIManager: ロン待機パネル (DialogueUI / BloodMeter より手前)</summary>
        public const int RonWaitPanel = 50;

        // ---- 強調・演出レイヤー (100 - 9999) ----

        /// <summary>GameUISkillController: マリガン牌交換アニメーションのコンテナ</summary>
        public const int MulliganSwapAnimation = 100;

        /// <summary>BettingUI: ベットパネル (親の PlayerInfoUI より手前に出す)</summary>
        public const int BettingPanel = 101;

        /// <summary>TutorialMaskUI: 誘導先だけを切り抜く集中マスク</summary>
        public const int TutorialMask = 900;

        /// <summary>TutorialArrowUI: 誘導矢印。穴の外にはみ出すので必ずマスクより手前に置く</summary>
        public const int TutorialArrow = 910;

        /// <summary>TilePoolManager のコンテナ / GameUIVisualController の AnimationCanvas。
        /// 牌の移動アニメーションを通常UIより手前で再生するためのレイヤー</summary>
        public const int TileAnimationLayer = 999;

        /// <summary>CommentaryFlowUI: 実況テキスト</summary>
        public const int CommentaryFlow = 999;

        /// <summary>GameUISkillController: マリガン中のプロンプトテキスト (ディマーより手前ではなく独立Canvas)</summary>
        public const int MulliganPromptText = 1000;

        /// <summary>ConfirmationDialogUI: 確認ダイアログ (手牌等より手前)</summary>
        public const int ConfirmationDialog = 9999;

        // ---- 最前面グループ (10000+) ----

        /// <summary>WaitUI: 待ち牌表示を最前面に出す際のレイヤー</summary>
        public const int WaitDisplayFront = 10000;

        /// <summary>YakuSelectionUI: 役選択パネル</summary>
        public const int YakuSelection = 10000;

        /// <summary>RonAnimationUI: ロン演出コンテナ</summary>
        public const int RonAnimation = 30000;

        /// <summary>GameUISkillController: マリガン中の全画面ディマー</summary>
        public const int SkillDimmer = 32000;

        /// <summary>GameUISkillController: マリガン中に選択対象の手牌/山UIをディマーより手前に出すレイヤー</summary>
        public const int MulliganFocusTiles = 21;

        /// <summary>ClickFeedbackManager: クリックエフェクト</summary>
        public const int ClickFeedback = 32000;

        /// <summary>CutinAnimationUI: カットイン演出</summary>
        public const int CutinAnimation = 32000;

        /// <summary>PhaseTransitionUI: フェーズ演出の最前面コンテナ</summary>
        public const int PhaseTransitionTop = 32700;

        /// <summary>AgariSelectionUI: 和了選択 (sortingOrder の最大値)</summary>
        public const int AgariSelectionMax = 32767;

        // ---- SpriteRenderer 用 (Canvas とは別系統) ----

        /// <summary>ClickableCharacter: デバッグ用クリック領域オーバーレイ (SpriteRenderer)</summary>
        public const int DebugOverlaySprite = 999;
    }
}
