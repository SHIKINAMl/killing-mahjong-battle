using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    public class YakuListUI : MonoBehaviour
    {
        [Header("Yaku List Panel (役Canvas2)")]
        [SerializeField] private GameObject yakuListPanel;
        [SerializeField] private Button toggleButton; // 役Canvas側の開くボタン
        [SerializeField] private Button closeButton;  // 役Canvas2側の閉じるボタン(xボタン)

        [Header("Yaku Items")]
        [SerializeField] private YakuItemUI itemPrefab;
        [SerializeField] private Transform contentContainer;
        private List<YakuItemUI> spawnedItems = new List<YakuItemUI>();

        [Header("Active Boosts (Always Visible)")]
        [SerializeField] private TextMeshProUGUI[] localActiveBoostTexts = new TextMeshProUGUI[3];
        [SerializeField] private TextMeshProUGUI[] enemyActiveBoostTexts = new TextMeshProUGUI[3];

        private void Awake()
        {
            if (yakuListPanel == null)
            {
                // Fallback to find the panel if unassigned
                Transform t = transform.Find("役Canvas2");
                if (t != null) yakuListPanel = t.gameObject;
                else if (gameObject.name == "役Canvas2") yakuListPanel = gameObject;
            }
        }

        private void Start()
        {
            // 文字を読めるようにするのは、データを入れるより先。
            ApplyActiveBoostReadability();

            if (yakuListPanel != null)
                yakuListPanel.SetActive(false); // 最初は非表示

            if (toggleButton != null)
                toggleButton.onClick.AddListener(OpenYakuList);
                
            if (closeButton != null)
                closeButton.onClick.AddListener(CloseYakuList);
            
            // 初期化時に New Text のままにならないようにデータを更新
            if (Managers.BoardStateManager.Instance != null)
            {
                UpdateBoostData(Managers.BoardStateManager.Instance.LocalBoostHandBonus, Managers.BoardStateManager.Instance.EnemyBoostHandBonus);
            }
            else
            {
                UpdateBoostData(null, null);
            }
        }

        /// <summary>役一覧が開いているか。チュートリアルの誘導待ちに使う。</summary>
        public bool IsOpen => yakuListPanel != null && yakuListPanel.activeSelf;

        /// <summary>役一覧を開くボタン。</summary>
        public RectTransform ToggleButtonRect =>
            toggleButton != null ? toggleButton.GetComponent<RectTransform>() : null;

        /// <summary>
        /// チュートリアルの誘導先。
        /// 開くボタンは画面右上の「役一覧」画像の上に重ねてあるだけで、それ単体を指しても
        /// どこを見ればよいか分からない。画像そのもの（親）を指して枠ごと見せる。
        /// </summary>
        public RectTransform GuideTargetRect
        {
            get
            {
                var buttonRect = ToggleButtonRect;
                if (buttonRect == null) return null;

                var parent = buttonRect.parent as RectTransform;
                return parent != null && parent.GetComponent<Image>() != null ? parent : buttonRect;
            }
        }

        public void OpenYakuList()
        {
            if (yakuListPanel != null)
            {
                yakuListPanel.SetActive(true);
            }

            CenterYakuGrid();
        }

        public void CloseYakuList()
        {
            if (yakuListPanel != null)
            {
                yakuListPanel.SetActive(false);
            }
        }

        /// <summary>
        /// 全役一覧のボックスを表示領域の中央へ寄せる。
        ///
        /// グリッドは `padding` が全て 0 の左揃えで、2列ぶんの幅（100×2 + 間隔5 = 205）に対して
        /// 表示領域が 233 あった。余りの 28 が全部右側に出て、ボックスが左に寄って見えていた。
        ///
        /// 余りを左右へ半分ずつ振り分ける。値を直に書かないのは、セルの大きさや列数を
        /// 変えたときに勝手に追従してほしいから。
        ///
        /// `childAlignment` を中央にする手もあるが、それだと**最終行が1個だけのとき
        /// その1個が中央に来て段がずれる**ので使わない。padding なら行の左揃えは保たれる。
        ///
        /// **シーンではなくここで直す。** 対局シーンが2つあるため、シーンを直すと
        /// 片方だけ直す事故が起きる。
        /// </summary>
        private void CenterYakuGrid()
        {
            var content = contentContainer as RectTransform;
            if (content == null) return;

            var grid = content.GetComponent<GridLayoutGroup>();
            var viewport = content.parent as RectTransform;
            if (grid == null || viewport == null) return;

            float viewportWidth = ResolveWidth(viewport);
            if (viewportWidth <= 0f) return;

            int columns = grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
                ? Mathf.Max(1, grid.constraintCount)
                : 1;

            float used = columns * grid.cellSize.x + (columns - 1) * grid.spacing.x;
            float slack = viewportWidth - used;
            if (slack <= 0f) return;            // 入りきっている。寄せる余地が無い

            int left = Mathf.RoundToInt(slack * 0.5f);
            if (grid.padding.left == left) return;

            // RectOffset は中の値を書き換えても再計算されないことがあるので、入れ替える
            grid.padding = new RectOffset(left, grid.padding.right, grid.padding.top, grid.padding.bottom);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        /// <summary>
        /// 幅を求める。**`rect.width` を待たない。**
        ///
        /// 開いた直後は `rect.width` が 0 で、`Canvas.ForceUpdateCanvases()` では直らない
        /// （あれは描画の更新で、レイアウトの再計算は走らない）。
        /// フレームを待つ方式も試したが、このパネルは開いた直後に別の都合で閉じられることがあり、
        /// 待っている間に条件が崩れて空振りする。
        ///
        /// 引き伸ばしアンカーなら幅は「親の幅 × アンカー幅 + sizeDelta」で決まるので、
        /// 0 のときは親をたどって計算する。レイアウトの実行順に依存しない。
        /// </summary>
        private static float ResolveWidth(RectTransform rect)
        {
            if (rect == null) return 0f;

            float width = rect.rect.width;
            if (width > 0f) return width;

            var parent = rect.parent as RectTransform;
            if (parent == null) return 0f;

            float anchorSpan = rect.anchorMax.x - rect.anchorMin.x;
            return anchorSpan * ResolveWidth(parent) + rect.sizeDelta.x;
        }

        private void InitializeItems()
        {
            if (itemPrefab == null || contentContainer == null) return;
            
            foreach (var yaku in allYakus)
            {
                var item = Instantiate(itemPrefab, contentContainer);
                spawnedItems.Add(item);
            }
        }

        public void UpdateBoostData(Dictionary<string, int> localBoost, Dictionary<string, int> enemyBoost)
        {
            if (spawnedItems.Count == 0) InitializeItems();

            if (localBoost == null) localBoost = new Dictionary<string, int>();
            if (enemyBoost == null) enemyBoost = new Dictionary<string, int>();

            for (int i = 0; i < allYakus.Length; i++)
            {
                string yaku = allYakus[i];
                int lBoost = localBoost.ContainsKey(yaku) ? localBoost[yaku] : 0;
                int eBoost = enemyBoost.ContainsKey(yaku) ? enemyBoost[yaku] : 0;

                if (i < spawnedItems.Count && spawnedItems[i] != null)
                {
                    spawnedItems[i].Setup(yaku, lBoost, eBoost);
                }
            }

            UpdateActiveBoosts(localBoost, localActiveBoostTexts);
            UpdateActiveBoosts(enemyBoost, enemyActiveBoostTexts);

            // **文字を入れて枠を表示させた「後」に当て直す。**
            // Start の時点では枠がまだ非アクティブで TMP が初期化されておらず、
            // `fontSharedMaterial` が null のためマテリアルの差し替えだけ空振りする
            // （自動縮小など他の設定は効くので、直ったように見えて輪郭だけ残る）。
            ApplyActiveBoostReadability();
        }

        /// <summary>常時表示の強化役（自3枠・敵3枠）の文字を読めるようにする。</summary>
        private void ApplyActiveBoostReadability()
        {
            ApplyActiveBoostReadability(localActiveBoostTexts);
            ApplyActiveBoostReadability(enemyActiveBoostTexts);
        }

        /// <summary>
        /// 常時表示の強化役が読みにくかった原因を潰す。
        ///
        /// 読みにくさの主犯だった**白い輪郭はマテリアル側で消してある**
        /// （`Assets/Resources/PixelMplus-20130602/PixelMplus-20130602/YakuChip_Outline.mat`
        /// の `_OutlineWidth` を 0.1 → 0）。このマテリアルは各シーンで6箇所＝チップ専用なので、
        /// 資産を直せば両シーンに一度で効く。
        ///
        /// **コードから `text.outlineWidth = 0` で消そうとしても効かない。** 輪郭の実体は
        /// マテリアルの `_OutlineWidth` で、プロパティ側を 0 にしても値が残る。
        /// マテリアルを実行時に差し替える手もあるが、`Start` の時点では枠が非アクティブで
        /// TMP が未初期化＝`fontSharedMaterial` が null のため空振りする。ここでは扱わない。
        ///
        /// このメソッドが持つのは、輪郭以外の読みやすさ（色と、枠に収める縮小）だけ。
        /// </summary>
        private void ApplyActiveBoostReadability(TextMeshProUGUI[] textArray)
        {
            if (textArray == null) return;

            foreach (var text in textArray)
            {
                if (text == null) continue;

                // 背景は自＝水色・敵＝赤で、どちらも明るい。黒で十分な差が出る
                text.color = Color.black;

                // **文字の矩形をチップに合わせる。** シーンでは文字が 200 幅で、
                // チップ(100幅)の倍あった。自動縮小は文字の矩形を基準にするので、
                // チップからはみ出していても縮まない（実際に「三色同順+1」が溢れていた）。
                var rect = text.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(ActiveBoostTextPadding, 0f);
                rect.offsetMax = new Vector2(-ActiveBoostTextPadding, 0f);

                // margin が残っていると中央揃えでも上下にずれる（おまかせボタンで踏んだ罠）
                text.margin = Vector4.zero;
                text.alignment = TextAlignmentOptions.Center;

                // 「混全帯么九+1」のような長い名前でも枠に収まるように縮める。
                // 折り返すと2行になって枠からはみ出すので、折り返しはさせない。
                text.enableAutoSizing = true;
                text.fontSizeMax = ActiveBoostFontSize;
                text.fontSizeMin = ActiveBoostMinFontSize;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
            }
        }

        /// <summary>常時表示の強化役の基本サイズ。シーンの値と同じ。</summary>
        private const float ActiveBoostFontSize = 20f;

        /// <summary>これ以上小さくすると読めなくなる下限。</summary>
        private const float ActiveBoostMinFontSize = 14f;

        /// <summary>文字がチップの縁に触れないようにする内側の余白（チップ幅100に対して）。</summary>
        private const float ActiveBoostTextPadding = 4f;

        private void UpdateActiveBoosts(Dictionary<string, int> boostDict, TextMeshProUGUI[] textArray)
        {
            if (textArray == null || textArray.Length == 0) return;

            List<KeyValuePair<string, int>> activeBoosts = new List<KeyValuePair<string, int>>();
            if (boostDict != null)
            {
                foreach (var kvp in boostDict)
                {
                    if (kvp.Value > 0)
                    {
                        activeBoosts.Add(kvp);
                    }
                }
            }

            // Dictionary の並び順は保証されないので、翻数の大きい順に並べ直す。
            // 枠に入りきらないときに「大きいものが落ちる」のを防ぐ意味もある。
            activeBoosts.Sort((a, b) =>
            {
                if (a.Value != b.Value) return b.Value.CompareTo(a.Value);
                return string.CompareOrdinal(a.Key, b.Key);
            });

            // 枠より多いときは、最後の枠を「あと何件あるか」に使う（要望22）。
            // 例）枠3・強化5件 → 上位2件を並べ、最後の枠に「+3」
            int slots = textArray.Length;
            bool overflow = activeBoosts.Count > slots;
            int shownCount = overflow ? slots - 1 : activeBoosts.Count;
            int hiddenCount = activeBoosts.Count - shownCount;

            for (int i = 0; i < textArray.Length; i++)
            {
                if (textArray[i] == null) continue;

                bool isOverflowSlot = overflow && i == slots - 1;

                if (i < shownCount || isOverflowSlot)
                {
                    textArray[i].text = isOverflowSlot
                        ? $"+{hiddenCount}"
                        : $"{activeBoosts[i].Key}+{activeBoosts[i].Value}";
                    textArray[i].gameObject.SetActive(true);
                    
                    // 背景画像（親オブジェクト）がある場合はそれも表示する
                    if (textArray[i].transform.parent != null && 
                        textArray[i].transform.parent.gameObject != yakuListPanel &&
                        !textArray[i].transform.parent.name.ToLower().Contains("panel") && // パネル全体を消さないように
                        textArray[i].transform.parent.GetComponent<UnityEngine.UI.Image>() != null)
                    {
                        textArray[i].transform.parent.gameObject.SetActive(true);
                    }
                }
                else
                {
                    textArray[i].gameObject.SetActive(false);
                    textArray[i].text = "";
                    
                    // 背景画像（親オブジェクト）がある場合は非表示にする
                    // ただし、それが親パネル（yakuListPanel）全体を包むものでないことを確認する
                    if (textArray[i].transform.parent != null && 
                        textArray[i].transform.parent.gameObject != yakuListPanel &&
                        !textArray[i].transform.parent.name.ToLower().Contains("panel") && // パネル全体を消さないように
                        textArray[i].transform.parent.GetComponent<UnityEngine.UI.Image>() != null)
                    {
                        textArray[i].transform.parent.gameObject.SetActive(false);
                    }
                }
            }
        }

        private readonly string[] allYakus = {
            "断么九", "平和", "一盃口", "東", "西", "一発", "河底撈魚",
            "三色同順", "三色同刻", "三暗刻", "対々和", "混老頭", "混全帯么九", "七対子",
            "二盃口", "混一色", "純全帯么九",
            "清一色",
            "九蓮宝燈", "緑一色", "清老頭", "四暗刻", "純正九蓮宝燈"
        };
    }
}
