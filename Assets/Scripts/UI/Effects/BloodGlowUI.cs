using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 体力メーターの血を、蛍光色に光っているように見せる（2026-09-29〜30 のユーザー指示）。
    ///
    /// **血と同じ形の絵を、加算合成（<c>KillingMahjong/NeonKeyAdd</c>）で上に重ねる。**
    /// 加算なので下の絵へ光を足すことになり、元の絵より明るくできる。
    ///
    /// 色はユーザーの参考画像（髪の内側と目だけが蛍光色で光っている絵）の
    /// 赤い部分を数えて出した値: 中央値 (252, 83, 98) ／ 目の部分 (239, 74, 79)。
    ///
    /// **ここへ来るまでに2回やり直している。記録として残す。**
    ///
    ///   1回目「後ろにやわらかい光の輪を敷く」…「なんか発光の感じが違う」と言われた。
    ///        参考画像で光って見えるのは周りがにじんでいるからではなく、
    ///        **その部分の色自体が明るく鮮やかだから**だった。
    ///   2回目「同じ絵を蛍光色で重ねる」…**色が乗らなかった。** 通常のUIマテリアルは
    ///        絵に色を掛け算するので、重ねた絵は「血を蛍光色で暗く染めたもの」になる。
    ///        実測で血の色が (248,126,126) から (247,118,118) にしか動かなかった。
    ///        掛け算では明るくできない、と自分で書いた注意書きに自分で引っかかっていた。
    ///
    /// **シーンには置かない。** 対局シーンが `UIテストシーン` と `OpeningScene` の
    /// 2つあるため、シーンに足すと片方にだけ入れる事故になる（AGENTS.md §2）。
    /// </summary>
    public class BloodGlowUI : MonoBehaviour
    {
        /// <summary>参考画像から測った蛍光色（赤い部分の中央値）。</summary>
        private static readonly Color NeonColor = new Color32(252, 83, 98, 255);

        /// <summary>
        /// 足す光の強さ。息をするようにこの幅で行き来する。
        ///
        /// **外へ広げる層をやめたぶん、本体を強くしてある（2026-09-30）。**
        /// 0.34〜0.60 のままだと、滲みが無くなった代わりに光って見えなくなる。
        /// </summary>
        private const float PowerMin = 0.52f;
        private const float PowerMax = 0.80f;

        /// <summary>明滅の速さ（1秒あたりの往復）。**速いと点滅に見えるので抑える。**</summary>
        private const float PulsePerSecond = 0.5f;

        private Image _blood;
        private Image _neon;
        private float _phase;

        /// <summary>
        /// <paramref name="blood"/> の上に蛍光色の光を足す。二重に呼んでも増えない。
        /// </summary>
        public static BloodGlowUI Attach(Image blood)
        {
            if (!Application.isPlaying || blood == null) return null;

            var existing = blood.GetComponentInChildren<BloodGlowUI>(true);
            if (existing != null) return existing;

            // **血の子にする。** uGUI は「親の絵 → 子の絵」の順に描くので、
            // 子にしておけば必ず血の上に乗る。位置・大きさ・拡大率も親に付いて回るので、
            // こちら側で座標を計算しなくて済む（以前はここを自前で合わせようとして、
            // 敵側の Canvas の拡大率 0.02 を見落とし、光が画面上 3x10 画素になっていた）。
            var go = new GameObject("BloodNeon", typeof(RectTransform), typeof(Image), typeof(BloodGlowUI));
            go.transform.SetParent(blood.transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            var view = go.GetComponent<BloodGlowUI>();
            view.Build(blood);
            return view;
        }

        private void Build(Image blood)
        {
            _blood = blood;
            _neon = GetComponent<Image>();
            _neon.raycastTarget = false;
            Material material = NeonMaterials.CreateAll(NeonColor);
            if (material == null)
            {
                // シェーダが無い。既定の材質で重ねると、光らずに血の色が濁るだけなので、出さない
                _neon.enabled = false;
                enabled = false;
                return;
            }
            _neon.material = material;
            _phase = Random.Range(0f, Mathf.PI * 2f);
            ApplyPower((PowerMin + PowerMax) * 0.5f);
            SyncShape();
        }

        // 血の外へ光をこぼす層（血の絵を少しずつ大きく・薄く6枚重ねる）は
        // 2026-09-30 に消した。ユーザーから「なんか少し滲んでいる感じになっている。
        // 純粋に発光している感じに」と言われたため。
        //
        // **あれは原理的に滲む。** 絵をずらして重ねるのはぼかしと同じことなので、
        // 「外へ広がる光」と「滲み」は同じものの別の呼び方でしかない。
        // ドット絵の中では、ぼけた縁は光ではなく汚れに見える。
        // いまは血そのものを強く光らせるだけにして、縁はドットのまま残している。

        private void LateUpdate()
        {
            if (_blood == null || _neon == null) { enabled = false; return; }

            SyncShape();

            _phase += Time.deltaTime * PulsePerSecond * Mathf.PI * 2f;
            float t = (Mathf.Sin(_phase) + 1f) * 0.5f;
            ApplyPower(Mathf.Lerp(PowerMin, PowerMax, t));
        }

        /// <summary>光の強さは頂点カラーのアルファで渡す（シェーダがそれを掛けている）。</summary>
        private void ApplyPower(float power)
        {
            _neon.color = new Color(1f, 1f, 1f, power);
        }

        /// <summary>
        /// 蛍光色の本体を、血と同じ形・同じ減り方にする。
        ///
        /// **`fillAmount` まで写すこと。** 子は親の `fillAmount` では切られないので、
        /// ここを写さないと体力が減っても光だけ枠いっぱいに残り、
        /// 袋が空なのか満タンなのか分からなくなる。
        /// </summary>
        private void SyncShape()
        {
            _neon.sprite = _blood.sprite;
            _neon.type = _blood.type;
            _neon.preserveAspect = _blood.preserveAspect;
            _neon.fillMethod = _blood.fillMethod;
            _neon.fillOrigin = _blood.fillOrigin;
            _neon.fillAmount = _blood.fillAmount;
            _neon.fillClockwise = _blood.fillClockwise;
            _neon.enabled = _blood.enabled && _blood.fillAmount > 0.001f;

        }
    }
}
