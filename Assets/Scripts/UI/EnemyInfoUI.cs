using UnityEngine;
using TMPro;

namespace KillingMahjong.UI
{
    public partial class EnemyInfoUI : MonoBehaviour
    {
        // partial の担当:
        // - Lifecycle: 初期化、まばたき、敵切替、死亡演出
        // - Reactions: セリフと一時的な立ち絵差し替え
        // - Hp: HPメーターとHPポップアップ
        // - Ready: 表示、準備札、立ち絵、バウンド
        // - Zoom: 点滴の拡大と復元

        [Header("Enemy HP Display")]
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private UnityEngine.UI.Image hpFillImage; 
        private int maxHp = 20000; 
        private int currentHp = 20000;
        
        [Header("Boost Bonus")]
        [SerializeField] private TextMeshProUGUI boostBonusText; // 動的生成も可
        
        [Header("Zoom Target")]
        [SerializeField] private Transform zoomTarget; // 追加：拡大させたい子オブジェクトを指定
        [SerializeField] private Vector2 zoomOffsetUI = new Vector2(-1200f, 100f); // UI時の移動量
        [SerializeField] private Vector3 zoomOffsetWorld = new Vector3(-4.0f, 1.0f, -2.0f); // 3D時の移動量

        [Header("Character Portrait")]
        [SerializeField] private SpriteRenderer characterRenderer;
        [SerializeField] private SpriteRenderer faceRenderer; // 追加：表情レイヤー用

        [Header("Death Animation")]
        [Tooltip("死亡演出で体が落ちる距離（ワールド単位）。立ち絵の高さは約17.65。" +
                 "20だと上端が画面下ぎりぎり（viewport -0.04）なので、余裕を見て24にしている")]
        [SerializeField] private float deathFallDistance = 24f;
        [Tooltip("死亡演出で落ち切るまでの時間（秒）")]
        [SerializeField] private float deathFallDuration = 1.2f;
        [Header("Ready Mark")]
        [SerializeField] private GameObject readyBoxContainer;
        [SerializeField] private GameObject readyCheckImage;
        [SerializeField] private CharacterData characterData; // キャラクター管理データ
        [SerializeField] private float bounceDuration = 0.5f; // 上下する時間（インスペクターで設定可能）
        [SerializeField] private float bounceHeight = 0.5f;   // 上下する高さ（インスペクターで設定可能）

        [Header("Available Enemies")]
        [SerializeField] private CharacterData[] availableEnemies; // インスペクターで登録する敵キャラクターリスト
        private int currentEnemyIndex = -1; // -1 = デフォルトの characterData を使用中

        [Header("Enemy Panel Settings")]
        [SerializeField] private GameObject enemyPanel; // 敵パネルの参照

        [Header("Prefabs")]
        [Tooltip("HP増減のポップアップ。未設定でも実行時に簡易版が生成される。")]
        [SerializeField] private GameObject damagePopupPrefab;

        [Tooltip("HP増減ポップアップの出現基準。未設定なら zoomTarget → enemyPanel の順で使う。")]
        [SerializeField] private RectTransform damagePopupAnchor;

        private Sprite normalSprite;
        private Sprite discardSprite;
        private Sprite normalFaceSprite; // 通常時の顔画像
        
        private Coroutine bounceCoroutine;
        private Coroutine reactionCoroutine;
        private Coroutine zoomCoroutine;
        private Coroutine blinkCoroutine;
        private Vector3 originalPosition;

        // ズーム用
        private Vector3 originalLocalPos;
        private Vector3 originalScale;

        /// <summary>
        /// 現在選択されている CharacterData を取得する
        /// </summary>
        public CharacterData CurrentCharacterData => characterData;

        // PlayerInfoUI と同じ理由でメーターの分母だけ最高HPまで広げる（ダメージSEの判定は maxHp のまま）。
        private int hpPeak;
        private int MeterMax => HpMeterMath.MeterMax(maxHp, hpPeak);

        /// <summary>ロン演出中の毎フレーム更新をまとめて1回の表示にする（HpPopupPresenter 側で処理）。</summary>
        private HpPopupPresenter hpPopup;

        private ReadyBadge readyBadge;
        private TurnGlow turnGlow;
        private TurnCharacterGlow characterGlow;
    }
}
