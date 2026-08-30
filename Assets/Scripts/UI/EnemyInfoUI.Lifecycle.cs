using UnityEngine;
using TMPro;

namespace KillingMahjong.UI
{
    public partial class EnemyInfoUI
    {
        private void OnEnable()
        {
            if (blinkCoroutine == null)
            {
                blinkCoroutine = StartCoroutine(BlinkRoutine());
            }
        }

        private void OnDisable()
        {
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
            }
        }

        /// <summary>
        /// 倒れる演出。顔が消えて、体が下へ落ちていく。
        ///
        /// まばたき・表情差し替え・バウンドはどれも顔と体を触るので、先に全部止める。
        /// 止めないと落下中に顔が戻ったり、バウンドが体を元の位置へ引き戻したりする。
        /// 呼んだあとは立ち絵が画面外にあるので、そのままシーン遷移する前提。
        /// </summary>
        public System.Collections.IEnumerator PlayDeathRoutine()
        {
            if (blinkCoroutine != null) { StopCoroutine(blinkCoroutine); blinkCoroutine = null; }
            if (reactionCoroutine != null) { StopCoroutine(reactionCoroutine); reactionCoroutine = null; }
            if (bounceCoroutine != null) { StopCoroutine(bounceCoroutine); bounceCoroutine = null; }

            if (faceRenderer != null) faceRenderer.enabled = false;

            if (characterRenderer == null) yield break;

            Transform body = characterRenderer.transform;
            Vector3 start = body.position;
            Vector3 end = start + Vector3.down * deathFallDistance;

            float duration = Mathf.Max(0.01f, deathFallDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / duration);
                // 落下は加速させる（等速だと「沈んでいく」ように見えて力が抜ける）
                body.position = Vector3.Lerp(start, end, p * p);
                yield return null;
            }
            body.position = end;
        }

        private System.Collections.IEnumerator BlinkRoutine()
        {
            while (true)
            {
                if (characterData == null || !characterData.enableBlink || faceRenderer == null)
                {
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }

                float waitTime = Random.Range(characterData.blinkIntervalMin, characterData.blinkIntervalMax);
                yield return new WaitForSeconds(waitTime);

                if (reactionCoroutine != null || faceRenderer.sprite != normalFaceSprite)
                    continue;

                Sprite fullBlink = characterData.faceSprites?.Find(x => x.id == characterData.blinkFaceId)?.sprite;

                if (fullBlink != null)
                {
                    if (faceRenderer.sprite == normalFaceSprite && reactionCoroutine == null)
                    {
                        faceRenderer.sprite = fullBlink;
                        yield return new WaitForSeconds(0.12f);
                    }

                    if (faceRenderer.sprite == fullBlink && reactionCoroutine == null)
                    {
                        faceRenderer.sprite = normalFaceSprite;
                    }
                }
            }
        }

        private void Awake()
        {
            ApplyHpLayer();

            if (characterData != null)
            {
                ApplyCharacterData(characterData);
            }
            else if (characterRenderer != null)
            {
                normalSprite = characterRenderer.sprite;
                if (faceRenderer != null) normalFaceSprite = faceRenderer.sprite;
            }
        }

        /// <summary>
        /// 敵HP（点滴の血袋）を盤面の牌より奥へ回す。
        ///
        /// 血袋から下に伸びるチューブが敵の牌に重なるので、牌の裏へ落とす。
        /// **シーンではなくここで当てる。** 対局シーンが2つ（`OpeningScene` /
        /// `UIテストシーン`）あるので、シーンを直すと片方だけ直す事故が起きる。
        /// 値の根拠は <see cref="Common.UISortingOrders.EnemyHpMeter"/> のコメント。
        /// </summary>
        private void ApplyHpLayer()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null) return;

            canvas.sortingOrder = Common.UISortingOrders.EnemyHpMeter;
        }

        private void Start()
        {
            if (characterRenderer != null)
            {
                originalPosition = characterRenderer.transform.localPosition;
            }

            // UI要素が前面に出て牌のクリック判定を吸い取るのを防ぐため、当たり判定を無効化する
            var graphics = GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            foreach (var g in graphics)
            {
                g.raycastTarget = false;
            }
        }

        /// <summary>
        /// 次の敵キャラクターに切り替える。
        /// availableEnemies リストの中を順番にループする。
        /// </summary>
        public void CycleEnemy()
        {
            if (availableEnemies == null || availableEnemies.Length == 0) return;

            currentEnemyIndex = (currentEnemyIndex + 1) % availableEnemies.Length;
            ApplyCharacterData(availableEnemies[currentEnemyIndex]);
        }

        /// <summary>
        /// 指定した CharacterData を適用して画像を切り替える
        /// </summary>
        private void ApplyCharacterData(CharacterData data)
        {
            if (data == null) return;

            characterData = data;
            CharacterVisualUtil.ResolveDefaultSprites(data, ref normalSprite, ref discardSprite, ref normalFaceSprite);

            CharacterVisualUtil.ApplyIfPresent(characterRenderer, normalSprite);
            CharacterVisualUtil.ApplyIfPresent(faceRenderer, normalFaceSprite);
        }
    }
}
