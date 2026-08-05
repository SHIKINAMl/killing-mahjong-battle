using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 自分の体力が減った瞬間、画面を横帯に切って過去のフレームを混ぜ込むノイズ。
    ///
    /// **ただの砂嵐にしない。** 過去の画面を保存しておいて一瞬だけ混ぜると、
    /// 機械の故障ではなく「記憶が混濁している」感じになる。
    /// 13フレーム前（直前）と73フレーム前（1秒以上前）の2枚を持ち、
    /// 帯ごとにどちらかをランダムに選ぶ。片方だけだと規則的に見えて効かない。
    ///
    /// **カメラのポストプロセスにはしていない。** この対局画面は Canvas が
    /// ScreenSpace-Overlay と ScreenSpace-Camera の混在で、ポストプロセスだと
    /// Overlay 側（自分の手牌・セリフ・体力表示・ボタン）に一切かからず、
    /// 画面の半分だけ歪む不自然な絵になる。
    /// `ScreenCapture.CaptureScreenshotIntoRenderTexture` は**合成後の画面**を撮るので、
    /// Overlay も含めて全部が歪む。
    ///
    /// 専用の画像は使わず実行時に組み立てる。**対局シーンが2つ（UIテストシーン /
    /// OpeningScene）あるので、調整値は SerializeField にせずここの定数で持つ。**
    /// </summary>
    public class HpDamageGlitch : MonoBehaviour
    {
        // ---- 調整値（シーンではなくここを触る）----

        /// <summary>走っている時間。長いと「演出」になってしまうので短く</summary>
        private const float Duration = 0.45f;

        /// <summary>画面を横に切る本数</summary>
        private const int BlockCount = 9;

        /// <summary>帯の最大ずれ幅。画面幅に対する割合</summary>
        private const float MaxShiftRatio = 0.05f;

        /// <summary>各フレーム、その帯を出す確率。1.0 にすると過去の画面で埋まって
        /// 現在の画面が見えなくなる。隙間から「今」が覗くから壊れて見える</summary>
        private const float BlockShowChance = 0.5f;

        /// <summary>色ズレを出す確率と、そのときの色</summary>
        private const float ColorGapChance = 0.35f;
        private static readonly Color GapWarm = new Color(1f, 0.55f, 0.55f, 1f);
        private static readonly Color GapCool = new Color(0.55f, 0.8f, 1f, 1f);

        /// <summary>直前の記憶と、少し前の記憶。素数にしてあるのは
        /// 2つの保存タイミングが重なって同じ絵にならないようにするため</summary>
        private const int OldFrameInterval1 = 13;
        private const int OldFrameInterval2 = 73;

        /// <summary>撮った画面の上下が反転するかどうか。
        /// `CaptureScreenshotIntoRenderTexture` はグラフィックスAPIによって
        /// 上下が逆になる。実機で見て違ったらここを反転させる</summary>
        private const bool FlipY = true;

        /// <summary>これ以上の割合を一度に失うと、いちばん激しく歪む</summary>
        private const float FullStrengthDamageRatio = 0.25f;

        private static HpDamageGlitch _instance;

        private RenderTexture _old1;
        private RenderTexture _old2;
        private RawImage[] _blocks;
        private int _frameCount;
        private float _remain;
        private float _strength = 1f;
        private int _rtWidth, _rtHeight;

        /// <summary>
        /// ノイズの器を作る。すでに作ってあればそれを返す。
        /// 盤面のどの UI にもぶら下げず、専用の Canvas を1枚持つ。
        /// </summary>
        public static HpDamageGlitch Ensure()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("HpDamageGlitch", typeof(RectTransform));
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UISortingOrders.DamageGlitch;

            // GraphicRaycaster は付けない。付けると画面全体のクリックを吸って
            // 牌もボタンも押せなくなる（TurnVignette で踏んだ）

            _instance = go.AddComponent<HpDamageGlitch>();
            _instance.Build();
            return _instance;
        }

        /// <summary>
        /// ノイズを走らせる。減った量が多いほど激しくなる。
        /// 連続で呼ばれても積み上がらず、時間が延びるだけ。
        /// （ロン演出中は SetHP が毎フレーム呼ばれるため）
        /// </summary>
        public static void Play(int lostAmount, int maxHp)
        {
            var g = Ensure();
            float ratio = (maxHp > 0) ? Mathf.Abs(lostAmount) / (float)maxHp : 1f;
            g._strength = Mathf.Clamp01(ratio / FullStrengthDamageRatio);
            g._remain = Duration;
        }

        private void Build()
        {
            _blocks = new RawImage[BlockCount];
            for (int i = 0; i < BlockCount; i++)
            {
                var go = new GameObject("Block" + i, typeof(RectTransform));
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(transform, false);
                rt.anchorMin = new Vector2(0f, i / (float)BlockCount);
                rt.anchorMax = new Vector2(1f, (i + 1) / (float)BlockCount);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                var img = go.AddComponent<RawImage>();
                img.raycastTarget = false;
                img.enabled = false;
                _blocks[i] = img;
            }
        }

        private void OnEnable()
        {
            StartCoroutine(CaptureLoop());
        }

        /// <summary>
        /// 画面を定期的に保存し続ける。
        ///
        /// **`WaitForEndOfFrame` でなければならない。** 合成後の画面を撮るため。
        /// **走っている間は撮らない。** 撮るとノイズ自身が写り込んで、
        /// 歪んだ絵をさらに歪める入れ子になる。止めておけば、
        /// バッファには「殴られる前」の画面が残る。それがそのまま狙いになる。
        /// </summary>
        private IEnumerator CaptureLoop()
        {
            var wait = new WaitForEndOfFrame();
            while (true)
            {
                yield return wait;

                _frameCount++;
                if (_remain > 0f) continue;

                EnsureBuffers();
                if (_old1 == null || _old2 == null) continue;

                if (_frameCount % OldFrameInterval1 == 0) ScreenCapture.CaptureScreenshotIntoRenderTexture(_old1);
                if (_frameCount % OldFrameInterval2 == 0) ScreenCapture.CaptureScreenshotIntoRenderTexture(_old2);
            }
        }

        /// <summary>
        /// 保存先を用意する。`CaptureScreenshotIntoRenderTexture` は
        /// 画面と同じ大きさを要求するので、解像度が変わったら作り直す。
        /// </summary>
        private void EnsureBuffers()
        {
            int w = Screen.width;
            int h = Screen.height;
            if (w <= 0 || h <= 0) return;
            if (_old1 != null && _rtWidth == w && _rtHeight == h) return;

            ReleaseBuffers();
            _rtWidth = w;
            _rtHeight = h;
            _old1 = new RenderTexture(w, h, 0);
            _old2 = new RenderTexture(w, h, 0);
            _old1.Create();
            _old2.Create();
        }

        private void ReleaseBuffers()
        {
            if (_old1 != null) { _old1.Release(); Destroy(_old1); _old1 = null; }
            if (_old2 != null) { _old2.Release(); Destroy(_old2); _old2 = null; }
        }

        private void OnDestroy()
        {
            ReleaseBuffers();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_remain <= 0f)
            {
                HideAll();
                return;
            }

            _remain -= Time.unscaledDeltaTime;

            // 終わりに向けて弱くする。切れ際まで同じ強さだと止まり方が不自然
            float decay = Mathf.Clamp01(_remain / Duration);
            float power = _strength * decay;

            float maxShift = Screen.width * MaxShiftRatio * power;

            for (int i = 0; i < _blocks.Length; i++)
            {
                var img = _blocks[i];
                if (img == null) continue;

                if (Random.value > BlockShowChance * Mathf.Max(power, 0.25f))
                {
                    img.enabled = false;
                    continue;
                }

                // 帯ごとに「直前の記憶」と「少し前の記憶」を選び分ける
                var tex = (Random.value > 0.5f) ? _old1 : _old2;
                if (tex == null) { img.enabled = false; continue; }

                img.texture = tex;
                img.enabled = true;

                float v0 = i / (float)BlockCount;
                if (FlipY) v0 = 1f - (i + 1) / (float)BlockCount;

                // 横にずらすのは uv 側。rect を動かすと画面の端に隙間が空く
                float uShift = Random.Range(-1f, 1f) * MaxShiftRatio * power;
                img.uvRect = new Rect(uShift, v0, 1f, 1f / BlockCount);

                // 帯そのものも少しずらす。uv だけだと「中身が動く」だけで
                // 画面が割れて見えない
                var rt = img.rectTransform;
                rt.anchoredPosition = new Vector2(Random.Range(-1f, 1f) * maxShift, 0f);

                if (Random.value < ColorGapChance)
                    img.color = (Random.value > 0.5f) ? GapWarm : GapCool;
                else
                    img.color = Color.white;
            }
        }

        private void HideAll()
        {
            if (_blocks == null) return;
            for (int i = 0; i < _blocks.Length; i++)
            {
                if (_blocks[i] != null && _blocks[i].enabled)
                {
                    _blocks[i].enabled = false;
                    _blocks[i].rectTransform.anchoredPosition = Vector2.zero;
                }
            }
        }
    }
}
