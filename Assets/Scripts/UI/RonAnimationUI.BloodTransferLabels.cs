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
        /// この局の増減を、**HPの表示の真上**に出す（2026-10-08 のユーザー指示）。
        ///
        /// HPが増減したときの浮き数字（<c>HpPopupPresenter</c>。演出の試写の hp.damage / hp.heal）と
        /// 同じ場所にそろえた。以前は「HPの隣（画面の内側）」に出していて、相手側はキャラの胸の上、
        /// 自分側は山牌の上に数字がかかっていた。ロンのあいだ浮き数字は止めてあるので、
        /// 同じ場所に出しても二重にはならない。
        /// </summary>
        /// <summary>基準が潰れているとみなす大きさ[px]。</summary>
        private const float MinAnchorSize = 20f;

        /// <summary>潰れていたときに置く場所（画面の割合）。点滴の右隣。</summary>
        private const float FallbackCenterX = 0.42f;
        private const float FallbackCenterY = 0.58f;

        /// <summary>
        /// HPの表示の上端から、数字の下端までのすき間（800x600 での画素数）。
        /// 相手側は上端のすぐ内側に「相手 18002」の文字があるので、上端より上に出せば重ならない。
        /// 自分側は上端の上にスマホの飾りがあり、その上へ出る。
        /// </summary>
        private const float HpDeltaGapAbove = 12f;

        /// <summary>数字の箱の高さ（Canvas の単位）。位置の計算にも使う。</summary>
        private const float HpDeltaBoxHeight = 44f;

        private TextMeshProUGUI SpawnHpDeltaLabel(RectTransform parent, RectTransform anchor, int delta, Color tint)
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
            text.alignment = TextAlignmentOptions.Center;
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 255);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(280f, HpDeltaBoxHeight);

            // 箱の中心を、HPの表示の中心の真上に置く
            rt.pivot = new Vector2(0.5f, 0.5f);

            // **基準の場所は、画面の座標へ直してから使う（2026-10-08）。**
            //
            // 以前は `GetWorldCorners` の値をそのまま画面ピクセルとして使っていた。
            // 自分側（スマホ）の Canvas は Overlay なのでそれで合うが、**相手側（EnemyInfoUI）の
            // Canvas は「Screen Space - Camera」**で、ワールド座標はピクセルではない。
            // 2026-09-17 に「相手の基準は 3x3px・座標(-5,3) に潰れている」と読んだのは
            // この取り違えで、潰れていたのではなく単位が違っていた。
            // そのせいで相手側は下の「決め打ちの場所」へ逃がされ、HPの表示から離れた所に
            // 数字が出ていた（ユーザー報告「体力の増減の数値が出る場所がおかしい」。
            // HpPopupPresenter の浮き数字は Canvas の中の座標で置くので、ずれていなかった）。
            Rect screenRect = ScreenRectOf(anchor);
            Vector3 center = new Vector3(screenRect.center.x, screenRect.center.y, 0f);

            // 画面の大きさが変わっても同じ見た目の間隔になるよう、800x600 に対する倍率で持つ
            float pixelScale = Screen.height / 600f;
            bool anchorUsable = true;

            // 基準が本当に取れなかったとき（画面の外、大きさ 0 など）の逃げ道は残してある
            float anchorWidth = screenRect.width;
            float anchorHeight = screenRect.height;
            if (anchorWidth < MinAnchorSize || anchorHeight < MinAnchorSize)
            {
                // 点滴は画面のやや左・中ほどにある（800x600 で x225〜270 / y270〜430 を実測）。
                // その右隣へ置く。画面の割合で持つので、解像度が変わっても付いてくる。
                center = new Vector3(Screen.width * FallbackCenterX,
                                     Screen.height * FallbackCenterY, 0f);
                anchorUsable = false;
            }

            if (anchorUsable)
            {
                // HPの表示の上端のすぐ上へ。箱の高さの半分だけ持ち上げて、数字の下端を上端に合わせる
                center.y = screenRect.yMax + (HpDeltaGapAbove + HpDeltaBoxHeight * 0.5f) * pixelScale;

                // 画面の端にかからないよう、左右と上を収める（額が大きいと数字が長くなる）
                float halfTextWidth = Mathf.Min(text.preferredWidth, rt.sizeDelta.x) * 0.5f * pixelScale;
                float margin = 6f * pixelScale;
                center.x = Mathf.Clamp(center.x, halfTextWidth + margin, Screen.width - halfTextWidth - margin);
                float maxY = Screen.height - (HpDeltaBoxHeight * 0.5f) * pixelScale - margin;
                if (center.y > maxY) center.y = maxY;
            }
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

        /// <summary>
        /// RectTransform の中心を**画面の座標**で返す。**サイズが 0 の空オブジェクトでも中心が取れる。**
        /// 血の数字を飛ばす先に使う。飛ばす側の Canvas は Overlay なので、画面の座標がそのまま位置になる。
        /// </summary>
        private static Vector3 AnchorCenter(RectTransform rt)
        {
            Rect r = ScreenRectOf(rt);
            return new Vector3(r.center.x, r.center.y, 0f);
        }

        /// <summary>
        /// RectTransform が画面のどこに映っているか（ピクセル）。
        ///
        /// Canvas が Overlay ならワールド座標がそのままピクセルだが、
        /// 「Screen Space - Camera」や World の Canvas では、そのカメラを通して変換しないと合わない。
        /// カメラが割り当てられていない Camera 方式の Canvas は、Unity が Overlay として扱う。
        /// </summary>
        private static Rect ScreenRectOf(RectTransform rt)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            Camera cam = null;
            Canvas canvas = rt.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                Canvas root = canvas.rootCanvas;
                if (root.renderMode != RenderMode.ScreenSpaceOverlay) cam = root.worldCamera;
            }

            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                                   Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
    }
}
