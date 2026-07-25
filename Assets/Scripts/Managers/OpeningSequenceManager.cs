using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using KillingMahjong.UI;
using KillingMahjong.Managers;

namespace KillingMahjong.Managers
{
    public class OpeningSequenceManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BlinkEffectUI blinkEffect;
        [SerializeField] private GameObject paperObject; // 卓中央の紙UI
        [SerializeField] private Button paperButton;
        
        [Header("Large Paper UI")]
        [SerializeField] private GameObject largePaperUI; // 大きな紙の画像UI
        [SerializeField] private Button largePaperNextButton; // 次へボタン
        
        [Header("Characters & Dialogues")]
        [SerializeField] private GameObject enemyCharacterObj; // 女の子のキャラクターオブジェクト
        [SerializeField] private DialogueUI dialogueUI;
        [SerializeField] private TutorialManager tutorialManager;

        private void Awake()
        {
            // 初期化
            paperObject.SetActive(false);
            enemyCharacterObj.SetActive(false); // 最初は女の子がいない
            if (largePaperUI != null) largePaperUI.SetActive(false); // 大きな紙も最初は隠す

            if (dialogueUI != null) dialogueUI.gameObject.SetActive(false); // 吹き出しを最初は消す

            // 不要なロード表示やマッチメイキングUIを強制オフ
            GameUIManager uiManager = UnityEngine.Object.FindFirstObjectByType<GameUIManager>();
            if (uiManager != null)
            {
                uiManager.IsTutorialMode = true; // チュートリアルモードであることを事前にセットしておく
                if (uiManager.MatchmakingUI != null) uiManager.MatchmakingUI.gameObject.SetActive(false);
                
                // GameInitDebug等があればオフにする
                var debugInit = UnityEngine.Object.FindFirstObjectByType<GameInitDebug>();
                if (debugInit != null) debugInit.gameObject.SetActive(false);
            }

            // 右下のロードUI（LoadingManager）が出ないようにする
            if (KillingMahjong.UI.LoadingManager.Instance != null)
            {
                KillingMahjong.UI.LoadingManager.Instance.ForceHide();
            }

            paperButton.onClick.AddListener(OnPaperClicked);

            // 演出開始
            StartCoroutine(SequenceRoutine());
        }

        private IEnumerator SequenceRoutine()
        {
            // 少し待ってから目を覚ます演出
            yield return new WaitForSeconds(1.0f);

            bool isBlinkDone = false;
            if (blinkEffect != null)
            {
                blinkEffect.PlayWakeUpEffect(() => isBlinkDone = true);
                yield return new WaitUntil(() => isBlinkDone);
            }

            yield return new WaitForSeconds(0.5f);

            // いつもの雀卓。卓中央に紙が出現
            paperObject.SetActive(true);
        }

        private void OnPaperClicked()
        {
            paperObject.SetActive(false); // 卓上の小さな紙を隠す

            if (largePaperUI != null && largePaperNextButton != null)
            {
                // もしCanvasGroupがあればアルファを1にしておく
                var cg = largePaperUI.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;

                // ボタンはスライド完了まで押せないようにする
                largePaperNextButton.interactable = false;
                largePaperNextButton.onClick.RemoveAllListeners();
                largePaperNextButton.onClick.AddListener(() => {
                    StartCoroutine(SlideOutLargePaperRoutine());
                });

                StartCoroutine(SlideInLargePaperRoutine());
            }
            else
            {
                // UIが設定されていなければすぐに次へ
                OnDialogClosed();
            }
        }

        private IEnumerator SlideInLargePaperRoutine()
        {
            if (KillingMahjong.Managers.AudioManager.Instance != null)
            {
                KillingMahjong.Managers.AudioManager.Instance.PlayPaperSlideSE();
            }
            largePaperUI.SetActive(true);
            RectTransform rt = largePaperUI.GetComponent<RectTransform>();
            
            // 中央表示時の位置をゴールとする（デフォルト位置）
            // UI作成時に中央に配置されている想定。念のため0fをターゲットにする。
            float targetY = 0f; 
            float startY = -2500f; // 画面外下部
            
            Vector2 pos = rt.anchoredPosition;
            pos.y = startY;
            rt.anchoredPosition = pos;

            float duration = 0.6f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                t = t * t * (3f - 2f * t); // SmoothStep
                pos.y = Mathf.Lerp(startY, targetY, t);
                rt.anchoredPosition = pos;
                yield return null;
            }
            pos.y = targetY;
            rt.anchoredPosition = pos;

            largePaperNextButton.interactable = true;
        }

        private IEnumerator SlideOutLargePaperRoutine()
        {
            if (KillingMahjong.Managers.AudioManager.Instance != null)
            {
                KillingMahjong.Managers.AudioManager.Instance.PlayPaperSlideSE();
            }
            largePaperNextButton.interactable = false; // 連打防止

            RectTransform rt = largePaperUI.GetComponent<RectTransform>();
            float startY = rt.anchoredPosition.y;
            float targetY = -2500f; // 画面外下部へ

            float duration = 0.6f; // スライドにかける時間
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                t = t * t * (3f - 2f * t); // SmoothStep
                Vector2 pos = rt.anchoredPosition;
                pos.y = Mathf.Lerp(startY, targetY, t);
                rt.anchoredPosition = pos;
                yield return null;
            }
            
            Vector2 finalPos = rt.anchoredPosition;
            finalPos.y = targetY;
            rt.anchoredPosition = finalPos;

            largePaperUI.SetActive(false);

            OnDialogClosed();
        }

        private void OnDialogClosed()
        {
            StartCoroutine(ShowEnemyRoutine());
        }

        private IEnumerator ShowEnemyRoutine()
        {
            // 女の子出現
            enemyCharacterObj.SetActive(true);

            // うっすらとフェードインする演出
            Image enemyImg = enemyCharacterObj.GetComponent<Image>();
            if (enemyImg != null)
            {
                Color c = enemyImg.color;
                c.a = 0f;
                enemyImg.color = c;
                
                float duration = 1.5f; // フェードインにかける時間
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    c.a = Mathf.Lerp(0f, 1f, elapsed / duration);
                    enemyImg.color = c;
                    yield return null;
                }
                c.a = 1f;
                enemyImg.color = c;
            }

            yield return new WaitForSeconds(0.5f);

            // 吹き出し（DialogueUI）を表示して会話を開始
            if (dialogueUI != null) dialogueUI.gameObject.SetActive(true);
            StartConversation();
        }

        private void StartConversation()
        {
            // CSVから特定の状態のセリフを引っ張ってくる、あるいは直接流し込む。
            // 今回はチュートリアル専用CSV（例：Condition="チュートリアル開始"）から取得する想定。
            // ここでは簡易的に、配列でセリフを流し、終わったらチュートリアルへ。
            
            StartCoroutine(ConversationRoutine());
        }

        private IEnumerator ConversationRoutine()
        {
            // 会話はTutorialManager側で行うため、ここではすぐに遷移する
            yield return null;

            if (tutorialManager != null)
            {
                tutorialManager.StartTutorial();
            }
        }
    }
}
