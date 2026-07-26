using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.UI;
using KillingMahjong.Managers;

namespace KillingMahjong.Managers
{
    public class TutorialManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameUIManager gameUIManager;
        [SerializeField] private DialogueUI dialogueUI;
        [SerializeField] private TutorialArrowUI arrowUI;

        [SerializeField] private TutorialMaskUI maskUI;

        [Header("Tutorial Data")]
        // 固定の配牌（ID）
        [SerializeField] private List<int> initialHandIds = new List<int>() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
        
        // チュートリアル用の山牌 (HandSelection用)
        [SerializeField] private List<int> initialWallIds = new List<int>() { 30, 31, 32 };
        
        // チュートリアルのステップごとのツモ牌と、捨てるべき牌のシーケンス
        [SerializeField] private List<int> tsumoTilesSequence = new List<int>() { 14, 15 };
        [SerializeField] private List<int> targetDiscardTilesSequence = new List<int>() { 30, 31 };

        public enum TutorialRound
        {
            Round1_BasicRon,
            Round2_Draw,
            Round3_FakeHint,
            Round4_Ability,
            Round5_Final
        }

        private TutorialRound currentRound;
        public TutorialRound CurrentRound => currentRound;
        
        // --- チュートリアル用の判定フラグ ---
        public bool HasClickedAutoMangan { get; set; } = false;
        
        private bool isWaitingForDiscard = false;
        private int currentTurnCount = 0;
        private bool isWaitingForHandSelectionComplete = false;

        public void StartTutorial()
        {
            StartCoroutine(TutorialSequenceRoutine());
        }

        private IEnumerator TutorialSequenceRoutine()
        {
            // 第1局
            currentRound = TutorialRound.Round1_BasicRon;
            yield return StartCoroutine(Round1Routine());

            // 第2局
            currentRound = TutorialRound.Round2_Draw;
            yield return StartCoroutine(Round2Routine());

            // 第3局
            currentRound = TutorialRound.Round3_FakeHint;
            yield return StartCoroutine(Round3Routine());

            // 第4局
            currentRound = TutorialRound.Round4_Ability;
            yield return StartCoroutine(Round4Routine());

            // 第5局
            currentRound = TutorialRound.Round5_Final;
            yield return StartCoroutine(Round5Routine());

            // チュートリアル終了 -> タイトルへ
            yield return StartCoroutine(ShowDialogues(
                "これでチュートリアルは終了よ。",
                "あずにゃん先輩「お疲れ様！次はマルチモードで対戦してみよう！」"
            ));
            SceneManager.LoadScene("タイトルシーン");
        }

        private IEnumerator ShowDialogues(params string[] texts)
        {
            foreach (var text in texts)
            {
                bool nextClicked = false;
                dialogueUI.ShowText(text);
                dialogueUI.ShowNextRoundButton(() => nextClicked = true);
                yield return new WaitUntil(() => nextClicked);
                dialogueUI.HideNextRoundButton();
            }
        }

        // ---------- 各ラウンドのロジック ---------- //

        private IEnumerator Round1Routine()
        {
            List<int> allWallIds = new List<int>();
            for (int i = 1; i <= 34; i++)
            {
                allWallIds.Add(i);
            }
            SetupMockHandAndWall(new List<int>(), allWallIds);
            yield return null;

            yield return StartCoroutine(ShowDialogues(
                "ふふっ、無事に契約完了ね。",
                "これでお前は私のモノ……と言いたいところだけど、まずはその腕前を見せてもらうわ。",
                "デス麻雀のルール、ちゃんと覚えてる？",
                "……まあいいわ。まずは基本的なやり方を教えてあげる。",
                "今回は特別に、自動で強い手牌を作ってあげるわ。"
            ));

            // （現在チュートリアル中はBGMを鳴らさない）
            
            HasClickedAutoMangan = false;
            isWaitingForHandSelectionComplete = true;

            // Autoボタンを強調
            if (gameUIManager != null && gameUIManager.HandUI != null && arrowUI != null)
            {
                arrowUI.ShowAt(gameUIManager.HandUI.AutoManganButtonRect, new Vector2(0, 50f));
            }

            // プレイヤーが「OK」を押すまで待機。
            yield return new WaitUntil(() => !isWaitingForHandSelectionComplete);

            if (arrowUI != null) arrowUI.Hide();

            // 対局（打牌）フェイズに移行してUIレイアウトを更新（手牌を下へ、不要なボタンを消す等）
            gameUIManager.SetCurrentPhaseStatus(KillingMahjong.EngineData.RoundStatus.Discard);
            yield return new WaitForSeconds(0.5f); // レイアウト更新と演出を少し待つ

            yield return StartCoroutine(ShowDialogues("いよいよ対局スタートよ。"));
            
            yield return new WaitForSeconds(0.5f);

            // 対局進行モック
            yield return StartCoroutine(MockBattleRoutine(5, "プレイヤーのロン"));

            yield return StartCoroutine(ShowDialogues(
                "ロン！あなたの上がりね。",
                "上がると、相手からダメージを奪えるのよ。",
                "獲得金も手に入るわ。これが基本ルールよ。"
            ));
        }

