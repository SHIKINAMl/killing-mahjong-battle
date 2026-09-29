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
        /// 血の縁から外へ光をにじませる幅。**血の横幅に対する割合で持つ**（2026-09-30）。
        ///
        /// 画素の決め打ちにしてはいけない。自分の体力（Overlay）と相手の血袋
        /// （ScreenSpaceCamera・拡大率 0.02）では単位の大きさが50倍違い、
        /// 同じ数を入れると片方だけ豆粒になる。実際それで失敗している。
        /// </summary>
        private const float HaloBleedRatio = 0.30f;

        /// <summary>
        /// にじみの濃さ。**本体よりかなり弱くする。**
        /// 0.22〜0.45 では卓の上に四角い光の板が乗って見えた（実機で確認）。
        /// </summary>
        private const float HaloPowerMin = 0.13f;
        private const float HaloPowerMax = 0.26f;

        private Image _blood;
        private Image _neon;
        private Image _halo;
        private RectTransform _haloRect;
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
            BuildHalo();
            _phase = Random.Range(0f, Mathf.PI * 2f);
            ApplyPower((PowerMin + PowerMax) * 0.5f, (HaloPowerMin + HaloPowerMax) * 0.5f);
            SyncShape();
        }

        /// <summary>
        /// 血の**外へ**こぼれる光を作る（2026-09-30 のユーザー指示
        /// 「そこから光を放っているような感じに」）。
        ///
        /// **血より手前に置くが、真ん中は透明にしてある。** 加算合成なので、
        /// 真ん中まで光らせると本体が二重に明るくなって白飛びする。
        /// 縁でいちばん明るく、外へ向かって消えていく輪にすることで、
        /// 「血から光が漏れている」ように見せる。
        /// </summary>
        private void BuildHalo()
        {
            var go = new GameObject("BloodHalo", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform.parent, false);
            // 蛍光色の本体より奥に置く。血 → にじみ → 本体 の順で描く
            go.transform.SetSiblingIndex(transform.GetSiblingIndex());

            _haloRect = (RectTransform)go.transform;
            _haloRect.anchorMin = new Vector2(0.5f, 0.5f);
            _haloRect.anchorMax = new Vector2(0.5f, 0.5f);
            _haloRect.pivot = new Vector2(0.5f, 0.5f);
            _haloRect.localScale = Vector3.one;

            _halo = go.GetComponent<Image>();
            _halo.raycastTarget = false;
            _halo.sprite = HaloSprite;
            _halo.type = Image.Type.Sliced;
            _halo.material = NeonMaterials.CreateAll(NeonColor);
        }

        private void LateUpdate()
        {
            if (_blood == null || _neon == null) { enabled = false; return; }

            SyncShape();

            _phase += Time.deltaTime * PulsePerSecond * Mathf.PI * 2f;
            float t = (Mathf.Sin(_phase) + 1f) * 0.5f;
            ApplyPower(Mathf.Lerp(PowerMin, PowerMax, t),
                       Mathf.Lerp(HaloPowerMin, HaloPowerMax, t));
        }

        /// <summary>光の強さは頂点カラーのアルファで渡す（シェーダがそれを掛けている）。</summary>
        private void ApplyPower(float power, float haloPower)
        {
            _neon.color = new Color(1f, 1f, 1f, power);
            if (_halo != null) _halo.color = new Color(1f, 1f, 1f, haloPower);
        }

        /// <summary>
        /// にじみを「いま血が入っている範囲」の外側に合わせる。
        ///
        /// **親（血）の中の座標で計算する。** にじみは血の子なので、
        /// 画面の拡大率を気にしなくてよい。以前これを親の外で計算して、
        /// 相手側（拡大率 0.02）の光が画面上 3x10 画素になった。
        /// </summary>
        private void SyncHalo()
        {
            if (_halo == null || _haloRect == null) return;

            Rect r = _blood.rectTransform.rect;
            float fill = _blood.type == Image.Type.Filled ? Mathf.Clamp01(_blood.fillAmount) : 1f;

            float w = r.width;
            float h = r.height;
            float cx = 0f;
            float cy = 0f;

            if (_blood.type == Image.Type.Filled && _blood.fillMethod == Image.FillMethod.Vertical)
            {
                h = r.height * fill;
                // fillOrigin 0 = 下から溜まる
                cy = _blood.fillOrigin == 0 ? -(r.height - h) * 0.5f : (r.height - h) * 0.5f;
            }
            else if (_blood.type == Image.Type.Filled && _blood.fillMethod == Image.FillMethod.Horizontal)
            {
                w = r.width * fill;
                cx = _blood.fillOrigin == 0 ? -(r.width - w) * 0.5f : (r.width - w) * 0.5f;
            }

            float bleed = r.width * HaloBleedRatio;
            // 絵のふち(HaloBorder)を bleed 画素ぶんに縮めて描かせる。
            // これを忘れると、ふちだけで中央が潰れて血の何倍もある板になる
            _halo.pixelsPerUnitMultiplier = HaloBorder / Mathf.Max(0.01f, bleed);

            _haloRect.sizeDelta = new Vector2(w + bleed * 2f, h + bleed * 2f);
            _haloRect.anchoredPosition = new Vector2(cx, cy);
            _halo.enabled = _blood.enabled && fill > 0.001f;
        }

        // ------------------------------------------------------------
        //  外へこぼれる光の絵を実行時に作る
        //
        //  **真ん中は透明。** 縁でいちばん濃く、外へ向かって消える。
        //  画像アセットは足さない（`PixelBloodEffect` が 1x1 の白を自前で
        //  作っているのと同じ考え方）。
        // ------------------------------------------------------------

        private const int HaloTextureSize = 64;
        private const int HaloBorder = 22;

        private static Sprite _haloSprite;

        private static Sprite HaloSprite
        {
            get
            {
                if (_haloSprite != null) return _haloSprite;

                var tex = new Texture2D(HaloTextureSize, HaloTextureSize, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;

                var pixels = new Color32[HaloTextureSize * HaloTextureSize];
                for (int y = 0; y < HaloTextureSize; y++)
                {
                    for (int x = 0; x < HaloTextureSize; x++)
                    {
                        // 内側の四角（＝血の縁）からどれだけ外に居るか
                        float dx = Mathf.Max(0f, Mathf.Max(HaloBorder - x, x - (HaloTextureSize - 1 - HaloBorder)));
                        float dy = Mathf.Max(0f, Mathf.Max(HaloBorder - y, y - (HaloTextureSize - 1 - HaloBorder)));
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / HaloBorder;
                        // 内側（d=0）は透明、縁の少し外でいちばん濃く、さらに外で消える
                        float a = Mathf.Clamp01(1f - d);
                        // **3乗にして裾を長くする。** 2乗だと落ちが早く、
                        // 光の輪郭が四角い板の縁として見えてしまう
                        a = a * a * a;
                        // 中央は完全に透明にして、本体を二重に光らせない
                        if (dx <= 0f && dy <= 0f) a = 0f;
                        pixels[y * HaloTextureSize + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                }
                tex.SetPixels32(pixels);
                tex.Apply();

                _haloSprite = Sprite.Create(
                    tex,
                    new Rect(0f, 0f, HaloTextureSize, HaloTextureSize),
                    new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect,
                    new Vector4(HaloBorder, HaloBorder, HaloBorder, HaloBorder));
                _haloSprite.name = "BloodHaloSprite";
                return _haloSprite;
            }
        }

        /// <summary>
        /// 血と同じ形・同じ減り方にする。
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

            SyncHalo();
        }
    }
}
