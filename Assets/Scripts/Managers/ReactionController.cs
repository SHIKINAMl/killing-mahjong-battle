using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.UI;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// キャラクターのリアクション、ログの表示待ち、シーケンシャルな演出などを管理するクラス
    /// </summary>
    public class ReactionController : MonoBehaviour
    {
        public static ReactionController Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private float reactionDisplayDuration = 2.0f; // リアクションを表示したまま待つ時間

        // UIの参照（シーン上でセットアップするか自動取得する）
        public DialogueUI dialogueUI;
        public EnemyInfoUI enemyInfoUI;

        private Queue<Action> reactionQueue = new Queue<Action>();
        private bool isProcessingReactions = false;

        private void Awake()
        {
            if (Instance == null) {
                Instance = this;
            } else {
                Destroy(gameObject);
            }
        }

        public void Setup(DialogueUI dialogueUI, EnemyInfoUI enemyInfoUI)
        {
            this.dialogueUI = dialogueUI;
            this.enemyInfoUI = enemyInfoUI;
            reactionQueue.Clear();
            isProcessingReactions = false;
        }

        /// <summary>
        /// 打牌時のリアクション（会話・画像変更）をキューに登録して実行する
        /// </summary>
        public void EnqueueDiscardReaction(int tileId, bool isLocalPlayer, string tileName)
        {
            reactionQueue.Enqueue(() => StartCoroutine(ProcessDiscardEvent(tileId, isLocalPlayer, tileName)));
            if (!isProcessingReactions)
            {
                ProcessNextReaction();
            }
        }

        public void ProcessNextReaction()
        {
            if (reactionQueue.Count > 0)
            {
                // ログが開かれている間はキューの消化を止める
                if (dialogueUI != null && dialogueUI.IsLogOpen)
                {
                    isProcessingReactions = false;
                    return;
                }

                isProcessingReactions = true;
                var action = reactionQueue.Dequeue();
                action.Invoke();
            }
            else
            {
                isProcessingReactions = false;
            }
        }

        private IEnumerator WaitWhileLogIsOpen(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (dialogueUI != null && dialogueUI.IsLogOpen)
                {
                    yield return null;
                    continue;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator ProcessDiscardEvent(int tileId, bool isLocalPlayer, string tileName)
        {
            Sprite reactionSprite = null; // 本来はResources等から取得すべき画像

            if (isLocalPlayer)
            {
                // プレイヤーの打牌に対する敵の反応
                if (dialogueUI != null)
                {
                    dialogueUI.ShowText("「プレイヤーが何かを捨てたな…」");
                }
                
                if (enemyInfoUI != null && reactionSprite != null) 
                {
                    enemyInfoUI.SetCharacterSprite(reactionSprite);
                }
            }
            else
            {
                // 敵の打牌宣言
                if (dialogueUI != null)
                {
                    dialogueUI.ShowText($"「{tileName}を切るわ！」");
                }
                
                if (enemyInfoUI != null && reactionSprite != null) 
                {
                    enemyInfoUI.SetCharacterSprite(reactionSprite);
                }
            }

            // ログが開かれている間は時間のカウントを一時停止して待つ
            yield return StartCoroutine(WaitWhileLogIsOpen(reactionDisplayDuration));

            ProcessNextReaction();
        }
    }
}
