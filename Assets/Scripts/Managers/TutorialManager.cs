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
        [SerializeField] private List<int> targetDiscardTilesSequence = new List<int>() { 1, 2 };

        // HandSelection用ステート
        private bool isWaitingForHandSelectionMove = false;
        private bool isWaitingForHandSelectionComplete = false;
        private int targetMoveTileId = -1;
        private bool targetMoveToHand = false;

        private bool isWaitingForDiscard = false;
        private int currentStepIndex = 0;
        private int targetDiscardTileId = -1;

        public void StartTutorial()
        {
            StartCoroutine(TutorialRoutine());
        }

        private IEnumerator TutorialRoutine()
        {
            // --- ステップ1: セリフと配牌（HandSelectionフェーズ） ---
            yield return StartCoroutine(PlayDialogueRoutine("チュートリアル_開始"));

            // HandSelectionモードとして手牌と山牌をセットアップ
            SetupMockHandAndWall();

            // 不要な牌を山に戻す誘導
            dialogueUI.ShowText("まずは手牌から不要な牌を山に戻してね。");
            targetMoveTileId = initialHandIds[0]; // 最初の牌（例: 1）
            targetMoveToHand = false;
            isWaitingForHandSelectionMove = true;
            ShowHighlightOnTile(targetMoveTileId, false);

            yield return new WaitUntil(() => !isWaitingForHandSelectionMove);
            arrowUI.Hide();
            if (maskUI != null) maskUI.Hide();

            // 山からドラ（特定の牌）を取る誘導
            dialogueUI.ShowText("次は山からドラ（や字牌）を手牌に入れてね。");
            targetMoveTileId = initialWallIds[0]; // 山の最初の牌（例: 30）
            targetMoveToHand = true;
            isWaitingForHandSelectionMove = true;
            ShowHighlightOnTile(targetMoveTileId, true);

            yield return new WaitUntil(() => !isWaitingForHandSelectionMove);
            arrowUI.Hide();
            if (maskUI != null) maskUI.Hide();

            // OKボタンで決定させる
            dialogueUI.ShowText("これで手配が完成したわね。「OK」ボタンを押して決定してね。");
            isWaitingForHandSelectionComplete = true;

            // 決定されるまで待つ（GameUIHandSelectionController側からOnTryCompleteHandSelectionが呼ばれる）
            yield return new WaitUntil(() => !isWaitingForHandSelectionComplete);

            // 決定後、少し待ってから打牌（Discard）フェーズへ
            yield return new WaitForSeconds(1.0f);
            dialogueUI.ShowText("いよいよ対局スタートよ。");
            gameUIManager.SetCurrentPhaseStatus(KillingMahjong.EngineData.RoundStatus.Discard);
            yield return new WaitForSeconds(1.0f);

            currentStepIndex = 0;

            // --- ステップ2: ターン進行のループ ---
            while (currentStepIndex < tsumoTilesSequence.Count)
            {
                int currentTsumo = tsumoTilesSequence[currentStepIndex];
                int currentTargetDiscard = targetDiscardTilesSequence[currentStepIndex];

                // ツモ牌を手牌に追加
                AddTileToPlayerHand(currentTsumo);

                yield return new WaitForSeconds(0.5f);

                // もし1巡目なら打牌指示のセリフを入れる、2巡目以降なら別のセリフなど
                if (currentStepIndex == 0)
                {
                    yield return StartCoroutine(PlayDialogueRoutine("チュートリアル_打牌指示"));
                }

                // 矢印とマスクUIを対象の牌に表示する
                ShowHighlightOnTile(currentTargetDiscard);
                isWaitingForDiscard = true;
                this.targetDiscardTileId = currentTargetDiscard; // 打牌検証用

                // プレイヤーが指定の牌を捨てるまで待機
                yield return new WaitUntil(() => !isWaitingForDiscard);

                // マスクと矢印を隠す
                arrowUI.Hide();
                if (maskUI != null) maskUI.Hide();

                yield return new WaitForSeconds(0.5f); // プレイヤーの打牌演出を少し待つ

                // --- 相手のターン（敵のアクション） ---
                if (currentStepIndex == 0)
                {
                    dialogueUI.ShowText("よくできたわね。");
                }
                else
                {
                    dialogueUI.ShowText("その調子よ。");
                }
                yield return new WaitForSeconds(1.0f);
                dialogueUI.HideText();

                // 敵が打牌する演出（Riverに牌を追加して音を鳴らすなど）
                yield return StartCoroutine(EnemyActionRoutine());

                currentStepIndex++;
            }

            // --- ステップ3: チュートリアル完了とタイトルへ ---
            yield return StartCoroutine(PlayDialogueRoutine("チュートリアル_完了"));

            // タイトルシーンへ遷移
            SceneManager.LoadScene("タイトルシーン");
        }

        private IEnumerator EnemyActionRoutine()
        {
            // 敵が打牌したように見せる
            if (gameUIManager != null && gameUIManager.EnemyRiverUI != null)
            {
                // 適当な牌（例：ピンズの1 = 10）を敵の河に捨てる
                gameUIManager.EnemyRiverUI.AddTile(10);
                // 打牌音を鳴らす（仮にPlayerの打牌と同じ扱いにしてピッチを上げる）
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayDiscardSE();
                }
            }
            yield return new WaitForSeconds(0.8f);
        }

        private IEnumerator PlayDialogueRoutine(string conditionKey)
        {
            var dialogueEntry = DialogueManager.Instance.GetDialogueEntry(conditionKey);
            if (dialogueEntry != null)
            {
                // セリフ1を表示
                if (!string.IsNullOrEmpty(dialogueEntry.Dialogue1))
                {
                    bool nextClicked = false;
                    dialogueUI.ShowText(dialogueEntry.Dialogue1);
                    dialogueUI.ShowNextRoundButton(() => nextClicked = true);
                    yield return new WaitUntil(() => nextClicked);
                    dialogueUI.HideNextRoundButton();
                }
                
                // セリフ2を表示
                if (!string.IsNullOrEmpty(dialogueEntry.Dialogue2))
                {
                    bool nextClicked = false;
                    dialogueUI.ShowText(dialogueEntry.Dialogue2);
                    dialogueUI.ShowNextRoundButton(() => nextClicked = true);
                    yield return new WaitUntil(() => nextClicked);
                    dialogueUI.HideNextRoundButton();
                }
            }
            else
            {
                // CSVに見つからなかった場合のフォールバック
                Debug.LogWarning($"[TutorialManager] Condition '{conditionKey}' がCSVに見つかりませんでした。");
                yield return new WaitForSeconds(1.0f);
            }
        }

        private void SetupMockHandAndWall()
        {
            if (gameUIManager != null && gameUIManager.HandUI != null && gameUIManager.WallUI != null)
            {
                gameUIManager.IsTutorialMode = true;
                gameUIManager.TutorialManager = this;
                
                if (gameUIManager.PhaseTransitionUI != null)
                {
                    gameUIManager.PhaseTransitionUI.gameObject.SetActive(false);
                }
                
                gameUIManager.ClearAllTiles();
                
                KillingMahjong.Managers.BoardStateManager.Instance.SetLocalTurn(true);
                gameUIManager.SetCurrentPhaseStatus(KillingMahjong.EngineData.RoundStatus.HandSelection);
                
                // 配牌 (BoardStateに直接登録してからUIに反映)
                List<int> handList = new List<int>(initialHandIds);
                List<int> wallList = new List<int>(initialWallIds);
                KillingMahjong.Managers.BoardStateManager.Instance.SetLocalState(wallList, handList, new List<int>());

                // UIへの追加 (Hand)
                foreach (int id in handList)
                {
                    AddTileToPlayerHand(id);
                }

                // UIへの追加 (Wall)
                foreach (int id in wallList)
                {
                    AddTileToWall(id);
                }
                
                if (gameUIManager.PhaseController != null)
                {
                    gameUIManager.PhaseController.HandlePhaseVisibility(KillingMahjong.EngineData.RoundStatus.HandSelection);
                }
            }
        }

        private void AddTileToPlayerHand(int id)
        {
            if (gameUIManager.TilePrefab == null) return;
            GameObject obj = Instantiate(gameUIManager.TilePrefab);
            RectTransform rt = obj.GetComponent<RectTransform>();
            
            var visual = obj.GetComponent<TileVisual>();
            if (visual != null && gameUIManager.TileResourceManager != null)
            {
                visual.SetTile(id, gameUIManager.TileResourceManager.GetTileSprite(id));
            }
            
            gameUIManager.HandUI.AddTileToHand(rt, id);
        }

        private void AddTileToWall(int id)
        {
            if (gameUIManager.TilePrefab == null) return;
            GameObject obj = Instantiate(gameUIManager.TilePrefab);
            RectTransform rt = obj.GetComponent<RectTransform>();
            
            var visual = obj.GetComponent<TileVisual>();
            if (visual != null && gameUIManager.TileResourceManager != null)
            {
                visual.SetTile(id, gameUIManager.TileResourceManager.GetTileSprite(id));
            }
            
            gameUIManager.WallUI.ReturnTileToWall(rt, id);

        }

        private void ShowHighlightOnTile(int tileId, bool isWallTile = false)
        {
            if (gameUIManager != null)
            {
                RectTransform targetRt = null;
                if (isWallTile && gameUIManager.WallUI != null)
                {
                    foreach (var rt in gameUIManager.WallUI.GetWallSlots())
                    {
                        var interaction = rt.GetComponent<KillingMahjong.UI.TileInteraction>();
                        if (interaction != null && interaction.TileId == tileId)
                        {
                            targetRt = rt;
                            break;
                        }
                    }
                }
                else if (!isWallTile && gameUIManager.HandUI != null)
                {
                    targetRt = gameUIManager.HandUI.GetTileSlotRectTransform(tileId);
                }

                if (targetRt != null)
                {
                    if (arrowUI != null) arrowUI.ShowAt(targetRt);
                    if (maskUI != null) maskUI.Show(targetRt);
                }
            }
        }

        // 外部（UIのボタンなど）から呼ばれる打牌時のフック
        public bool OnTryDiscardTile(int tileId)
        {
            if (!isWaitingForDiscard) return false; // チュートリアルで打牌を待っていない時は無視（または禁止）

            if (tileId == targetDiscardTileId)
            {
                // 正解の牌を捨てた
                isWaitingForDiscard = false;
                return true; // 実際の打牌処理を許可する
            }
            else
            {
                // 不正解の牌
                dialogueUI.ShowText("そっちじゃないわ。指定した牌を捨ててね。");
                return false; // 打牌処理をキャンセル
            }
        }

        // HandSelection時のフック
        public bool OnTryMoveTile(int tileId, bool toHand)
        {
            if (!isWaitingForHandSelectionMove) return true; // 何でも許可する状態（ただしシナリオ中は常に待機状態）
            
            if (tileId == targetMoveTileId && toHand == targetMoveToHand)
            {
                isWaitingForHandSelectionMove = false; // 正解
                return true;
            }
            else
            {
                dialogueUI.ShowText("そっちじゃないわ。指定した牌を選んでね。");
                return false;
            }
        }

        public bool OnTryCompleteHandSelection()
        {
            if (isWaitingForHandSelectionMove)
            {
                dialogueUI.ShowText("まずは指定された牌を移動させてね。");
                return false; // まだ牌の移動が終わっていない
            }

            if (!isWaitingForHandSelectionComplete)
            {
                dialogueUI.ShowText("まだ手牌が完成していないわ。");
                return false;
            }
            
            isWaitingForHandSelectionComplete = false; // 正解
            return true;
        }
    }
}
