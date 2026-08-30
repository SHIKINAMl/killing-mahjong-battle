using UnityEngine;
using TMPro;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public partial class PlayerInfoUI : MonoBehaviour
    {
        // partial の担当:
        // - Lifecycle: 初期化、まばたき、ターンタイマー
        // - Hp: HPメーター、瀕死演出、HPポップアップ
        // - Character: 立ち絵、打牌姿、バウンド
        // - Zoom: 賭け金時のスマホ拡大と描画順
        // - Ready: 準備札、手番表示、体力表示の開閉

        [Header("HP Display")]
        [SerializeField] private TextMeshProUGUI hpText;

        /// <summary>
        /// 体力の数字に添える「自分／相手」の大きさ（数字に対する割合）。
        /// 実機で合わせた値。これ以上大きいと点滴の管に、小さいと実表示で潰れる。
        /// EnemyInfoUI 側と揃えること。
        /// </summary>
        internal const string HpOwnerLabelScale = "65%";
        [SerializeField] private UnityEngine.UI.Image hpFillImage; // 追加: 人型のHPメーター用画像
        private int maxHp = 20000; // 最大HP（割合計算用）

        [Header("Boost Bonus")]
        [SerializeField] private TextMeshProUGUI boostBonusText; // 動的生成も可

        [Header("Ready Mark")]
        [SerializeField] private GameObject readyBoxContainer;
        [SerializeField] private GameObject readyCheckImage;

        [Header("Zoom Target")]
        [SerializeField] private Transform zoomTarget; // 追加：拡大させたい子オブジェクトを指定
        [SerializeField] private Vector3 zoomedLocalPos = new Vector3(0, 50f, 0f);
        [SerializeField] private Vector3 zoomedScale = new Vector3(1.2f, 1.2f, 1.0f);
        [SerializeField] private Vector3 zoomOffsetUI = new Vector3(-1200f, 100f, -500f); // ズーム時に手前に出すためVector3に変更
        [SerializeField] private Vector3 zoomOffsetWorld = new Vector3(-4.0f, 1.0f, -2.0f); // 3D時の移動量

        [Header("Prefabs")]
        [SerializeField] private GameObject damagePopupPrefab;

        [Tooltip("HP増減ポップアップの出現基準。未設定なら zoomTarget（スマホ）を使う。")]
        [SerializeField] private RectTransform damagePopupAnchor;

        [Header("Character Portrait")]
        [SerializeField] private SpriteRenderer characterRenderer;
        [SerializeField] private SpriteRenderer faceRenderer; // 追加：表情レイヤー用
        [SerializeField] private CharacterData characterData; // キャラクター管理データ

        [Header("Timer UI")]
        [SerializeField] private TimerUI timerUI; // インスペクターからセットする

        public CharacterData CurrentCharacterData => characterData;

        private int currentHp = 20000; // 暫定の初期HP
        private Sprite normalSprite;
        private Sprite discardSprite;
        private Sprite normalFaceSprite; // 通常時の顔画像
        
        private Coroutine bounceCoroutine;
        private Coroutine zoomCoroutine;
        private Coroutine blinkCoroutine;
        private Vector3 originalPosition;
        
        // ズーム用
        private Vector3 originalLocalPos;
        private Vector3 originalScale;
        private bool isZoomedIn = false; // 追加：ズーム状態を管理

        private bool isInitialized = false;

        // ロンで血を奪うと開始HPを超えるため、到達した最高HPまでメーターの分母を広げる。
        // 分母を開始HP固定にすると fillAmount が1で頭打ちになり、
        // 33000 → 14000 のような大きな減少がメーター上で見えなくなる。
        // 瀕死ビネットとダメージSEの判定は「絶対量としてどれだけ残っているか」なので maxHp のまま。
        private int hpPeak;
        private int MeterMax => HpMeterMath.MeterMax(maxHp, hpPeak);

        [Header("Effects")]
        [SerializeField] private KillingMahjong.UI.Effects.HeartbeatEffect heartbeatEffect;

        // 決着後に SetHP が呼ばれてビネットが復活しないようにするフラグ
        private bool heartbeatSuppressed = false;

        /// <summary>
        /// HP増減のポップアップとSEをまとめる。ロン演出中は毎フレーム SetHP が呼ばれるため、
        /// 変化が落ち着くまで溜めてから1回だけ表示する（HpPopupPresenter 側で処理）。
        /// </summary>
        private HpPopupPresenter hpPopup;

        /// <summary>
        /// ロンの血の移動中だけ true にして、浮き数字とSEを止める。
        ///
        /// **止めないと同じ数字が同じ場所に二重に出る。** 血の移動は着弾点に増減ラベルを自分で出すので、
        /// そこへ HpPopupPresenter の浮き数字が重なると、額が同じぶんかえって読めなくなる。
        /// **対局中の他の場面（打牌でHPが動くなど）では止めない。**
        /// </summary>
        public bool SuppressHpPopup { get; set; }

        /// <summary>
        /// ダメージSEのピッチ判定に使う分母。**メーターの分母（<c>MeterMax</c>）ではない。**
        /// 「絶対量としてどれだけ残っているか」で音が変わる作りなので、最高HPまで広げてはいけない。
        /// </summary>
        public int MaxHp => maxHp;

        /// <summary>
        /// HPが見えている場所（スマホ）。**血の移動の着弾点と、増減ラベルの置き場所に使う。**
        /// 取り方は <see cref="HpPopup"/> と同じ。null は返さない（最後は自分の RectTransform）。
        /// </summary>
        public RectTransform HpAnchor
        {
            get
            {
                if (damagePopupAnchor != null) return damagePopupAnchor;
                var zoom = zoomTarget as RectTransform;
                if (zoom != null) return zoom;
                return transform as RectTransform;
            }
        }

        /// <summary>強調表示時の前面化と復元。プロジェクトルールに従いルートCanvasの overrideSorting のみを操作する。</summary>
        private readonly CanvasSortingScope _sortingScope = new CanvasSortingScope();

        // ---- 賭け金フェイズのスマホの寄せ方（調整値。シーンではなくここを触る）----
        //
        // 狙いは「スマホの画面だけが見えていて、下がっている懐中時計は画面の外」。
        // **懐中時計は独立した GameObject ではなく `HPUI0heart` に描き込まれている**ので、
        // 何かを SetActive(false) しても消せない。倍率と位置で画面の外へ送るしかない。
        // （`時計` の GameObject は数字を文字盤に載せるための入れ物で、絵は持っていない）

        /// <summary>`メーター` の矩形の高さ。絵はこの矩形いっぱいに引き伸ばされる</summary>
        private const float MeterRectHeight = 280f;

        /// <summary>キャンバス（800x600）の高さの半分</summary>
        private const float CanvasHalfHeight = 300f;

        /// <summary>
        /// 絵のどこを画面の下端に合わせるか（スプライト上端からの割合）。
        ///
        /// `HPUI0heart`（880x1600）は **y=1139 でスマホ本体が終わり、y=1170 から懐中時計の輪が始まる**。
        /// その隙間の 1160 を画面の下端に置くと、本体は下まで見えたまま時計だけが画面の外へ落ちる。
        /// 絵を差し替えたら、この2つの境界を測り直すこと。
        /// </summary>
        private const float BettingZoomBottomCut = 1160f / 1600f;

        /// <summary>
        /// 賭け金フェイズでスマホ（zoomTarget = HPPanel）を拡大する倍率。
        ///
        /// **スマホの画像を差し替えたら必ずここを見直すこと。** 倍率は矩形ではなく
        /// 「絵が画面上で何pxになるか」に効くので、絵の縦横比が変わると勝手にずれる。
        /// 元の 4.5 は 1600x1600 の旧画像（`HPUI0haert` / メーターの矩形 170x186）に
        /// 合わせた値で、2026-08-14 の差し替え（`HPUI0heart` 880x1600・矩形 170x280）により
        /// 画面上の実寸が 631x779 → 747x1040 へ育ち、800x600 の画面から縦が大きくはみ出していた。
        /// 3.8 はスマホ本体が画面の縦にちょうど収まり（上端 y=21・下端 y=586）、
        /// 続く懐中時計が下端の外（y=607〜）へ出る値。
        ///
        /// 呼び出し側（GameUIPhaseController / TutorialManager）で同じ数値を書かないこと。
        /// 2か所に散っていたのが、差し替え時に見落とされた原因そのものだった。
        /// </summary>
        public const float BettingZoomScale = 3.8f;

        /// <summary>
        /// 拡大時にスマホを持ち上げる量(px)。負なら下げる。
        ///
        /// `BettingZoomBottomCut` の位置が画面の下端に来るよう、矩形の中心からのずれを打ち消す。
        /// **矩形の中心で止めてはいけない。** 絵は矩形の中で下に寄っている（上に 260/1600 の
        /// 透明余白があるのに下は 20/1600 しかない）ため、矩形が画面内でも絵の下だけがはみ出す。
        /// 矩形を見ていても気付けない類のずれなので、必ず絵の側の比率から出すこと。
        /// </summary>
        public static float BettingZoomLift =>
            MeterRectHeight * BettingZoomScale * (BettingZoomBottomCut - 0.5f) - CanvasHalfHeight;

        private ReadyBadge readyBadge;
        private TurnGlow turnGlow;
    }
}
