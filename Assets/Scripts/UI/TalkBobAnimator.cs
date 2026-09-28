using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 喋っているあいだ、キャラクターを小刻みに上下させる（ぴょこぴょこ）。
    ///
    /// **参考にしたのはユーザーの録画『画面録画 2026-09-29 005417.mp4』。**
    /// 24.0〜25.0 秒を1コマずつ並べて見ると、
    ///
    ///   ・**体ごと縦に平行移動している。** 伸び縮み（スケール）ではない
    ///   ・上下の幅はキャラの背丈に対してごく小さい（数パーセント）
    ///   ・1秒に何度も跳ねる、速い動き
    ///
    /// **数値そのものは、この録画からは取れなかった。**
    /// カメラが寄ったり流れたりする編集済みの動画で、相関で追うと
    /// カメラの動きに振り切れて、キャラ自身の跳ねと分離できなかった
    /// （±60px まで広げても飽和し、傾きを引いた残りは追従外れの棘だらけ）。
    /// なので下の既定値は**録画から測った値ではなく、見た目を写した手当て**。
    /// 直すときは Inspector で振り幅と速さをいじれば足りる。
    ///
    /// **跳ね方は `|sin|` にしてある。** ただの sin だと下にも同じだけ沈むので、
    /// 浮いている（<see cref="FloatingAnimator"/>）のと区別がつかない。
    /// 絶対値にすると必ず「地から上へ跳ねて戻る」形になり、跳ねて見える。
    /// </summary>
    public class TalkBobAnimator : MonoBehaviour
    {
        [Header("跳ね方")]
        [Tooltip("1秒あたり何回跳ねるか")]
        [SerializeField] private float hopsPerSecond = 2.6f;

        [Tooltip("跳ぶ高さ。キャラの見た目の背丈に対する割合（0.02 で2%）")]
        [SerializeField] private float heightRatio = 0.025f;

        [Tooltip("背丈を測れなかったときに使う、跳ぶ高さ（ローカル単位）")]
        [SerializeField] private float fallbackHeight = 0.12f;

        [Tooltip("喋り終わってから地に着くまでの時間（秒）。ぴたっと止めると固く見える")]
        [SerializeField] private float settleSeconds = 0.12f;

        private Vector3 _initialPosition;
        private float _phase;
        private float _weight;      // 0=止まっている 1=跳ねている
        private float _hopHeight;
        private bool _measured;

        private void Start()
        {
            _initialPosition = transform.localPosition;
        }

        private void OnEnable()
        {
            // 伏せられているあいだに動かされていることがあるので、起きた時に測り直す。
            // **`Start` だけだと、フェードインで後から出てくる女の子に間に合わない。**
            _initialPosition = transform.localPosition;
            _measured = false;
            _phase = 0f;
            _weight = 0f;
        }

        private void Update()
        {
            if (!_measured) MeasureHopHeight();

            bool talking = DialogueUI.IsTalking;

            // 立ち上がりは即、止まるときだけ余韻を付ける
            if (talking) _weight = 1f;
            else if (_weight > 0f)
            {
                _weight -= Time.deltaTime / Mathf.Max(0.01f, settleSeconds);
                if (_weight < 0f) _weight = 0f;
            }

            if (_weight <= 0f)
            {
                // **位相も戻す。** 残したままだと、次に喋り出した瞬間に
                // 跳ねの途中から始まって、いきなり浮いた所から出る
                _phase = 0f;
                transform.localPosition = _initialPosition;
                return;
            }

            _phase += Time.deltaTime * hopsPerSecond * Mathf.PI;
            float hop = Mathf.Abs(Mathf.Sin(_phase)) * _hopHeight * _weight;
            transform.localPosition = _initialPosition + new Vector3(0f, hop, 0f);
        }

        /// <summary>
        /// 跳ぶ高さを、キャラの見た目の背丈から決める。
        ///
        /// **決め打ちの数値にしないのは、ここの単位が場面によって違うから。**
        /// スプライトならワールド単位、UI の下に置かれていれば画素になる。
        /// 背丈に対する割合で持てば、どちらでも同じ見た目になる。
        /// </summary>
        private void MeasureHopHeight()
        {
            float height = 0f;

            var sprites = GetComponentsInChildren<SpriteRenderer>(true);
            if (sprites.Length > 0)
            {
                var bounds = sprites[0].bounds;
                for (int i = 1; i < sprites.Length; i++) bounds.Encapsulate(sprites[i].bounds);
                // ワールドの高さを、この物の親の物差しに直す
                float scaleY = transform.lossyScale.y;
                if (scaleY > 0.0001f) height = bounds.size.y / scaleY * transform.localScale.y;
            }

            if (height <= 0.0001f)
            {
                var rt = transform as RectTransform;
                if (rt != null) height = rt.rect.height;
            }

            _hopHeight = height > 0.0001f ? height * heightRatio : fallbackHeight;
            // 背丈が取れるのは、スプライトが読み込まれて大きさを持ってから。
            // 0 のうちは測れていないので、次のフレームでもう一度測る
            _measured = height > 0.0001f;
        }
    }
}