        private IEnumerator Round2Routine()
        {
            List<int> allWallIds = new List<int>(initialHandIds);
            allWallIds.AddRange(initialWallIds);
            SetupMockHandAndWall(new List<int>(), allWallIds);
            yield return null;

            yield return StartCoroutine(ShowDialogues(
                "次は流局について教えるわ。",
                "とりあえず『自動』ボタン（オート満貫）を押してね。"
            ));

            HasClickedAutoMangan = false;
            isWaitingForHandSelectionComplete = true;
            
            if (gameUIManager != null && gameUIManager.HandUI != null && arrowUI != null)
            {
                arrowUI.ShowAt(gameUIManager.HandUI.AutoManganButtonRect, new Vector2(0, 50f));
            }
            
            yield return new WaitUntil(() => !isWaitingForHandSelectionComplete);
            
            if (arrowUI != null) arrowUI.Hide();

            gameUIManager.SetCurrentPhaseStatus(KillingMahjong.EngineData.RoundStatus.Discard);
            yield return new WaitForSeconds(1.0f);

            // 5手で強制流局
            yield return StartCoroutine(MockBattleRoutine(5, "流局"));

            yield return StartCoroutine(ShowDialogues(
                "流局よ。誰も上がらずに山牌がなくなると流局になるわ。",
                "流局時にも色々とルールがあるのよ。"
            ));
        }

        private IEnumerator Round3Routine()
        {
            List<int> allWallIds = new List<int>(initialHandIds);
            allWallIds.AddRange(initialWallIds);
            SetupMockHandAndWall(new List<int>(), allWallIds);
            yield return null;

            yield return StartCoroutine(ShowDialogues(
                "次の局よ。今度も流局について教えるわ。",
                "とりあえず『自動』ボタンを押してね。"
            ));

            HasClickedAutoMangan = false;
            isWaitingForHandSelectionComplete = true;
            
            if (gameUIManager != null && gameUIManager.HandUI != null && arrowUI != null)
            {
                arrowUI.ShowAt(gameUIManager.HandUI.AutoManganButtonRect, new Vector2(0, 50f));
            }
            
            yield return new WaitUntil(() => !isWaitingForHandSelectionComplete);
            
            if (arrowUI != null) arrowUI.Hide();

            gameUIManager.SetCurrentPhaseStatus(KillingMahjong.EngineData.RoundStatus.Discard);
            yield return new WaitForSeconds(1.0f);

            yield return StartCoroutine(ShowDialogues(
                "フフフ…私、ちょっとビビっちゃったかも。",
                "このまま延々と流局を続けようかしら。",
                "そうそう、私の待ち牌は一萬よ。一萬だけは絶対に出さないでね。"
            ));

            // 特定の牌をブロックするなどのフラグ設定をここで行う（後で追加）

            // 5手で敵がロン上がり（プレイヤーの放銃）
            yield return StartCoroutine(MockBattleRoutine(5, "敵のロン"));

            yield return StartCoroutine(ShowDialogues(
                "ロン！ふふっ、騙されたわね！",
                "実は流局じゃなくて、最初からこれを狙っていたのよ！",
                "流局のダメージと、単騎待ちのダメージを食らいなさい！"
            ));
        }

        private IEnumerator Round4Routine()
        {
            yield return StartCoroutine(ShowDialogues(
                "次は能力の使用についての説明よ。",
                "ふふっ、私の能力を見せてあげるわ！"
            ));
            
            // 敵が能力を使いまくる演出
            yield return new WaitForSeconds(2.0f);

            yield return StartCoroutine(ShowDialogues(
                "これが能力よ。",
                "能力を使えば、対局を有利に進められるの。",
                "能力の強化については役一覧から確認してね。"
            ));
        }

