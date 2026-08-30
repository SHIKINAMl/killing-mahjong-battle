using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public partial class RonAnimationUI
    {
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
            //
            // **内訳（settlement）を持たない経路はもう無い。** 本編は
            // `GameUIPhaseController` がサーバーの liquidation から、チュートリアルは
            // `TutorialManager` が台本から、どちらも必ず組んで渡す（2026-08-29）。
            // ここへ null で来るのは **liquidation が欠けた異常時だけ**なので、
            // 旧来の「式 → ランク → 巨大な数字」は丸ごと消した。
            //
            // **一緒に消したもの**: FormulaText / RankText / 巨大スコア / 血しぶき /
            // キラキラ星（PlayWinnerSparkleEffect）/ 赤フラッシュ（PlayLoserDamageEffect）/
            // 中央から勝者へ吸い込むパーティクル（SpawnAbsorbParticles）。
            // **キラキラと赤フラッシュは 2026-08-20 の演出削減バッチ1で既に呼び出しが
            // コメントアウトされており、以後一度も画面に出ていない。**
            if (settlement == null)
            {
                // **HPだけは必ず最終値に合わせる。** ここで抜けて放置すると
                // サーバーの結果と画面がずれたまま次の局へ進む
                Debug.LogWarning("[RonAnimationUI] 内訳の無いロン。パネルを出さずHPだけ最終値に合わせる");
                Destroy(container);
                if (playerInfo != null) playerInfo.SetHP(newLocalHp);
                if (enemyInfo != null) enemyInfo.SetHP(newEnemyHp);
                yield return new WaitForSeconds(0.2f);
                onComplete?.Invoke();
                yield break;
            }

            yield return SettlementRoutine(containerRt, container, settlement,
                playerInfo, enemyInfo, prevLocalHp, newLocalHp, prevEnemyHp, newEnemyHp);

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
    }
}
