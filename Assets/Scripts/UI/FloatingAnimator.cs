using KillingMahjong.Managers;
using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// UI要素や2Dスプライトなどを上下左右にふらふらと浮遊させる汎用アニメーションスクリプト
    ///
    /// **曲が鳴っているあいだは、揺れを曲の拍に合わせる**（2026-09-13 のユーザー指示）。
    /// 自分のHP（スマホ）・相手のHP（点滴）・相手の吹き出しが、ばらばらの周期で
    /// 揺れていたのを、音楽と同じテンポに揃えるため。
    ///
    /// **テンポが分かる曲のときだけ合わせる。** `AudioManager` の
    /// テンポ表に載っていない曲、あるいは音を止めているあいだは、
    /// 今までどおり時間で揺れる（見た目は変わらない）。
    /// </summary>
    public class FloatingAnimator : MonoBehaviour
    {
        [Header("Floating Settings")]
        [Tooltip("ふらふら動くスピード")]
        [SerializeField] private float floatSpeed = 2f;
        
        [Tooltip("縦に動く幅（ピクセル/ユニット）")]
        [SerializeField] private float floatAmplitudeY = 10f;
        
        [Tooltip("横に動く幅（ピクセル/ユニット）")]
        [SerializeField] private float floatAmplitudeX = 5f;
        
        [Header("Rotation Settings (Optional)")]
        [Tooltip("ゆらゆら回転するスピード")]
        [SerializeField] private float rotationSpeed = 1f;
        
        [Tooltip("ゆらゆら回転する角度の幅")]
        [SerializeField] private float rotationAmplitude = 2f;

        [Header("音に合わせる")]
        [Tooltip("曲の拍に合わせて揺らす。曲が鳴っていないときは今までどおり時間で揺れる")]
        [SerializeField] private bool followMusic = true;

        [Tooltip("縦に1往復する拍数。4で1小節ぶん")]
        [SerializeField] private float beatsPerCycleY = 4f;

        // **縦の整数倍にすること。** 半端にすると、曲がループした瞬間に揺れが飛ぶ
        [Tooltip("横に1往復する拍数。縦の整数倍にすること")]
        [SerializeField] private float beatsPerCycleX = 8f;

        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Vector3 initialScale; // 追加：初期スケール
        private float randomOffset;

        private void Start()
        {
            // スクリプトがアタッチされたオブジェクトの初期座標と回転を記憶
            initialPosition = transform.localPosition;
            initialRotation = transform.localRotation;
            initialScale = transform.localScale; // スケールも記憶
            
            // 複数のオブジェクトが全く同じタイミングで動かないように、開始時間をランダムにずらす
            randomOffset = Random.Range(0f, 100f);
        }

        private void Update()
        {
            // ズーム中（スケールが変わっている時）は揺れアニメーションを停止する
            if (transform.localScale != initialScale)
            {
                return;
            }

            float phaseY, phaseX;
            if (!TryGetMusicPhase(out phaseY, out phaseX))
            {
                // 曲が無いときは今までどおり。**開始位置をずらしてばらけさせる**
                float time = Time.time + randomOffset;
                phaseY = time * floatSpeed;
                phaseX = time * floatSpeed * 0.8f;
            }

            // Sin波とCos波を使って滑らかな浮遊オフセットを計算
            float offsetY = Mathf.Sin(phaseY) * floatAmplitudeY;
            float offsetX = Mathf.Cos(phaseX) * floatAmplitudeX;
            
            // 初期座標に対してオフセットを足す（回転処理のみジャギー防止のため無効化）
            transform.localPosition = initialPosition + new Vector3(offsetX, offsetY, 0);
        }
        
        /// <summary>
        /// いま鳴っている曲の拍から、揺れの位相を求める。
        ///
        /// **曲の先頭から数えた拍をそのまま使う。** 自分で位相を積み上げると、
        /// 曲が変わるたびに揃え直しが要るうえ、3つの揺れが少しずつずれていく。
        /// 拍から直に出せば、どれも勝手に揃う。
        ///
        /// **ここでランダムなずれを足さない。** 足すと拍から外れて、
        /// 合わせた意味が無くなる。
        /// </summary>
        private bool TryGetMusicPhase(out float phaseY, out float phaseX)
        {
            phaseY = 0f;
            phaseX = 0f;
            if (!followMusic) return false;

            var audio = AudioManager.Instance;
            if (audio == null) return false;

            double beats;
            if (!audio.TryGetBeatPosition(out beats)) return false;

            float cycleY = Mathf.Max(0.25f, beatsPerCycleY);
            float cycleX = Mathf.Max(0.25f, beatsPerCycleX);
            phaseY = (float)(beats / cycleY) * Mathf.PI * 2f;
            phaseX = (float)(beats / cycleX) * Mathf.PI * 2f;
            return true;
        }

        /// <summary>
        /// もしプログラムから外部的に位置を移動させた場合、
        /// このメソッドを呼ぶことで「ふらふらの中心座標」を再設定できる。
        /// </summary>
        public void UpdateInitialPosition()
        {
            initialPosition = transform.localPosition;
        }
    }
}
