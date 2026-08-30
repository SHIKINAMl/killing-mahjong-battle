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
    }
}
