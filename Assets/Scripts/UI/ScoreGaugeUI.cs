using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 獲得ポイントのゲージ。画面を横切る1本の帯で、中央の王冠が勝利ライン。
    ///
    /// 左半分が相手、右半分が自分。それぞれ外側の端から中央へ向かって伸び、
    /// 王冠まで届いたら勝ち（＝目標値に到達）。
    /// 局ごとの獲得額（liquidation の winner_gain）を積み上げて表示する。
    ///
    /// **勝敗の判定はここでは行わない。** 判定はサーバーの担当で、
    /// ここは受け取った額を積んで見せるだけ。
    /// サーバーが獲得での決着に対応するまでは、表示のみが先行する状態になる。
    ///
    /// 専用の画像アセットは使わず、実行時に組み立てる。
    /// </summary>
    public class ScoreGaugeUI : MonoBehaviour
    {
        [Header("ルール")]
        [Tooltip("この値に到達したら勝ち。サーバー側の判定と必ず揃えること")]
        [SerializeField] private int targetScore = 30000;

        [Header("配置")]
        [Tooltip("帯の全幅(px)。左右あわせた長さ")]
        [SerializeField] private float barWidth = 400f;
        [Tooltip("帯の高さ(px)")]
        [SerializeField] private float barHeight = 26f;
        [Tooltip("画面上端からの距離(px)")]
        [SerializeField] private float topMargin = 14f;
        [Tooltip("中央の王冠の大きさ(px)")]
        [SerializeField] private float crownSize = 30f;
        [Header("賭け金のフィールド")]
        [Tooltip("賭け金を出す枠の幅(px)")]
        [SerializeField] private float stakeFieldWidth = 104f;
        [Tooltip("賭け金を出す枠の高さ(px)")]
        [SerializeField] private float stakeFieldHeight = 24f;
        [Tooltip("帯の下端から枠までの隙間(px)")]
        [SerializeField] private float stakeGap = 4f;
        [Tooltip("帯の外端から枠までの寄せ(px)")]
        [SerializeField] private float stakeInset = 6f;

        [Header("吸収演出")]
        [Tooltip("賭け金がゲージへ吸い込まれるまでの秒数")]
        [SerializeField] private float absorbDuration = 0.45f;

        [Header("色")]
        [Tooltip("自分（右）")]
        [SerializeField] private Color selfColor = new Color32(70, 140, 255, 255);
        [Tooltip("相手（左）")]
        [SerializeField] private Color enemyColor = new Color32(230, 60, 55, 255);
        [SerializeField] private Color backColor = new Color(0.06f, 0.06f, 0.08f, 0.85f);
        [SerializeField] private Color crownColor = new Color32(255, 190, 40, 255);

        private RectTransform _selfFill, _enemyFill;
        private TextMeshProUGUI _selfText, _enemyText;
        private TextMeshProUGUI _selfStake, _enemyStake;
        private RectTransform _selfStakeField, _enemyStakeField;
        private int _selfScore, _enemyScore;
        private int _selfStakeValue, _enemyStakeValue;
        private Coroutine _selfAnim, _enemyAnim, _absorbAnim;
        private bool _built;

        /// <summary>片側の長さ。ここいっぱいで目標到達</summary>
        private float HalfWidth => (barWidth - crownSize) * 0.5f;

        public int SelfScore => _selfScore;
        public int EnemyScore => _enemyScore;

        private void Awake()
        {
            EnsureBuilt();
            Refresh(true);
        }

        /// <summary>
        /// 中身を作る。Awake はエディタ停止中には呼ばれないので、
        /// 公開メソッド側からも通して確実に組み上がるようにしている。
        /// </summary>
        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            Build();
        }

        private void Build()
        {
            var canvasGo = new GameObject("ScoreGaugeCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 盤面の情報表示なので「場の血」と同じ段。演出の黒帯より奥に居るべきもの
            canvas.sortingOrder = KillingMahjong.Common.UISortingOrders.BetPot;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800, 600);
            scaler.matchWidthOrHeight = 0f;

            // 帯の土台。画面上端の中央に置く
            var barGo = new GameObject("Bar", typeof(RectTransform));
            var bar = barGo.GetComponent<RectTransform>();
            bar.SetParent(canvasGo.transform, false);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(barWidth, barHeight);
            bar.anchoredPosition = new Vector2(0f, -topMargin);

            var bg = barGo.AddComponent<Image>();
            bg.color = backColor;
            bg.raycastTarget = false;
            var ol = barGo.AddComponent<Outline>();
            ol.effectColor = Color.black;
            ol.effectDistance = new Vector2(2f, -2f);

            // 右＝自分、左＝相手。どちらも外側の端から中央へ伸ばす
            BuildHalf(bar, true,  selfColor,  out _selfFill,  out _selfText);
            BuildHalf(bar, false, enemyColor, out _enemyFill, out _enemyText);

            BuildCrown(bar);

            // 賭け金は帯のすぐ下。決着でここからゲージへ吸い込まれる
            BuildStake(bar, true,  selfColor,  out _selfStakeField,  out _selfStake);
            BuildStake(bar, false, enemyColor, out _enemyStakeField, out _enemyStake);
        }

        /// <summary>
        /// 今その人が賭けている額を出す枠。帯の下、自分の側の端に寄せて置く。
        /// 枠の色で持ち主が分かるようにし、数字は白で大きめに出す。
        /// 決着したらゲージへ吸い込まれるので、位置は動かす前提で持っておく。
        /// </summary>
        private void BuildStake(RectTransform bar, bool isRight, Color tint,
                                out RectTransform field, out TextMeshProUGUI label)
        {
            var go = new GameObject(isRight ? "SelfStakeField" : "EnemyStakeField", typeof(RectTransform));
            field = go.GetComponent<RectTransform>();
            field.SetParent(bar, false);
            field.anchorMin = field.anchorMax = new Vector2(isRight ? 1f : 0f, 0f);
            field.pivot = new Vector2(isRight ? 1f : 0f, 1f);
            field.sizeDelta = new Vector2(stakeFieldWidth, stakeFieldHeight);
            field.anchoredPosition = new Vector2(isRight ? -stakeInset : stakeInset, -stakeGap);

            // 外枠を持ち主の色、内側を暗く塗って縁取りにする
            var border = go.AddComponent<Image>();
            border.color = tint;
            border.raycastTarget = false;

            var innerGo = new GameObject("Inner", typeof(RectTransform));
            var inner = innerGo.GetComponent<RectTransform>();
            inner.SetParent(field, false);
            inner.anchorMin = Vector2.zero;
            inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(2f, 2f);
            inner.offsetMax = new Vector2(-2f, -2f);
            var innerImg = innerGo.AddComponent<Image>();
            innerImg.color = backColor;
            innerImg.raycastTarget = false;

            // 吸収演出でこの数字だけを動かすので、伸縮アンカーではなく中央固定にする。
            // 伸縮のままだとレイアウトの再計算で位置が戻されてしまう。
            var textGo = new GameObject("Value", typeof(RectTransform));
            var trt = textGo.GetComponent<RectTransform>();
            trt.SetParent(field, false);
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(stakeFieldWidth - 8f, stakeFieldHeight);
            trt.anchoredPosition = Vector2.zero;

            label = textGo.AddComponent<TextMeshProUGUI>();
            label.text = "0";
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = 11f;
            label.fontSizeMax = 18f;
            label.fontMaterial.EnableKeyword("OUTLINE_ON");
            label.outlineColor = Color.black;
            label.outlineWidth = 0.2f;
        }

        private void BuildHalf(RectTransform bar, bool isRight, Color fillColor,
                               out RectTransform fill, out TextMeshProUGUI label)
        {
            var halfGo = new GameObject(isRight ? "SelfHalf" : "EnemyHalf", typeof(RectTransform));
            var half = halfGo.GetComponent<RectTransform>();
            half.SetParent(bar, false);
            half.anchorMin = half.anchorMax = new Vector2(isRight ? 1f : 0f, 0.5f);
            half.pivot = new Vector2(isRight ? 1f : 0f, 0.5f);
            half.sizeDelta = new Vector2(HalfWidth, barHeight - 6f);
            half.anchoredPosition = new Vector2(isRight ? -3f : 3f, 0f);

            // 伸びる部分。外側の端に貼り付けて、中央へ向かって長くする
            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fill = fillGo.GetComponent<RectTransform>();
            fill.SetParent(half, false);
            fill.anchorMin = new Vector2(isRight ? 1f : 0f, 0f);
            fill.anchorMax = new Vector2(isRight ? 1f : 0f, 1f);
            fill.pivot = new Vector2(isRight ? 1f : 0f, 0.5f);
            fill.anchoredPosition = Vector2.zero;
            fill.sizeDelta = new Vector2(0f, 0f);

            var fimg = fillGo.AddComponent<Image>();
            fimg.color = fillColor;
            fimg.raycastTarget = false;

            // 数字は自分側の端寄せ。0 のときも必ず出すので、伸びる部分ではなく枠に置く
            var textGo = new GameObject("Value", typeof(RectTransform));
            var trt = textGo.GetComponent<RectTransform>();
            trt.SetParent(half, false);
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8f, 0f);
            trt.offsetMax = new Vector2(-8f, 0f);

            label = textGo.AddComponent<TextMeshProUGUI>();
            label.text = "0";
            label.color = Color.white;
            label.alignment = isRight ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            label.raycastTarget = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = 10f;
            label.fontSizeMax = 16f;
            // 伸びた部分と重なっても読めるように縁取る
            label.fontMaterial.EnableKeyword("OUTLINE_ON");
            label.outlineColor = Color.black;
            label.outlineWidth = 0.22f;
        }

        /// <summary>
        /// 中央の王冠。ここが勝利ライン（目標値）。
        /// フォントの記号（♛）はTMPの既定フォントに無いことがあるので、
        /// ドット絵のテクスチャを生成して貼る。専用の画像が来たらこれを差し替える。
        /// </summary>
        private void BuildCrown(RectTransform bar)
        {
            var go = new GameObject("Crown", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(bar, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // 元絵は 11x8。縦横比を保ったまま crownSize に収める
            rt.sizeDelta = new Vector2(crownSize, crownSize * 8f / 11f);
            rt.anchoredPosition = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.sprite = BuildCrownSprite();
            img.raycastTarget = false;
            img.color = Color.white; // 色はテクスチャ側で塗る
        }

        /// <summary>
        /// 11x8 のドット絵の王冠。黒フチを焼き込んである。
        /// '#' が王冠の色、'.' が黒フチ、' ' が透明。
        /// </summary>
        private Sprite BuildCrownSprite()
        {
            string[] rows =
            {
                "...........",
                ".#...#...#.",
                ".#..###..#.",
                ".#########.",
                ".#########.",
                ".#.#####.#.",
                "..#######..",
                "...........",
            };

            int w = rows[0].Length;
            int h = rows.Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,   // ドットをぼかさない
                wrapMode = TextureWrapMode.Clamp
            };

            var clear = new Color(0f, 0f, 0f, 0f);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Texture2D は下が y=0 なので行を反転して読む
                    char c = rows[h - 1 - y][x];
                    px[y * w + x] = c == '#' ? crownColor : clear;
                }
            }

            // 塗ったドットの周り1マスを黒で囲む（あとから塗ると角も拾える）
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a > 0f) continue;
                    bool touching = false;
                    for (int dy = -1; dy <= 1 && !touching; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            char c = rows[h - 1 - ny][nx];
                            if (c == '#') { touching = true; break; }
                        }
                    }
                    if (touching) px[y * w + x] = Color.black;
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        /// <summary>局の決着で得た額を積む。</summary>
        public void AddScore(bool isSelf, int gain)
        {
            EnsureBuilt();
            if (gain <= 0) return;
            if (isSelf) _selfScore += gain;
            else _enemyScore += gain;
            Refresh(false);
        }

        /// <summary>対局のやり直しなどで積み上げを捨てる。賭け金の表示も戻す。</summary>
        public void ResetScores()
        {
            EnsureBuilt();
            if (_absorbAnim != null) { StopCoroutine(_absorbAnim); _absorbAnim = null; }
            RestoreStakeLabels();
            _selfScore = 0;
            _enemyScore = 0;
            _selfStakeValue = 0;
            _enemyStakeValue = 0;
            RefreshStakes();
            Refresh(true);
        }

        /// <summary>吸収演出の最中か。</summary>
        public bool IsAbsorbing => _absorbAnim != null;

        /// <summary>今この局で賭けている額を設定し直す。</summary>
        public void SetStakes(int player, int enemy)
        {
            EnsureBuilt();
            // 吸収中に 0 を書き込まれると、飛んでいる途中の数字が消えてしまう。
            // 演出の終わりに自分で 0 にするので、その間は外からの書き込みを無視する。
            if (IsAbsorbing) return;
            _selfStakeValue = Mathf.Max(0, player);
            _enemyStakeValue = Mathf.Max(0, enemy);
            RefreshStakes();
        }

        /// <summary>
        /// 賭け金が確定したときに呼ぶ。既に積まれている分へ積み増す。
        /// 流局では決着しないので次の局へ持ち越され、そこでも同額が賭けられる。
        /// </summary>
        public void AddStakes(int player, int enemy)
        {
            SetStakes(_selfStakeValue + player, _enemyStakeValue + enemy);
        }

        private void RefreshStakes()
        {
            // 枠は常に出しておく。賭けていないときは 0 と出す（ゲージの数字と同じ扱い）
            if (_selfStake != null) _selfStake.text = _selfStakeValue.ToString();
            if (_enemyStake != null) _enemyStake.text = _enemyStakeValue.ToString();
        }

        /// <summary>
        /// 決着したときに呼ぶ。場に出ている賭け金が勝者のゲージへ吸い込まれ、
        /// 吸い終わってからゲージが伸びる。
        /// gain は liquidation の winner_gain（勝者が実際に受け取る額）。
        /// </summary>
        public void AbsorbStakesIntoGauge(bool winnerIsSelf, int gain)
        {
            EnsureBuilt();

            bool nothingToAbsorb = _selfStakeValue <= 0 && _enemyStakeValue <= 0;
            if (nothingToAbsorb || !Application.isPlaying || !isActiveAndEnabled)
            {
                _selfStakeValue = 0;
                _enemyStakeValue = 0;
                RefreshStakes();
                AddScore(winnerIsSelf, gain);
                return;
            }

            if (_absorbAnim != null) StopCoroutine(_absorbAnim);
            _absorbAnim = StartCoroutine(AbsorbRoutine(winnerIsSelf, gain));
        }

        private IEnumerator AbsorbRoutine(bool winnerIsSelf, int gain)
        {
            // 吸い込み先は勝者側の半分の真ん中。TransformPoint(0) だとピボット＝外端に
            // 飛んでしまうので、rect の中心を使ってゲージの内側へ入れる
            var winnerHalf = (winnerIsSelf ? _selfFill : _enemyFill).parent as RectTransform;
            Vector3 targetWorld = winnerHalf.TransformPoint(winnerHalf.rect.center);

            // **枠は動かさない。** 中の数字だけが枠から飛び出してゲージへ吸い込まれる
            var labels = new[] { _selfStake, _enemyStake };
            var deltas = new Vector2[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                var lbl = labels[i];
                if (lbl == null || !lbl.gameObject.activeInHierarchy) continue;
                var rt = lbl.rectTransform;
                // 狙う点を数字の親（枠）の座標系へ移し、いま居る場所との差を出す
                Vector3 local = rt.parent.InverseTransformPoint(targetWorld);
                deltas[i] = new Vector2(local.x, local.y) - (Vector2)rt.localPosition;
            }

            float t = 0f;
            while (t < absorbDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / absorbDuration);
                float e = p * p; // 吸い寄せられるので終わりにかけて加速させる
                for (int i = 0; i < labels.Length; i++)
                {
                    var lbl = labels[i];
                    if (lbl == null || !lbl.gameObject.activeInHierarchy) continue;
                    var rt = lbl.rectTransform;
                    rt.anchoredPosition = Vector2.Lerp(Vector2.zero, deltas[i], e);
                    float s = Mathf.Lerp(1f, 0.4f, e);
                    rt.localScale = new Vector3(s, s, 1f);
                    lbl.alpha = 1f - e;
                }
                yield return null;
            }

            _selfStakeValue = 0;
            _enemyStakeValue = 0;
            RefreshStakes();
            RestoreStakeLabels();

            AddScore(winnerIsSelf, gain);
            _absorbAnim = null;
        }

        /// <summary>吸い込みで飛ばした数字を枠の中央へ戻す。枠自体は動かしていない。</summary>
        private void RestoreStakeLabels()
        {
            RestoreOne(_selfStake);
            RestoreOne(_enemyStake);
        }

        private static void RestoreOne(TextMeshProUGUI label)
        {
            if (label == null) return;
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.rectTransform.localScale = Vector3.one;
            label.alpha = 1f;
        }

        private void Refresh(bool immediate)
        {
            SetGauge(ref _selfAnim, _selfFill, _selfText, _selfScore, immediate);
            SetGauge(ref _enemyAnim, _enemyFill, _enemyText, _enemyScore, immediate);
        }

        private void SetGauge(ref Coroutine anim, RectTransform fill, TextMeshProUGUI label, int score, bool immediate)
        {
            if (fill == null) return;
            if (label != null) label.text = score.ToString();

            float ratio = targetScore > 0 ? Mathf.Clamp01(score / (float)targetScore) : 0f;
            float target = HalfWidth * ratio;

            // 再生中でなければコルーチンが回らないので、その場で反映する
            if (immediate || !Application.isPlaying || !isActiveAndEnabled)
            {
                fill.sizeDelta = new Vector2(target, fill.sizeDelta.y);
                return;
            }
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(GrowRoutine(fill, target));
        }

        private IEnumerator GrowRoutine(RectTransform fill, float target)
        {
            float from = fill.sizeDelta.x;
            float t = 0f;
            const float dur = 0.6f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);
                p = 1f - Mathf.Pow(1f - p, 3f); // 勢いよく伸びて着地で緩める
                fill.sizeDelta = new Vector2(Mathf.Lerp(from, target, p), fill.sizeDelta.y);
                yield return null;
            }
            fill.sizeDelta = new Vector2(target, fill.sizeDelta.y);
        }

        public void SetVisible(bool v)
        {
            EnsureBuilt();
            for (int i = 0; i < transform.childCount; i++)
                transform.GetChild(i).gameObject.SetActive(v);
        }
    }
}
