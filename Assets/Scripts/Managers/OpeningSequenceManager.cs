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

            if (dialogueUI != null)
            {
                dialogueUI.gameObject.SetActive(false); // 吹き出しを最初は消す

                // チュートリアルの文字は読みやすさを優先して固定する。
                // DialoguePanel 自体を揺らすと、子の TMP 文字まで毎フレーム 3px / 5px
                // 動いて録画でも読みにくくなるため、他画面の浮遊演出は変えずにここだけ止める。
                Transform dialoguePanel = dialogueUI.transform.Find("DialoguePanel");
                if (dialoguePanel != null)
                {
                    var floatingAnimator = dialoguePanel.GetComponent<FloatingAnimator>();
                    if (floatingAnimator != null) floatingAnimator.enabled = false;
                }
            }

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

        private void Start()
        {
            // EnemyInfoUI の Awake 後に、OpeningScene にだけ登録したチュートリアル用
            // CharacterData へ切り替える。共有アセットは変更しない。
            GameUIManager uiManager = UnityEngine.Object.FindFirstObjectByType<GameUIManager>();
            if (uiManager != null && uiManager.EnemyInfoUI != null)
            {
                uiManager.EnemyInfoUI.CycleEnemy();
            }
        }

        private IEnumerator SequenceRoutine()
        {
            // **目を開けてから紙を取るまでは無音にする（2026-09-23 のユーザー指示）。**
            // 一度は「時計じかけの謎」を流したが、要らないと言われて戻した。
            // 曲は契約書のあと、台本の1行目で TutorialAudioDirector が流し始める。
            //
            // **旗は、AudioManager が居なくても立てる（2026-10-10）。**
            // 前は「居るときだけ」立てていた。ビルドではこちらの Awake が先に走って、まだ居ないことがあり、
            // 旗が立たずにタイトル曲が鳴っていた（Awake の順番は、エディタとビルドで同じとは限らない）。
            // AudioManager.Start はこのあとに走り、旗を見て、タイトル曲を流すのをやめる
            KillingMahjong.Managers.AudioManager.StartupBgmSuppressed = true;
            if (KillingMahjong.Managers.AudioManager.Instance != null)
            {
                KillingMahjong.Managers.AudioManager.Instance.StopBGM();
            }
            else
            {
                Debug.Log("[OpeningSequence] AudioManager がまだ居なかった（旗だけ先に立てた）");
            }

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
                // 契約書が迫り上がる厚みを足す（2026-09-11）。
                // **ここまでは無音。** 契約書の場面は、この効果音だけで静かに進める。
                KillingMahjong.Managers.AudioManager.Instance.PlayStinger("se_paper");
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

        /// <summary>
        /// 契約書を閉じたあと。**ここでは立ち絵を出さない。**
        ///
        /// 順序は「契約書を閉じる → 女の子がフェードイン → セリフ（と同時に曲）」
        /// （2026-09-27 の指示。2026-09-12 のフロー図とは逆になっている）。
        /// フェードインは <see cref="TutorialManager.CharacterRevealRequested"/> 経由で
        /// TutorialManager が台詞を出す前に呼び戻してくる。
        /// </summary>
        private IEnumerator ShowEnemyRoutine()
        {
            // **吹き出しはまだ出さない。** 立ち絵より先に枠だけ出ると、
            // 誰もいない画面に喋る場所だけがある絵になる。
            yield return null;

            if (tutorialManager != null)
            {
                tutorialManager.CharacterRevealRequested = () => StartCoroutine(FadeInEnemyRoutine());
            }

            StartConversation();
        }

        /// <summary>フェードインにかける時間（秒）。</summary>
        private const float EnemyFadeInSeconds = 1.5f;

        /// <summary>
        /// 女の子をうっすら浮かび上がらせる。**台詞より先に呼ばれる。**
        ///
        /// 立ち絵は UI の Image ではなく **SpriteRenderer**（体と、子の顔の2枚）。
        /// 以前は Image だけを探していたので必ず null になり、フェードを素通りして
        /// SetActive(true) の瞬間にぱっと出ていた。子まで含めて両方を拾う。
        ///
        /// 出し終わったら <see cref="TutorialManager.CharacterRevealFinished"/> を立てる。
        /// **どの抜け方をしても必ず立てること。** 立て忘れると台詞が出なくなる。
        /// </summary>
        private IEnumerator FadeInEnemyRoutine()
        {
            enemyCharacterObj.SetActive(true);

            var sprites = enemyCharacterObj.GetComponentsInChildren<SpriteRenderer>(true);
            var images = enemyCharacterObj.GetComponentsInChildren<Image>(true);

            // **元の不透明度を覚えておく。** 半透明で置いてある部品を
            // 勝手に不透明にしてしまわないため。
            var spriteAlpha = new float[sprites.Length];
            for (int i = 0; i < sprites.Length; i++) spriteAlpha[i] = sprites[i].color.a;
            var imageAlpha = new float[images.Length];
            for (int i = 0; i < images.Length; i++) imageAlpha[i] = images[i].color.a;

            if (sprites.Length == 0 && images.Length == 0)
            {
                NotifyRevealFinished();
                yield break;
            }

            ApplyEnemyAlpha(sprites, spriteAlpha, images, imageAlpha, 0f);

            float elapsed = 0f;
            while (elapsed < EnemyFadeInSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / EnemyFadeInSeconds);
                // 立ち上がりを緩めて、滲み出てくるように見せる
                ApplyEnemyAlpha(sprites, spriteAlpha, images, imageAlpha, t * t * (3f - 2f * t));
                yield return null;
            }
            ApplyEnemyAlpha(sprites, spriteAlpha, images, imageAlpha, 1f);

            NotifyRevealFinished();

            // 出し終わってから跳ねる部品を付ける。**シーンには足さない。**
            // 対局シーンが `UIテストシーン` と `OpeningScene` の2つあるので、
            // シーンに置くと片方にだけ入れる事故になる（ScreenFlash と同じ理由）。
            // フェードが終わってからなら、立ち絵の大きさも確定している。
            if (enemyCharacterObj.GetComponent<UI.TalkBobAnimator>() == null)
                enemyCharacterObj.AddComponent<UI.TalkBobAnimator>();

            // **髪の黄色だけを蛍光色に光らせる**（2026-09-30 のユーザー指示）。
            // **フェードが終わってから付けること。** 先に付けると、上の
            // `ApplyEnemyAlpha` が光の板まで掴んで不透明度を上書きしてしまう
            // （あの配列はここより前に作られていて、光の板は入っていない）。
            foreach (var sr in sprites)
            {
                UI.Effects.HairNeonGlow.Attach(sr);
            }
        }

        private void NotifyRevealFinished()
        {
            if (tutorialManager != null) tutorialManager.CharacterRevealFinished = true;
        }

        private static void ApplyEnemyAlpha(SpriteRenderer[] sprites, float[] spriteAlpha,
                                            Image[] images, float[] imageAlpha, float t)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                Color c = sprites[i].color;
                c.a = spriteAlpha[i] * t;
                sprites[i].color = c;
            }
            for (int i = 0; i < images.Length; i++)
            {
                Color c = images[i].color;
                c.a = imageAlpha[i] * t;
                images[i].color = c;
            }
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
