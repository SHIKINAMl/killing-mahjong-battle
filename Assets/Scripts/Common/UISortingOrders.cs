namespace KillingMahjong.Common
{
    /// <summary>
    /// Canvas / SpriteRenderer の sortingOrder を一元管理する定数群。
    /// 数値の大小関係がそのまま画面上の重なり順（大きいほど手前）を表す。
    ///
    /// 【番号体系】全て 0 - 99 に収める。10 の位が用途の区切り。
    ///   0 -  9 : 盤面（山牌・背景パーティクル）
    ///  10 - 19 : 常設UI（ドラ表示・情報パネルの通常状態）
    ///  20 - 29 : 情報パネルの強調・ツールチップ
    ///  30 - 39 : システムオーバーレイ（ローディング）
    ///  40 - 49 : 空き
    ///  50 - 59 : ロン待機・結果パネル・軽い演出
    ///  60 - 69 : チュートリアル誘導
    ///  70 - 79 : 牌アニメーション・実況
    ///  80 - 89 : ダイアログ・最前面パネル
    ///  90 - 99 : 全画面演出・システム最前面
    ///
    /// シーン上の Canvas もこの体系に従うこと。新しい値を足すときは
    /// 該当する 10 の位の空き番号を使い、既存の値はずらさない。
    ///
    /// なお、このプロジェクトの Sorting Layer は Default のみ（ProjectSettings/TagManager.asset）。
    /// 存在しないレイヤー名を sortingLayerName に代入しても無視されるだけなので、
    /// 重なり順の調整は必ず sortingOrder で行う。
    /// </summary>
    public static class UISortingOrders
    {
        // ---- 0 - 9 盤面 ----

        /// <summary>
        /// EnemyInfoUI: 敵HP（点滴の血袋）の Canvas。**盤面の牌より必ず奥**。
        ///
        /// 血袋から下へ伸びるチューブが敵の牌に重なるので、牌の裏へ回す。
        /// 盤面の Canvas は実測でこう並んでいる（2026-08-24・全て ScreenSpaceOverlay）:
        ///
        ///   **敵HP -1** ／ RiverCanvs・EnemyRiverCanvas・WallCanvas・EnemyWallUI 0 ／
        ///   HandCanvas 1 ／ EnemyHandCanvas 3
        ///
        /// **0 ではなく -1 にしてある。** 0 だと山牌・河と同値になり、
        /// 前後がシーン内の並び順という**書いていない規則**で決まってしまう。
        ///
        /// 卓や背景に沈む心配は無い。`マージャン卓`(-1) と `背景Canvas`(-3) は
        /// **ScreenSpaceCamera** で、Overlay の Canvas は sortingOrder に関係なく
        /// 必ずその手前に出る（同じ -1 でも競合しない）。
        /// 同値の Overlay は `役Canvas2` だけで、中身は overrideSorting で 55 に上がっている。
        ///
        /// 2026-08-20 の R-1 では `EnemyHandCanvas` を 0→3 に上げたが、
        /// **敵の山牌・河（0）は敵HP(1)より奥のまま**だったので直り切っていなかった。
        /// 8/16 のスクショで血袋のチューブが横切っていた2段の牌は、手牌ではなく**山牌**。
        /// </summary>
        public const int EnemyHpMeter = -1;

        /// <summary>WallUI: 山牌の通常表示</summary>
        public const int WallBase = 1;

        /// <summary>WallUI: 打牌フェーズ中の山牌（通常表示より手前）</summary>
        public const int WallDiscardPhase = 2;

        /// <summary>TitleEffectCreator: タイトル画面のパーティクル（キャラ・ボタンより奥）</summary>
        public const int TitleParticle = 5;

        // ---- 10 - 19 常設UI ----

        /// <summary>DoraDisplayUI: ドラ表示牌 (WorldSpace Canvas)</summary>
        public const int DoraTile = 10;

        /// <summary>BetPotUI: 場に出ている血（賭け金プール）の表示。
        /// 盤面より手前・情報パネルより奥。チュートリアルのマスク(60)より奥なので誘導中は一緒に暗くなる。
        /// WaitDeductionUI（相手の待ち候補）も同じ段。どちらも盤面の情報表示で、
        /// フェーズ演出の黒帯より奥に居るべきもの</summary>
        public const int BetPot = 14;

        /// <summary>AbilityUI / PlayerInfoUI: 強調していない通常時のパネル</summary>
        public const int InfoPanelNormal = 15;

        /// <summary>WallUI: 演出中に山牌を通常UIより手前へ出すときの値</summary>
        public const int WallFront = 16;

        /// <summary>HandUI: 「手牌を見る」ボタンの通常時（覗いていない間）。
        /// 山牌(16)より手前だが、**フェーズ演出(19)より奥**に置くこと。
        /// 覗いている間だけ HandPeekTiles+1 まで上げる（そうしないと中央へ寄せた手牌に隠れる）。
        /// 前面に置きっぱなしにすると、賭け金確定後の演出にボタンが被る</summary>
        public const int HandPeekButtonIdle = 17;

        /// <summary>BettingUI: ベット時の背景ディマー（BettingPanel より奥）</summary>
        public const int BettingDimmer = 19;

        /// <summary>PhaseTransitionUI: フェーズ演出の基本レイヤー</summary>
        public const int PhaseTransitionBase = 19;

        /// <summary>
        /// AbilityUI: フェーズ演出中の退避先。**フェーズ演出(19)より必ず下**であること。
        ///
        /// 能力パネルは通常 20、説明ツールチップは 25 なので、
        /// 「決定！」などの帯(19)が出ている間は構造上かならず帯の上に乗ってしまう
        /// （2026-08-19 にプランナーから指摘。実機の動画で確認済み）。
        /// GameUIManager.SetIsTransitioning から AbilityUI.SetSuppressedForTransition 経由で当てる。
        /// </summary>
        public const int AbilityDuringTransition = 18;

        // ---- 20 - 29 情報パネル強調 ----

        /// <summary>AbilityUI ルート / PlayerInfoUI 強調表示時の共通レイヤー</summary>
        public const int InfoPanelHighlight = 20;

        /// <summary>BettingUI: ベットパネル（敵のダイアログより手前に出す）。
        /// InfoPanelHighlight と同値なのは意図的で、どちらも「情報パネルの強調段」に属する</summary>
        public const int BettingPanel = 20;

        /// <summary>GameUISkillController: マリガン中に選択対象の手牌/山UIをディマーより手前に出すレイヤー</summary>
        public const int MulliganFocusTiles = 21;

        /// <summary>AbilityUI: ツールチップ (InfoPanelHighlight より手前)</summary>
        public const int AbilityTooltip = 25;

        // ---- 30 - 39 システムオーバーレイ ----

        /// <summary>LoadingManager: ローディング画面</summary>
        public const int LoadingScreen = 30;

        // ---- 50 - 59 ロン待機・結果パネル・軽い演出 ----

        /// <summary>GameUIManager: ロン待機パネル (DialogueUI / BloodMeter より手前)</summary>
        public const int RonWaitPanel = 50;

        /// <summary>シーン上の 役ListPanel / 勝敗Canvas。
        /// コードからは設定しないが、他の値との関係を把握するためここに記録する</summary>
        public const int ResultPanel = 55;

        /// <summary>MulliganSwapAnimator: マリガン牌交換アニメーションのコンテナ</summary>
        public const int MulliganSwapAnimation = 55;

        // ---- 60 - 69 チュートリアル誘導 ----

        /// <summary>TutorialMaskUI: 誘導先だけを切り抜く集中マスク</summary>
        public const int TutorialMask = 60;

        /// <summary>TutorialArrowUI: 誘導矢印。穴の外にはみ出すので必ずマスクより手前に置く</summary>
        public const int TutorialArrow = 65;

        // ---- 70 - 79 牌アニメーション・実況 ----

        /// <summary>TilePoolManager のコンテナ / GameUIVisualController の AnimationCanvas。
        /// 牌の移動アニメーションを通常UIより手前で再生するためのレイヤー</summary>
        public const int TileAnimationLayer = 70;

        /// <summary>CommentaryFlowUI: 実況テキスト（飛んでいる牌に隠れないよう牌アニメより手前）</summary>
        public const int CommentaryFlow = 74;

        /// <summary>GameUISkillController: マリガン中のプロンプトテキスト (独立Canvas)</summary>
        public const int MulliganPromptText = 78;

        // ---- 80 - 89 ダイアログ・最前面パネル ----

        /// <summary>
        /// RoomScreenUI: タイトルからゲーム開始後に表示する部屋の待機画面。
        /// 対戦相手の探し方を選ぶモーダル(81)より一段奥に置く。
        /// </summary>
        public const int TitleRoomScreen = 80;

        /// <summary>ConfirmationDialogUI: 確認ダイアログ (手牌等より手前)</summary>
        public const int ConfirmationDialog = 80;

        /// <summary>
        /// TitleMultiMenuUI: タイトルの「マルチ」で出す対戦相手の探し方メニュー。
        ///
        /// **専用の Canvas と GraphicRaycaster を持たせること。**
        /// 2026-08-07 に、既存の Canvas を探して間借りする作りで「見えているのに押せない」
        /// 不具合を出した。原因は取り付け先が `UICursorCanvas`（自前マウスカーソル・
        /// sortingOrder 99）になっていたこと。**あの Canvas は GraphicRaycaster が
        /// 意図的に disabled** で（カーソルの絵がクリックを食わないように）、
        /// 配下の UI はすべて当たり判定を失う。描画は最前面なので見た目だけは正しく、
        /// 原因が見えにくい。
        ///
        /// タイトルシーンには 80 番台の住人が他に居ないのでここを使う。
        /// カーソル(99)より奥なのは意図どおりで、カーソルはメニューの上に出てよい。
        /// </summary>
        public const int TitleMenuOverlay = 81;

        /// <summary>WaitUI: 待ち牌表示を最前面に出す際のレイヤー</summary>
        public const int WaitDisplayFront = 84;

        /// <summary>HandUI: 手牌を覗いている間の暗幕</summary>
        public const int HandPeekDimmer = 85;

        /// <summary>HandUI: 覗いている間の手牌そのもの。
        /// 待ち牌表示(84)より手前に出さないと、覗いた手牌が隠れてしまう</summary>
        public const int HandPeekTiles = 86;

        /// <summary>YakuSelectionUI: 役選択パネル（モーダルなので待ち牌表示より手前）</summary>
        public const int YakuSelection = 88;

        /// <summary>HpDamageGlitch: 体力が減った瞬間のノイズ。
        /// 元は手番の画面ふち（TurnVignette）が使っていた空き番。
        ///
        /// **ロン演出(90)や瀕死ビネット(91)より奥にいる。**
        /// ノイズが撮っているのは合成後の画面なので、それらも帯の中には写っている。
        /// 手前に出すと決着の演出そのものを覆ってしまうため、あえて奥に置いた</summary>
        public const int DamageGlitch = 89;

        // ---- 90 - 99 全画面演出・システム最前面 ----

        /// <summary>RonAnimationUI: ロン演出コンテナ</summary>
        public const int RonAnimation = 90;

        /// <summary>HeartbeatEffect: 瀕死時の赤/黒ビネット。
        /// シーン上の HeartbeatEffectPanel の Canvas に直接設定する（コードからは触らない）</summary>
        public const int HeartbeatVignette = 91;

        /// <summary>MatchMomentumUI: 決着時の戦況グラフ。
        /// シーン上の MatchMomentumPanel の Canvas に直接設定する（コードからは触らない）</summary>
        public const int MatchMomentum = 92;

        /// <summary>GameUISkillController: マリガン中の全画面ディマー（現在はシーン側で設定。参照用）</summary>
        public const int SkillDimmer = 93;

        /// <summary>ClickFeedbackManager: クリックエフェクト</summary>
        public const int ClickFeedback = 94;

        /// <summary>CutinAnimationUI: カットイン演出</summary>
        public const int CutinAnimation = 95;

        /// <summary>PhaseTransitionUI: フェーズ演出の最前面コンテナ</summary>
        public const int PhaseTransitionTop = 96;

        /// <summary>AgariSelectionUI: 和了選択</summary>
        public const int AgariSelection = 97;

        /// <summary>BlinkEffectUI: オープニングのまぶた。開ききるまで画面全体を覆う</summary>
        public const int OpeningEyelid = 98;

        /// <summary>ScreenFlash: 場面の切り替わりを示す一瞬の全画面フラッシュ。
        ///
        /// フェーズ演出(96)や和了選択(97)の**手前**に出す必要がある。
        /// それらの直前に光らせるためのもので、奥に置くと肝心の演出に隠れて見えない。
        /// OpeningEyelid と同値だが、まぶたはオープニング専用で対局シーンには存在しないため衝突しない。
        /// カーソル(99)より奥なのは意図的で、光っている間もカーソルは見えていてよい。</summary>
        public const int ScreenFlash = 98;

        /// <summary>CustomCusor: 自前マウスカーソル。常に全UIより手前</summary>
        public const int MouseCursor = 99;

        // ---- SpriteRenderer 用 (Canvas とは別系統) ----
        // ScreenSpace-Overlay の Canvas とは描画パスが違うため、上の値とは比較できない。
        // 比較対象は PlayerInfoUI が触る SpriteRenderer (最大 InfoPanelHighlight) だけ。

        /// <summary>ClickableCharacter: デバッグ用クリック領域オーバーレイ (SpriteRenderer)</summary>
        public const int DebugOverlaySprite = 99;
    }
}
