using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 「準備完了」の小さな札。持ち主の体力表示（自分＝スマホ／相手＝点滴）の真下に出る。
    ///
    /// シーンに置かれている `ReadyBoxContainer` を実行時に組み直して使う。
    /// **対局シーンが2つある（UIテストシーン / OpeningScene）ので、見た目の調整値は
    /// SerializeField にせずここの定数で持つ。** シーンに焼き付くとコード側の変更が効かなくなる。
    ///
    /// 既存のラベルを使い回すのは、フォントを引き継ぐため。新しく TextMeshProUGUI を作ると
    /// TMP 既定の LiberationSans になり、「準備完了」が全部豆腐になる。
    ///
    /// 手牌選択・ベット・次局待ちの3か所から同じ札を使う。
    /// </summary>
    public class ReadyBadge : MonoBehaviour
    {
        // ---- 調整値（シーンではなくここを触る）----
        private const float Width = 104f;
        private const float Height = 24f;
        /// <summary>体力表示の下端から札までの隙間(px)</summary>
        private const float GapBelowHp = 14f;
        private const float BorderThickness = 2f;
        private const float FontSize = 14f;
        /// <summary>未確定のときに枠の色を落とす率。落としすぎると黒い帯にしか見えない</summary>
        private const float DimRate = 0.5f;

        /// <summary>
        /// 体力表示の矩形が絵より縦に大きい分の持ち上げ(px)。
        ///
        /// 自分のスマホは矩形（y170..320）と絵がほぼ一致するので 0 でよい。
        /// **相手は EnemyPanel の矩形が y310..480 なのに点滴の絵が y365 で終わっている。**
        /// 矩形の下端に合わせると絵から70pxほど離れ、札が翼の上に乗る。
        /// 2026-08-03 に Play 中で実測して詰めた値。
        /// </summary>
        private const float SelfLift = 0f;
        private const float EnemyLift = 55f;

        private static readonly Color SelfColor = new Color32(70, 140, 255, 255);
        private static readonly Color EnemyColor = new Color32(230, 60, 55, 255);
        private static readonly Color BackColor = new Color(0.06f, 0.06f, 0.08f, 0.92f);

        private const string ReadyText = "準備完了";

        private RectTransform _rect;
        private RectTransform _hpAnchor;
        private Image _border;
        private TextMeshProUGUI _label;
        private Color _tint;
        private float _lift;
        private bool _isReady;
        private bool _wantVisible;
        private bool _suppressed;

        /// <summary>
        /// シーンの ReadyBoxContainer を札に作り替える。すでに作り替えてあればそれを返す。
        /// </summary>
        /// <param name="container">シーンの ReadyBoxContainer</param>
        /// <param name="legacyCheck">旧デザインのチェック画像。札では使わないので隠す</param>
        /// <param name="hpAnchor">真下に置く体力表示（自分＝HPPanel／相手＝EnemyPanel）</param>
        /// <param name="isSelf">自分側なら true（枠が青）、相手側なら false（赤）</param>
        public static ReadyBadge Attach(GameObject container, GameObject legacyCheck,
                                        RectTransform hpAnchor, bool isSelf)
        {
            if (container == null) return null;

            var badge = container.GetComponent<ReadyBadge>();
            if (badge != null)
            {
                badge._hpAnchor = hpAnchor; // 参照が張り直された場合に備える
                return badge;
            }

            badge = container.AddComponent<ReadyBadge>();
            badge._hpAnchor = hpAnchor;
            badge._tint = isSelf ? SelfColor : EnemyColor;
            badge._lift = isSelf ? SelfLift : EnemyLift;
            badge.Build(legacyCheck);
            return badge;
        }

        private void Build(GameObject legacyCheck)
        {
            _rect = GetComponent<RectTransform>();
            _rect.sizeDelta = new Vector2(Width, Height);

            // 旧デザインのチェック画像は使わない。枠が光ることで状態を示す
            if (legacyCheck != null) legacyCheck.SetActive(false);

            // 既存のラベルを探す。フォントを引き継ぐため作り直さない
            _label = GetComponentInChildren<TextMeshProUGUI>(true);

            // 枠（持ち主の色）。既存の子より奥に置きたいので先頭へ差し込む
            var borderGo = new GameObject("Border", typeof(RectTransform));
            var borderRect = borderGo.GetComponent<RectTransform>();
            borderRect.SetParent(_rect, false);
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            borderRect.SetAsFirstSibling();
            _border = borderGo.AddComponent<Image>();
            _border.raycastTarget = false;

            // 内側の暗い塗り。賭け金フィールドと同じ様式（外枠が持ち主の色・中は暗い）
            var innerGo = new GameObject("Inner", typeof(RectTransform));
            var innerRect = innerGo.GetComponent<RectTransform>();
            innerRect.SetParent(borderRect, false);
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(BorderThickness, BorderThickness);
            innerRect.offsetMax = new Vector2(-BorderThickness, -BorderThickness);
            var innerImg = innerGo.AddComponent<Image>();
            innerImg.color = BackColor;
            innerImg.raycastTarget = false;

            if (_label != null)
            {
                // 旧デザインは x-118.91 押し出されていて、盤面の牌やドラの上に乗っていた。
                // 札の中央へ引き戻す
                var lrt = _label.rectTransform;
                lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
                lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.sizeDelta = new Vector2(Width - 6f, Height);
                lrt.anchoredPosition = Vector2.zero;
                lrt.localScale = Vector3.one;

                _label.text = ReadyText;
                _label.fontSize = FontSize;
                _label.enableAutoSizing = false;
                _label.alignment = TextAlignmentOptions.Center;
                _label.color = Color.white;
                _label.raycastTarget = false;
                _label.transform.SetAsLastSibling();
            }

            ApplyState();
        }

        /// <summary>札を出す／隠す。</summary>
        public void SetVisible(bool visible)
        {
            _wantVisible = visible;
            ApplyVisibility();
        }

        /// <summary>
        /// 一時的に伏せる。ベット中にスマホが4.5倍へ拡大すると札がその裏に入るので、
        /// 拡大している間だけ伏せて、縮み終わってから出し直す。
        /// 「出す／隠す」とは別軸で持つ。フェイズ側の意思を上書きせずに戻せるようにするため。
        /// </summary>
        public void SetSuppressed(bool suppressed)
        {
            _suppressed = suppressed;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            bool show = _wantVisible && !_suppressed;
            gameObject.SetActive(show);
            if (!show) return;

            // 位置合わせは表示してから。非アクティブのままだと測れずに飛ぶ
            AlignUnderHp();
        }

        /// <summary>準備完了かどうか。未確定は枠が暗く、文字が出ない。</summary>
        public void SetReady(bool ready)
        {
            _isReady = ready;
            ApplyState();
        }

        private void ApplyState()
        {
            if (_border != null)
            {
                _border.color = _isReady
                    ? _tint
                    : new Color(_tint.r * DimRate, _tint.g * DimRate, _tint.b * DimRate, 0.9f);
            }
            // 未確定は空の札にする。文字が出ているのに未確定、という紛らわしさを避ける
            if (_label != null) _label.enabled = _isReady;
        }

        /// <summary>
        /// 体力表示の下端の真下へ置く。
        /// アンカーが点（anchorMin == anchorMax）なので sizeDelta がそのまま実寸になり、
        /// レイアウト確定を待たずに測れる。
        /// </summary>
        private void AlignUnderHp()
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            if (_hpAnchor == null) return;

            float hpHeight = _hpAnchor.sizeDelta.y;
            float hpBottom = _hpAnchor.anchoredPosition.y - hpHeight * _hpAnchor.pivot.y;

            _rect.anchorMin = _rect.anchorMax = _hpAnchor.anchorMin;
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.sizeDelta = new Vector2(Width, Height);
            _rect.anchoredPosition = new Vector2(
                _hpAnchor.anchoredPosition.x,
                hpBottom - GapBelowHp - Height * 0.5f + _lift);
        }
    }
}