        private IEnumerator Round5Routine()
        {
            List<int> allWallIds = new List<int>(initialHandIds);
            allWallIds.AddRange(initialWallIds);
            SetupMockHandAndWall(new List<int>(), allWallIds);
            yield return null;

            yield return StartCoroutine(ShowDialogues(
                "さあ、これが最後の対局よ！",
                "今度は自分で手を組んでみてね。自動ボタンを使ってもいいわよ。"
            ));

            HasClickedAutoMangan = false;
            isWaitingForHandSelectionComplete = true;
            yield return new WaitUntil(() => !isWaitingForHandSelectionComplete);

            gameUIManager.SetCurrentPhaseStatus(KillingMahjong.EngineData.RoundStatus.Discard);
            yield return new WaitForSeconds(1.0f);

            // 5手で敵が放銃
            yield return StartCoroutine(MockBattleRoutine(5, "プレイヤーのロン"));

            yield return StartCoroutine(ShowDialogues(
                "きゃあああ！やられたわ……！"
            ));
            // 死亡演出
            yield return new WaitForSeconds(2.0f);
        }

        // 共通のモック対局進行（N手進めてから指定の結末へ）
        private IEnumerator MockBattleRoutine(int targetTurns, string resultType)
        {
            currentTurnCount = 0;
            while (currentTurnCount < targetTurns)
            {
                KillingMahjong.Managers.BoardStateManager.Instance.SetLocalTurn(true);
                isWaitingForDiscard = true;
                
                // プレイヤーの打牌を待つ
                yield return new WaitUntil(() => !isWaitingForDiscard);
                yield return new WaitForSeconds(0.5f);
                
                // 敵の打牌
                if (gameUIManager != null && gameUIManager.EnemyRiverUI != null)
                {
                    int enemyDiscard = 10 + currentTurnCount; // 適当な牌

                    // プレイヤーのロンの場合、最後のターンでアタリ牌（ここでは仮に1=一萬とする）を捨てる
                    if (currentTurnCount == targetTurns - 1 && resultType == "プレイヤーのロン")
                    {
                        enemyDiscard = 1;
                    }

                    gameUIManager.EnemyRiverUI.AddTile(enemyDiscard);
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayDiscardSE();
                }
                yield return new WaitForSeconds(0.8f);

                currentTurnCount++;
            }

            // 結末に応じた処理
            if (resultType == "プレイヤーのロン")
            {
                gameUIManager.SetCurrentPhaseStatus(KillingMahjong.EngineData.RoundStatus.Agari);
                
                if (gameUIManager.AgariSelectionUI != null)
                {
                    bool agariSelected = false;
                    gameUIManager.AgariSelectionUI.Show(
                        () => { agariSelected = true; }, 
                        () => { agariSelected = true; }
                    );
                    
                    // ロンボタンが押されるのを待つ
                    yield return new WaitUntil(() => agariSelected);
                }
                else
                {
                    yield return new WaitForSeconds(2.0f);
                }
                
                if (gameUIManager.RonAnimationUI != null)
                {
                    List<int> dummyHand = new List<int> { 2, 3, 4, 5, 6, 7, 8, 8, 9, 9, 9, 31, 31 };
                    List<string> dummyYaku = new List<string> { "満貫" };
                    bool isPlayerWin = (resultType == "プレイヤーのロン");
                    
                    gameUIManager.RonAnimationUI.PlayRonSequence(
                        dummyHand, 
                        1, 
                        dummyYaku, 
                        "満貫", 
                        "満貫", 
                        8000, 
                        isPlayerWin, 
                        gameUIManager.PlayerInfoUI, 
                        gameUIManager.EnemyInfoUI, 
                        20000, 
                        isPlayerWin ? 28000 : 12000, 
                        20000, 
                        isPlayerWin ? 12000 : 28000, 
                        () => { }
                    );
                }
                yield return new WaitForSeconds(4.0f);
            }
            else if (resultType == "流局")
            {
                gameUIManager.SetCurrentPhaseStatus(KillingMahjong.EngineData.RoundStatus.Draw);
                yield return new WaitForSeconds(2.0f);
            }
            else if (resultType == "敵のロン")
            {
                gameUIManager.SetCurrentPhaseStatus(KillingMahjong.EngineData.RoundStatus.Agari);
                
                // 敵のアガリ演出（ロンアニメーション）
                if (gameUIManager.RonAnimationUI != null)
                {
                    List<int> dummyHand = new List<int> { 2, 3, 4, 5, 6, 7, 8, 8, 9, 9, 9, 31, 31 };
                    List<string> dummyYaku = new List<string> { "満貫" };
                    bool isPlayerWin = (resultType == "プレイヤーのロン");
                    
                    gameUIManager.RonAnimationUI.PlayRonSequence(
                        dummyHand, 
                        1, 
                        dummyYaku, 
                        "満貫", 
                        "満貫", 
                        8000, 
                        isPlayerWin, 
                        gameUIManager.PlayerInfoUI, 
                        gameUIManager.EnemyInfoUI, 
                        20000, 
                        isPlayerWin ? 28000 : 12000, 
                        20000, 
                        isPlayerWin ? 12000 : 28000, 
                        () => { }
                    );
                }
                yield return new WaitForSeconds(4.0f); // 演出の分待機する
            }
        }

