using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    /// <summary>
    /// ボタンを紹介するとき、**絵だけの複製**を画面中央に大きく出し、
    /// 紹介が終わったら本来の位置まで飛ばして消す（2026-09-29 のユーザー指示）。
    ///
    /// **本物を出したまま4秒ハイライトするのをやめるために作った。**
    /// 『おまかせ』の紹介では、本物のボタンを出して4秒間ハイライトしていた。
    /// 押せる見た目のまま4秒も光っているので、ユーザーから
    /// 「押せそうな雰囲気を出しているし、その間押しそうになる」と言われた。
    /// 複製には <see cref="Button"/> も `GraphicRaycaster` も付けないので、
    /// **紹介のあいだは押しようがない。** 本物は飛び終わってから出す。
    ///
    /// **シーンには置かない。** 呼ばれるたびに自前の Canvas を作り、
    /// 終わったら自分を消す（<see cref="Effects.ScreenFlash"/> と同じ理由で、
    /// 対局シーンが2つあるため片方に入れ忘れる事故を避ける）。
    /// </summary>
    public class TutorialButtonIntroUI : MonoBehaviour
    {
        /// <summary>中央でどれだけ大きく見せるか。</summary>
        public const float DefaultCenterScale = 2.2f;

        /// <summary>
        /// 中央から縦にずらす量（画素・上が正）。
        ///
        /// **女の子の顔に被せないための値**（2026-09-29 のユーザー指示）。
        /// 800x600 の実機画面で測ると、立ち絵の顔は上から **y 105〜250**（横は 360〜460）。
        /// 2.2倍の複製は高さ 88px なので、画面中央（y 300）に置くと上端が y 256 に来て、
        /// 顎(250)とほぼ接する。**40px 下げると上端が y 296** になり、顎から 46px 空く。
        ///
        /// 下げすぎると卓の縁（y 390 から）に掛かるので、これ以上は下げられない。
        /// 以前は逆に 60px 上げていたが、それだと口元をちょうど隠していた。
        ///
        /// 吹き出しとも重ならない。吹き出しの文字は x 455〜650 / y 200〜260 に出るので、
        /// 複製（x 268〜532 / y 296〜384）とは縦で離れている。
        /// </summary>
        public const float DefaultCenterOffsetY = -40f;

        private const float PopInSeconds = 0.26f;
        private const float MoveSeconds = 0.45f;

        /// <summary>
        /// 1フレームで進めてよい最大の時間（秒）。
        ///
        /// **これが無いと、引っかかった直後に瞬間移動する。**
        /// 画面が一度止まると、再開した最初のフレームの `unscaledDeltaTime` に
        /// 止まっていた分がまとめて乗る。0.45秒の移動が2フレームで終わってしまい、
        /// 飛んでいる姿が1コマも出ない（2026-09-29 に録画で確認）。
        /// 30分の1秒で頭打ちにすれば、重い場面でも必ず十数コマは描かれる。
        /// そのぶん実時間は延びるが、短い演出なので気にならない。
        /// </summary>
        private const float MaxStepSeconds = 1f / 30f;

        private static float Step()
        {
            return Mathf.Min(Time.unscaledDeltaTime, MaxStepSeconds);
        }

        private RectTransform _copy;
        private CanvasGroup _group;
        private float _centerScale;

        /// <summary>
        /// <paramref name="source"/> の見た目をそのまま複製して中央に出す。
        /// 本物には触らないので、呼ぶ側が本物を伏せたままにしておくこと。
        /// </summary>
        public static TutorialButtonIntroUI Show(RectTransform source,
                                                 float centerScale = DefaultCenterScale,
                                                 float centerOffsetY = DefaultCenterOffsetY)
        {
            if (!Application.isPlaying || source == null) return null;

            var go = new GameObject("TutorialButtonIntro");
            var view = go.AddComponent<TutorialButtonIntroUI>();
            view._centerScale = centerScale;
            view.Build(source, centerOffsetY);
            return view;
        }

        private void Build(RectTransform source, float centerOffsetY)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UISortingOrders.TutorialButtonIntro;
            // **GraphicRaycaster は付けない。** 付けると絵が下のUIのクリックを吸う。
            // 付けないので、この複製自体も当たり判定を持たない（＝押せない）。

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            var copyGo = Instantiate(source.gameObject, transform);
            copyGo.name = "Preview";
            copyGo.SetActive(true);

            // **押せる部品を根こそぎ外す。** 見た目だけを残す。
            foreach (var selectable in copyGo.GetComponentsInChildren<Selectable>(true))
            {
                Destroy(selectable);
            }
            foreach (var graphic in copyGo.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            _copy = copyGo.GetComponent<RectTransform>();
            _copy.anchorMin = new Vector2(0.5f, 0.5f);
            _copy.anchorMax = new Vector2(0.5f, 0.5f);
            _copy.pivot = new Vector2(0.5f, 0.5f);
            _copy.anchoredPosition = new Vector2(0f, centerOffsetY);
            _copy.localRotation = Quaternion.identity;
            _copy.localScale = Vector3.one * _centerScale;
        }

        /// <summary>中央へ「バッ」と出す。行き過ぎてから戻る形にして、置きに行った感じを消す。</summary>
        public IEnumerator PopIn()
        {
            if (_copy == null) yield break;

            if (Managers.AudioManager.Instance != null)
            {
                Managers.AudioManager.Instance.PlayUIPopupSE();
            }

            float t = 0f;
            while (t < PopInSeconds)
            {
                t += Step();
                float u = Mathf.Clamp01(t / PopInSeconds);
                if (_copy == null) yield break;
                // 0.55 倍から 1.12 倍まで行き過ぎて、等倍へ戻る
                float overshoot = 0.55f + (1.12f - 0.55f) * Mathf.Sin(u * Mathf.PI * 0.5f);
                if (u > 0.55f) overshoot = Mathf.Lerp(1.12f, 1f, (u - 0.55f) / 0.45f);
                _copy.localScale = Vector3.one * (_centerScale * overshoot);
                _group.alpha = Mathf.Clamp01(u * 3f);
                yield return null;
            }
            _copy.localScale = Vector3.one * _centerScale;
            _group.alpha = 1f;
        }

        /// <summary>
        /// 本来の位置まで飛ばして消える。
        ///
        /// **行き先は伏せたままの本物から採る。** `RectTransform.position` は
        /// GameObject が伏せていても親から求まるので、本物を出してから測る必要はない。
        /// 出してから測ると、飛んでいる間ずっと本物が同じ場所に見えてしまう。
        /// </summary>
        public IEnumerator MoveToPlace(RectTransform target)
        {
            if (_copy == null)
            {
                Finish();
                yield break;
            }

            Vector3 from = _copy.position;
            Vector3 to = target != null ? target.position : from;
            float fromScale = _centerScale;

            float t = 0f;
            while (t < MoveSeconds)
            {
                t += Step();
                float u = Mathf.Clamp01(t / MoveSeconds);
                if (_copy == null) yield break;
                float e = u * u * (3f - 2f * u);   // 緩やかに出て緩やかに止まる
                _copy.position = Vector3.Lerp(from, to, e);
                _copy.localScale = Vector3.one * Mathf.Lerp(fromScale, 1f, e);
                yield return null;
            }

            Finish();
        }

        /// <summary>途中で畳むとき用。飛ばさずに消す。</summary>
        public void Finish()
        {
            if (this != null && gameObject != null) Destroy(gameObject);
        }
    }
}
