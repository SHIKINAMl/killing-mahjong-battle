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

        /// <summary>足す光の強さ。息をするようにこの幅で行き来する。</summary>
        private const float PowerMin = 0.34f;
        private const float PowerMax = 0.60f;

        /// <summary>明滅の速さ（1秒あたりの往復）。**速いと点滅に見えるので抑える。**</summary>
        private const float PulsePerSecond = 0.5f;

        /// <summary>
        /// 光をどれだけ外へ広げるか（血の横幅に対する割合）。
        ///
        /// **画素の決め打ちにしてはいけない。** 自分の体力（Overlay）と相手の血袋
        /// （ScreenSpaceCamera・拡大率 0.02）では単位の大きさが50倍違い、
        /// 同じ数を入れると片方だけ豆粒になる。実際それで一度失敗している。
        /// </summary>
        private const float GlowSpreadRatio = 0.34f;

        /// <summary>
        /// 光を何枚に分けて重ねるか。
        /// **枚数が少ないと、一番外の一枚の縁が輪郭として見える。**
        /// </summary>
        private const int GlowLayerCount = 6;

        /// <summary>いちばん内側の一枚の濃さ。外へ行くほど薄くなる。</summary>
        private const float GlowPowerMin = 0.10f;
        private const float GlowPowerMax = 0.19f;

        private Image _blood;
        private Image _neon;
        private Image[] _glowLayers;
        private RectTransform[] _glowRects;
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
            _neon.material = NeonMaterials.CreateAll(NeonColor);
            BuildGlowLayers();
            _phase = Random.Range(0f, Mathf.PI * 2f);
            ApplyPower((PowerMin + PowerMax) * 0.5f, (GlowPowerMin + GlowPowerMax) * 0.5f);
            SyncShape();
        }

        /// <summary>
        /// 血の**外へ**こぼれる光を作る（2026-09-30 のユーザー指示
        /// 「そこから光を放っているような感じに」）。
        ///
        /// **血と同じ絵を、少しずつ大きく・薄くして何枚も加算で重ねる。**
        /// ぼかした絵を一枚かぶせるのと近い結果になるが、レンダーテクスチャが要らない。
        ///
        /// **四角いぼかし板を一枚置くのは失敗だった（2026-09-30）。**
        /// 血の形と関係ない箱なので、卓の上に光の四角が乗っているようにしか見えず、
        /// ユーザーから「四角い枠が光るのではなく、中の赤い血が光を出している感じに」
        /// と言われた。ここでは全部の層が血そのものの形なので、箱の縁は出ない。
        ///
        /// **本物の Bloom を使わない理由。** URP の後処理は ScreenSpaceOverlay の
        /// Canvas には効かない（後処理より後に描かれるため）。自分の体力は Overlay に
        /// 居るので原理的に光らない。Camera 方式へ移せば効くが、手牌(1)や吹き出し(16)
        /// など他の Overlay すべての後ろへ回ってしまい、表示が崩れる。
        /// </summary>
        private void BuildGlowLayers()
        {
            _glowLayers = new Image[GlowLayerCount];
            _glowRects = new RectTransform[GlowLayerCount];

            for (int i = 0; i < GlowLayerCount; i++)
            {
                var go = new GameObject("BloodGlow" + i, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform.parent, false);
                // 蛍光色の本体より奥。外側の層ほど先に描く
                go.transform.SetSiblingIndex(transform.GetSiblingIndex());

                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.localScale = Vector3.one;

                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                img.material = NeonMaterials.CreateAll(NeonColor);

                _glowRects[i] = rect;
                _glowLayers[i] = img;
            }
        }

        private void LateUpdate()
        {
            if (_blood == null || _neon == null) { enabled = false; return; }

            SyncShape();

            _phase += Time.deltaTime * PulsePerSecond * Mathf.PI * 2f;
            float t = (Mathf.Sin(_phase) + 1f) * 0.5f;
            ApplyPower(Mathf.Lerp(PowerMin, PowerMax, t),
                       Mathf.Lerp(GlowPowerMin, GlowPowerMax, t));
        }

        /// <summary>光の強さは頂点カラーのアルファで渡す（シェーダがそれを掛けている）。</summary>
        private void ApplyPower(float power, float glowPower)
        {
            _neon.color = new Color(1f, 1f, 1f, power);
            if (_glowLayers == null) return;

            for (int i = 0; i < _glowLayers.Length; i++)
            {
                if (_glowLayers[i] == null) continue;
                // 外の層ほど薄く。二乗で落として、いちばん外を目立たなくする
                float u = (i + 1) / (float)_glowLayers.Length;
                float fade = (1f - u) * (1f - u);
                _glowLayers[i].color = new Color(1f, 1f, 1f, glowPower * fade);
            }
        }

        /// <summary>
        /// 光の各層を、血と同じ形のまま少しずつ大きくして並べる。
        ///
        /// **親（血）の中の座標で計算する。** 光は血の子なので、画面の拡大率を
        /// 気にしなくてよい。以前これを親の外で計算して、相手側（拡大率 0.02）の
        /// 光が画面上 3x10 画素になった。
        ///
        /// **`fillAmount` も写す。** 写さないと、体力が減っても光だけ枠いっぱいに残る。
        /// </summary>
        private void SyncGlowLayers()
        {
            if (_glowLayers == null) return;

            Rect r = _blood.rectTransform.rect;
            bool filled = _blood.type == Image.Type.Filled;
            float fill = filled ? Mathf.Clamp01(_blood.fillAmount) : 1f;
            bool alive = _blood.enabled && fill > 0.001f;

            float spread = r.width * GlowSpreadRatio;

            for (int i = 0; i < _glowLayers.Length; i++)
            {
                var img = _glowLayers[i];
                var rect = _glowRects[i];
                if (img == null || rect == null) continue;

                img.enabled = alive;
                if (!alive) continue;

                // 血と同じ絵・同じ減り方にする。これで層も血の形になる
                img.sprite = _blood.sprite;
                img.type = _blood.type;
                img.preserveAspect = _blood.preserveAspect;
                img.fillMethod = _blood.fillMethod;
                img.fillOrigin = _blood.fillOrigin;
                img.fillAmount = _blood.fillAmount;
                img.fillClockwise = _blood.fillClockwise;

                // 外の層ほど大きく広げる
                float step = spread * (i + 1) / _glowLayers.Length;
                rect.sizeDelta = new Vector2(r.width + step * 2f, r.height + step * 2f);

                // **広げた分の半分だけ下げる。** 血は下から溜まるので、枠だけ
                // 上下に広げると、溜まっている部分が上へずれてしまう
                float cy = 0f;
                if (filled && _blood.fillMethod == Image.FillMethod.Vertical)
                {
                    cy = _blood.fillOrigin == 0 ? -step * (1f - fill) : step * (1f - fill);
                }
                rect.anchoredPosition = new Vector2(0f, cy);
            }
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

            SyncGlowLayers();
        }
    }
}
