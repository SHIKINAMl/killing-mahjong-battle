using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Managers;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 賭け金を選んでいる間の「血を賭けている」演出（2026-10-17）。
    ///
    /// ユーザーの言葉:「説明口調すぎます。演出？で見せてくれませんか」。
    /// 数字や説明を足すのではなく、額が血に対して重くなるほど体で感じるようにする:
    ///   - 賭け金の枠に、賭けた血が溜まる（いまの血の 25% で満杯）
    ///   - 心音が鳴り続け、額が重いほど速く・重い音になる
    ///   - 画面の縁が心音に合わせて赤く脈打つ
    ///   - いちばん重い段では、心音のたびにパネルがびくっと揺れる
    ///
    /// 重さは「賭け金 ÷ いまの血」。同じ 2000 でも、血が減っていれば重くなる。
    ///
    /// 心音の強さの段は、フォーラムの演出ルールどおり**入る条件と戻る条件をずらす**
    /// （上げ下げで境目をまたいだときに、音がばたばた切り替わらないように）。
    /// </summary>
    public sealed class BetStakeFx : MonoBehaviour
    {
        /// <summary>演出コレクションの ID。</summary>
        public const string EffectId = "phase.bet_stake";
        /// <summary>没案「賭けたあとの血」の札（<see cref="BetOutcomePreviewUI"/>）の ID。</summary>
        public const string CardEffectId = "phase.bet_outcome_card";

        public const string FillName = "BetBloodFill";
        public const string VignetteName = "BetStakeVignette";

        /// <summary>この割合（賭け金 ÷ いまの血）で「いちばん重い」。枠が満杯になる</summary>
        private const float FullRatio = 0.25f;

        // 心音の段。上がる境目 / 下がる境目（重さ 0..1 で）
        private const float MediumUp = 0.35f, MediumDown = 0.28f;
        private const float StrongUp = 0.72f, StrongDown = 0.62f;

        // 心音の間隔（秒）。軽いとき / 重いとき
        private const float SlowBeat = 1.35f, FastBeat = 0.48f;

        private const float VignetteMax = 0.42f;
        private static readonly Color BloodColor = new Color32(176, 16, 36, 255);

        private RectTransform fill;
        private float fillMaxHeight;
        private Image vignette;
        private RectTransform shakeTarget;
        private Vector2 shakeHome;

        private bool running;
        private float weight, shownWeight;
        private int stage;
        private float beatTimer;
        private float pulse;     // 心音の直後に跳ねて、すぐ戻る
        private float shake;

        /// <summary>
        /// 部品を探す（無ければ作る）。<paramref name="betBox"/> は賭け金の数字が載っている紺の枠。
        /// </summary>
        public static BetStakeFx Ensure(Transform bettingRoot, RectTransform betBox, RectTransform shakeTarget)
        {
            if (bettingRoot == null) return null;
            var fx = bettingRoot.GetComponent<BetStakeFx>();
            if (fx == null) fx = bettingRoot.gameObject.AddComponent<BetStakeFx>();
            fx.Build(betBox, shakeTarget);
            return fx;
        }

        private void Build(RectTransform betBox, RectTransform shakeTo)
        {
            shakeTarget = shakeTo;

            // 画面の縁。BettingUI のキャンバス直下、いちばん奥（パネルより後ろ）に置く
            var vg = SceneFirst.Child(transform, VignetteName, out bool made, typeof(RectTransform), typeof(Image));
            var vr = (RectTransform)vg.transform;
            vignette = vg.GetComponent<Image>();
            if (made)
            {
                vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
                vr.offsetMin = vr.offsetMax = Vector2.zero;
                vr.SetSiblingIndex(0);
                vignette.color = new Color(BloodColor.r, BloodColor.g, BloodColor.b, 0f);
            }
            vignette.raycastTarget = false;
            // 縁ほど濃い板は実行時に作る絵なので、シーンには保存されない。毎回ここで挿す
            //（編集中に挿すと、保存されない絵への参照がシーンに残るので再生中だけ）
            if (vignette.sprite == null && Application.isPlaying) vignette.sprite = EdgeSprite();

            // 賭け金の枠の中に溜まる血。数字より奥に置く
            if (betBox != null)
            {
                var fg = SceneFirst.Child(betBox, FillName, out made, typeof(RectTransform), typeof(Image));
                fill = (RectTransform)fg.transform;
                if (made)
                {
                    fill.SetSiblingIndex(0);
                    fill.anchorMin = new Vector2(0f, 0f); fill.anchorMax = new Vector2(1f, 0f);
                    fill.pivot = new Vector2(0.5f, 0f);
                    fill.offsetMin = new Vector2(2f, 2f); fill.offsetMax = new Vector2(-2f, 2f);
                    var img = fg.GetComponent<Image>(); img.color = BloodColor; img.raycastTarget = false;
                }
                fg.GetComponent<Image>().raycastTarget = false;
                fillMaxHeight = Mathf.Max(0f, betBox.rect.height - 4f);
                SetFillHeight(0f);
            }
        }

        /// <summary>賭けフェイズが始まったときに呼ぶ。</summary>
        public void Begin(int bet, int currentHp)
        {
            running = true;
            stage = -1;
            beatTimer = 0.25f;   // 開いてすぐ1拍目
            if (shakeTarget != null) shakeHome = shakeTarget.anchoredPosition;
            SetStake(bet, currentHp);
            shownWeight = weight;
        }

        /// <summary>額を変えるたびに呼ぶ。</summary>
        public void SetStake(int bet, int currentHp)
        {
            weight = currentHp > 0 ? Mathf.Clamp01(bet / (currentHp * FullRatio)) : 1f;
        }

        /// <summary>決定したとき・閉じるときに呼ぶ。枠の血は残し、心音と縁は引いていく。</summary>
        public void End()
        {
            running = false;
            if (shakeTarget != null && shake > 0f) shakeTarget.anchoredPosition = shakeHome;
            shake = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            shownWeight = Mathf.Lerp(shownWeight, weight, 1f - Mathf.Exp(-dt * 14f));
            SetFillHeight(shownWeight * fillMaxHeight);

            if (running)
            {
                UpdateStage();
                beatTimer -= dt;
                if (beatTimer <= 0f)
                {
                    Beat();
                    beatTimer = Mathf.Lerp(SlowBeat, FastBeat, weight);
                }
            }

            pulse = Mathf.MoveTowards(pulse, 0f, dt * 2.2f);
            float baseAlpha = running ? weight * VignetteMax * 0.55f : 0f;
            float a = Mathf.Clamp01(baseAlpha + pulse * weight * VignetteMax);
            if (vignette != null)
            {
                var c = vignette.color;
                c.a = Mathf.MoveTowards(c.a, a, dt * 3f);
                vignette.color = c;
            }

            if (shakeTarget != null && shake > 0f)
            {
                shake = Mathf.MoveTowards(shake, 0f, dt * 10f);
                // ドット絵なので整数で揺らす
                float off = shake > 0f ? Mathf.Round(Mathf.Sin(shake * 40f) * 2f * shake) : 0f;
                shakeTarget.anchoredPosition = shakeHome + new Vector2(off, 0f);
            }
        }

        private void UpdateStage()
        {
            int next = stage;
            if (stage < 0) next = weight >= StrongUp ? 2 : weight >= MediumUp ? 1 : 0;
            else if (stage == 0 && weight >= MediumUp) next = weight >= StrongUp ? 2 : 1;
            else if (stage == 1) next = weight >= StrongUp ? 2 : weight < MediumDown ? 0 : 1;
            else if (stage == 2 && weight < StrongDown) next = weight < MediumDown ? 0 : 1;
            stage = next;
        }

        /// <summary>心音を鳴らしたとき（段 0..2）。録画に音を重ねるための記録用。</summary>
        public static event System.Action<int> Beaten;

        private void Beat()
        {
            Beaten?.Invoke(stage);
            var audio = AudioManager.Instance;
            if (audio != null)
            {
                switch (stage)
                {
                    case 2: audio.PlayHeartbeat(HeartbeatStrength.Strong, HeartbeatSpacing.Spaced); break;
                    case 1: audio.PlayHeartbeat(HeartbeatStrength.Medium, HeartbeatSpacing.Spaced); break;
                    default: audio.PlayHeartbeat(HeartbeatStrength.Weak, HeartbeatSpacing.Compact); break;
                }
            }
            pulse = 1f;
            if (stage == 2) shake = 1f;
        }

        private void SetFillHeight(float h)
        {
            if (fill == null) return;
            var s = fill.sizeDelta;
            s.y = Mathf.Round(h);
            fill.sizeDelta = s;
        }

        private static Sprite _edge;

        /// <summary>中心が透明で、縁ほど濃い板。粗い解像度でドットの段を残す。</summary>
        private static Sprite EdgeSprite()
        {
            if (_edge != null) return _edge;
            const int w = 64, h = 48;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point; tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Min(x, w - 1 - x) / (w * 0.5f);
                float dy = Mathf.Min(y, h - 1 - y) / (h * 0.5f);
                float d = Mathf.Min(dx, dy);                 // 0 が縁
                float a = Mathf.Clamp01(1f - d / 0.38f);
                a = Mathf.Round(a * a * 5f) / 5f;            // 段をつける
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _edge = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
            _edge.name = "BetStakeEdge";
            return _edge;
        }
    }
}
