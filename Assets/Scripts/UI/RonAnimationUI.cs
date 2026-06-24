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
            
            // 勝者（ロンした側）の画像を取得（顔パーツではなく身体のあるnormalSpriteを使用）
            Sprite winnerSprite = null;
            Sprite faceSprite = null;
            string cutinText = "ロン！";

            if (isLocalPlayerWin && playerInfo != null && playerInfo.CurrentCharacterData != null)
            {
                winnerSprite = playerInfo.CurrentCharacterData.normalSprite;
                if (playerInfo.CurrentCharacterData.faceSprites != null && playerInfo.CurrentCharacterData.faceSprites.Count > 0)
                {
                    var match = playerInfo.CurrentCharacterData.faceSprites.Find(x => x.id == playerInfo.CurrentCharacterData.defaultFaceId);
                    faceSprite = match != null ? match.sprite : playerInfo.CurrentCharacterData.faceSprites[0].sprite;
                }
            }
            else if (!isLocalPlayerWin && enemyInfo != null && enemyInfo.CurrentCharacterData != null)
            {
                winnerSprite = enemyInfo.CurrentCharacterData.normalSprite;
                if (enemyInfo.CurrentCharacterData.faceSprites != null && enemyInfo.CurrentCharacterData.faceSprites.Count > 0)
                {
                    var match = enemyInfo.CurrentCharacterData.faceSprites.Find(x => x.id == enemyInfo.CurrentCharacterData.defaultFaceId);
                    faceSprite = match != null ? match.sprite : enemyInfo.CurrentCharacterData.faceSprites[0].sprite;
                }
            }

            if (winnerSprite != null)
            {
                CutinAnimationUI cutinUI = gameObject.AddComponent<CutinAnimationUI>();
                cutinUI.PlayCutin(winnerSprite, faceSprite, customFont, cutinText, () => {
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
            
            // スマホUIなどよりも確実に最前面に表示するため、Canvasを追加
            Canvas containerCanvas = container.AddComponent<Canvas>();
            containerCanvas.overrideSorting = true;
            containerCanvas.sortingLayerName = "UI";
            containerCanvas.sortingOrder = 30000;
            container.AddComponent<UnityEngine.UI.GraphicRaycaster>();

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
            
            // 役テキストは後で1つずつ表示するため最初は空にする
            yakuText.text = "";
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

            // --- 役を1つずつ表示する演出 ---
            string currentYakuStr = "";
            foreach (var yaku in yakuList)
            {
                if (!string.IsNullOrEmpty(currentYakuStr)) currentYakuStr += "・";
                currentYakuStr += yaku;
                yakuText.text = currentYakuStr;
                
                // 役を1つ表示するごとの間隔
                yield return new WaitForSeconds(0.4f);
            }
            
            // --- タメ（ここで役と手牌をしっかり見せる） ---
            yield return new WaitForSeconds(1.0f);

            // 【追加】計算式の表示（ダミー）
            GameObject formulaTextObj = new GameObject("FormulaText");
            formulaTextObj.transform.SetParent(containerRt, false);
            TextMeshProUGUI formulaText = formulaTextObj.AddComponent<TextMeshProUGUI>();
            if (customFont != null) formulaText.font = customFont;
            
            // Python側からスコアしか来ないので、ダミーの数式を表示
            formulaText.text = $"??? × ??? = {score}";
            formulaText.color = new Color(1f, 1f, 0.5f); // 薄い黄色
            formulaText.fontSize = 60; 
            formulaText.alignment = TextAlignmentOptions.Center;
            
            // 手牌の上に配置する
            RectTransform formulaTextRt = formulaTextObj.GetComponent<RectTransform>();
            formulaTextRt.anchorMin = new Vector2(0.5f, 0.35f); // 手牌が0.20fなので少し上
            formulaTextRt.anchorMax = new Vector2(0.5f, 0.35f);
            formulaTextRt.sizeDelta = new Vector2(800, 100);
            formulaTextRt.anchoredPosition = Vector2.zero;

            // アニメーション（ふわっと浮き出る）
            float fadeTime = 0.5f;
            for (float animT = 0; animT < fadeTime; animT += Time.deltaTime)
            {
                float alpha = animT / fadeTime;
                formulaText.color = new Color(1f, 1f, 0.5f, alpha);
                formulaTextRt.anchoredPosition = new Vector2(0, -20f + (20f * alpha)); // 下から少し上がる
                yield return null;
            }
            formulaText.color = new Color(1f, 1f, 0.5f, 1f);

            // 計算式表示後のタメ
            yield return new WaitForSeconds(1.5f);

            // 【追加】役ランク（跳満・満貫など）の中央表示
            GameObject rankTextObj = new GameObject("RankText");
            rankTextObj.transform.SetParent(containerRt, false);
            TextMeshProUGUI rankTextUI = rankTextObj.AddComponent<TextMeshProUGUI>();
            if (customFont != null) rankTextUI.font = customFont;
            
            rankTextUI.text = rankName;
            rankTextUI.color = new Color(1f, 0.8f, 0.2f); // ゴールドっぽい色
            rankTextUI.fontSize = 150; 
            rankTextUI.alignment = TextAlignmentOptions.Center;
            rankTextUI.fontStyle = FontStyles.Bold;
            rankTextUI.outlineWidth = 0.2f;
            rankTextUI.outlineColor = new Color32(0, 0, 0, 255); // 黒フチ
            
            RectTransform rankTextRt = rankTextObj.GetComponent<RectTransform>();
            rankTextRt.anchorMin = new Vector2(0.5f, 0.5f);
            rankTextRt.anchorMax = new Vector2(0.5f, 0.5f);
            rankTextRt.sizeDelta = new Vector2(1000, 300);
            rankTextRt.anchoredPosition = Vector2.zero; // 画面中央

            // ドンッと出るアニメーション
            float rankAnimTime = 0.3f;
            Vector3 initialRankScale = new Vector3(3f, 3f, 1f);
            for (float animT = 0; animT < rankAnimTime; animT += Time.deltaTime)
            {
                float progress = animT / rankAnimTime;
                float scale = 1f - Mathf.Pow(1f - progress, 3f);
                rankTextRt.localScale = Vector3.LerpUnclamped(initialRankScale, Vector3.one, scale);
                // αフェードイン
                rankTextUI.color = new Color(1f, 0.8f, 0.2f, progress);
                yield return null;
            }
            rankTextRt.localScale = Vector3.one;
            rankTextUI.color = new Color(1f, 0.8f, 0.2f, 1f);

            // 役ランク表示後のタメ
            yield return new WaitForSeconds(1.0f);

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

            // 画面揺れは不要とのことなので削除し、位置を確実に戻すのみ
            Vector3 originalPos = containerRt.localPosition;
            containerRt.localPosition = originalPos;

            // スコアをしばらく見せる
            yield return new WaitForSeconds(2.0f);

            // 点数計算画面を先に消す
            Destroy(container);

            // 画面が消えてから、スマホ（自軍）と敵メーターに注目させるためのタメ
            yield return new WaitForSeconds(0.5f);

            // 【追加】勝者へのキラキラエフェクトと敗者へのダメージエフェクト（点数画面が消えた後に発生）
            PlayWinnerSparkleEffect(isLocalPlayerWin);
            PlayLoserDamageEffect(isLocalPlayerWin);

            // 画面中央から勝者のHPゲージへパーティクルが吸い込まれる演出
            Transform winnerTransform = isLocalPlayerWin ? playerInfo?.transform : enemyInfo?.transform;
            if (winnerTransform != null)
            {
                // containerRtは破棄されているため、このスクリプト自体のRectTransformを親にする
                StartCoroutine(SpawnAbsorbParticles(GetComponent<RectTransform>(), new Vector3(Screen.width/2f, Screen.height/2f, 0), winnerTransform.position));
            }

            // キラキラ・ダメージエフェクトを見せつつHPを増減させる
            float holdTime = 3.0f;
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

            // 終了処理
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

        private void PlayWinnerSparkleEffect(bool isLocalPlayerWin)
        {
            StartCoroutine(SparkleRoutine(isLocalPlayerWin));
        }

        private IEnumerator SparkleRoutine(bool isLocalPlayerWin)
        {
            GameObject sparkleContainer = new GameObject("SparkleContainer");
            sparkleContainer.transform.SetParent(transform, false);
            sparkleContainer.transform.SetAsLastSibling();
            RectTransform containerRt = sparkleContainer.AddComponent<RectTransform>();
            containerRt.anchorMin = Vector2.zero;
            containerRt.anchorMax = Vector2.one;
            containerRt.sizeDelta = Vector2.zero;

            int numSparkles = 20;
            float duration = 3.0f; // エフェクト継続時間
            float timer = 0;

            while (timer < duration)
            {
                GameObject star = new GameObject("Star");
                star.transform.SetParent(containerRt, false);
                Image img = star.AddComponent<Image>();
                img.color = new Color(1f, 1f, 0f, 0f); // 透明な黄色
                RectTransform rt = star.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(40, 40);
                
                // 勝者が自分なら下部、敵なら上部付近にキラキラを発生
                float randX = Random.Range(-500f, 500f);
                float randY = isLocalPlayerWin ? Random.Range(-400f, -50f) : Random.Range(50f, 400f);
                rt.anchoredPosition = new Vector2(randX, randY);
                rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

                StartCoroutine(SingleSparkleAnim(rt, img));

                timer += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }

            yield return new WaitForSeconds(1.0f);
            Destroy(sparkleContainer);
        }

        private IEnumerator SingleSparkleAnim(RectTransform rt, Image img)
        {
            float t = 0;
            float life = Random.Range(0.5f, 1.2f);
            float rotSpeed = Random.Range(90f, 180f);
            
            while(t < life && rt != null && img != null)
            {
                t += Time.deltaTime;
                float progress = t / life;
                
                // フェードイン＆アウト
                float alpha = Mathf.Sin(progress * Mathf.PI);
                img.color = new Color(1f, 1f, 0.6f, alpha);
                
                // 拡縮（星のまたたき表現）
                float scale = 0.5f + alpha * 1.5f;
                rt.localScale = new Vector3(scale, scale, 1f);
                
                rt.Rotate(0, 0, rotSpeed * Time.deltaTime);
                rt.anchoredPosition += new Vector2(0, 50f * Time.deltaTime);
                
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        private void PlayLoserDamageEffect(bool isLocalPlayerWin)
        {
            StartCoroutine(DamageRoutine(isLocalPlayerWin));
        }

        private IEnumerator DamageRoutine(bool isLocalPlayerWin)
        {
            GameObject damageContainer = new GameObject("DamageContainer");
            damageContainer.transform.SetParent(transform, false);
            damageContainer.transform.SetAsLastSibling();
            RectTransform containerRt = damageContainer.AddComponent<RectTransform>();
            containerRt.anchorMin = Vector2.zero;
            containerRt.anchorMax = Vector2.one;
            containerRt.sizeDelta = Vector2.zero;

            // 敗者側を赤くフラッシュさせるパネル
            GameObject flash = new GameObject("RedFlash");
            flash.transform.SetParent(containerRt, false);
            Image img = flash.AddComponent<Image>();
            img.color = new Color(1f, 0f, 0f, 0f);
            
            RectTransform rt = flash.GetComponent<RectTransform>();
            if (isLocalPlayerWin)
            {
                // 勝者が自分 = 敗者は敵（画面上部）
                rt.anchorMin = new Vector2(0, 0.5f);
                rt.anchorMax = new Vector2(1, 1);
            }
            else
            {
                // 勝者が敵 = 敗者は自分（画面下部）
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 0.5f);
            }
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            // 赤フラッシュアニメーション（3回激しく点滅）
            for (int i = 0; i < 3; i++)
            {
                float t = 0;
                float flashIn = 0.05f;
                float flashOut = 0.1f;
                
                while(t < flashIn && img != null)
                {
                    t += Time.deltaTime;
                    img.color = new Color(1f, 0f, 0f, (t/flashIn) * 0.8f);
                    yield return null;
                }
                
                t = 0;
                while(t < flashOut && img != null)
                {
                    t += Time.deltaTime;
                    img.color = new Color(1f, 0f, 0f, 0.8f - (t/flashOut) * 0.8f);
                    yield return null;
                }
            }

            if (damageContainer != null) Destroy(damageContainer);
        }
    }
}
