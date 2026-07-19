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
        
        // チュートリアルのステップごとのツモ牌と、捨てるべき牌のシーケンス
        // 簡単なループを作成するため、リストにします。
        [SerializeField] private List<int> tsumoTilesSequence = new List<int>() { 14, 15 };
        [SerializeField] private List<int> targetDiscardTilesSequence = new List<int>() { 1, 2 };

        private bool isWaitingForDiscard = false;
        private int currentStepIndex = 0;
        private int targetDiscardTileId = -1;

        public void StartTutorial()
        {
            StartCoroutine(TutorialRoutine());
        }

        private IEnumerator TutorialRoutine()
        {
            // --- ステップ1: セリフと配牌 ---
            yield return StartCoroutine(PlayDialogueRoutine("チュートリアル_開始"));

            // GameUIManagerを経由するか、直接HandUIに牌を追加する
            SetupMockHand();

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

        private void SetupMockHand()
        {
            if (gameUIManager != null && gameUIManager.HandUI != null)
            {
                gameUIManager.IsTutorialMode = true;
                gameUIManager.TutorialManager = this;
                
                // チュートリアル中はフェーズ遷移演出は不要なのでOFFにする
                if (gameUIManager.PhaseTransitionUI != null)
                {
                    gameUIManager.PhaseTransitionUI.gameObject.SetActive(false);
                }
                
                gameUIManager.ClearAllTiles();
                
                // 配牌
                foreach (int id in initialHandIds)
                {
                    AddTileToPlayerHand(id);
                }

                // ツモ牌
                AddTileToPlayerHand(tsumoTilesSequence[0]);
                
                // チュートリアル中はローカルで完結させるため、GameUIManagerのフェーズとターンを手動で設定
                KillingMahjong.Managers.BoardStateManager.Instance.SetLocalTurn(true);
                gameUIManager.SetCurrentPhaseStatus(KillingMahjong.EngineData.RoundStatus.Discard);
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

        private void ShowHighlightOnTile(int tileId)
        {
            if (gameUIManager != null && gameUIManager.HandUI != null)
            {
                // UIから対象の牌のRectTransformを探す
                RectTransform targetRt = gameUIManager.HandUI.GetTileSlotRectTransform(tileId);
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
    }
}