        // ---------- 既存のモックセットアップ処理 ---------- //
        private void SetupMockHandAndWall(List<int> hIds, List<int> wIds)
        {
            if (gameUIManager != null && gameUIManager.HandUI != null && gameUIManager.WallUI != null)
            {
                gameUIManager.IsTutorialMode = true;
                gameUIManager.TutorialManager = this;
                
                if (gameUIManager.PhaseTransitionUI != null) gameUIManager.PhaseTransitionUI.gameObject.SetActive(false);
                gameUIManager.ClearAllTiles();
                
                KillingMahjong.Managers.BoardStateManager.Instance.SetLocalTurn(true);
                
                // フェイズとUIレイアウトの更新を連動させる
                if (gameUIManager.PhaseController != null)
                {
                    gameUIManager.PhaseController.UpdatePhaseStatus(KillingMahjong.EngineData.RoundStatus.HandSelection);
                }
                else
                {
                    gameUIManager.SetCurrentPhaseStatus(KillingMahjong.EngineData.RoundStatus.HandSelection);
                }
                
                List<int> handList = new List<int>(hIds);
                List<int> fullWallList = new List<int>(hIds);
                fullWallList.AddRange(wIds);
                
                KillingMahjong.Managers.BoardStateManager.Instance.SetLocalState(fullWallList, handList, new List<int>());
                if (gameUIManager.VisualController != null) gameUIManager.VisualController.RebuildAllTilesFromState();
            }
        }

        // ---------- UIからのコールバック ---------- //

        public bool OnTryMoveTile(int tileId, bool toHand)
        {
            // 第5局以外は手動での牌移動を禁止し、オート満貫ボタンを押させる
            if (currentRound != TutorialRound.Round5_Final)
            {
                StartCoroutine(ShowDialogues("今は『自動』ボタンを押してね。"));
                return false;
            }
            return true;
        }

        public bool OnTryCompleteHandSelection()
        {
            // 第1局〜第3局は、オート満貫ボタンを押さなければ弾く
            if ((currentRound == TutorialRound.Round1_BasicRon || 
                 currentRound == TutorialRound.Round2_Draw || 
                 currentRound == TutorialRound.Round3_FakeHint) && !HasClickedAutoMangan)
            {
                StartCoroutine(ShowDialogues(
                    "満貫手以上じゃないとゲームが開始できないわよ！",
                    "『自動』ボタン（オート満貫）を押して、手牌を作ってね。"
                ));
                return false;
            }

            // ここではまだ待機を解除しない。ConfirmationDialogUIで決定された時に解除する。
            return true;
        }

        public void ConfirmHandSelectionComplete()
        {
            isWaitingForHandSelectionComplete = false;
        }

        public bool OnTryDiscardTile(int tileId)
        {
            if (!isWaitingForDiscard) return false;

            // 第3局で特定の牌（例：一萬 = 1）を禁止する
            if (currentRound == TutorialRound.Round3_FakeHint && tileId == 1)
            {
                StartCoroutine(ShowDialogues("その牌は出しちゃダメって言ったでしょ！"));
                return false;
            }

            isWaitingForDiscard = false;
            return true;
        }

        public void ApplyMockAutoMangan()
        {
            if (HasClickedAutoMangan) return;
            HasClickedAutoMangan = true;
            
            // チュートリアル用の固定の手牌（13枚）
            // 1〜9萬, 1〜3筒, 西
            List<int> manganHandIds = new List<int>() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 30 }; 
            
            // 残りの牌を山牌にセット（1〜34のうち、手牌に使っていないもの）
            List<int> dummyWallIds = new List<int>();
            for(int i = 1; i <= 34; i++)
            {
                if (!manganHandIds.Contains(i))
                {
                    dummyWallIds.Add(i);
                }
            }
            
            SetupMockHandAndWall(manganHandIds, dummyWallIds);
            
            if (gameUIManager != null) gameUIManager.ClearSelection();
            
            if (isWaitingForHandSelectionComplete && arrowUI != null && gameUIManager != null && gameUIManager.HandUI != null)
            {
                // Autoボタンを押したら、次はDecideボタンを押すように誘導する
                arrowUI.ShowAt(gameUIManager.HandUI.DecideButtonRect, new Vector2(0, 50f));
            }
        }
    }
}
