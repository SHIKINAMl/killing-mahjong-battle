using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    /// <summary>
    /// クリックした場所に波紋を出す。
    ///
    /// **以前は自前で描いた黄色い丸だった（2026-09-26 に差し替え）。**
    /// プランナーから支給されたドット絵のスプライトシートに置き換えた。
    /// `Resources/Effects/attack_ripple_sheet_8x64.png`、64×64 が横に8コマ。
    /// 1コマ 70ms、合計 0.56 秒で1回だけ再生して消える。
    ///
    /// **ドットを崩さないために、この Canvas には CanvasScaler を付けない。**
    /// 以前は 1920×1080 基準の ScaleWithScreenSize が付いていた。それだと
    /// 800×600 の Game View で倍率が 0.4812 になり、64px の絵が 61.6px という
    /// 半端な大きさに描かれて、ドットの目が潰れる。倍率1に固定したうえで
    /// 表示サイズを 64 の整数倍にすれば、1ドットが必ず整数個の画素になる。
    /// </summary>
    public class ClickFeedbackManager : MonoBehaviour
    {
        private static ClickFeedbackManager instance;
        private Canvas targetCanvas;
        private Sprite[] rippleFrames;

        /// <summary>1コマの長さ。支給された spec.json の 70ms。</summary>
        private const float FrameSeconds = 0.070f;

        /// <summary>コマ数。シートは横に8コマ並んでいる。</summary>
        private const int FrameCount = 8;

        /// <summary>素材1コマの大きさ。</summary>
        private const int SourceSize = 64;

        /// <summary>
        /// 画面に出すときの拡大率。**整数でなければならない。**
        /// 半端な倍率にすると1ドットが画素に割り切れず、目が潰れて滲んで見える。
        /// </summary>
        private const int PixelScale = 2;

        private const string SheetPath = "Effects/attack_ripple_sheet_8x64";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (instance == null)
            {
                GameObject obj = new GameObject("ClickFeedbackManager");
                instance = obj.AddComponent<ClickFeedbackManager>();
                DontDestroyOnLoad(obj);
            }
        }

        private void Awake()
        {
            CreateCanvas();
            LoadFrames();
        }

        private void CreateCanvas()
        {
            GameObject canvasObj = new GameObject("ClickFeedbackCanvas");
            canvasObj.transform.SetParent(transform);
            targetCanvas = canvasObj.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.sortingOrder = UISortingOrders.ClickFeedback;
            // CanvasScaler は付けない（クラスのコメント参照）。既定の倍率1で、
            // 1 UI 単位がそのまま画面の1画素になる。
        }

        /// <summary>
        /// シートを8枚のスプライトに切る。
        ///
        /// **切り口は .meta ではなくコードで持つ。** .meta に焼くと、
        /// 再インポートや素材の差し替えで切り口が崩れたときに気づけない。
        /// ここなら1コマの大きさが定数として見えるし、大きさが違えば警告を出せる。
        /// </summary>
        private void LoadFrames()
        {
            var tex = Resources.Load<Texture2D>(SheetPath);
            if (tex == null)
            {
                Debug.LogWarning("[ClickFeedback] スプライトシートが見つかりません: Resources/" + SheetPath);
                return;
            }

            // にじみを止める。読み込み設定が変わっていても、ここで必ず点で拾う
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            if (tex.width != SourceSize * FrameCount || tex.height != SourceSize)
            {
                Debug.LogWarning("[ClickFeedback] シートの大きさが想定と違います: "
                                 + tex.width + "x" + tex.height
                                 + "（想定 " + (SourceSize * FrameCount) + "x" + SourceSize + "）");
                return;
            }

            rippleFrames = new Sprite[FrameCount];
            for (int i = 0; i < FrameCount; i++)
            {
                rippleFrames[i] = Sprite.Create(
                    tex,
                    new Rect(i * SourceSize, 0, SourceSize, SourceSize),
                    new Vector2(0.5f, 0.5f),
                    // 1ユニット＝1画素にしておく。UI では Image が大きさを持つので
                    // 見た目には効かないが、値をそろえておくと拡大率を読み違えない
                    SourceSize);
                rippleFrames[i].name = "attack_ripple_" + i.ToString("00");
            }
        }

        private void Update()
        {
            bool isClicked = false;
            Vector2 clickPos = Vector2.zero;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                isClicked = true;
                clickPos = Mouse.current.position.ReadValue();
            }
            else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                isClicked = true;
                clickPos = Touchscreen.current.primaryTouch.position.ReadValue();
            }

            if (isClicked) SpawnRipple(clickPos);
        }

        /// <summary>クリックした場所に波紋を1回だけ出す。</summary>
        public void SpawnRipple(Vector2 screenPos)
        {
            if (rippleFrames == null || targetCanvas == null) return;

            var go = new GameObject("ClickRipple", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(targetCanvas.transform, false);

            var img = go.GetComponent<Image>();
            img.sprite = rippleFrames[0];
            // **クリックを邪魔しない。** これを外すと、波紋が出ているあいだ
            // その下の牌やボタンが押せなくなる
            img.raycastTarget = false;
            img.preserveAspect = true;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(SourceSize * PixelScale, SourceSize * PixelScale);

            // Canvas の倍率が1なので、画面座標をそのまま使える。
            // **位置は整数に丸める。** 半画素ずれるとドットの境目が2画素に割れて滲む
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetCanvas.transform as RectTransform, screenPos, null, out local);
            rt.anchoredPosition = new Vector2(Mathf.Round(local.x), Mathf.Round(local.y));

            StartCoroutine(PlayOnce(go, img));
        }

        /// <summary>
        /// 8コマを順に出して消す。**繰り返さない。**
        ///
        /// 待ちは実時間で取る。`WaitForSeconds` は Time.timeScale の影響を受けるので、
        /// スロー演出の最中にクリックするとコマ送りが伸びる。
        /// </summary>
        private IEnumerator PlayOnce(GameObject go, Image img)
        {
            for (int i = 0; i < FrameCount; i++)
            {
                img.sprite = rippleFrames[i];
                yield return new WaitForSecondsRealtime(FrameSeconds);
            }
            Destroy(go);
        }
    }
}
