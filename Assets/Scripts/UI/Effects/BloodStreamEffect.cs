using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 血が一本の筋になって流れる演出（2026-10-18）。卓の血だまりは <see cref="BloodPoolEffect"/>。
    ///
    /// ユーザーの言葉:「血を掛け合っているという部分を演出でみられるようにしたい」。
    /// 今までは数字が飛ぶだけで、血袋の量が変わっても血が動いて見えなかった。
    ///
    /// 絵は使わない（AIで描かない）。PixelBloodEffect と同じく 1x1 の点を四角いドットとして並べる。
    ///
    /// **滴をばらばらに飛ばすと、点が散って筋に見えなかった**（同日の試写で確認）。
    /// そこで「つながった一本の柱」として描く:
    ///   ・先頭が始点から終点へ伸びていき、流している間は始点と終点が筋でつながったまま
    ///   ・流し終えたら尻尾が始点を離れて、終点へ吸い込まれる
    ///   ・筋の中を明るい帯が終点へ向かって走るので、どちら向きに流れているか分かる
    ///   ・終点では跳ねが出続ける
    /// 座標はグリッドに量子化し、色は数色だけにする（なめらかにしない）。
    ///
    /// **血袋に出入りするときはホースを通す**（同日のユーザー指示「ホースのところから流れ込んでる感じに」）。
    /// 血袋の真ん中へ直接刺さっていたのをやめ、宙を飛ぶ部分のあとに、ホースの絵に沿ったまっすぐな区間をつなぐ。
    /// ホースの中は細く（絵のホースの太さ）、うねらせない。ホースが赤く染まっていくように見える。
    ///
    /// 位置はすべて**ワールド座標（Overlay ならスクリーン座標）**で受け取る。
    /// 入れ物は呼び出し側の全画面の RectTransform を使う。
    /// </summary>
    public sealed class BloodStreamEffect : MonoBehaviour
    {
        private static Sprite _dot;
        private static Sprite Dot
        {
            get
            {
                if (_dot == null)
                {
                    var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    tex.SetPixel(0, 0, Color.white);
                    tex.filterMode = FilterMode.Point;
                    tex.Apply();
                    _dot = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                    _dot.name = "BloodStreamDot";
                }
                return _dot;
            }
        }

        // 明るい順
        private static readonly Color Bright = new Color32(232, 44, 40, 255);
        private static readonly Color Body = new Color32(170, 14, 34, 255);
        private static readonly Color Dark = new Color32(104, 6, 20, 255);

        /// <summary>ドットの1辺（800x600 基準の px）。</summary>
        private const float Grid = 4f;
        /// <summary>先頭が始点から終点まで伸びる時間[秒]。尻尾が吸い込まれる時間も同じ。</summary>
        private const float TravelSeconds = 0.32f;
        /// <summary>明るい帯が流れる速さ（筋の長さに対する割合／秒）。</summary>
        private const float BandSpeed = 2.2f;

        private readonly List<Image> dots = new List<Image>();
        private readonly List<Splash> splashes = new List<Splash>();
        private RectTransform stage;
        private Vector2 from, to;
        // 経路を細かい点に分けたもの。累積の長さで 0〜1 の位置から引く
        private readonly List<Vector2> pathPts = new List<Vector2>();
        private readonly List<float> pathLen = new List<float>();
        private readonly List<bool> pathHose = new List<bool>();
        private float totalLength;
        private bool endsInHose;
        private float pourSeconds, elapsed, scale, width, splashCarry;
        private bool running;

        private struct Splash
        {
            public Image Img;
            public Vector2 Pos, Vel;
            public float Age;
        }

        /// <summary>流している途中か（尻尾が終点に届くまで true）。</summary>
        public bool IsRunning => running;

        /// <summary>
        /// <paramref name="fromWorld"/> から <paramref name="toWorld"/> へ、<paramref name="seconds"/> 秒のあいだ血を流す。
        /// <paramref name="arc"/> は弧の高さ（800x600 基準の px、正で上へふくらむ）。
        /// <paramref name="thickness"/> は筋の太さの倍率（額が大きいほど太くする）。0.6〜2.2。
        ///
        /// <paramref name="leaveHoseAt"/> を渡すと、始点（血袋の口）からそこまではホースの中を流れ、そこから宙へ出る。
        /// <paramref name="enterHoseAt"/> を渡すと、宙からそこでホースに入り、終点（血袋の口）までホースの中を流れる。
        /// </summary>
        public static BloodStreamEffect Pour(RectTransform stage, Vector3 fromWorld, Vector3 toWorld,
                                             float seconds, float arc = 60f, float thickness = 1f,
                                             Vector3? leaveHoseAt = null, Vector3? enterHoseAt = null)
        {
            if (stage == null) return null;
            var go = new GameObject("BloodStream", typeof(RectTransform));
            go.transform.SetParent(stage, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var fx = go.AddComponent<BloodStreamEffect>();
            fx.stage = rt;
            // ワールド（画面）の 1 グリッドの大きさ。入れ物のキャンバスの縮尺から取る
            var canvas = stage.GetComponentInParent<Canvas>();
            fx.scale = canvas != null ? Mathf.Max(0.25f, canvas.rootCanvas.transform.lossyScale.x) : 1f;
            fx.from = fromWorld;
            fx.to = toWorld;
            fx.BuildPath(arc, leaveHoseAt, enterHoseAt);
            fx.pourSeconds = Mathf.Max(0.05f, seconds);
            fx.width = Mathf.Clamp(thickness, 0.6f, 2.2f);
            fx.running = true;
            return fx;
        }

        /// <summary>尻尾が終点に届くまで待つ。</summary>
        public IEnumerator WaitDone()
        {
            while (this != null && running) yield return null;
        }

        /// <summary>ホースの区間（直線）＋ 宙の区間（弧）＋ ホースの区間（直線）を、細かい点の列にする。</summary>
        private void BuildPath(float arc, Vector3? leaveHoseAt, Vector3? enterHoseAt)
        {
            Vector2 airFrom = leaveHoseAt.HasValue ? (Vector2)leaveHoseAt.Value : from;
            Vector2 airTo = enterHoseAt.HasValue ? (Vector2)enterHoseAt.Value : to;
            endsInHose = enterHoseAt.HasValue;

            if (leaveHoseAt.HasValue) AddLine(from, airFrom, true);
            Vector2 control = (airFrom + airTo) * 0.5f + Vector2.up * arc * scale;
            const int airSteps = 32;
            for (int i = pathPts.Count == 0 ? 0 : 1; i <= airSteps; i++)
            {
                float t = i / (float)airSteps, u = 1f - t;
                AddPoint(u * u * airFrom + 2f * u * t * control + t * t * airTo, false);
            }
            if (enterHoseAt.HasValue) AddLine(airTo, to, true);
            totalLength = pathLen[pathLen.Count - 1];
        }

        private void AddLine(Vector2 a, Vector2 b, bool hose)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / (Grid * scale)));
            for (int i = pathPts.Count == 0 ? 0 : 1; i <= steps; i++) AddPoint(Vector2.Lerp(a, b, i / (float)steps), hose);
        }

        private void AddPoint(Vector2 p, bool hose)
        {
            pathLen.Add(pathPts.Count == 0 ? 0f : pathLen[pathLen.Count - 1] + Vector2.Distance(pathPts[pathPts.Count - 1], p));
            pathPts.Add(p);
            pathHose.Add(hose);
        }

        /// <summary>経路上の位置 0〜1（長さの割合）の点。<paramref name="inHose"/> はホースの中か。</summary>
        private Vector2 PathPoint(float t, out bool inHose)
        {
            float d = Mathf.Clamp01(t) * totalLength;
            int i = 1;
            while (i < pathLen.Count - 1 && pathLen[i] < d) i++;
            float seg = pathLen[i] - pathLen[i - 1];
            float k = seg > 1e-5f ? (d - pathLen[i - 1]) / seg : 1f;
            // 区間の境目では、先の区間（ホースに入ったら細く）を優先する
            inHose = pathHose[i];
            return Vector2.Lerp(pathPts[i - 1], pathPts[i], k);
        }

        private Vector2 PathPoint(float t) => PathPoint(t, out _);

        private Vector2 Snap(Vector2 p)
        {
            float g = Grid * scale;
            return new Vector2(Mathf.Round(p.x / g) * g, Mathf.Round(p.y / g) * g);
        }

        private Image NewDot()
        {
            var go = new GameObject("Drop", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(stage, false);
            var img = go.GetComponent<Image>();
            img.sprite = Dot;
            img.raycastTarget = false;
            ((RectTransform)go.transform).sizeDelta = new Vector2(Grid, Grid);
            return img;
        }

        private void Update()
        {
            if (!running) return;
            float dt = Time.deltaTime;
            elapsed += dt;

            // 筋の先頭と尻尾（経路上の位置 0〜1）
            float head = Mathf.Clamp01(elapsed / TravelSeconds);
            float tail = Mathf.Clamp01((elapsed - pourSeconds) / TravelSeconds);
            head = 1f - (1f - head) * (1f - head);   // 出だしは勢いよく
            tail = tail * tail;                        // 吸い込まれるときは最後に速く

            // 経路の長さから、1グリッドおきに点を置く数を決める
            float g = Grid * scale;
            float length = totalLength;
            int along = Mathf.Max(2, Mathf.CeilToInt(length * Mathf.Max(0f, head - tail) / g));
            // 太さは横に並べる点の数。真ん中ほど濃い
            int across = width < 0.9f ? 2 : width < 1.4f ? 3 : 4;
            // ホースの中は絵のホースの太さ（1〜2点）にする
            int hoseAcross = width < 1.4f ? 1 : 2;
            int need = head > tail ? along * across : 0;
            while (dots.Count < need) dots.Add(NewDot());

            int used = 0;
            for (int i = 0; i < along && need > 0; i++)
            {
                float u = Mathf.Lerp(tail, head, along == 1 ? 1f : i / (float)(along - 1));
                Vector2 p = PathPoint(u, out bool inHose);
                Vector2 tangent = PathPoint(Mathf.Min(1f, u + 0.01f)) - PathPoint(Mathf.Max(0f, u - 0.01f));
                Vector2 normal = tangent.sqrMagnitude > 1e-6f ? new Vector2(-tangent.y, tangent.x).normalized : Vector2.up;
                // 液体らしく少しうねる。うねりも終点へ流れていく。ホースの中はうねらない
                if (!inHose) p += normal * Mathf.Sin(u * 18f - elapsed * 14f) * g * 0.35f;
                int n = inHose ? hoseAcross : across;

                // 明るい帯が終点へ走る。先頭の数マスは明るく
                float band = Mathf.Repeat(u * 3f - elapsed * BandSpeed, 1f);
                bool tip = head - u < 0.04f && head < 1f;
                for (int k = 0; k < n; k++)
                {
                    var img = dots[used++];
                    img.gameObject.SetActive(true);
                    float off = (k - (n - 1) * 0.5f) * g;
                    img.rectTransform.position = Snap(p + normal * off);
                    bool edge = !inHose && n > 1 && (k == 0 || k == n - 1);
                    img.color = tip ? Bright : band < 0.18f ? Bright : edge ? Dark : Body;
                }
            }
            for (int i = used; i < dots.Count; i++) dots[i].gameObject.SetActive(false);

            // ホースから血袋へ入るときは跳ねない（中へ吸い込まれる）
            UpdateSplashes(dt, head >= 1f && tail < 1f && !endsInHose);

            if (tail >= 1f && splashes.Count == 0)
            {
                running = false;
                Destroy(gameObject);
            }
        }

        /// <summary>終点で跳ねる滴。筋が届いている間だけ出す。透明度は段で落とす。</summary>
        private void UpdateSplashes(float dt, bool landing)
        {
            if (landing)
            {
                splashCarry += dt * 26f * width;
                while (splashCarry >= 1f)
                {
                    splashCarry -= 1f;
                    var img = NewDot();
                    float s = Random.value < 0.3f ? 2f : 1f;
                    img.rectTransform.sizeDelta = new Vector2(Grid * s, Grid * s);
                    img.color = Random.value < 0.5f ? Bright : Body;
                    splashes.Add(new Splash
                    {
                        Img = img,
                        Pos = to,
                        Vel = new Vector2(Random.Range(-140f, 140f), Random.Range(110f, 240f)) * scale,
                    });
                }
            }

            for (int i = splashes.Count - 1; i >= 0; i--)
            {
                var sp = splashes[i];
                sp.Age += dt;
                sp.Vel += Vector2.down * 900f * scale * dt;
                sp.Pos += sp.Vel * dt;
                sp.Img.rectTransform.position = Snap(sp.Pos);
                if (sp.Age > 0.16f) sp.Img.color = new Color(sp.Img.color.r, sp.Img.color.g, sp.Img.color.b, 0.5f);
                if (sp.Age > 0.28f)
                {
                    Destroy(sp.Img.gameObject);
                    splashes.RemoveAt(i);
                    continue;
                }
                splashes[i] = sp;
            }
        }
    }
}
