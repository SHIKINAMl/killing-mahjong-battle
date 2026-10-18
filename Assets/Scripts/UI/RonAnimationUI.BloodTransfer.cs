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
            using (var scope = presentations.Begin())
                yield return scope.Run(BloodTransferVisualsRoutine(scope, panelContainer, s, myBetText, theirBetText,
                    playerInfo, enemyInfo, prevLocalHp, newLocalHp, prevEnemyHp, newEnemyHp));
        }

        private IEnumerator BloodTransferVisualsRoutine(PresentationScope scope, GameObject panelContainer, RonSettlementInfo s,
            TextMeshProUGUI myBetText, TextMeshProUGUI theirBetText, PlayerInfoUI playerInfo, EnemyInfoUI enemyInfo,
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
            if (playerInfo != null)
            {
                bool before = playerInfo.SuppressHpPopup;
                scope.AddCleanup(() => { if (playerInfo != null) playerInfo.SuppressHpPopup = before; });
                playerInfo.SuppressHpPopup = true;
            }
            if (enemyInfo != null)
            {
                bool before = enemyInfo.SuppressHpPopup;
                scope.AddCleanup(() => { if (enemyInfo != null) enemyInfo.SuppressHpPopup = before; });
                enemyInfo.SuppressHpPopup = true;
            }

            // パネルが消えても残る入れ物。**ディマーは置かない**（盤面を見せたまま血を動かす）
            GameObject stage = scope.Own(new GameObject("BloodTransferStage"));
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

            // **場に出ていた血を、卓の上の血だまりとして見せる（2026-10-18）。**
            // 「血を掛け合っている」ことを形で見せるため。数字の下、卓の真ん中に広がる。
            // 大きさは場の血の額で決める（両者の賭け金の合計。8000 で最大）
            var pool = Effects.BloodPoolEffect.Create(stageRt, TablePoolPosition());
            pool.transform.SetAsFirstSibling();
            pool.SetTarget(Mathf.Clamp(PoolAmountFor(s.MyBet + s.TheirBet), 0.25f, 1f));

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
            //
            // **数字と一緒に、血が勝った側の血袋へ流れ込む（2026-10-18）。**
            // 場の血だまりから勝った側へ。負けた側が賭け金より多く失うときは、その血袋からも勝った側へ流す。
            // メーターが動く間（④）も流し続ける
            var streams = StartBloodStreams(stageRt, pool, s, playerInfo, enemyInfo);
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
            // 横に出す増減は「画面の血が動く量」。賭けた血は先に場へ出しているので、
            // サーバーの増減とは違う（RonSettlementInfo.MyShownDelta）。動かない側（満貫で負けた側など）は出さない
            if (playerInfo != null && s.MyShownDelta != 0) SpawnHpDeltaLabel(stageRt, playerInfo.HpAnchor, s.MyShownDelta, AccentMine);
            if (enemyInfo != null && s.TheirShownDelta != 0) SpawnHpDeltaLabel(stageRt, enemyInfo.HpAnchor, s.TheirShownDelta, AccentThem);

            PlayBloodSE(isLocalSide: true, delta: s.MyShownDelta, newHp: newLocalHp,
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
                    PlayBloodSE(isLocalSide: false, delta: s.TheirShownDelta, newHp: newEnemyHp,
                                maxHp: enemyInfo != null ? enemyInfo.MaxHp : 0);
                }
                yield return null;
            }
            if (!enemySePlayed)
            {
                PlayBloodSE(isLocalSide: false, delta: s.TheirShownDelta, newHp: newEnemyHp,
                            maxHp: enemyInfo != null ? enemyInfo.MaxHp : 0);
            }

            if (playerInfo != null) playerInfo.SetHP(newLocalHp);
            if (enemyInfo != null) enemyInfo.SetHP(newEnemyHp);

            // 読み切るための間。**実機で見て一番余っていたので 1.00 → 0.50 に詰めた（8/29）**
            yield return new WaitForSeconds(0.5f);
            // 血の最後の一滴が届くまで待つ（入れ物ごと消すと途中で消える）
            foreach (var stream in streams)
                if (stream != null) yield return stream.WaitDone();

            if (playerInfo != null) playerInfo.SuppressHpPopup = false;
            if (enemyInfo != null) enemyInfo.SuppressHpPopup = false;
            Destroy(stage);
            yield return new WaitForSeconds(0.2f);
        }

        /// <summary>血が流れている長さ[秒]。数字が落ちる 0.30秒と、メーターが動く 0.8秒に合わせる。</summary>
        private const float BloodStreamSeconds = 1.0f;

        /// <summary>この額（両者の賭け金の合計）で、卓の血だまりがいちばん大きくなる。</summary>
        private const int PoolFullAmount = 8000;

        private static float PoolAmountFor(int pot) => Mathf.Clamp01(pot / (float)PoolFullAmount);

        /// <summary>卓の真ん中（山牌の手前）。800x600 で下から 32% の高さ。</summary>
        private static Vector3 TablePoolPosition() => new Vector3(Screen.width * 0.5f, Screen.height * 0.32f, 0f);

        // ホースの絵の位置（絵の左上を 0,0 とした割合）。血袋の絵のピクセルから測った（2026-10-18）。
        // 相手: 点滴の下から伸びるピンクのチューブ。ENEHPUI_NO（640x2560）の x=300〜329、y=1942 から下の端まで
        private static readonly Vector2 EnemyHoseMouthUv = new Vector2(0.491f, 0.759f);
        // 自分: メーターの左上の針から右上へ伸びる黄色いホース。HPUI0heart（54x140）の (34,30)〜(53,17)
        private static readonly Vector2 PlayerHoseMouthUv = new Vector2(0.63f, 0.21f);
        private static readonly Vector2 PlayerHoseOuterUv = new Vector2(0.98f, 0.12f);

        /// <summary>
        /// 血袋のホース。<see cref="Mouth"/> は血袋につながっている所、<see cref="Outer"/> は血が宙へ出入りする所。
        ///
        /// **血袋の真ん中へ直接流していたのをやめた**（2026-10-18 のユーザー指示「ホースのところから流れ込んでる感じに」）。
        /// </summary>
        private struct BagHose
        {
            public Vector3 Mouth, Outer;
        }

        /// <summary>メーターの絵の中の点（左上が 0,0 の割合）を画面座標にする。縦横比を保つ設定なら、その余白も除く。</summary>
        private static Vector3 GaugePoint(RectTransform gauge, Vector2 uv)
        {
            Rect r = ScreenRectOf(gauge);
            var img = gauge.GetComponent<Image>();
            if (img != null && img.preserveAspect && img.sprite != null && r.width > 0f && r.height > 0f)
            {
                float aspect = img.sprite.rect.width / img.sprite.rect.height;
                if (r.width / r.height > aspect)
                {
                    float w = r.height * aspect;
                    r = new Rect(r.center.x - w * 0.5f, r.y, w, r.height);
                }
                else
                {
                    float h = r.width / aspect;
                    r = new Rect(r.x, r.center.y - h * 0.5f, r.width, h);
                }
            }
            return new Vector3(r.xMin + uv.x * r.width, r.yMax - uv.y * r.height, 0f);
        }

        /// <summary>
        /// 相手のチューブは牌の裏を通って卓へ下りている。卓の血だまりの高さを、宙へ出入りする所にする。
        /// 自分のホースは右上へ伸びて画面の外へ出るので、見えている端を出入りする所にする。
        /// </summary>
        private static BagHose HoseOf(bool enemy, RectTransform gauge, Vector3 poolPos)
        {
            if (enemy)
            {
                Vector3 mouth = GaugePoint(gauge, EnemyHoseMouthUv);
                Vector3 bottom = GaugePoint(gauge, new Vector2(EnemyHoseMouthUv.x, 1f));
                return new BagHose { Mouth = mouth, Outer = new Vector3(mouth.x, Mathf.Clamp(poolPos.y, bottom.y, mouth.y), 0f) };
            }
            return new BagHose { Mouth = GaugePoint(gauge, PlayerHoseMouthUv), Outer = GaugePoint(gauge, PlayerHoseOuterUv) };
        }

        /// <summary>
        /// 場の血だまり → 勝った側、と、負けた側の血袋 → 勝った側、の血の筋を流し始める。
        /// どちらも勝っていない（増えた側がいない）ときは何も流さない。
        /// </summary>
        private List<Effects.BloodStreamEffect> StartBloodStreams(RectTransform stageRt, Effects.BloodPoolEffect pool,
            RonSettlementInfo s, PlayerInfoUI playerInfo, EnemyInfoUI enemyInfo)
        {
            var list = new List<Effects.BloodStreamEffect>();
            bool mineWins = s.MyShownDelta > 0;
            bool theirsWins = s.TheirShownDelta > 0;
            if (!mineWins && !theirsWins)
            {
                if (pool != null) pool.SetTarget(0f);
                return list;
            }

            RectTransform winnerGauge = mineWins
                ? (playerInfo != null ? playerInfo.HpGaugeAnchor : null)
                : (enemyInfo != null ? enemyInfo.HpGaugeAnchor : null);
            RectTransform loserGauge = mineWins
                ? (enemyInfo != null ? enemyInfo.HpGaugeAnchor : null)
                : (playerInfo != null ? playerInfo.HpGaugeAnchor : null);
            int loserDelta = mineWins ? s.TheirShownDelta : s.MyShownDelta;
            int winnerDelta = mineWins ? s.MyShownDelta : s.TheirShownDelta;
            if (winnerGauge == null) return list;

            Vector3 poolPos = pool != null ? pool.transform.position : TablePoolPosition();
            BagHose winnerHose = HoseOf(!mineWins, winnerGauge, poolPos);
            // 太さは勝った側に入る額で決める（満貫 2000 前後で 1、大きい手ほど太い）
            float thick = Mathf.Lerp(0.8f, 2f, Mathf.Clamp01(winnerDelta / 12000f));

            if (pool != null)
            {
                // 自分のホースは上から入るので高く弧を描く。相手のチューブは卓の上を横へ這わせる
                list.Add(Effects.BloodStreamEffect.Pour(stageRt, poolPos, winnerHose.Mouth,
                                                        BloodStreamSeconds, arc: mineWins ? 150f : 24f, thickness: thick,
                                                        enterHoseAt: winnerHose.Outer));
                pool.SetTarget(0f);
            }

            // 負けた側が賭け金より多く失う（倍率が掛かった）ときは、その血袋からも卓へ流す。
            // **血袋から血袋へ直接流すと、筋がキャラの顔を横切った**（2026-10-18 の試写）。
            // 卓の血だまりを通して勝った側へ渡すので、「血は場を通って奪われる」形にもそろう
            if (loserDelta < 0 && loserGauge != null)
            {
                BagHose loserHose = HoseOf(mineWins, loserGauge, poolPos);
                list.Add(Effects.BloodStreamEffect.Pour(stageRt, loserHose.Mouth, poolPos,
                                                        BloodStreamSeconds, arc: mineWins ? 24f : 150f,
                                                        thickness: Mathf.Lerp(0.7f, 1.8f, Mathf.Clamp01(-loserDelta / 12000f)),
                                                        leaveHoseAt: loserHose.Outer));
            }
            return list;
        }
    }
}
