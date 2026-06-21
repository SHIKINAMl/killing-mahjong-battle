using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    public class RonAnimationUI : MonoBehaviour
    {
        [Header("Cinematic Assets")]
        [SerializeField] private Sprite bloodSplatterSprite;
        [SerializeField] private TMP_FontAsset customFont;

        [Header("Player Ron Bubble (Pre-Animation)")]
        [Tooltip("自分がロンした瞬間に盤面上に出す吹き出し")]
        [SerializeField] private GameObject playerRonBubbleContainer;
        
        [Header("Hand Display Layout")]
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private TileResourceManager tileResourceManager;
        [SerializeField] private float tileSpacing = 115f;
        [SerializeField] private float tileScale = 1.5f;

        private void Start()
        {
            PrepareForPreDialogue();
        }

        public void PrepareForPreDialogue()
        {
            if (playerRonBubbleContainer != null) playerRonBubbleContainer.SetActive(false);
        }

        public bool HasPlayerRonBubble()
        {
            return playerRonBubbleContainer != null;
        }

        public void ShowPlayerRonBubble(bool show)
        {
            if (playerRonBubbleContainer != null)
            {
                playerRonBubbleContainer.SetActive(show);
            }
        }

        public void PlayRonSequence(List<int> handTiles, int ronTile, List<string> yakuList, string formula, string rankName, int score, bool isLocalPlayerWin, 
            PlayerInfoUI playerInfo, EnemyInfoUI enemyInfo, int prevLocalHp, int newLocalHp, int prevEnemyHp, int newEnemyHp, System.Action onComplete)
        {
            StartCoroutine(SequenceRoutine(handTiles, ronTile, yakuList, formula, rankName, score, isLocalPlayerWin, playerInfo, enemyInfo, prevLocalHp, newLocalHp, prevEnemyHp, newEnemyHp, onComplete));
        }

        private IEnumerator SequenceRoutine(List<int> handTiles, int ronTile, List<string> yakuList, string formula, string rankName, int score, bool isLocalPlayerWin, 
            PlayerInfoUI playerInfo, EnemyInfoUI enemyInfo, int prevLocalHp, int newLocalHp, int prevEnemyHp, int newEnemyHp, System.Action onComplete)
        {
            // 0. カットイン演出（勝者の顔と「ロン！」を表示）
            bool cutinFinished = false;
            
            // 勝者の画像を取得
            Sprite winnerSprite = null;
            if (isLocalPlayerWin && playerInfo != null && playerInfo.CurrentCharacterData != null)
            {
                if (playerInfo.CurrentCharacterData.faceSprites.Count > 0)
                    winnerSprite = playerInfo.CurrentCharacterData.faceSprites[0].sprite;
                else
                    winnerSprite = playerInfo.CurrentCharacterData.normalSprite;
            }
            else if (!isLocalPlayerWin && enemyInfo != null && enemyInfo.CurrentCharacterData != null)
            {
                if (enemyInfo.CurrentCharacterData.faceSprites.Count > 0)
                    winnerSprite = enemyInfo.CurrentCharacterData.faceSprites[0].sprite;
                else
                    winnerSprite = enemyInfo.CurrentCharacterData.normalSprite;
            }

            if (winnerSprite != null)
            {
                CutinAnimationUI cutinUI = gameObject.AddComponent<CutinAnimationUI>();
                cutinUI.PlayCutin(winnerSprite, customFont, () => {
                    cutinFinished = true;
                    Destroy(cutinUI);
                });

                // カットインが終わるまで待機
                while (!cutinFinished) yield return null;
            }

            // 1. 大枠コンテナの生成（すべてを包括する最前面キャンバス）
            GameObject container = new GameObject("RonCinematicContainer");
            container.transform.SetParent(transform, false);
            container.transform.SetAsLastSibling();
            RectTransform containerRt = container.AddComponent<RectTransform>();
            containerRt.anchorMin = Vector2.zero;
            containerRt.anchorMax = Vector2.one;
            containerRt.sizeDelta = Vector2.zero;

            // 2. 暗転ディマー（背景のスマホ等が見えるように少し薄めに）
            GameObject dimmer = new GameObject("Dimmer");
            dimmer.transform.SetParent(containerRt, false);
            Image dimmerImg = dimmer.AddComponent<Image>();
            dimmerImg.color = new Color(0, 0, 0, 0.65f);
            RectTransform dimmerRt = dimmer.GetComponent<RectTransform>();
            dimmerRt.anchorMin = Vector2.zero;
            dimmerRt.anchorMax = Vector2.one;
            dimmerRt.sizeDelta = Vector2.zero;

            // 3. 役と飜数を示すリボン（帯）
            GameObject ribbonObj = new GameObject("YakuRibbon");
            ribbonObj.transform.SetParent(containerRt, false);
            Image ribbonImg = ribbonObj.AddComponent<Image>();
            ribbonImg.color = new Color(0.05f, 0.05f, 0.05f, 0.95f); // 黒い帯
            
            // 少し斜めにするスタイリッシュな表現
            ribbonObj.transform.localRotation = Quaternion.Euler(0, 0, 2f);

            RectTransform ribbonRt = ribbonObj.GetComponent<RectTransform>();
            ribbonRt.anchorMin = new Vector2(0, 0.40f);
            ribbonRt.anchorMax = new Vector2(1, 0.40f);
            ribbonRt.sizeDelta = new Vector2(200, 100); // 画面幅＋余白、高さ100
            ribbonRt.anchoredPosition = Vector2.zero;

            GameObject yakuTextObj = new GameObject("YakuText");
            yakuTextObj.transform.SetParent(ribbonObj.transform, false);
            TextMeshProUGUI yakuText = yakuTextObj.AddComponent<TextMeshProUGUI>();
            if (customFont != null) yakuText.font = customFont;
            
            // 役を一行にまとめる
            string yakuJoined = string.Join("・", yakuList);
            
            yakuText.text = $"{yakuJoined}   <color=#FFFF00>×{formula}</color>"; // 黄色で倍率を強調
            yakuText.color = new Color(1f, 1f, 1f); 
            yakuText.enableAutoSizing = true;
            yakuText.fontSizeMin = 20;
            yakuText.fontSizeMax = 50;
            yakuText.alignment = TextAlignmentOptions.Center;
            yakuText.textWrappingMode = TextWrappingModes.NoWrap;
            
            RectTransform yakuTextRt = yakuTextObj.GetComponent<RectTransform>();
            yakuTextRt.anchorMin = Vector2.zero;
            yakuTextRt.anchorMax = Vector2.one;
            yakuTextRt.sizeDelta = new Vector2(-100, 0); // 左右に50pxずつの余白を設ける
            yakuTextRt.anchoredPosition = Vector2.zero;

            // 4. 手牌の生成と配置（帯の下）
            GameObject handContainer = new GameObject("HandContainer");
            handContainer.transform.SetParent(containerRt, false);
            RectTransform handContainerRt = handContainer.AddComponent<RectTransform>();
            handContainerRt.anchorMin = new Vector2(0.5f, 0.20f); // 下部20%の位置
            handContainerRt.anchorMax = new Vector2(0.5f, 0.20f);
            handContainerRt.sizeDelta = Vector2.zero;
            handContainerRt.anchoredPosition = Vector2.zero;

            if (tilePrefab != null && tileResourceManager != null)
            {
                int handCount = handTiles.Count;
                for (int i = 0; i < handCount; i++)
                {
                    GameObject obj = Instantiate(tilePrefab, handContainerRt);
                    InitializeTileVisual(obj, handTiles[i]);
                    RectTransform rt = obj.GetComponent<RectTransform>();
                    ApplyTileRectSettings(rt);
                    float offset_x = (i - (handCount - 1) / 2f) * tileSpacing - 30f; // 少し左に詰める
                    rt.anchoredPosition3D = new Vector3(offset_x, 0, 0);
                }
                
                // アガリ牌（ロン牌）を少し離して配置
                if (ronTile > 0)
                {
                    GameObject obj = Instantiate(tilePrefab, handContainerRt);
                    InitializeTileVisual(obj, ronTile);
                    RectTransform rt = obj.GetComponent<RectTransform>();
                    ApplyTileRectSettings(rt);
                    float offset_x = ((handCount) - (handCount - 1) / 2f) * tileSpacing + 20f; 
                    rt.anchoredPosition3D = new Vector3(offset_x, 0, 0);
                }
            }

            // --- 0.5秒のタメ（ここでカットインと手牌が見える） ---
            yield return new WaitForSeconds(0.5f);

            // 5. 血飛沫と巨大スコアのバウンド表示（ドンッ！）
            GameObject splatterObj = null;
            if (bloodSplatterSprite != null)
            {
                splatterObj = new GameObject("BloodSplatter");
                splatterObj.transform.SetParent(containerRt, false);
                Image splatterImg = splatterObj.AddComponent<Image>();
                splatterImg.sprite = bloodSplatterSprite;
                splatterImg.preserveAspect = true;
                splatterImg.color = new Color(0.8f, 0f, 0f, 0.9f); // 濃い赤
                RectTransform splatterRt = splatterObj.GetComponent<RectTransform>();
                splatterRt.anchorMin = new Vector2(0.5f, 0.5f);
                splatterRt.anchorMax = new Vector2(0.5f, 0.5f);
                splatterRt.sizeDelta = new Vector2(1000, 1000);
                splatterRt.anchoredPosition = new Vector2(0, 150);
            }

            GameObject scoreTextObj = new GameObject("ScoreText");
            scoreTextObj.transform.SetParent(containerRt, false);
            TextMeshProUGUI scoreText = scoreTextObj.AddComponent<TextMeshProUGUI>();
            if (customFont != null) scoreText.font = customFont;
            
            // スコアが0の時などは役満や満貫などのランク名をそのまま表示
            scoreText.text = score > 0 ? score.ToString() : rankName;
            scoreText.color = new Color(1f, 0.2f, 0.2f); // 真っ赤な文字
            scoreText.fontSize = 250; // 巨大文字
            scoreText.alignment = TextAlignmentOptions.Center;
            scoreText.fontStyle = FontStyles.Bold;
            
            // 白いアウトラインで文字を際立たせる
            scoreText.outlineWidth = 0.2f;
            scoreText.outlineColor = new Color32(255, 255, 255, 255);
            
            RectTransform scoreTextRt = scoreTextObj.GetComponent<RectTransform>();
            scoreTextRt.anchorMin = new Vector2(0.5f, 0.5f);
            scoreTextRt.anchorMax = new Vector2(0.5f, 0.5f);
            scoreTextRt.sizeDelta = new Vector2(1200, 300);
            scoreTextRt.anchoredPosition = new Vector2(0, 150);

            // スタンプのようにドンッ！と落ちてくるアニメーション
            float t = 0;
            float duration = 0.2f; // 高速で落下
            Vector3 initialScale = new Vector3(5f, 5f, 1f);
            Vector3 targetScale = Vector3.one;
            while (t < duration)
            {
                float progress = t / duration;
                float scaleProgress = 1f - Mathf.Pow(1f - progress, 4f); 
                scoreTextRt.localScale = Vector3.LerpUnclamped(initialScale, targetScale, scaleProgress);
                if (splatterObj != null) splatterObj.transform.localScale = scoreTextRt.localScale;
                t += Time.deltaTime;
                yield return null;
            }
            scoreTextRt.localScale = targetScale;
            if (splatterObj != null) splatterObj.transform.localScale = targetScale;

            // 激しい画面揺れ
            Vector3 originalPos = containerRt.localPosition;
            float shakeElapsed = 0;
            while (shakeElapsed < 0.3f)
            {
                float x = UnityEngine.Random.Range(-1f, 1f) * 30f;
                float y = UnityEngine.Random.Range(-1f, 1f) * 30f;
                containerRt.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
                shakeElapsed += Time.deltaTime;
                yield return null;
            }

            // HP UIを最前面へ表示する処理はユーザー要望により削除

            // 勝者のHPゲージへパーティクルが吸い込まれる演出
            Transform winnerTransform = isLocalPlayerWin ? playerInfo?.transform : enemyInfo?.transform;
            if (winnerTransform != null)
            {
                StartCoroutine(SpawnAbsorbParticles(containerRt, scoreTextRt.position, winnerTransform.position));
            }

            // 画像のように、一枚絵としてプレイヤーにしばらく見せつけつつHPを増減させる
            float holdTime = 3.5f;
            float hpTimer = 0;
            while (hpTimer < holdTime)
            {
                float progress = Mathf.Clamp01(hpTimer / 1.5f); // 1.5秒かけてHP増減
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                
                if (playerInfo != null) playerInfo.SetHP(Mathf.RoundToInt(Mathf.Lerp(prevLocalHp, newLocalHp, eased)));
                if (enemyInfo != null) enemyInfo.SetHP(Mathf.RoundToInt(Mathf.Lerp(prevEnemyHp, newEnemyHp, eased)));
                
                hpTimer += Time.deltaTime;
                yield return null;
            }
            
            if (playerInfo != null) playerInfo.SetHP(newLocalHp);
            if (enemyInfo != null) enemyInfo.SetHP(newEnemyHp);

            // 復元処理は削除済み

            // 終了処理
            Destroy(container);
            onComplete?.Invoke();
        }

        private void InitializeTileVisual(GameObject tileObj, int tileId)
        {
            TileVisual visual = tileObj.GetComponent<TileVisual>();
            if (visual != null && tileResourceManager != null)
            {
                visual.SetTile(tileId, tileResourceManager.GetTileSprite(tileId));
            }
            
            // Interactionは不要なので消すか無効化する
            TileInteraction interaction = tileObj.GetComponent<TileInteraction>();
            if (interaction != null) Destroy(interaction);
        }

        private void ApplyTileRectSettings(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            
            rt.anchoredPosition3D = Vector3.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = new Vector3(tileScale, tileScale, 1f);
        }

        private IEnumerator SpawnAbsorbParticles(RectTransform containerRt, Vector3 startPos, Vector3 endPos)
        {
            int count = 15;
            for (int i = 0; i < count; i++)
            {
                if (containerRt == null) break;
                
                GameObject p = new GameObject("AbsorbParticle");
                p.transform.SetParent(containerRt, false);
                Image img = p.AddComponent<Image>();
                img.sprite = bloodSplatterSprite;
                img.color = new Color(1f, 0.2f, 0.2f, 0.8f);
                
                RectTransform rt = p.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(80, 80);
                rt.position = startPos;
                
                StartCoroutine(AnimateParticle(rt, startPos, endPos));
                yield return new WaitForSeconds(0.08f);
            }
        }

        private IEnumerator AnimateParticle(RectTransform rt, Vector3 startPos, Vector3 endPos)
        {
            float t = 0;
            float duration = 0.6f;
            
            // 弧を描くように軌道を少し散らす
            Vector3 midPoint = (startPos + endPos) / 2f;
            midPoint.y += UnityEngine.Random.Range(50f, 250f);
            midPoint.x += UnityEngine.Random.Range(-250f, 250f);
            
            while (t < duration)
            {
                if (rt == null) break;
                float progress = t / duration;
                
                // ベジェ曲線
                Vector3 m1 = Vector3.Lerp(startPos, midPoint, progress);
                Vector3 m2 = Vector3.Lerp(midPoint, endPos, progress);
                rt.position = Vector3.Lerp(m1, m2, progress);
                
                rt.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.2f, progress);
                
                t += Time.deltaTime;
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        // --- Tester Context Menu ---
        [ContextMenu("Test Ron Animation Local Win")]
        public void TestRonLocalWin()
        {
            List<int> dummyHand = new List<int> { 1, 2, 3, 5, 6, 7, 10, 11, 12, 19, 20, 21, 28 };
            List<string> dummyYaku = new List<string> { "立直", "一発", "門前清自摸和", "ドラ3" };
            string dummyFormula = "6飜";
            string dummyRank = "跳満";
            PlayRonSequence(dummyHand, 28, dummyYaku, dummyFormula, dummyRank, 12000, true, null, null, 20000, 26000, 20000, 14000, () => Debug.Log("Test Local Win complete"));
        }
        
        [ContextMenu("Test Ron Animation Enemy Win")]
        public void TestRonEnemyWin()
        {
            List<int> dummyHand = new List<int> { 9, 9, 9, 18, 18, 18, 27, 27, 27, 30, 30, 30, 33 };
            List<string> dummyYaku = new List<string> { "大三元", "字一色" };
            string dummyFormula = "ダブル役満";
            string dummyRank = "ダブル役満";
            PlayRonSequence(dummyHand, 33, dummyYaku, dummyFormula, dummyRank, 64000, false, null, null, 20000, 20000, 20000, 84000, () => Debug.Log("Test Enemy Win complete"));
        }
    }
}
