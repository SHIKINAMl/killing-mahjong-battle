using UnityEngine;
using TMPro;

namespace KillingMahjong.UI
{
    public partial class EnemyInfoUI
    {
        /// <summary>
        /// 「準備完了」の札。シーンの ReadyBoxContainer を実行時に組み直して使う。
        /// 点滴（EnemyPanel）の真下に置く。
        /// </summary>
        private ReadyBadge EnsureReadyBadge()
        {
            RectTransform anchor = (enemyPanel != null)
                ? enemyPanel.GetComponent<RectTransform>() : null;
            return ReadyBoxUtil.EnsureBadge(
                ref readyBadge, readyBoxContainer, readyCheckImage, anchor, isSelf: false);
        }

        /// <summary>
        /// **相手の「準備完了」は出さない（2026-08-14 の指示）。**
        ///
        /// 相手が手牌を決めた瞬間が画面から読めると、**そこを狙って透視を撃てば必ず
        /// 完成手を覗ける**ので能力が強すぎる。自分側の札（<see cref="PlayerInfoUI"/>）は残す。
        ///
        /// 呼び出し側（<see cref="GameUIPhaseController"/>）は10箇所以上あるので、
        /// 個々の呼び出しを消すのではなくここで受け止めて常に伏せる。
        /// **消し忘れの経路が残らないのが利点。** 復活させたくなったらこのメソッドだけ戻す。
        /// </summary>
        public void ShowReadyBox(bool show)
        {
            ReadyBoxUtil.HideReadyBox(EnsureReadyBadge(), readyBoxContainer, readyCheckImage);
        }

        /// <summary>相手の札は出さないので、チェックの受け口も何もしない。</summary>
        public void SetReadyCheck(bool isReady)
        {
        }

        /// <summary>相手の札も拡大したスマホの裏に入るので、自分側と同じタイミングで伏せる。</summary>
        public void SetReadyBoxSuppressed(bool suppressed)
        {
            var badge = EnsureReadyBadge();
            if (badge != null) badge.SetSuppressed(suppressed);
        }

        /// <summary>
        /// 相手の手番のとき点滴を赤く脈打たせる。
        /// 血袋も FloatingAnimator で揺れているので、EnemyPanel の中に影絵を敷いて
        /// 揺れごと追従させる。血袋の形は絵そのものなので矩形の実測は要らない。
        ///
        /// 女の子（立ち絵）も同時に光らせる。点滴は画面の隅にあって気づきにくいため。
        /// 以前は画面のふちに枠を出していたが、盤面が狭く見えるのでこちらへ替えた。
        /// </summary>
        /// <summary>
        /// **相手の手番の光り物は出さない（2026-08-14 の指示）。**
        ///
        /// 立ち絵を赤く染める <see cref="TurnCharacterGlow"/> も、点滴の後ろに影絵を敷く
        /// <see cref="TurnGlow"/> も、どちらも「敵の色が変わる」ように見えて違和感があった。
        /// 相手の手番の合図は「ENEMY TURN」の文字に任せる。
        ///
        /// **自分側（<see cref="PlayerInfoUI"/>）の青い縁取りは残してある。**
        /// 自分の番が分かることは操作に直結するため。
        ///
        /// 呼び出し元は <c>GameUIManager.UpdateTurnIndicator</c> の1箇所だが、
        /// 「相手側は光らせない」という判断はこちら側の都合なのでここで受け止める。
        /// 戻したくなったらこのメソッドの中身だけ復元すればよい（両クラスとも残してある）。
        /// </summary>
        public void SetTurnGlow(bool on)
        {
        }

        /// <summary>
        /// 点滴（体力表示）だけ出し入れする。**立ち絵と表情は触らない。**
        /// チュートリアル第1局で「牌を選ぶUI以外を伏せる」のに使う。
        /// `gameObject` ごと消すと女の子まで消えてセリフの相手がいなくなる。
        /// </summary>
        public void SetVitalsVisible(bool visible)
        {
            if (enemyPanel != null) enemyPanel.SetActive(visible);
        }

        public void SetCharacterSprite(Sprite sprite)
        {
            // SetBodyPoseかSetFaceExpressionを本来は推奨するが、旧互換として残す
            CharacterVisualUtil.ApplyIfPresent(characterRenderer, sprite);
        }

        public void SetBodyPose(string poseId)
        {
            if (characterData == null || characterRenderer == null) return;
            if (CharacterVisualUtil.TryFindBodySprite(characterData, poseId, out var sprite))
            {
                characterRenderer.sprite = sprite;
            }
        }

        public void SetFaceExpression(string expressionId)
        {
            if (characterData == null || faceRenderer == null) return;
            if (CharacterVisualUtil.TryFindFaceSprite(characterData, expressionId, out var sprite))
            {
                faceRenderer.sprite = sprite;
            }
        }

        public void SetDiscardingState(bool isDiscarding)
        {
            if (characterRenderer != null)
            {
                // インスペクターで実行中に変更された場合も反映されるように、characterDataから直接読み取る
                Sprite target = CharacterVisualUtil.ResolveDiscardingSprite(characterData, discardSprite, normalSprite, isDiscarding);
                if (target != null)
                {
                    characterRenderer.sprite = target;
                }
            }
        }

        public void PlayBounceAnimation(float duration = 0.5f)
        {
            if (characterRenderer == null) return;
            if (bounceCoroutine != null) StopCoroutine(bounceCoroutine);
            // 引数で渡されたduration（例: 5秒）に関わらず、インスペクターで設定した時間で一瞬跳ねる
            bounceCoroutine = StartCoroutine(BounceRoutine(bounceDuration));
        }

        private System.Collections.IEnumerator BounceRoutine(float durationToBounce)
        {
            float elapsed = 0f;
            // durationToBounce の時間内で Sin(0) から Sin(PI) まで推移するように速度を計算 (1回跳ねる)
            float bounceSpeed = Mathf.PI / durationToBounce;
            
            while (elapsed < durationToBounce)
            {
                elapsed += Time.deltaTime;
                float yOffset = Mathf.Sin(elapsed * bounceSpeed) * bounceHeight;
                characterRenderer.transform.localPosition = originalPosition + new Vector3(0, yOffset, 0);
                yield return null;
            }
            
            characterRenderer.transform.localPosition = originalPosition;
            bounceCoroutine = null;
        }
    }
}
