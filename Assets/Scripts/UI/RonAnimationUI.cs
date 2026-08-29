using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public class RonAnimationUI : MonoBehaviour
    {
        [Header("Cinematic Assets")]
        [SerializeField] private Sprite bloodSplatterSprite;

        [Header("ドット血しぶき（レトロ演出）")]
        [Tooltip("ロン時の血しぶきをドット絵風にする。切るとスプライト1枚の従来演出だけになる")]
        [SerializeField] private bool usePixelBlood = true;
        [Tooltip("飛ばすドットの数")]
        [SerializeField] private int pixelBloodDotCount = 70;
        [Tooltip("座標を丸めるグリッド幅(px)。大きいほど粗くレトロになる")]
        [SerializeField] private float pixelBloodGridSize = 6f;
        [Tooltip("従来のスプライト血しぶきも一緒に出すか")]
        [SerializeField] private bool keepSpriteSplatter = false;
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

        /// <param name="formula">「6飜」のような飜数の文字列。表示には使わず、安手かどうかの判定に使う</param>
        /// <param name="scoreFormula">
        /// 「200 × 1.5」のような計算式。**サーバーの liquidation から作って渡す。**
        /// 渡さなかった場合は式を出さず、獲得額だけを見せる。
        /// </param>
        /// <param name="settlement">
        /// 清算パネルの内容。**渡すと「式 → ランク → 巨大な数字」の代わりに1枚のパネルを出す。**
        /// null なら従来の見せ方に落ちる（チュートリアルなど内訳を持たない経路のため）。
        /// </param>
        public void PlayRonSequence(List<int> handTiles, int ronTile, List<string> yakuList, string formula, string rankName, int score, bool isLocalPlayerWin,
            PlayerInfoUI playerInfo, EnemyInfoUI enemyInfo, int prevLocalHp, int newLocalHp, int prevEnemyHp, int newEnemyHp, System.Action onComplete,
            string scoreFormula = null, RonSettlementInfo settlement = null)
        {
            StartCoroutine(SequenceRoutine(handTiles, ronTile, yakuList, formula, rankName, score, isLocalPlayerWin, playerInfo, enemyInfo, prevLocalHp, newLocalHp, prevEnemyHp, newEnemyHp, onComplete, scoreFormula, settlement));
        }

        private IEnumerator SequenceRoutine(List<int> handTiles, int ronTile, List<string> yakuList, string formula, string rankName, int score, bool isLocalPlayerWin,
            PlayerInfoUI playerInfo, EnemyInfoUI enemyInfo, int prevLocalHp, int newLocalHp, int prevEnemyHp, int newEnemyHp, System.Action onComplete,
            string scoreFormula, RonSettlementInfo settlement = null)
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
                // カットインと同時に「ロン！」ボイスを再生
                if (KillingMahjong.Managers.AudioManager.Instance != null)
                {
                    KillingMahjong.Managers.AudioManager.Instance.PlayRonVoice();
                }

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
            containerCanvas.sortingOrder = UISortingOrders.RonAnimation;
            container.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // 2. 暗転ディマー（背景のスマホ等が見えるように少し薄めに）
            GameObject dimmer = new GameObject("Dimmer");
            dimmer.transform.SetParent(containerRt, false);
            Image dimmerImg = dimmer.AddComponent<Image>();
            // **カットインの黒幕(α0.7)の直後にもう1枚重なるため、濃いと「暗転が明けない」感覚になる。**
            // 盤面が透けるくらいに落として、対局が続いていることを見せる（2026-08-20 の演出削減バッチ1）。
            dimmerImg.color = new Color(0, 0, 0, 0.35f);
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
            yakuText.fontSizeMin = KillingMahjong.Common.UITypography.BodySmall;
            yakuText.fontSizeMax = KillingMahjong.Common.UITypography.BodyLarge;
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
                // 牌IDは 0 始まり（0 = 一萬）なので、0 を「無し」と誤判定しないこと。
                // 無効値は -1 で表される。
                if (ronTile >= 0)
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
                
                // 役名ボイスを再生（「タンヤオ」「ピンフ」等）
                if (KillingMahjong.Managers.AudioManager.Instance != null)
                {
                    KillingMahjong.Managers.AudioManager.Instance.PlayYakuVoice(yaku);
                }

                // 役を1つ表示するごとの間隔（ボイスの長さに合わせて少し延長）
                yield return new WaitForSeconds(0.6f);
            }
            
            // --- ここから先が「点数の説明」 ---
            //
            // **役の宣言（上のカットインと帯）はそのまま残す。** 置き換えたのはこの後ろだけ。
            // 内訳が渡ってきていれば清算パネルへ、渡ってきていなければ従来の
            // 「式 → ランク → 巨大な数字」へ落ちる（チュートリアルなどはこちらを通る）。
            if (settlement != null)
            {
                yield return SettlementRoutine(containerRt, container, settlement,
                    playerInfo, enemyInfo, prevLocalHp, newLocalHp, prevEnemyHp, newEnemyHp);

                onComplete?.Invoke();
                yield break;
            }

            // --- タメ（ここで役と手牌をしっかり見せる） ---
            yield return new WaitForSeconds(1.0f);

            // 計算式の表示
            GameObject formulaTextObj = new GameObject("FormulaText");
            formulaTextObj.transform.SetParent(containerRt, false);
            TextMeshProUGUI formulaText = formulaTextObj.AddComponent<TextMeshProUGUI>();
            if (customFont != null) formulaText.font = customFont;

            // サーバーの liquidation から作った式を出す。
            // かつては内訳が来ておらず「??? × ??? = 額」と伏せていたが、
            // 現在は winner_bet / multiplier が届くので実際の値を出せる。
            // 渡されなかったときだけ、式を伏せて額だけ見せる
            // **scoreFormula は完成した文字列。ここで答えを足さないこと。**
            // 以前は "{式} = {score}" と連結していたが、強襲を撃った局は獲得が 0 に潰されるので
            // 「5000 × 1 = 0」という嘘の式が出ていた。式の組み立ては
            // GameUIPhaseController.BuildScoreFormula に一本化してある
            formulaText.text = string.IsNullOrEmpty(scoreFormula) ? $"{score}" : scoreFormula;
            formulaText.color = new Color(1f, 1f, 0.5f); // 薄い黄色
            formulaText.fontSize = KillingMahjong.Common.UITypography.Header; 
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
            rankTextUI.fontSize = KillingMahjong.Common.UITypography.Huge; 
            rankTextUI.alignment = TextAlignmentOptions.Center;
            rankTextUI.fontStyle = FontStyles.Bold;
            rankTextUI.outlineWidth = 0.2f;
            rankTextUI.outlineColor = new Color32(0, 0, 0, 255); // 黒フチ
            
            RectTransform rankTextRt = rankTextObj.GetComponent<RectTransform>();
            rankTextRt.anchorMin = new Vector2(0.5f, 0.5f);
            rankTextRt.anchorMax = new Vector2(0.5f, 0.5f);
            rankTextRt.sizeDelta = new Vector2(1000, 300);
            rankTextRt.anchoredPosition = Vector2.zero; // 画面中央

            // ランクボイスを再生（「満貫！」「跳満！」等）
            if (KillingMahjong.Managers.AudioManager.Instance != null)
            {
                KillingMahjong.Managers.AudioManager.Instance.PlayRankVoice(rankName);
            }

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

            // ドット絵の血しぶき。スコアが落ちてくるのと同じ瞬間に飛ばす
            if (usePixelBlood)
            {
                KillingMahjong.Visuals.PixelBloodEffect.Play(
                    containerRt, new Vector2(0, 150), pixelBloodDotCount, pixelBloodGridSize);
            }

            GameObject splatterObj = null;
            if (bloodSplatterSprite != null && keepSpriteSplatter)
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
            scoreText.fontSize = KillingMahjong.Common.UITypography.Giant; // 巨大文字
            scoreText.alignment = TextAlignmentOptions.Center;
            scoreText.fontStyle = FontStyles.Bold;
            
            // 赤字は血飛沫と同系色で埋もれるため、黒フチで縁取る（役ランク表示と揃える）
            scoreText.outlineWidth = 0.2f;
            scoreText.outlineColor = new Color32(0, 0, 0, 255);
            
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
            // **キラキラ星と赤フラッシュは止めた（2026-08-20 の演出削減バッチ1）。**
            // 決着後は HPポップアップ・HPメーター・ゲージ吸い込みが同じ「勝敗」を既に伝えており、
            // 星20個と半画面の赤点滅3回は情報を足さないまま画面を埋めていた。
            // メソッド本体は残してあるので、戻すならこの2行のコメントを外すだけでよい。
            // PlayWinnerSparkleEffect(isLocalPlayerWin);
            // PlayLoserDamageEffect(isLocalPlayerWin);

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

        // ============================================================
        //  清算パネル
        //
        //  考え方は「**枠を先に完成させ、中の数字だけ後から入れる**」。
        //  以前は 式 → ランク → 巨大な数字 を順に「生成」していたので、後から出た大きい文字が
        //  先の文字を物理的に覆い、**4つが揃った瞬間には役名がもう読めなかった**
        //  （2026-08-27 の計測で、因果を目で追える時間は 0 秒）。
        //  枠が動かなければ、覆う問題そのものが起きない。
        // ============================================================

        private static readonly Color PanelLine  = new Color32(0x3A, 0x44, 0x68, 0xFF);
        private static readonly Color PanelBg    = new Color32(0x0F, 0x13, 0x26, 0xF2);
        private static readonly Color PanelInk   = new Color32(0xE8, 0xE4, 0xF0, 0xFF);
        private static readonly Color PanelFaint = new Color32(0x97, 0xA0, 0xC0, 0xFF);
        private static readonly Color AccentGold = new Color32(0xFF, 0xD3, 0x4D, 0xFF); // 翻数
        private static readonly Color AccentMine = new Color32(0x57, 0xC7, 0xE8, 0xFF); // 自分
        private static readonly Color AccentThem = new Color32(0xF2, 0x70, 0x5A, 0xFF); // 相手

        private const float PanelWidth = 560f;
        private const float ValueColumn = 150f;

        private IEnumerator SettlementRoutine(RectTransform containerRt, GameObject container, RonSettlementInfo s,
            PlayerInfoUI playerInfo, EnemyInfoUI enemyInfo, int prevLocalHp, int newLocalHp, int prevEnemyHp, int newEnemyHp)
        {
            // 役の帯は役目を終えている。パネルが同じ役名を翻数つきで出し直すので、
            // 情報を落とさずに場所を空けられる。**宣言そのもの（1つずつ出る所）は上でやり終えている。**
            var ribbon = containerRt.Find("YakuRibbon");
            CanvasGroup ribbonGroup = null;
            if (ribbon != null)
            {
                ribbonGroup = ribbon.gameObject.GetComponent<CanvasGroup>();
                if (ribbonGroup == null) ribbonGroup = ribbon.gameObject.AddComponent<CanvasGroup>();
            }

            var hanTexts = new List<TextMeshProUGUI>();
            TextMeshProUGUI totalHanText, multiplierText;
            TextMeshProUGUI myBetText, theirBetText, myMultText, theirMultText;
            TextMeshProUGUI tankiMine, tankiTheirs;
            TextMeshProUGUI myDeltaText, theirDeltaText, myHpText, theirHpText;

            CanvasGroup panelGroup = BuildSettlementPanel(containerRt, s,
                hanTexts, out totalHanText, out multiplierText,
                out myBetText, out theirBetText, out myMultText, out theirMultText,
                out tankiMine, out tankiTheirs,
                out myDeltaText, out theirDeltaText, out myHpText, out theirHpText);

            // 枠がフェードインする。ここではまだ数字は入っていない
            const float fadeIn = 0.3f;
            for (float t = 0; t < fadeIn; t += Time.deltaTime)
            {
                float p = t / fadeIn;
                panelGroup.alpha = p;
                if (ribbonGroup != null) ribbonGroup.alpha = 1f - p;
                yield return null;
            }
            panelGroup.alpha = 1f;
            if (ribbonGroup != null) ribbonGroup.alpha = 0f;

            // ① 役ごとの翻数が上から入る
            for (int i = 0; i < hanTexts.Count && i < s.Rows.Count; i++)
            {
                // **`飜`(U+98BB) は PixelMplus に入っていない。** 使うと □ になる。
                // ゲームの他の表示テキスト（AbilityUI・チュートリアル）は `翻`(U+7FFB) を使っているので揃える。
                // コード中のコメントや Tooltip には `飜` が残っているが、あれは画面に出ない。
                hanTexts[i].text = s.ShowPerRowHan ? $"{s.Rows[i].Han}翻" : "";
                yield return new WaitForSeconds(0.12f);
            }

            // ② 合計と倍率
            totalHanText.text = $"{s.TotalHan}翻";
            yield return new WaitForSeconds(0.25f);
            multiplierText.text = "×" + s.Multiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            yield return new WaitForSeconds(0.4f);

            // ③ 素点と倍率が左右に入る
            string multLabel = "×" + s.Multiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            myBetText.text = s.MyBet.ToString();
            theirBetText.text = s.TheirBet.ToString();
            yield return new WaitForSeconds(0.18f);
            myMultText.text = multLabel;
            theirMultText.text = multLabel;
            yield return new WaitForSeconds(0.18f);

            // 単騎で倍になるのは負けた側だけ。**今まで画面のどこにも出ていなかった行。**
            if (s.IsTankiWait && tankiMine != null && tankiTheirs != null)
            {
                // **ダッシュ `—`(U+2014) もフォントに無い。** ASCII のハイフンで代用する
                tankiMine.text = s.LocalWon ? "-" : "×2";
                tankiTheirs.text = s.LocalWon ? "×2" : "-";
                yield return new WaitForSeconds(0.25f);
            }

            // ④ 表を読み切る間。**パネルはこのあと消える**ので、ここが表を見られる最後の時間。
            //
            // **`myDeltaText` / `theirDeltaText` / `myHpText` / `theirHpText` には何も入れない。**
            // 血の増減はこの下の演出が答えとして出すもので、先にパネルへ書くと山が消える。
            // 行そのものも `BuildSettlementPanel` で非アクティブにしてある（out は署名維持のために残している）。
            if (KillingMahjong.Managers.AudioManager.Instance != null)
            {
                KillingMahjong.Managers.AudioManager.Instance.PlayRankVoice(s.RankName);
            }

            yield return new WaitForSeconds(0.8f);

            // ⑤ 血が動く。**パネルを消しながら**素点の数字を持ち出す
            yield return BloodTransferRoutine(container, s, myBetText, theirBetText,
                playerInfo, enemyInfo, prevLocalHp, newLocalHp, prevEnemyHp, newEnemyHp);
        }

        private static string FormatDelta(int v)
        {
            return v > 0 ? "+" + v : v.ToString(); // 負号は int の表記がそのまま使える
        }

        // ============================================================
        //  血が動く瞬間
        //
        //  2026-08-29 の指示（**それぞれ非対称な表示方法で**）:
        //   ① 素点の数字が画面中央へ、大きくなりながら移動する
        //   ② 「満貫」表示くらいの大きさになったら、数値が変化する
        //   ③ 変化した数値がHPへ向かって収縮しながら移動し、HPの数値が動く
        //   ④ 同時に、自分と相手のHPの隣にこの局の増減を出す
        //
        //  **飛ぶのは片側だけ。これが「非対称」の中身であり、同時に嘘を防いでいる。**
        //  勝者の獲得と敗者の損失は母数が違う（単騎なら負けた側だけ2倍、強襲なら勝者は0）ので、
        //  両側から数字を飛ばすと「血が相手から自分へ移った」ように見えてしまう。
        //  旧経路の SpawnAbsorbParticles（中央 → 勝者）がまさにその絵で、新経路には持ってきていない。
        //
        //  尺: 離陸0.35 → 変化0.25 → 静止0.25 → 着弾0.30 → HP0.80 → 静止0.50 ＝ 約2.45秒。
        //  **パネルを読ませる 0.8秒（SettlementRoutine 側）を別に取ってある。**
        //  最後の静止は 2026-08-29 の実機確認のあと 1.00 → 0.50 に詰めた（見ていて一番余っていた場所）。
        //  **中央の静止 0.25 は逆に短いくらいなので、削るならここではない。**
        // ============================================================

        /// <summary>
        /// 中央で止まるときの文字の大きさ。**ゲーム内の「満貫」表示と同じ寸法にする**という指示。
        /// 拡大は fontSize ではなく localScale でやる（毎フレーム fontSize を動かすと再レイアウトが走る）ので、
        /// 文字は最初からこの大きさで作って縮めた状態から始める。
        /// </summary>
        private const float BloodPeakFontSize = UITypography.Huge;

        /// <summary>着弾したときの大きさ。HPの数字と同じくらいに収める。</summary>
        private const float BloodLandFontSize = 28f;

        private IEnumerator BloodTransferRoutine(GameObject panelContainer, RonSettlementInfo s,
            TextMeshProUGUI myBetText, TextMeshProUGUI theirBetText,
            PlayerInfoUI playerInfo, EnemyInfoUI enemyInfo,
            int prevLocalHp, int newLocalHp, int prevEnemyHp, int newEnemyHp)
        {
            // どちらの数字を飛ばすか。通常は自分。
            // **強襲で自分の獲得が 0 に潰れた局だけ相手側を飛ばす。**
            // 0 を満貫サイズまで拡大しても何も伝わらないし、強襲が何をしたのかは
            // いま画面のどこにも動きで出ていない（2026-08-29 に判断を仰いで決めた）。
            bool flyMine = s.MyDelta != 0 || s.TheirDelta == 0;

            TextMeshProUGUI source = flyMine ? myBetText : theirBetText;
            int fromValue = flyMine ? s.MyBet : s.TheirBet;
            int toValue = flyMine ? s.MyDelta : s.TheirDelta;
            Color tint = flyMine ? AccentMine : AccentThem;

            RectTransform target = null;
            if (flyMine) { if (playerInfo != null) target = playerInfo.HpAnchor; }
            else { if (enemyInfo != null) target = enemyInfo.HpAnchor; }

            // 飛ばす元か先が取れないときは演出を諦める。
            // **ただしHPは必ず最終値に合わせる。** ここで抜けるとサーバーの結果と画面がずれる
            if (source == null || target == null)
            {
                Debug.LogWarning("[RonAnimationUI] 血の移動を省略した（起点か着弾点が無い）。HPは最終値に合わせる");
                if (panelContainer != null) Destroy(panelContainer);
                if (playerInfo != null) playerInfo.SetHP(newLocalHp);
                if (enemyInfo != null) enemyInfo.SetHP(newEnemyHp);
                yield return new WaitForSeconds(0.2f);
                yield break;
            }

            // 浮き数字を止める。**着弾点に増減ラベルを自分で出すので、重なると額が同じぶんかえって読めない。**
            // SEもここで止まるので、下で鳴らし直す（ずらして鳴らすのが目的でもある）
            if (playerInfo != null) playerInfo.SuppressHpPopup = true;
            if (enemyInfo != null) enemyInfo.SuppressHpPopup = true;

            // パネルが消えても残る入れ物。**ディマーは置かない**（盤面を見せたまま血を動かす）
            GameObject stage = new GameObject("BloodTransferStage");
            stage.transform.SetParent(transform, false);
            stage.transform.SetAsLastSibling();
            RectTransform stageRt = stage.AddComponent<RectTransform>();
            stageRt.anchorMin = Vector2.zero;
            stageRt.anchorMax = Vector2.one;
            stageRt.sizeDelta = Vector2.zero;
            Canvas stageCanvas = stage.AddComponent<Canvas>();
            stageCanvas.overrideSorting = true;
            stageCanvas.sortingOrder = UISortingOrders.RonAnimation;

            Vector3 startPos = source.rectTransform.position;
            Vector3 centerPos = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
            Vector3 endPos = AnchorCenter(target);

            float startScale = Mathf.Max(0.05f, source.fontSize / BloodPeakFontSize);
            float landScale = BloodLandFontSize / BloodPeakFontSize;

            // 飛ぶ数字。**素点の複製**なので、元の行は最後まで消えない（パネルごと消えるだけ）
            GameObject flyObj = new GameObject("BloodValue");
            flyObj.transform.SetParent(stageRt, false);
            TextMeshProUGUI fly = flyObj.AddComponent<TextMeshProUGUI>();
            fly.text = fromValue.ToString();
            fly.color = tint;
            fly.fontSize = BloodPeakFontSize;
            fly.alignment = TextAlignmentOptions.Center;
            fly.fontStyle = FontStyles.Bold;
            fly.textWrappingMode = TextWrappingModes.NoWrap;
            // 盤面（赤い壁・緑の卓）の上を通るので、縁取りが無いと途中で読めなくなる
            fly.outlineWidth = 0.2f;
            fly.outlineColor = new Color32(0, 0, 0, 255);
            RectTransform flyRt = flyObj.GetComponent<RectTransform>();
            flyRt.sizeDelta = new Vector2(1000f, 260f);
            flyRt.position = startPos;
            flyRt.localScale = Vector3.one * startScale;

            // 何が掛かってこの数字になったのか。**大きい数字の下に小さく出す**（8/29 に許可を取った）。
            // 数字の子にしてあるので、拡大・収縮も一緒に付いてくる
            GameObject noteObj = new GameObject("BloodNote");
            noteObj.transform.SetParent(flyRt, false);
            TextMeshProUGUI note = noteObj.AddComponent<TextMeshProUGUI>();
            note.text = BuildMultiplierNote(s, toValue);
            note.color = AccentGold;
            note.fontSize = 34f;
            note.alignment = TextAlignmentOptions.Center;
            note.fontStyle = FontStyles.Bold;
            note.textWrappingMode = TextWrappingModes.NoWrap;
            note.outlineWidth = 0.2f;
            note.outlineColor = new Color32(0, 0, 0, 255);
            note.alpha = 0f; // 数値が変わる瞬間まで出さない
            RectTransform noteRt = noteObj.GetComponent<RectTransform>();
            noteRt.anchorMin = new Vector2(0.5f, 0f);
            noteRt.anchorMax = new Vector2(0.5f, 0f);
            noteRt.pivot = new Vector2(0.5f, 1f);
            noteRt.sizeDelta = new Vector2(700f, 50f);
            noteRt.anchoredPosition = new Vector2(0f, 4f);

            // 清算パネルを丸ごと消しにかかる。**パネルだけ消して手牌や暗幕が残ると、
            // 一拍おいてから全部が同時に消えることになって目立つ**ので、コンテナごと1枚で落とす
            CanvasGroup containerGroup = null;
            if (panelContainer != null)
            {
                containerGroup = panelContainer.GetComponent<CanvasGroup>();
                if (containerGroup == null) containerGroup = panelContainer.AddComponent<CanvasGroup>();
            }

            // ① 離陸 → 画面中央。パネルは同じ時間で消える
            const float riseTime = 0.35f;
            for (float t = 0; t < riseTime; t += Time.deltaTime)
            {
                float p = Mathf.Clamp01(t / riseTime);
                float eased = 1f - Mathf.Pow(1f - p, 3f);
                flyRt.position = Vector3.Lerp(startPos, centerPos, eased);
                flyRt.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, eased);
                if (containerGroup != null) containerGroup.alpha = 1f - p;
                yield return null;
            }
            flyRt.position = centerPos;
            flyRt.localScale = Vector3.one;
            if (panelContainer != null) Destroy(panelContainer);

            // ② 数値が変わる。**潰れきった瞬間に入れ替える**ので、途中の混ざった数字が読めてしまわない
            string landed = FormatDelta(toValue);
            bool swapped = false;
            const float popTime = 0.25f;
            for (float t = 0; t < popTime; t += Time.deltaTime)
            {
                float p = Mathf.Clamp01(t / popTime);
                if (p < 0.5f)
                {
                    float q = p / 0.5f;
                    flyRt.localScale = new Vector3(Mathf.Lerp(1f, 1.25f, q), Mathf.Lerp(1f, 0.2f, q), 1f);
                }
                else
                {
                    if (!swapped)
                    {
                        swapped = true;
                        fly.text = landed;
                        note.alpha = 1f;
                        if (KillingMahjong.Managers.AudioManager.Instance != null)
                        {
                            // コインを置くような二段の決定音。**額が確定した音として借りている**
                            KillingMahjong.Managers.AudioManager.Instance.PlayBetConfirmSE();
                        }
                    }
                    float q = (p - 0.5f) / 0.5f;
                    float e = 1f - Mathf.Pow(1f - q, 3f);
                    flyRt.localScale = new Vector3(Mathf.Lerp(1.25f, 1f, e), Mathf.Lerp(0.2f, 1f, e), 1f);
                }
                yield return null;
            }
            fly.text = landed;
            note.alpha = 1f;
            flyRt.localScale = Vector3.one;

            // 満貫サイズのまま静止。**ここが読ませる時間**
            yield return new WaitForSeconds(0.25f);

            // ③ 収縮しながらHPへ落ちる
            //
            // **落ちながら白へ寄せる（2026-08-29 の実機確認で決めた）。**
            // 着弾点は HpAnchor の真ん中、つまり自分ならスマホの画面、相手なら血袋の中。
            // **飛ぶ数字の色は自分が #57C7E8、相手が #F2705A で、どちらも着地先とほぼ同じ色**なので、
            // 素の色のまま落とすと最後の数桁が背景に溶けて、いちばん見せたい着弾が読めなくなる。
            // 黒縁を太くするのは**ドット絵の細い字には効かない**ので採らなかった。
            const float diveTime = 0.30f;
            for (float t = 0; t < diveTime; t += Time.deltaTime)
            {
                float p = Mathf.Clamp01(t / diveTime);
                float eased = p * p; // 落ちるほど速く
                flyRt.position = Vector3.Lerp(centerPos, endPos, eased);
                flyRt.localScale = Vector3.one * Mathf.Lerp(1f, landScale, eased);
                // **着弾より少し手前で白まで振り切る。** 背景に重なるのは終盤なので、
                // 最後の一瞬で切り替えると間に合わない
                fly.color = Color.Lerp(tint, Color.white, Mathf.Clamp01(p * 1.4f));
                note.alpha = 1f - p; // 注記は途中で用済み
                yield return null;
            }
            Destroy(flyObj);

            // ④ 着弾。**両方のHPの隣に増減が出て、両方のメーターが同時に動き出す**
            if (playerInfo != null) SpawnHpDeltaLabel(stageRt, playerInfo.HpAnchor, s.MyDelta, AccentMine, placeLeft: true);
            if (enemyInfo != null) SpawnHpDeltaLabel(stageRt, enemyInfo.HpAnchor, s.TheirDelta, AccentThem, placeLeft: false);

            PlayBloodSE(isLocalSide: true, delta: s.MyDelta, newHp: newLocalHp,
                        maxHp: playerInfo != null ? playerInfo.MaxHp : 0);

            const float hpTime = 0.8f;
            bool enemySePlayed = false;
            for (float t = 0; t < hpTime; t += Time.deltaTime)
            {
                float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / hpTime), 3f);
                if (playerInfo != null) playerInfo.SetHP(Mathf.RoundToInt(Mathf.Lerp(prevLocalHp, newLocalHp, eased)));
                if (enemyInfo != null) enemyInfo.SetHP(Mathf.RoundToInt(Mathf.Lerp(prevEnemyHp, newEnemyHp, eased)));

                // **PlayDamageSE と PlayHitSE はHPの残量でピッチが変わる作りなのに、
                // 今まで同時に鳴って潰し合っていた。** 少しずらすだけで聞き分けられる
                if (!enemySePlayed && t >= 0.12f)
                {
                    enemySePlayed = true;
                    PlayBloodSE(isLocalSide: false, delta: s.TheirDelta, newHp: newEnemyHp,
                                maxHp: enemyInfo != null ? enemyInfo.MaxHp : 0);
                }
                yield return null;
            }
            if (!enemySePlayed)
            {
                PlayBloodSE(isLocalSide: false, delta: s.TheirDelta, newHp: newEnemyHp,
                            maxHp: enemyInfo != null ? enemyInfo.MaxHp : 0);
            }

            if (playerInfo != null) playerInfo.SetHP(newLocalHp);
            if (enemyInfo != null) enemyInfo.SetHP(newEnemyHp);

            // 読み切るための間。**実機で見て一番余っていたので 1.00 → 0.50 に詰めた（8/29）**
            yield return new WaitForSeconds(0.5f);

            if (playerInfo != null) playerInfo.SuppressHpPopup = false;
            if (enemyInfo != null) enemyInfo.SuppressHpPopup = false;
            Destroy(stage);
            yield return new WaitForSeconds(0.2f);
        }

        /// <summary>
        /// 素点がこの額になった理由。**倍率・単騎の2倍・強襲の上乗せを、掛かった順に並べる。**
        ///
        /// 単騎の2倍と強襲の上乗せが乗るのは<strong>負けた側だけ</strong>なので、
        /// 飛んでいる側が負けている（額が負）ときにだけ足す。
        /// **全角の `＋`(U+FF0B) はフォントに無い。** ASCII の `+` を使うこと（§4 の欠字表）。
        /// </summary>
        private static string BuildMultiplierNote(RonSettlementInfo s, int flownDelta)
        {
            string note = "×" + s.Multiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            bool flownIsLoser = flownDelta < 0;
            if (s.IsTankiWait && flownIsLoser) note += " ×2";
            if (s.AssaultApplied && flownIsLoser) note += " +強襲";
            return note;
        }

        /// <summary>
        /// HPの隣に出すこの局の増減。**「隣」は画面の内側**（自分＝右のスマホなのでその左、相手＝左の血袋なのでその右）。
        /// 上に出すと、いま止めた <c>HpPopupPresenter</c> の浮き数字と同じ場所になってしまう。
        /// </summary>
        private TextMeshProUGUI SpawnHpDeltaLabel(RectTransform parent, RectTransform anchor, int delta, Color tint, bool placeLeft)
        {
            if (anchor == null) return null;

            GameObject go = new GameObject("HpDelta");
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.text = FormatDelta(delta);
            text.color = tint;
            text.fontSize = 30f;
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = placeLeft ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 255);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(280f, 44f);
            // **ピボットを文字が寄る側の端に置く。** 既定の中心(0.5)のままだと `rt.position` が箱の中心になり、
            // 右寄せの文字は箱の半分ぶん外側へずれてスマホや血袋に重なる。
            // **しかも sizeDelta はキャンバス単位・position は画面ピクセルなので、
            // 解像度によってずれ方が変わる。机上では気付けない類のずれ。**
            rt.pivot = new Vector2(placeLeft ? 1f : 0f, 0.5f);

            Vector3[] corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            Vector3 center = (corners[0] + corners[2]) * 0.5f;
            float halfWidth = (corners[2].x - corners[0].x) * 0.5f;
            // HPの絵に食い込まないぶんだけ内側へ逃がす（画面ピクセル）。
            // **24 では足りなかった（2026-08-29 の実機確認）。** ここで基準にしている HpAnchor は
            // スマホの中の `HPPanel`（x 669..759）で、**スマホの外枠はそこから 24px ほど外へ出ている。**
            // 逃がした量と枠までの距離がちょうど同じで、実質の隙間が 0 になっていた。
            const float gap = 48f;
            center.x += placeLeft ? -(halfWidth + gap) : (halfWidth + gap);
            rt.position = center;

            StartCoroutine(HpDeltaLabelRoutine(rt, text));
            return text;
        }

        /// <summary>増減ラベルの出方。ふわっと出して、少しだけ浮かせる。</summary>
        private IEnumerator HpDeltaLabelRoutine(RectTransform rt, TextMeshProUGUI text)
        {
            Vector3 basePos = rt.position;
            const float appear = 0.18f;
            for (float t = 0; t < appear; t += Time.deltaTime)
            {
                if (rt == null) yield break;
                float p = Mathf.Clamp01(t / appear);
                text.alpha = p;
                rt.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, 1f - Mathf.Pow(1f - p, 3f));
                rt.position = basePos + new Vector3(0f, Mathf.Lerp(-10f, 0f, p), 0f);
                yield return null;
            }
            if (rt == null) yield break;
            text.alpha = 1f;
            rt.localScale = Vector3.one;
            rt.position = basePos;
        }

        /// <summary>
        /// 血が動く音。**元は <c>HpPopupPresenter.PlaySound</c> が鳴らしていたもの**で、
        /// 浮き数字ごと止めたぶんをここで鳴らし直している。自分側は被弾音、相手側は打撃音。
        /// </summary>
        private static void PlayBloodSE(bool isLocalSide, int delta, int newHp, int maxHp)
        {
            if (delta == 0) return;
            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio == null) return;

            if (delta > 0) { audio.PlayHealSE(); return; }

            float ratio = maxHp > 0 ? (float)newHp / maxHp : 1f;
            if (isLocalSide) audio.PlayDamageSE(ratio);
            else audio.PlayHitSE(ratio);
        }

        /// <summary>RectTransform の中心をワールド座標で返す。**サイズが 0 の空オブジェクトでも中心が取れる。**</summary>
        private static Vector3 AnchorCenter(RectTransform rt)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
        }

        private CanvasGroup BuildSettlementPanel(RectTransform parent, RonSettlementInfo s,
            List<TextMeshProUGUI> hanTexts,
            out TextMeshProUGUI totalHanText, out TextMeshProUGUI multiplierText,
            out TextMeshProUGUI myBetText, out TextMeshProUGUI theirBetText,
            out TextMeshProUGUI myMultText, out TextMeshProUGUI theirMultText,
            out TextMeshProUGUI tankiMine, out TextMeshProUGUI tankiTheirs,
            out TextMeshProUGUI myDeltaText, out TextMeshProUGUI theirDeltaText,
            out TextMeshProUGUI myHpText, out TextMeshProUGUI theirHpText)
        {
            // 外枠（線の色）→ 内側（地の色）の2枚重ね。1ドット＝2UI単位なので枠は4
            GameObject root = new GameObject("SettlementPanel");
            root.transform.SetParent(parent, false);
            Image rootImg = root.AddComponent<Image>();
            rootImg.color = PanelLine;

            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            // **下端を固定して上へ伸ばす。** 役が増えても下（手牌）を押さない。
            // 基準 800x600 で手牌の上端は中心から -146 なので、その少し上に置く。
            // 上へ使えるのは中心から +300 まで。役5行でちょうど収まる寸法にしてある
            // （実機で -105 に置いたら見出しの帯が画面外に出た）。
            rootRt.pivot = new Vector2(0.5f, 0f);
            rootRt.anchoredPosition = new Vector2(0f, -140f);
            rootRt.sizeDelta = new Vector2(PanelWidth, 0f);

            var rootFit = root.AddComponent<ContentSizeFitter>();
            rootFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var rootLayout = root.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(4, 4, 4, 4);
            rootLayout.spacing = 0;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            // ---- 見出しの帯：左に「和了」、右にランク ----
            GameObject head = MakeBox(root.transform, PanelLine, 8, 4);
            MakeText(head.transform, "和了", 20f, PanelInk, TextAlignmentOptions.Left, true);
            MakeText(head.transform, s.RankName, 30f, AccentGold, TextAlignmentOptions.Right, false, 200f);

            // ---- 中身（地の色） ----
            GameObject body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<Image>().color = PanelBg;
            var bodyLayout = body.AddComponent<VerticalLayoutGroup>();
            bodyLayout.padding = new RectOffset(12, 12, 6, 6);
            bodyLayout.spacing = 1;
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = true;
            bodyLayout.childForceExpandHeight = false;

            // ---- 役の行 ----
            foreach (var row in s.Rows)
            {
                GameObject r = MakeBox(body.transform, Color.clear, 0, 1);
                MakeText(r.transform, row.Name, 22f, PanelInk, TextAlignmentOptions.Left, true);
                // 強化ぶんは黄色で独立させる。`断幺九+1` と地の文に埋めない
                MakeText(r.transform, row.Boost > 0 ? "+" + row.Boost : "", 18f, AccentGold, TextAlignmentOptions.Right, false, 70f);
                hanTexts.Add(MakeText(r.transform, "", 22f, PanelInk, TextAlignmentOptions.Right, false, 90f));
            }

            // ---- 合計 → 倍率 ----
            MakeRule(body.transform);
            GameObject sum = MakeBox(body.transform, Color.clear, 0, 2);
            MakeText(sum.transform, "合計", 22f, PanelFaint, TextAlignmentOptions.Left, true);
            totalHanText = MakeText(sum.transform, "", 24f, AccentGold, TextAlignmentOptions.Right, false, 90f);
            multiplierText = MakeText(sum.transform, "", 24f, AccentGold, TextAlignmentOptions.Right, false, 70f);

            // ---- 自分と相手の内訳 ----
            MakeRule(body.transform);

            GameObject colHead = MakeBox(body.transform, Color.clear, 0, 1);
            MakeText(colHead.transform, "", 18f, PanelFaint, TextAlignmentOptions.Left, true);
            MakeText(colHead.transform, "自分", 18f, AccentMine, TextAlignmentOptions.Right, false, ValueColumn);
            MakeText(colHead.transform, "相手", 18f, AccentThem, TextAlignmentOptions.Right, false, ValueColumn);

            // 持ち越しがあると素点が膨らむ。**その理由が今までどこにも出ていなかった**
            string betLabel = s.CarryRounds > 1 ? $"素点（持ち越し{s.CarryRounds}局ぶん）" : "素点";
            GameObject betRow = MakeBox(body.transform, Color.clear, 0, 1);
            MakeText(betRow.transform, betLabel, 18f, PanelFaint, TextAlignmentOptions.Left, true);
            myBetText = MakeText(betRow.transform, "", 21f, PanelInk, TextAlignmentOptions.Right, false, ValueColumn);
            theirBetText = MakeText(betRow.transform, "", 21f, PanelInk, TextAlignmentOptions.Right, false, ValueColumn);

            GameObject multRow = MakeBox(body.transform, Color.clear, 0, 1);
            MakeText(multRow.transform, "倍率", 18f, PanelFaint, TextAlignmentOptions.Left, true);
            myMultText = MakeText(multRow.transform, "", 21f, PanelInk, TextAlignmentOptions.Right, false, ValueColumn);
            theirMultText = MakeText(multRow.transform, "", 21f, PanelInk, TextAlignmentOptions.Right, false, ValueColumn);

            tankiMine = null;
            tankiTheirs = null;
            if (s.IsTankiWait)
            {
                GameObject tankiRow = MakeBox(body.transform, Color.clear, 0, 1);
                MakeText(tankiRow.transform, "単騎待ち", 18f, PanelFaint, TextAlignmentOptions.Left, true);
                tankiMine = MakeText(tankiRow.transform, "", 21f, AccentGold, TextAlignmentOptions.Right, false, ValueColumn);
                tankiTheirs = MakeText(tankiRow.transform, "", 21f, AccentGold, TextAlignmentOptions.Right, false, ValueColumn);
            }

            // 強襲は「獲得が 0 に潰れ、その分が相手への追加ダメージへ回る」。式ではなく文で見せる
            if (s.AssaultApplied)
            {
                GameObject assaultRow = MakeBox(body.transform, Color.clear, 0, 1);
                MakeText(assaultRow.transform, "強襲", 18f, PanelFaint, TextAlignmentOptions.Left, true);
                MakeText(assaultRow.transform, s.LocalWon ? "獲得なし" : "", 20f, AccentGold, TextAlignmentOptions.Right, false, ValueColumn);
                MakeText(assaultRow.transform, s.LocalWon ? "+" + s.AssaultBonusDamage : "獲得なし", 20f, AccentGold, TextAlignmentOptions.Right, false, ValueColumn);
            }

            MakeRule(body.transform);

            GameObject deltaRow = MakeBox(body.transform, Color.clear, 0, 2);
            MakeText(deltaRow.transform, "血", 22f, PanelInk, TextAlignmentOptions.Left, true);
            myDeltaText = MakeText(deltaRow.transform, "", 30f, AccentMine, TextAlignmentOptions.Right, false, ValueColumn);
            theirDeltaText = MakeText(deltaRow.transform, "", 30f, AccentThem, TextAlignmentOptions.Right, false, ValueColumn);

            GameObject hpRow = MakeBox(body.transform, Color.clear, 0, 1);
            MakeText(hpRow.transform, "", 18f, PanelFaint, TextAlignmentOptions.Left, true);
            myHpText = MakeText(hpRow.transform, "", 18f, PanelFaint, TextAlignmentOptions.Right, false, ValueColumn);
            theirHpText = MakeText(hpRow.transform, "", 18f, PanelFaint, TextAlignmentOptions.Right, false, ValueColumn);

            // **血とHPの2行は伏せておく。**（2026-08-29）
            // 血の増減はこのあとの「飛ぶ数字」が答えとして出すもので、パネルに先に書くと山が消える。
            // 行そのものを消さずに非アクティブにしているのは、
            // **out で返す4本の参照を維持したまま**（＝リフレクション経由の検証手順を壊さずに）
            // 見せ方だけ戻せるようにするため。戻すなら SetActive(true) の1行でよい。
            deltaRow.SetActive(false);
            hpRow.SetActive(false);

            return group;
        }

        /// <summary>横1列の入れ物。中身は左から順に並ぶ。</summary>
        private GameObject MakeBox(Transform parent, Color bg, int padX, int padY)
        {
            GameObject go = new GameObject("Row");
            go.transform.SetParent(parent, false);

            if (bg.a > 0f) go.AddComponent<Image>().color = bg;

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(padX, padX, padY, padY);
            layout.spacing = 6;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            return go;
        }

        /// <summary>区切りの細い線。1ドット＝2UI単位なので高さ2。</summary>
        private void MakeRule(Transform parent)
        {
            GameObject go = new GameObject("Rule");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = PanelLine;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 2f;
            le.preferredHeight = 2f;
        }

        /// <param name="flexible">true なら残りの幅を全部取る（左の見出し用）</param>
        /// <param name="width">flexible が false のときの固定幅</param>
        private TextMeshProUGUI MakeText(Transform parent, string content, float size, Color color,
            TextAlignmentOptions align, bool flexible, float width = 0f)
        {
            GameObject go = new GameObject("Text");
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (customFont != null) tmp.font = customFont;
            tmp.text = content;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = size * 1.15f;
            if (flexible)
            {
                le.flexibleWidth = 1f;
                le.minWidth = 0f;
            }
            else
            {
                le.flexibleWidth = 0f;
                le.preferredWidth = width;
                le.minWidth = width;
            }

            return tmp;
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
