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
    }
}
