using System.Collections.Generic;
using TMPro;
using UnityEngine;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 左上の打牌数を、文字（「後：三打目」）ではなく円グラフで見せる（2026-11-08 ユーザー指示
    /// 「左上の先攻、後攻は円グラフにしてみようかな。先攻、後攻は色で示す感じで」）。
    ///
    /// 1局で打てるのは1人 <see cref="DiscardsPerPlayer"/> 打。円を同じ数に切り、
    /// 打ち終えたぶんを12時から時計回りに塗る。これから打つ1つは明るい色で示す。
    /// **先攻か後攻かは塗りの色で分ける**（先攻＝金、後攻＝青）。
    ///
    /// ドット絵に合わせ、2単位の升目ごとに色を決めて並べる（滑らかな扇形は描かない）。
    /// 置き場所はシーン（RiverUI の turnPie）。編集中も見本の値で描くので、シーンで位置を合わせられる。
    ///
    /// **進むたびに波紋を出す（2026-11-21 ユーザー指示「ターンメーターも進むたびに波紋出してほしい。
    /// 5ターンごとに少し強めの演出して、3,2,1前は徐々に終わる雰囲気の演出を」）。**
    ///   ・ふだん: 塗りの色の輪が1つ広がる
    ///   ・5打ごと（5・10）: 輪が2つ＋塗ったばかりの1切れが白く光る
    ///   ・残り3・2・1: 輪が血の赤になり、残りが減るほど数が増えて遅く・大きくなる。
    ///     円の縁が心臓の鼓動のように赤く脈打ち、残りが減るほど速く・強くなる。空きの切れも暗い赤に沈む
    ///     （15打目は5打ごとの節目でもあるが、こちらを優先する）
    ///   ・打ち切り（残り0）: 暗い赤の輪が1つ、ゆっくり広がって脈が止まる
    /// 輪は円グラフと同じ板に描く（新しい部品は足さない）。
    ///
    /// **円の真ん中に、今のターンの数字を出す（2026-11-22 ユーザー指示「ターン数を表す円グラフのところは
    /// 現在のターンの数字表示してほしい」）。** 数字は**残りのターン数**で、17 から 1 打ごとに減り、打ち切りで 0
    /// （2026-11-22 Discord「数字が増えるんじゃなくて、減る方式でお願いします」。前は何打目かを 1〜17 で増やしていた）。
    /// 文字はシーンに置いた子（TurnNumber）で、ここは数字を入れるだけ。
    ///
    /// **相手の番のあいだは、塗りを赤にする（2026-11-22「相手ターンの時はあかいろにしてほしい」）。**
    /// 自分の番に戻ると、先攻＝金・後攻＝青に戻る。
    /// </summary>
    public class TurnPieUI : PixelShapeGraphic
    {
        /// <summary>1人が1局で打てる数。サーバーの DISCARDS_PER_PLAYER（mahjong_engine/engine/game_engine.py）と同じ。</summary>
        public const int DiscardsPerPlayer = 17;

        /// <summary>コレクションの「演出」タブの ID（<see cref="GameEffectCatalog"/>）。</summary>
        public const string EffectId = "turn.pie";

        [SerializeField] private float radius = 22f;
        [SerializeField] private Color firstColor = new Color32(232, 180, 64, 255);
        [SerializeField] private Color firstNextColor = new Color32(255, 232, 160, 255);
        [SerializeField] private Color secondColor = new Color32(72, 132, 220, 255);
        [SerializeField] private Color secondNextColor = new Color32(170, 210, 255, 255);
        [Tooltip("相手の番のときの塗りの色（2026-11-22「相手ターンの時はあかいろにしてほしい」）")]
        [SerializeField] private Color opponentColor = new Color32(206, 44, 54, 255);
        [SerializeField] private Color opponentNextColor = new Color32(255, 150, 156, 255);
        [SerializeField] private Color emptyColor = new Color32(26, 14, 16, 220);
        [SerializeField] private Color edgeColor = new Color32(12, 6, 8, 255);
        [Tooltip("円の真ん中に出す、今のターンの数字（シーンの子 TurnNumber）。空なら数字は出さない")]
        [SerializeField] private TMP_Text numberLabel;

        /// <summary>残りわずかの輪と脈の色（役名表示の血と同じ系統）。</summary>
        private static readonly Color BloodColor = new Color(0.80f, 0.05f, 0.10f, 1f);
        private static readonly Color DeepBloodColor = new Color(0.40f, 0.01f, 0.04f, 1f);
        private static readonly Color BoneColor = new Color(0.96f, 0.90f, 0.80f, 1f);

        /// <summary>何打ごとに強めの演出にするか。</summary>
        private const int MilestoneEvery = 5;
        /// <summary>残りがこの数以下になったら「終わりが近い」演出にする。</summary>
        private const int EndingFrom = 3;
        private const float SliceFlashSeconds = 0.35f;

        // 編集中に描く見本（先攻で5打済み）
        private int _discarded = 5;
        private bool _first = true;
        private bool _shownOnce;

        private struct Ripple
        {
            public float Start;
            public float Seconds;
            public float From;
            public float To;
            public float Thickness;
            public Color Color;
        }

        private readonly List<Ripple> _ripples = new List<Ripple>();
        private float _sliceFlashStart = -10f;

        /// <summary>打ち終えた数と、自分が先攻か。</summary>
        public void Show(int discarded, bool first)
        {
            int next = Mathf.Clamp(discarded, 0, DiscardsPerPlayer);
            // 1つ進んだときだけ演出する（局の頭の 0 や、作り直しで一気に飛んだときは出さない）
            bool advanced = _shownOnce && Application.isPlaying && next == _discarded + 1 && first == _first;
            _discarded = next;
            _first = first;
            _shownOnce = true;
            if (advanced) PlayAdvance(next);
            else { _ripples.Clear(); _sliceFlashStart = -10f; }
            UpdateNumber();
            Redraw();
        }

        /// <summary>今のターン（これから打つのが何打目か）。打ち切りのあとは最後の 17 のまま。</summary>
        public int CurrentTurn => Mathf.Min(_discarded + 1, DiscardsPerPlayer);

        private void UpdateNumber()
        {
            // 残りのターン数（減っていく）。何打目か（CurrentTurn）ではない
            if (numberLabel != null) numberLabel.text = Remaining.ToString();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            UpdateNumber();
            Redraw();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            UpdateNumber();
            Redraw();
        }
#endif

        private int Remaining => DiscardsPerPlayer - _discarded;
        private bool IsEnding => Remaining > 0 && Remaining <= EndingFrom;
        /// <summary>
        /// 演出の時計。1コマで進む量に上限（1/30秒）を付けて自前で数える。
        /// 実時間（unscaledTime）だと、画面が一度引っかかったときに輪が1コマも描かれずに終わる
        /// （<see cref="HandRankCallUI"/> の Step と同じ理由）。
        /// </summary>
        private float _clock;
        private float Now => _clock;

        /// <summary>1つ進んだ瞬間の輪を積む。</summary>
        private void PlayAdvance(int discarded)
        {
            int remaining = DiscardsPerPlayer - discarded;
            Color done = DoneColor;
            float t = Now;

            if (remaining <= 0)
            {
                // 打ち切り: 暗い赤の輪が1つ、ゆっくり広がる
                AddRipple(t, 1.2f, radius, radius * 3.2f, 4f, DeepBloodColor);
                return;
            }
            if (remaining <= EndingFrom)
            {
                // 残り3→1と減るほど、輪が増えて遅く・大きくなる
                int count = EndingFrom + 1 - remaining; // 残り3で1つ、2で2つ、1で3つ
                float seconds = Mathf.Lerp(0.8f, 1.1f, (count - 1) / 2f);
                for (int i = 0; i < count; i++)
                    AddRipple(t + i * 0.16f, seconds, radius, radius * (2.4f + 0.4f * count), 4f, i % 2 == 0 ? BloodColor : BoneColor);
                return;
            }
            if (discarded % MilestoneEvery == 0)
            {
                // 5打ごと: 輪が2つ＋塗ったばかりの切れが白く光る
                AddRipple(t, 0.65f, radius, radius * 2.8f, 4f, BoneColor);
                AddRipple(t + 0.1f, 0.65f, radius, radius * 2.4f, 4f, done);
                _sliceFlashStart = t;
                return;
            }
            // ふだん: 塗りの色の輪が1つ
            AddRipple(t, 0.5f, radius, radius * 2f, 2f, done);
        }

        private void AddRipple(float start, float seconds, float from, float to, float thickness, Color color)
        {
            _ripples.Add(new Ripple { Start = start, Seconds = seconds, From = from, To = to, Thickness = thickness, Color = color });
        }

        /// <summary>相手の番か。相手の番のあいだは、塗りを赤にする（先攻・後攻の金・青の代わり）。</summary>
        private bool _opponentTurn;

        /// <summary>塗った切れの色。相手の番は赤、自分の番は先攻＝金・後攻＝青。</summary>
        private Color DoneColor => _opponentTurn ? opponentColor : (_first ? firstColor : secondColor);

        /// <summary>
        /// コレクションの試写用。盤面が無いので、相手の番かどうかをこちらで決める（null で盤面に従う）。
        /// </summary>
        public bool? PreviewOpponentTurn { get; set; }

        /// <summary>
        /// コレクションの試写用。シーンに置いた円グラフが無い場面では数字の子も無いので、試写が作った字を渡す。
        /// 本編・チュートリアルはシーンの TurnNumber を使う。
        /// </summary>
        public void BindPreviewNumberLabel(TMP_Text label)
        {
            numberLabel = label;
            UpdateNumber();
        }

        /// <summary>数字の字を持っているか（試写が足すかどうかを決める）。</summary>
        public bool HasNumberLabel => numberLabel != null;

        private void Update()
        {
            if (!Application.isPlaying) return;
            // 番が替わったら色を塗り直す（BoardStateManager.IsLocalTurn を毎フレーム見る）
            var board = KillingMahjong.Managers.BoardStateManager.Instance;
            bool opponent = PreviewOpponentTurn ?? (board != null && !board.IsLocalTurn);
            if (opponent != _opponentTurn) { _opponentTurn = opponent; Redraw(); }
            _clock += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            // 輪が出ている間・光っている間・終わり際の脈がある間だけ描き直す
            if (_ripples.Count > 0 || Now - _sliceFlashStart < SliceFlashSeconds || IsEnding) Redraw();
        }

        /// <summary>
        /// 終わり際の脈（0〜1）。心臓の鼓動のように2回打って休む。残りが減るほど間隔が短く、強い。
        /// </summary>
        private float Heartbeat()
        {
            if (!IsEnding || !Application.isPlaying) return 0f;
            int r = Remaining;
            float period = r == 3 ? 1.3f : r == 2 ? 0.95f : 0.65f;
            float strength = r == 3 ? 0.45f : r == 2 ? 0.7f : 1f;
            float p = (Now % period) / period;
            float beat = Mathf.Max(Pulse(p, 0f), 0.6f * Pulse(p, 0.22f));
            return beat * strength;
        }

        private static float Pulse(float p, float at)
        {
            float d = p - at;
            if (d < 0f || d > 0.18f) return 0f;
            return 1f - d / 0.18f;
        }

        private void Redraw()
        {
            const float cell = 2f; // 1ドット＝2単位
            float slice = 360f / DiscardsPerPlayer;
            Color done = DoneColor;
            Color next = _opponentTurn ? opponentNextColor : (_first ? firstNextColor : secondNextColor);

            float beat = Heartbeat();
            Color edge = Color.Lerp(edgeColor, BloodColor, beat);
            // 終わり際は空きの切れを暗い赤に沈める（残りが減るほど濃く）
            Color empty = emptyColor;
            if (IsEnding)
            {
                empty = Color.Lerp(emptyColor, DeepBloodColor, 0.35f + 0.15f * (EndingFrom - Remaining) + 0.25f * beat);
                empty.a = emptyColor.a;
            }
            float flash = Application.isPlaying ? 1f - Mathf.Clamp01((Now - _sliceFlashStart) / SliceFlashSeconds) : 0f;
            // 脈が強い瞬間は、縁を1ドット太らせる
            float edgeOuter = radius + (beat > 0.5f ? 2f * cell : cell);
            float outer = radius + 2f * cell;

            Begin();
            for (float y = -outer; y < outer; y += cell)
            {
                // 横に同じ色が続く升目はまとめて1枚にする
                float runStart = 0f;
                Color runColor = Color.clear;
                bool inRun = false;
                for (float x = -outer; x <= outer; x += cell)
                {
                    Color c = CellColor(x + cell * .5f, y + cell * .5f, slice, done, next, empty, edge, edgeOuter, flash);
                    if (inRun && c == runColor) continue;
                    if (inRun) Box(runStart, y, x, y + cell, runColor);
                    inRun = c.a > 0f;
                    runStart = x;
                    runColor = c;
                }
                if (inRun) Box(runStart, y, outer + cell, y + cell, runColor);
            }
            DrawRipples();
            End();
        }

        private void DrawRipples()
        {
            if (_ripples.Count == 0) return;
            float now = Now;
            for (int i = _ripples.Count - 1; i >= 0; i--)
            {
                var r = _ripples[i];
                float u = (now - r.Start) / r.Seconds;
                if (u > 1f) { _ripples.RemoveAt(i); continue; }
                if (u < 0f) continue;
                float e = 1f - (1f - u) * (1f - u);
                var c = r.Color;
                c.a = Mathf.Pow(1f - u, 1.3f);
                float thick = Mathf.Max(2f, Mathf.Round(Mathf.Lerp(r.Thickness, 2f, u) / 2f) * 2f);
                PixelRing(Vector2.zero, Mathf.Lerp(r.From, r.To, e), thick, 2f, c);
            }
        }

        private Color CellColor(float x, float y, float slice, Color done, Color next, Color empty, Color edge, float edgeOuter, float flash)
        {
            float d = Mathf.Sqrt(x * x + y * y);
            if (d > edgeOuter) return Color.clear;
            if (d > radius) return edge;
            // 12時から時計回りの角度
            float angle = Mathf.Atan2(x, y) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            int index = Mathf.FloorToInt(angle / slice);
            if (index < _discarded)
            {
                // 5打ごとの節目は、塗ったばかりの1切れを白く光らせる
                if (flash > 0f && index == _discarded - 1) return Color.Lerp(done, Color.white, flash);
                return done;
            }
            if (index == _discarded) return next;
            return empty;
        }
    }
}
