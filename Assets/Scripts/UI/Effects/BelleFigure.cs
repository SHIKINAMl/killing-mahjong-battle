using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 演出の中に出てくる悪魔ベルル（板を持った白い悪魔。ギャンブルの悪魔で、能力は彼に血を払って使う）。
    ///
    /// **絵はもとからある物だけを使う。** AIで作った絵・新しく描いた絵は無い。
    ///   出てくる所 … スキル一覧が開くときのコマ（`Resources/UI_Anim1〜7`）。
    ///                 黄色い輪が描かれ、そこからベルルが転がり出て、板を持って構える
    ///   目が光る所 … 自分のスキルのカットインの絵（`skill_cutin_player`。同じ絵で、目が黄色い）
    /// この2つは同じ大きさの同じ絵なので、重ねる位置を合わせて差し替えると「目が光った」に見える。
    ///
    /// 絵の寸法（測った値。2026-10-09）
    ///   コマ           … 1010x1570。ベルルが居るのは上のほうの (230,50)-(850,500)
    ///   カットインの絵 … 660x510。ベルルが居るのは (30,60)-(650,510)
    /// どちらも同じ絵（620x450）で、コマの (x+200, y-10) がカットインの (x, y) に当たる。
    ///
    /// **役強化（<see cref="BoostHandSkillEffect"/>）は、同じ数字を自前で持っている**
    /// （先に作って確かめてあるので、動かしていない）。寸法を直すときは両方直すこと。
    /// </summary>
    internal sealed class BelleFigure
    {
        private const string FramePrefix = "UI_Anim";
        private const int FrameCount = 7;

        /// <summary>1コマの秒数。スキル一覧が開くとき（AbilityUI）と同じ。</summary>
        public const float FrameSeconds = 0.06f;

        private static readonly Vector2 FramePixels = new Vector2(1010f, 1570f);
        private static readonly Vector2 PoweredPixels = new Vector2(660f, 510f);
        private static readonly Vector2 FrameFigureCenter = new Vector2(540f, 275f);
        private static readonly Vector2 PoweredFigureCenter = new Vector2(340f, 285f);
        /// <summary>カットインの絵の中の、板の空いている所（頭の下、両手のあいだ）の真ん中。</summary>
        private static readonly Vector2 PoweredSignCenter = new Vector2(340f, 340f);

        /// <summary>ベルルの目と輪の色に合わせた黄色。</summary>
        public static readonly Color Yellow = new Color(1f, 0.92f, 0.2f, 1f);

        private readonly Sprite[] _frames;
        private readonly Image _frame;
        private readonly Image _powered;
        private bool _poweredOn;

        /// <summary>ベルルの真ん中（舞台の座標。画面の中心が原点）。</summary>
        public readonly Vector2 Center;

        /// <summary>板の空いている所の真ん中。ここに文字や印を置く。</summary>
        public readonly Vector2 SignPosition;

        /// <summary>出てくるコマを送り終えたか。</summary>
        public bool Arrived { get; private set; }

        /// <param name="scale">絵の1ドットを画面の何単位で出すか。0.55 でベルルの横幅が約340</param>
        /// <param name="poweredSprite">目が光っている絵。無ければ目は光らない</param>
        /// <returns>コマが読めなければ null（呼ぶ側はベルル無しで進める）</returns>
        public static BelleFigure Create(SkillEffectStage stage, Vector2 center, float scale, Sprite poweredSprite)
        {
            var frames = new Sprite[FrameCount];
            for (int i = 0; i < FrameCount; i++)
            {
                frames[i] = Resources.Load<Sprite>(FramePrefix + (i + 1));
                if (frames[i] == null)
                {
                    Debug.LogWarning("[BelleFigure] " + FramePrefix + (i + 1) + " が読めませんでした。ベルル無しで進めます");
                    return null;
                }
            }
            return new BelleFigure(stage, center, scale, frames, poweredSprite);
        }

        private BelleFigure(SkillEffectStage stage, Vector2 center, float scale, Sprite[] frames, Sprite poweredSprite)
        {
            _frames = frames;
            Center = center;

            // コマは縦に長い1枚（下は開いた板のための空き）。ベルルの真ん中が center に来るように置く
            Vector2 frameCenter = center - Offset(FrameFigureCenter, FramePixels, scale);
            _frame = stage.AddSprite("Belle", frames[0], frameCenter, FramePixels * scale);
            _frame.enabled = false;

            Vector2 poweredCenter = center - Offset(PoweredFigureCenter, PoweredPixels, scale);
            if (poweredSprite != null)
            {
                _powered = stage.AddSprite("BellePowered", poweredSprite, poweredCenter, PoweredPixels * scale);
                _powered.enabled = false;
            }
            SignPosition = poweredCenter + Offset(PoweredSignCenter, PoweredPixels, scale);
        }

        /// <summary>黄色い輪が描かれて、ベルルが出てくる。</summary>
        public IEnumerator Arrive()
        {
            _frame.enabled = true;
            for (int i = 0; i < _frames.Length; i++)
            {
                if (_frame == null) yield break;
                _frame.sprite = _frames[i];
                yield return new WaitForSeconds(FrameSeconds);
            }
            Arrived = true;
        }

        /// <summary>目が光る（絵を差し替える）。光る絵が無ければ何もしない。</summary>
        public void PowerOn()
        {
            if (_powered == null || _frame == null) return;
            _frame.enabled = false;
            _powered.enabled = true;
            _poweredOn = true;
        }

        /// <summary>いま見えている絵の大きさ（1 が等倍）。目が光った瞬間に一度ふくらませるのに使う。</summary>
        public void SetScale(float scale)
        {
            Image shown = _poweredOn ? _powered : _frame;
            if (shown != null) shown.rectTransform.localScale = Vector3.one * scale;
        }

        /// <summary>輪へ帰る。出てきたときのコマを逆に送る。</summary>
        public IEnumerator Leave(float seconds)
        {
            if (_frame == null) yield break;

            if (_powered != null) _powered.enabled = false;
            _poweredOn = false;
            _frame.rectTransform.localScale = Vector3.one;
            _frame.enabled = true;
            float step = seconds / _frames.Length;
            for (int i = _frames.Length - 1; i >= 0; i--)
            {
                if (_frame == null) yield break;
                _frame.sprite = _frames[i];
                yield return new WaitForSeconds(step);
            }
            if (_frame != null) _frame.enabled = false;
        }

        /// <summary>すぐ消す。</summary>
        public void Hide()
        {
            if (_frame != null) _frame.enabled = false;
            if (_powered != null) _powered.enabled = false;
        }

        /// <summary>絵の中の1点（左上が原点のドット）を、絵の真ん中からのずれ（画面の単位。上が正）に直す。</summary>
        private static Vector2 Offset(Vector2 pixel, Vector2 size, float scale)
        {
            return new Vector2(pixel.x - size.x * 0.5f, size.y * 0.5f - pixel.y) * scale;
        }
    }
}
