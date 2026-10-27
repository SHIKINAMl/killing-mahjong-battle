using UnityEngine;
using TMPro;

namespace KillingMahjong.UI
{
    public partial class EnemyInfoUI
    {
        public const string TutorialRevealEffectId = "tutorial.character_reveal";
        private CharacterData tutorialOriginalCharacter, tutorialRevealedCharacter;

        /// <summary>赤い服の女の子の文字送りの音の高さ。通常顔.asset の値とそろえる。</summary>
        private const float RedGirlTextBlipStartHz = 392f, RedGirlTextBlipEndHz = 392f;

        /// <summary>チュートリアルで先輩から本来の姿へ切り替える。共有アセットには書き込まない。</summary>
        public System.Collections.IEnumerator RevealTutorialCharacter()
        {
            var body = Resources.Load<Sprite>("女の子/通常時身体");
            var face = Resources.Load<Sprite>("女の子/通常顔");
            if (body == null || face == null)
                throw new System.InvalidOperationException("正体を見せるための立ち絵がありません。");
            if (tutorialRevealedCharacter != null) yield break;
            tutorialOriginalCharacter = characterData;
            tutorialRevealedCharacter = ScriptableObject.CreateInstance<CharacterData>();
            tutorialRevealedCharacter.characterName = "ヴァンちゃん";
            // 文字送りの音は、本編の赤い服の女の子（通常顔.asset）と同じ高さ
            tutorialRevealedCharacter.textBlipStartHz = RedGirlTextBlipStartHz;
            tutorialRevealedCharacter.textBlipEndHz = RedGirlTextBlipEndHz;
            tutorialRevealedCharacter.normalSprite = body;
            tutorialRevealedCharacter.discardSprite = body;
            tutorialRevealedCharacter.bodySprites.Add(new NamedSprite { id = "normal", sprite = body });
            tutorialRevealedCharacter.faceSprites.Add(new NamedSprite { id = "normal", sprite = face });
            tutorialRevealedCharacter.enableBlink = false;
            if (blinkCoroutine != null) { StopCoroutine(blinkCoroutine); blinkCoroutine = null; }
            if (reactionCoroutine != null) { StopCoroutine(reactionCoroutine); reactionCoroutine = null; }
            if (bounceCoroutine != null) { StopCoroutine(bounceCoroutine); bounceCoroutine = null; }
            Color bodyColor = characterRenderer != null ? characterRenderer.color : Color.white;
            Color faceColor = faceRenderer != null ? faceRenderer.color : Color.white;
            // 先輩が消えて女の子が浮かぶまで、合わせて 2.5 秒かける（2026-10-22 プランナー指摘
            // 「先輩から女の子に変身するときは、結構2-3秒くらいかけてフェードしてもいい」。以前は 1 秒）
            const float halfSeconds = 1.25f;
            try
            {
                for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / halfSeconds)
                {
                    SetPortraitAlpha(bodyColor, faceColor, 1f - Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }
                SetPortraitAlpha(bodyColor, faceColor, 0f);
                ApplyCharacterData(tutorialRevealedCharacter);
                for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / halfSeconds)
                {
                    SetPortraitAlpha(bodyColor, faceColor, Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }
            }
            finally { SetPortraitAlpha(bodyColor, faceColor, 1f); }
        }

        private void SetPortraitAlpha(Color body, Color face, float alpha)
        {
            if (characterRenderer != null) { body.a *= alpha; characterRenderer.color = body; }
            if (faceRenderer != null) { face.a *= alpha; faceRenderer.color = face; }
        }

        public void RestoreTutorialCharacter()
        {
            if (tutorialRevealedCharacter == null) return;
            ApplyCharacterData(tutorialOriginalCharacter);
            Destroy(tutorialRevealedCharacter);
            tutorialRevealedCharacter = null;
            tutorialOriginalCharacter = null;
            if (isActiveAndEnabled && blinkCoroutine == null) blinkCoroutine = StartCoroutine(BlinkRoutine());
        }

        private void OnDestroy()
        {
            if (tutorialRevealedCharacter != null) Destroy(tutorialRevealedCharacter);
        }

        private void OnEnable()
        {
            if (blinkCoroutine == null)
            {
                blinkCoroutine = StartCoroutine(BlinkRoutine());
            }
        }

        private void OnDisable()
        {
            CancelDiscardAttention();
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
            CancelDiscardAttention();
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
                float p = Mathf.Clamp01(PresentationTiming.Sample(elapsed) / duration);
                // 落下は加速させる（等速だと「沈んでいく」ように見えて力が抜ける）
                body.position = Vector3.Lerp(start, end, p * p);
                yield return null;
            }
            body.position = end;
        }

        /// <summary>まばたきを止めておく（拍に合わせた動きのあいだ。<see cref="SetBeatDance"/>）。</summary>
        private bool suppressBlink;

        private System.Collections.IEnumerator BlinkRoutine()
        {
            while (true)
            {
                if (characterData == null || !characterData.enableBlink || faceRenderer == null || suppressBlink)
                {
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }

                float waitTime = Random.Range(characterData.blinkIntervalMin, characterData.blinkIntervalMax);
                yield return new WaitForSeconds(waitTime);

                if (reactionCoroutine != null || discardAttention != null || faceRenderer.sprite != normalFaceSprite)
                    continue;

                Sprite fullBlink = characterData.faceSprites?.Find(x => x.id == characterData.blinkFaceId)?.sprite;

                if (fullBlink != null)
                {
                    if (faceRenderer.sprite == normalFaceSprite && reactionCoroutine == null && discardAttention == null)
                    {
                        faceRenderer.sprite = fullBlink;
                        yield return new WaitForSeconds(0.12f);
                    }

                    if (faceRenderer.sprite == fullBlink && reactionCoroutine == null && discardAttention == null)
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

            // **血を光らせる（2026-09-29 のユーザー指示）。**
            // 自分側（PlayerInfoUI）と同じ部品で、同じ見え方にする。
            Effects.BloodGlowUI.Attach(hpFillImage);
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

            // 顔まで1枚に描いてある絵（顔の差分を持たないキャラ）は、顔の層を空にする。
            // 残すと前のキャラの顔が新しい絵の上に重なる（2026-10-25、先輩の絵を1枚絵に替えたとき）
            if (data.faceSprites == null || data.faceSprites.Count == 0)
            {
                normalFaceSprite = null;
                if (faceRenderer != null) faceRenderer.sprite = null;
            }
            else CharacterVisualUtil.ApplyIfPresent(faceRenderer, normalFaceSprite);
        }
    }
}
