using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    public partial class PhaseTransitionUI
    {

        private IEnumerator ScreenShakeRoutine(float duration, float magnitude)
        {
            Vector3 originalPos = transform.localPosition;
            float elapsed = 0.0f;
            
            while (elapsed < duration)
            {
                float x = UnityEngine.Random.Range(-1f, 1f) * magnitude;
                float y = UnityEngine.Random.Range(-1f, 1f) * magnitude;
                
                transform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            transform.localPosition = originalPos;
        }

        private IEnumerator ScoreSettlementRoutine(
            bool isLocalWin,
            int winnerGain,
            int loserLoss,
            int prevLocalHp,
            int prevEnemyHp,
            int newLocalHp,
            int newEnemyHp,
            string resultLabel,
            Action onComplete)
        {
            ResetVisuals();

            // 1. 半透明の暗転（ディマー）を作成・表示
            GameObject dimmerObj = new GameObject("DimmerOverlay");
            dimmerObj.transform.SetParent(transform, false);
            dimmerObj.transform.SetAsFirstSibling();
            var dimmerImage = dimmerObj.AddComponent<Image>();
            dimmerImage.color = new Color(0, 0, 0, 0); // 初期は透明
            var dimmerRt = dimmerObj.GetComponent<RectTransform>();
            dimmerRt.anchorMin = Vector2.zero;
            dimmerRt.anchorMax = Vector2.one;
            dimmerRt.sizeDelta = Vector2.zero;

            // フェードイン
            float t = 0;
            while (t < 0.3f)
            {
                dimmerImage.color = Color.Lerp(new Color(0, 0, 0, 0), dimmerColor, t / 0.3f);
                t += Time.deltaTime;
                yield return null;
            }
            dimmerImage.color = dimmerColor;

            // 2. 役名のバウンド表示
            if (centerText != null)
            {
                centerText.text = resultLabel;
                centerText.gameObject.SetActive(true);
                centerText.color = Color.red;
                
                // ドンッ！とスタンプのように出現するアニメーション
                t = 0;
                float duration = 0.4f;
                Vector3 initialScale = new Vector3(3f, 3f, 1f);
                Vector3 targetScale = Vector3.one;
                
                while (t < duration)
                {
                    float progress = t / duration;
                    // EaseInCubic または Overshoot っぽい動き
                    float scaleProgress = 1f - Mathf.Pow(1f - progress, 4f); 
                    centerText.transform.localScale = Vector3.LerpUnclamped(initialScale, targetScale, scaleProgress);
                    t += Time.deltaTime;
                    yield return null;
                }
                centerText.transform.localScale = targetScale;
                
                // 画面揺れ（着弾の衝撃）
                StartCoroutine(ScreenShakeRoutine(0.2f, 20f));
            }
            
            yield return new WaitForSeconds(1.0f);
            if (centerText != null) centerText.gameObject.SetActive(false);

            // 3. HP表示と血飛沫＆画面揺れ
            if (hpBetContainer != null)
            {
                hpBetContainer.SetActive(true);

                if (isLocalWin)
                {
                    if (playerBetObj != null) playerBetObj.text = $"獲得: +{winnerGain}";
                    if (enemyBetObj != null) enemyBetObj.text = $"喪失: -{loserLoss}";
                }
                else
                {
                    if (playerBetObj != null) playerBetObj.text = $"喪失: -{loserLoss}";
                    if (enemyBetObj != null) enemyBetObj.text = $"獲得: +{winnerGain}";
                }

                // 血飛沫画像の生成（血の移動演出を使うときは出さない）
                GameObject splatterObj = null;
                Image splatterImage = null;
                if (bloodSplatterSprite != null && !useBloodTransfer)
                {
                    splatterObj = new GameObject("BloodSplatter");
                    splatterObj.transform.SetParent(transform, false);
                    splatterImage = splatterObj.AddComponent<Image>();
                    splatterImage.sprite = bloodSplatterSprite;
                    splatterImage.preserveAspect = true;
                    
                    RectTransform srt = splatterObj.GetComponent<RectTransform>();
                    srt.sizeDelta = new Vector2(800, 800);
                    
                    // 敗者側に血飛沫を配置（簡易的に上下で位置を分ける）
                    srt.anchoredPosition = isLocalWin ? new Vector2(0, 300) : new Vector2(0, -300);
                    srt.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0, 360));
                    splatterImage.color = new Color(1, 1, 1, 0); // 初期透明
                }

                // 激しい画面揺れと血飛沫表示
                StartCoroutine(ScreenShakeRoutine(0.5f, 30f));

                if (splatterImage != null)
                {
                    splatterImage.color = new Color(1, 1, 1, 0.8f);
                }

                // 敗者側の位置。isLocalWin なら相手（上）が失い、そうでなければ自分（下）が失う
                Vector2 loserSide  = isLocalWin ? new Vector2(0, 300)  : new Vector2(0, -300);
                Vector2 winnerSide = isLocalWin ? new Vector2(0, -300) : new Vector2(0, 300);

                if (useBloodTransfer)
                {
                    // 血が敗者から勝者へ移る。奪い合いであることを向きで見せる
                    KillingMahjong.Visuals.BloodTransferEffect.Play(
                        transform as RectTransform, loserSide, winnerSide,
                        hpDeductionDuration, pixelBloodGridSize);
                }
                else if (usePixelBlood)
                {
                    KillingMahjong.Visuals.PixelBloodEffect.Play(
                        transform as RectTransform, loserSide,
                        pixelBloodDotCount, pixelBloodGridSize);
                }

                // HPカウントアニメーション
                t = 0;
                while (t < hpDeductionDuration)
                {
                    float progress = t / hpDeductionDuration;
                    float eased = 1f - Mathf.Pow(1f - progress, 3f);

                    int currentPlayerAnimHp = Mathf.RoundToInt(Mathf.Lerp(prevLocalHp, newLocalHp, eased));
                    int currentEnemyAnimHp = Mathf.RoundToInt(Mathf.Lerp(prevEnemyHp, newEnemyHp, eased));

                    if (playerHpObj != null) playerHpObj.text = "Your HP: " + currentPlayerAnimHp;
                    if (enemyHpObj != null) enemyHpObj.text = "Enemy HP: " + currentEnemyAnimHp;

                    // 血飛沫のフェードアウト
                    if (splatterImage != null && progress > 0.5f)
                    {
                        float fadeOutProgress = (progress - 0.5f) * 2f;
                        splatterImage.color = new Color(1, 1, 1, 0.8f * (1f - fadeOutProgress));
                    }

                    t += Time.deltaTime;
                    yield return null;
                }
                
                if (playerHpObj != null) playerHpObj.text = "Your HP: " + newLocalHp;
                if (enemyHpObj != null) enemyHpObj.text = "Enemy HP: " + newEnemyHp;
                
                if (splatterObj != null) Destroy(splatterObj);

                yield return new WaitForSeconds(1.5f);
                hpBetContainer.SetActive(false);
            }
            else
            {
                yield return new WaitForSeconds(1.5f);
            }

            // ディマーフェードアウト
            t = 0;
            while (t < 0.3f)
            {
                dimmerImage.color = Color.Lerp(dimmerColor, new Color(0, 0, 0, 0), t / 0.3f);
                t += Time.deltaTime;
                yield return null;
            }
            Destroy(dimmerObj);

            Debug.Log("[ScoreSettlement] Complete");
            onComplete?.Invoke();
        }
    }
}
