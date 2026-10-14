using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 役強化で、強める役を選ぶ画面（2026-10-09 に作り直した）。
    ///
    /// **コレクションの「役」タブと同じ形**（ユーザーの指示）。
    /// 左に役の一覧（翻数ごとに区切る）、右に選んだ役の説明と、牌で組んだ例。
    /// それまでは、白いボタンを3列に並べただけの表で、役名と1行の条件しか読めなかった。
    ///
    /// コレクションと違う所は2つ。
    ///   ・並ぶのは強化できる役だけ（<see cref="Yakus"/>）。すでに強めている役には「+1」が付く
    ///   ・右上の翻数は「6翻 → 7翻」。右下に「この役を強化する」「やめる」
    /// **行を押しただけでは決まらない。** 押すと右に説明が出て、「この役を強化する」で決まる
    /// （前は押した瞬間に決まっていたので、説明を読んでから選ぶことができなかった）。
    ///
    /// **部品はシーンに置いてある**（`Assets/Prefabs/UI/YakuSelection.prefab`。対局のシーン2つに置く）。
    /// ここのコードは「シーンに置いてあればそれを使い、無ければ作る」（<see cref="SceneFirst"/>）。
    /// 位置・色・決まった文字はシーンが正で、コードの数字は無かったときに作る控え。
    /// 足したら `Tools > UI > 実行時UIをシーンへ置く（対局）` で焼き込むこと。
    /// 再生時に書き換えるのは、選んだ役で変わる所（右の説明・翻数・例の牌・行の色・「+1」）だけ。
    /// </summary>
    public class YakuSelectionUI : MonoBehaviour
    {
        /// <summary>シーンに置く入れ物の名前。</summary>
        public const string RootName = "YakuSelection";

        /// <summary>
        /// 強化できる役。翻数の小さい順。**サーバーへ送る名前そのまま**（画面には
        /// <see cref="YakuExamples.DisplayName"/> を通して出す）。
        /// </summary>
        private static readonly string[] Yakus =
        {
            "断么九", "平和", "一盃口", "東", "西", "一発", "河底撈魚",
            "三色同順", "三色同刻", "三暗刻", "対々和", "混老頭", "混全帯么九", "七対子",
            "二盃口", "混一色", "純全帯么九",
            "清一色",
            "九蓮宝燈", "緑一色", "清老頭", "四暗刻", "純正九蓮宝燈"
        };

        // 色はコレクション（CollectionUI）と同じ
        private static readonly Color TextMain = new Color32(240, 232, 236, 255);
        private static readonly Color TextDim = new Color32(170, 156, 164, 255);
        private static readonly Color Marker = new Color32(214, 40, 62, 255);
        private static readonly Color PanelBg = new Color32(28, 18, 26, 252);
        private static readonly Color PanelEdge = new Color32(80, 48, 62, 255);
        private static readonly Color RowSelected = new Color32(64, 28, 40, 255);
        private static readonly Color ButtonBg = new Color32(52, 30, 42, 255);
        private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        /// <summary>強化した分の色。牌交換の IN・役強化の演出の「新しい翻数」と同じ水色。</summary>
        private const string BoostHex = "#33CCFF";
        private static readonly Color BoostColor = new Color(0.2f, 0.8f, 1f, 1f);

        // 割り付け（800x600 の画面の中心が原点）。コレクションの役タブと同じ寸法を、枠に合わせて上へ寄せた
        private static readonly Vector2 PanelSize = new Vector2(720f, 480f);
        private const float ListCenterX = -250f;
        private const float ListWidth = 200f;
        private const float ListTop = 184f;
        private const float ListBottom = -224f;
        private const float RowPitch = 21f;
        private const float DetailCenterX = 110f;
        private const float DetailWidth = 460f;
        private const float TileWidth = 28f;
        private const float TileHeight = 37f;

        /// <summary>
        /// 役一覧（閲覧）で出す役。強化できる役（<see cref="Yakus"/>）に、強化できないが型に入る役
        /// （一気通貫・ドラなど）を足し、翻数の順に並べる（<see cref="AllYakus"/>）。
        /// 2026-10-15 にいったん型ごとに並べたが、ユーザーの指示で翻数ごとに戻し、
        /// 型は役名の横の札で出すことにした。
        /// </summary>
        private static string[] AllYakus()
        {
            var list = new List<string>(Yakus);
            foreach (var info in HandArchetype.All)
                foreach (var yaku in info.Yaku)
                    if (!list.Contains(yaku)) list.Add(yaku);
            // 翻数の順。同じ翻数の中では先に入れた順（OrderBy は並びを崩さない）
            return System.Linq.Enumerable.ToArray(
                System.Linq.Enumerable.OrderBy(list, y => GameRules.GetBaseHan(y)));
        }

        private GameObject _body;
        private readonly List<Image> _rowBgs = new List<Image>();
        private readonly List<TextMeshProUGUI> _rowBadges = new List<TextMeshProUGUI>();
        private RectTransform _hanContent;
        private RectTransform _allContent;
        private readonly List<Image> _allRowBgs = new List<Image>();
        private readonly List<TextMeshProUGUI> _allRowBadges = new List<TextMeshProUGUI>();
        private readonly List<string> _allRowYaku = new List<string>();
        private TextMeshProUGUI _typeText;
        private HandTypeBadge _typeBadge;
        private int _selectedAll;
        private TextMeshProUGUI _nameText;
        private TextMeshProUGUI _hanText;
        private TextMeshProUGUI _descText;
        private TextMeshProUGUI _boostText;
        private TextMeshProUGUI _exampleHead;
        private TextMeshProUGUI _noteText;
        private RectTransform _tileRow;
        private ScrollRect _scroll;
        private Button _confirm;
        private Button _cancel;
        private TMP_FontAsset _font;
        private bool _built;
        private int _selected;

        private TileResourceManager _tiles;
        private IDictionary<string, int> _boosts;
        private Action<string> _onSelected;
        private Action _onCanceled;
        private bool _browseOnly;
        private IDictionary<string, int> _enemyBoosts;

        /// <summary>いま開いているか。</summary>
        public bool IsOpen { get { return _body != null && _body.activeSelf; } }

        /// <summary>シーンに置いてある画面を探す。無ければ作る（そのときは再生時に警告が出る）。</summary>
        public static YakuSelectionUI FindOrCreate()
        {
            foreach (var found in FindObjectsByType<YakuSelectionUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!found._browseOnly) return found;

            GameObject go = SceneFirst.Root(RootName, out bool _, typeof(RectTransform));
            var ui = go.GetComponent<YakuSelectionUI>();
            return ui != null ? ui : go.AddComponent<YakuSelectionUI>();
        }

        /// <param name="onSelected">「この役を強化する」を押したとき。サーバーへ送る役名が渡る</param>
        /// <param name="onCanceled">「やめる」を押したとき</param>
        /// <param name="tiles">牌の絵の表。例の牌を並べるのに使う。null なら例は出さない</param>
        /// <param name="boosts">いま自分が強めている役（役名 → 何翻ぶん）。null なら強化なしとして出す</param>
        public void Show(Action<string> onSelected, Action onCanceled,
            TileResourceManager tiles = null, IDictionary<string, int> boosts = null)
        {
            Build();

            _onSelected = onSelected;
            _onCanceled = onCanceled;
            _tiles = tiles;
            _boosts = boosts;

            _body.SetActive(true);
            UseAllList(false);

            for (int i = 0; i < _rowBadges.Count; i++)
            {
                int bonus = BonusOf(Yakus[i]);
                if (_rowBadges[i] != null) _rowBadges[i].text = bonus > 0 ? "+" + bonus : "";
            }

            // 開くたびに、いちばん上の役から。前に選んだ役を残すと、一覧の見えていない所が選ばれたままになる
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
            Select(0);
        }

        /// <summary>同じ部品を使った独立した閲覧画面。スキル選択画面と状態を共有しない。</summary>
        public static YakuSelectionUI CreateBrowser()
        {
            var copy = Instantiate(FindOrCreate().gameObject);
            copy.name = "AllYakuBrowser";
            var ui = copy.GetComponent<YakuSelectionUI>();
            ui._browseOnly = true;
            // 一覧は従来と同じ重なり順。チュートリアルの会話・クリック受付を覆わない。
            copy.GetComponent<Canvas>().sortingOrder = UISortingOrders.YakuOverview;
            ui.Hide();
            return ui;
        }

        public void ShowBrowser(Action onClose, TileResourceManager tiles,
            IDictionary<string, int> localBoosts, IDictionary<string, int> enemyBoosts)
        {
            bool wasOpen = IsOpen;
            int selected = _selectedAll;
            float scroll = _scroll != null ? _scroll.verticalNormalizedPosition : 1;
            _enemyBoosts = enemyBoosts;
            Show(null, onClose, tiles, localBoosts);
            _body.transform.Find("Panel/Title").GetComponent<TMP_Text>().text = "役一覧";
            _confirm.gameObject.SetActive(false);
            _cancel.GetComponentInChildren<TMP_Text>().text = "閉じる";
            // 一覧として見るときは、強化できない役も入れた並び（翻数ごと、役名の横に型の札）
            UseAllList(_allContent != null);
            if (_allContent == null)
            {
                Select(wasOpen ? _selected : 0);
                return;
            }
            for (int i = 0; i < _allRowBadges.Count; i++)
            {
                int local = BonusOf(_allRowYaku[i]);
                int enemy = EnemyBonusOf(_allRowYaku[i]);
                _allRowBadges[i].text = (local > 0 ? "<color=#33CCFF>+" + local + "</color> " : "")
                    + (enemy > 0 ? "<color=#FF4858>+" + enemy + "</color>" : "");
            }
            SelectAllRow(wasOpen ? selected : 0);
            if (_scroll != null) _scroll.verticalNormalizedPosition = wasOpen ? scroll : 1f;
        }

        /// <summary>左の一覧を、役一覧用の並び（全部の役）と役強化用の並び（強化できる役）で切り替える。</summary>
        private void UseAllList(bool all)
        {
            if (_allContent == null) all = false;
            if (_hanContent != null) _hanContent.gameObject.SetActive(!all);
            if (_allContent != null) _allContent.gameObject.SetActive(all);
            if (_scroll != null)
            {
                var content = all ? _allContent : _hanContent;
                if (content != null) _scroll.content = content;
            }
        }

        private int EnemyBonusOf(string yaku)
        {
            int value;
            return _enemyBoosts != null && _enemyBoosts.TryGetValue(yaku, out value) ? value : 0;
        }

        /// <summary>閉じる。どちらの合図も出さない。</summary>
        public void Hide()
        {
            if (_body != null) _body.SetActive(false);
        }

        /// <summary>
        /// 再生していないときに、足りない部品をシーンへ置く（<c>SceneFirstBaker</c> が呼ぶ）。
        /// 置いたあとは閉じた状態にしておく。
        /// </summary>
        /// <returns>置いた物の名前（「、」区切り）。何も足さなかったら空文字</returns>
        public static string BakeForEditor()
        {
            YakuSelectionUI ui = FindOrCreate();
            ui._built = false;
            string made = ui.Build();
            ui._body.SetActive(false);
            return made;
        }

        private int BonusOf(string yaku)
        {
            int bonus;
            return _boosts != null && _boosts.TryGetValue(yaku, out bonus) && bonus > 0 ? bonus : 0;
        }

        // ------------------------------------------------------------
        //  組み立て（シーンに置いてあればそれを使う）
        // ------------------------------------------------------------

        private string Build()
        {
            if (_built) return "";
            _built = true;

            _rowBgs.Clear();
            _rowBadges.Clear();
            _allRowBgs.Clear();
            _allRowBadges.Clear();
            _allRowYaku.Clear();
            _font = BorrowJapaneseFont();

            // 自前の Canvas。対局の Canvas は種類がまちまち（カメラに貼った物もある）なので、借りない
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = UISortingOrders.YakuSelection;

                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(800f, 600f);
                scaler.matchWidthOrHeight = 0.5f;

                gameObject.AddComponent<GraphicRaycaster>();
            }

            _body = SceneFirst.Child(transform, "Body", out bool madeBody, typeof(RectTransform));
            if (madeBody) Stretch((RectTransform)_body.transform);

            // 後ろの対局を暗く落とす。押しても下へ抜けないよう、クリックはここで受ける
            GameObject scrim = SceneFirst.Child(_body.transform, "Scrim", out bool madeScrim,
                typeof(RectTransform), typeof(Image));
            if (madeScrim)
            {
                Stretch((RectTransform)scrim.transform);
                var img = scrim.GetComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0.8f);
                img.raycastTarget = true;
            }

            Image panel = Img(_body.transform, "Panel", PanelBg, Vector2.zero, PanelSize);
            Transform p = panel.transform;

            Txt(p, "Title", "強化する役を選んでください", new Vector2(0f, 214f), new Vector2(680f, 28f),
                20f, TextAlignmentOptions.Left, TextMain);
            Img(p, "TitleRule", PanelEdge, new Vector2(0f, 196f), new Vector2(680f, 1f));

            BuildList(p);
            BuildDetail(p);
            BuildButtons(p);

            return SceneFirst.Report("YakuSelectionUI");
        }

        // ---- 左: 役の一覧 ----

        private void BuildList(Transform parent)
        {
            float height = ListTop - ListBottom;
            float centerY = (ListTop + ListBottom) * 0.5f;

            // 枠。透明でも raycastTarget は要る（切るとホイールを拾えない）
            GameObject viewportGo = SceneFirst.Child(parent, "ListViewport", out bool madeViewport,
                typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            var viewport = (RectTransform)viewportGo.transform;
            if (madeViewport)
            {
                Center(viewport, new Vector2(ListWidth, height));
                viewport.anchoredPosition = new Vector2(ListCenterX, centerY);
                var img = viewportGo.GetComponent<Image>();
                img.color = Clear;
                img.raycastTarget = true;
            }

            GameObject contentGo = SceneFirst.Child(viewport, "Content", out bool madeContent, typeof(RectTransform));
            var content = (RectTransform)contentGo.transform;
            if (madeContent)
            {
                content.anchorMin = new Vector2(0.5f, 1f);
                content.anchorMax = new Vector2(0.5f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
            }

            float y = -4f;
            int lastHan = int.MinValue;
            for (int i = 0; i < Yakus.Length; i++)
            {
                int han = GameRules.GetBaseHan(Yakus[i]);
                if (han != lastHan)
                {
                    // 翻数が変わる所に見出しを入れる
                    lastHan = han;
                    if (i > 0) y -= 6f;
                    TextMeshProUGUI head = Txt(content, "Section_" + han, YakuExamples.SectionName(han), Vector2.zero,
                        new Vector2(ListWidth - 8f, 20f), 13f, TextAlignmentOptions.Left, Marker, out bool madeHead);
                    if (madeHead) PlaceRow(head.rectTransform, y);
                    y -= RowPitch;
                }

                GameObject rowGo = SceneFirst.Child(content, "Yaku_" + i, out bool madeRow,
                    typeof(RectTransform), typeof(Image), typeof(Button));
                var rowImage = rowGo.GetComponent<Image>();
                var button = rowGo.GetComponent<Button>();
                if (madeRow)
                {
                    var rt = (RectTransform)rowGo.transform;
                    rt.sizeDelta = new Vector2(ListWidth, 20f);
                    PlaceRow(rt, y);
                    rowImage.color = Clear;
                    rowImage.raycastTarget = true;
                    button.targetGraphic = rowImage;
                }

                Txt(rowGo.transform, "Name", YakuExamples.DisplayName(Yakus[i]), new Vector2(-14f, 0f),
                    new Vector2(ListWidth - 44f, 18f), 14f, TextAlignmentOptions.Left, TextMain);
                // すでに強めている役に付ける「+1」。中身は開くたびに入れる
                TextMeshProUGUI badge = Txt(rowGo.transform, "Boost", "", new Vector2(ListWidth * 0.5f - 22f, 0f),
                    new Vector2(36f, 18f), 13f, TextAlignmentOptions.Right, BoostColor);

                // 押したときの動きはシーンに保存しない。置いてあるボタンは使い回すので、つなぎ直す
                int index = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => Select(index));

                _rowBgs.Add(rowImage);
                _rowBadges.Add(badge);
                y -= RowPitch;
            }

            if (madeContent)
            {
                content.sizeDelta = new Vector2(ListWidth, -y + 6f);
                content.anchoredPosition = Vector2.zero;
            }
            _hanContent = content;

            BuildAllList(viewport);

            _scroll = viewportGo.GetComponent<ScrollRect>();
            if (madeViewport)
            {
                _scroll.content = content;
                _scroll.viewport = viewport;
                _scroll.horizontal = false;
                _scroll.vertical = true;
                _scroll.movementType = ScrollRect.MovementType.Clamped;
                _scroll.scrollSensitivity = 18f;
                _scroll.inertia = false;
            }

            // 右端の細いつまみ（無いとスクロールできると気づけない）
            GameObject trackGo = SceneFirst.Child(parent, "ListBar", out bool madeBar,
                typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            if (madeBar)
            {
                var track = (RectTransform)trackGo.transform;
                Center(track, new Vector2(4f, height));
                track.anchoredPosition = new Vector2(ListCenterX + ListWidth * 0.5f + 4f, centerY);
                trackGo.GetComponent<Image>().color = new Color32(40, 24, 34, 255);

                var area = new GameObject("SlidingArea", typeof(RectTransform));
                area.transform.SetParent(track, false);
                Stretch((RectTransform)area.transform);

                var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
                handleGo.transform.SetParent(area.transform, false);
                var handle = (RectTransform)handleGo.transform;
                handle.anchorMin = new Vector2(0f, 0f);
                handle.anchorMax = new Vector2(1f, 1f);
                handle.sizeDelta = Vector2.zero;
                var handleImage = handleGo.GetComponent<Image>();
                handleImage.color = Marker;

                var bar = trackGo.GetComponent<Scrollbar>();
                bar.handleRect = handle;
                bar.targetGraphic = handleImage;
                bar.direction = Scrollbar.Direction.BottomToTop;
                _scroll.verticalScrollbar = bar;
                _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }

            // 一覧と説明を分ける細い線
            Img(parent, "ListDivider", PanelEdge,
                new Vector2(ListCenterX + ListWidth * 0.5f + 16f, centerY), new Vector2(1f, height));
        }

        /// <summary>
        /// 役一覧で使う並び（2026-10-15）。全部の役を翻数ごとに並べ、役名の横に型の札（角丸の文字の札）を置く。
        /// 役強化の並びと同じ枠に重ねて置き、開き方で片方だけ出す（<see cref="UseAllList"/>）。
        /// </summary>
        private void BuildAllList(RectTransform viewport)
        {
            GameObject contentGo = SceneFirst.Child(viewport, "ContentAll", out bool madeContent, typeof(RectTransform));
            var content = (RectTransform)contentGo.transform;
            if (madeContent)
            {
                content.anchorMin = new Vector2(0.5f, 1f);
                content.anchorMax = new Vector2(0.5f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
            }

            string[] yakus = AllYakus();
            float y = -4f;
            int lastHan = int.MinValue;
            for (int i = 0; i < yakus.Length; i++)
            {
                string yaku = yakus[i];
                int han = GameRules.GetBaseHan(yaku);
                if (han != lastHan)
                {
                    lastHan = han;
                    if (i > 0) y -= 6f;
                    TextMeshProUGUI head = Txt(content, "AllSection_" + han, YakuExamples.SectionName(han), Vector2.zero,
                        new Vector2(ListWidth - 8f, 20f), 13f, TextAlignmentOptions.Left, Marker, out bool madeHead);
                    if (madeHead) PlaceRow(head.rectTransform, y);
                    y -= RowPitch;
                }

                GameObject rowGo = SceneFirst.Child(content, "AllYaku_" + i, out bool madeRow,
                    typeof(RectTransform), typeof(Image), typeof(Button));
                var rowImage = rowGo.GetComponent<Image>();
                var button = rowGo.GetComponent<Button>();
                if (madeRow)
                {
                    var rt = (RectTransform)rowGo.transform;
                    rt.sizeDelta = new Vector2(ListWidth, 20f);
                    PlaceRow(rt, y);
                    rowImage.color = Clear;
                    rowImage.raycastTarget = true;
                    button.targetGraphic = rowImage;
                }

                // 名前は左寄せ。右端の「+1 +1」（自分・敵の強化）のぶん幅を空ける
                TextMeshProUGUI name = Txt(rowGo.transform, "Name", YakuExamples.DisplayName(yaku), new Vector2(-30f, 0f),
                    new Vector2(ListWidth - 76f, 18f), 14f, TextAlignmentOptions.Left, TextMain);
                TextMeshProUGUI badge = Txt(rowGo.transform, "Boost", "", new Vector2(ListWidth * 0.5f - 34f, 0f),
                    new Vector2(62f, 18f), 13f, TextAlignmentOptions.Right, BoostColor);
                BuildRowType(rowGo.transform, name, HandArchetype.Of(yaku));

                int index = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectAllRow(index));

                _allRowBgs.Add(rowImage);
                _allRowBadges.Add(badge);
                _allRowYaku.Add(yaku);
                y -= RowPitch;
            }

            if (madeContent)
            {
                content.sizeDelta = new Vector2(ListWidth, -y + 6f);
                content.anchoredPosition = Vector2.zero;
                // 置いた時点では役強化の並びを出しておく（役強化で開くのはこちら）
                contentGo.SetActive(false);
            }
            _allContent = content;
        }

        // ---- 役名の横の型の札（2026-10-15、ユーザーの指示「型はその役名の横に文字のアイコンを」） ----

        private const float RowTypeFontSize = 12f;

        private void BuildRowType(Transform row, TextMeshProUGUI name, HandArchetype.Info info)
        {
            HandTypeBadge badge = HandTypeBadge.Ensure(row, "TypeBadge", RowTypeFontSize, _font, true, out bool made);
            if (!made) return;

            // 役名の文字の幅のすぐ右。役名は置いたあと変わらないので、置くときに一度だけ測る
            float width = name.GetPreferredValues(name.text).x;
            if (width <= 0f) width = name.fontSize * name.text.Length;
            var nameRt = name.rectTransform;
            float nameLeft = nameRt.anchoredPosition.x - nameRt.sizeDelta.x * 0.5f;

            var rt = (RectTransform)badge.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(Mathf.Round(nameLeft + width + 4f), 0f);
            badge.Show(info);
        }

        // ---- 右: 選んだ役の説明と例 ----

        private void BuildDetail(Transform parent)
        {
            float left = DetailCenterX - DetailWidth * 0.5f;

            _nameText = Txt(parent, "YakuName", "", new Vector2(DetailCenterX, 164f),
                new Vector2(DetailWidth, 40f), 28f, TextAlignmentOptions.Left, TextMain);
            _hanText = Txt(parent, "YakuHan", "", new Vector2(DetailCenterX, 162f),
                new Vector2(DetailWidth, 30f), 19f, TextAlignmentOptions.Right, TextMain);
            Img(parent, "YakuNameRule", PanelEdge, new Vector2(DetailCenterX, 140f), new Vector2(DetailWidth, 1f));

            _descText = Txt(parent, "YakuDesc", "", new Vector2(DetailCenterX, 108f),
                new Vector2(DetailWidth, 48f), 16f, TextAlignmentOptions.TopLeft, TextMain, out bool madeDesc);
            if (madeDesc) _descText.textWrappingMode = TextWrappingModes.Normal;

            _boostText = Txt(parent, "YakuBoostNow", "", new Vector2(DetailCenterX, 66f),
                new Vector2(DetailWidth, 20f), 13f, TextAlignmentOptions.Left, BoostColor);

            _exampleHead = Txt(parent, "YakuExampleHead", "例", new Vector2(DetailCenterX, 40f),
                new Vector2(DetailWidth, 20f), 13f, TextAlignmentOptions.Left, Marker);

            GameObject rowGo = SceneFirst.Child(parent, "YakuTiles", out bool madeTiles, typeof(RectTransform));
            _tileRow = (RectTransform)rowGo.transform;
            if (madeTiles)
            {
                Center(_tileRow, new Vector2(DetailWidth, TileHeight));
                _tileRow.anchoredPosition = new Vector2(DetailCenterX, 4f);
            }

            _noteText = Txt(parent, "YakuNote", "", new Vector2(DetailCenterX, -38f),
                new Vector2(DetailWidth, 36f), 12.5f, TextAlignmentOptions.TopLeft, TextDim, out bool madeNote);
            if (madeNote) _noteText.textWrappingMode = TextWrappingModes.Normal;

            // 翻数と倍率の表。強めた1翻でどこまで届くかを、選びながら確かめられるように
            // 値はサーバーの精算（game_engine.py の _get_liquidation_multiplier）と同じ
            Img(parent, "TableRule", PanelEdge, new Vector2(DetailCenterX, -70f), new Vector2(DetailWidth, 1f));
            Txt(parent, "TableHead", "翻数の合計と倍率", new Vector2(DetailCenterX, -86f),
                new Vector2(DetailWidth, 20f), 13f, TextAlignmentOptions.Left, Marker);

            string[] ranks = { "満貫", "跳満", "倍満", "三倍満", "役満", "ダブル役満" };
            int[] hans = { 4, 6, 8, 11, 13, 26 };
            float cell = DetailWidth / ranks.Length;
            for (int i = 0; i < ranks.Length; i++)
            {
                float x = left + cell * (i + 0.5f);
                Txt(parent, "Rank_" + i, ranks[i], new Vector2(x, -108f), new Vector2(cell, 20f),
                    13f, TextAlignmentOptions.Center, TextMain);
                Txt(parent, "RankHan_" + i, hans[i] + "翻から", new Vector2(x, -126f), new Vector2(cell, 18f),
                    12f, TextAlignmentOptions.Center, TextDim);
                Txt(parent, "RankRate_" + i, "×" + GameRules.GetMultiplier(hans[i]).ToString("0.#"),
                    new Vector2(x, -144f), new Vector2(cell, 18f), 12f, TextAlignmentOptions.Center, TextDim);
            }

            // その役が入る型と、相手がその型のときに捨て牌に出る特徴（2026-10-15）
            // 左に型の札、右に特徴。札の幅は型で変わるので、文は一番広い札（ドラ頼み）の右から始める
            _typeBadge = HandTypeBadge.Ensure(parent, "YakuTypeBadge", 13f, _font, true, out bool madeBadge);
            if (madeBadge)
            {
                var rt = (RectTransform)_typeBadge.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(left, -166f);
                _typeBadge.Show(HandArchetype.Some);
            }
            _typeText = Txt(parent, "YakuType", "", new Vector2(DetailCenterX + 40f, -166f),
                new Vector2(DetailWidth - 80f, 18f), 12.5f, TextAlignmentOptions.Left, TextDim);

        }

        // ---- 右下: 決める・やめる ----

        private void BuildButtons(Transform parent)
        {
            _cancel = Btn(parent, "CancelButton", "やめる", new Vector2(-45f, -206f), new Vector2(150f, 40f), 16f, ButtonBg);
            _confirm = Btn(parent, "ConfirmButton", "この役を強化する", new Vector2(190f, -206f), new Vector2(300f, 40f), 18f, Marker);

            _cancel.onClick.RemoveAllListeners();
            _cancel.onClick.AddListener(() =>
            {
                Hide();
                if (_onCanceled != null) _onCanceled();
            });

            _confirm.onClick.RemoveAllListeners();
            _confirm.onClick.AddListener(() =>
            {
                if (_selected < 0 || _selected >= Yakus.Length) return;
                Hide();
                if (_onSelected != null) _onSelected(Yakus[_selected]);
            });
        }

        // ------------------------------------------------------------
        //  選んだ役で変わる所
        // ------------------------------------------------------------

        private void Select(int index)
        {
            if (index < 0 || index >= Yakus.Length) index = 0;
            _selected = index;

            HighlightRows(_rowBgs, index);
            ShowDetail(Yakus[index]);
        }

        /// <summary>役一覧の並びで行を選ぶ。</summary>
        private void SelectAllRow(int index)
        {
            if (index < 0 || index >= _allRowYaku.Count) index = 0;
            if (_allRowYaku.Count == 0) return;
            _selectedAll = index;
            HighlightRows(_allRowBgs, index);
            ShowDetail(_allRowYaku[index]);
        }

        private static void HighlightRows(List<Image> rows, int index)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] == null) continue;
                rows[i].color = i == index ? RowSelected : Clear;
                UIInteractionFeedback.Attach(rows[i].gameObject).SetSelected(i == index);
            }
        }

        private void ShowDetail(string yaku)
        {
            YakuExamples.Entry entry = YakuExamples.Find(yaku);
            int baseHan = GameRules.GetBaseHan(yaku);
            int bonus = BonusOf(yaku);
            int now = baseHan + bonus;

            if (_nameText != null) _nameText.text = YakuExamples.DisplayName(yaku);
            // **役強化は「翻数 +1」ではなく「倍率を1段上げる」**（2026-10-11 のスキル変更）。
            // 翻数は変わらないので「6翻 → 7翻」は出さない。どこまで上がれるかは役で決まる
            if (_hanText != null)
            {
                string cap = "<color=" + BoostHex + ">倍率 1段昇格（" + GameRules.GetBoostTierCapLabel(yaku) + "まで）</color>";
                _hanText.text = _browseOnly ? (baseHan > 0 ? baseHan + "翻" : "役満")
                    : baseHan > 0 ? baseHan + "翻  " + cap : cap;
            }
            if (_descText != null) _descText.text = YakuInfo.GetDescription(yaku);
            if (_boostText != null)
            {
                _boostText.text = _browseOnly
                    ? "<color=#33CCFF>自分：" + (bonus > 0 ? "強化済み +" + bonus : "強化なし")
                        + "</color>　<color=#FF4858>敵：" + (EnemyBonusOf(yaku) > 0 ? "強化済み +" + EnemyBonusOf(yaku) : "強化なし") + "</color>"
                    : bonus > 0 ? "強化済み。1つの役は1回だけ強化できる" : "";
            }
            // 強化済みの役は選べない（送るとサーバーがエラーを返す）
            if (_confirm != null) _confirm.interactable = bonus <= 0;

            bool hasExample = ShowExample(entry.Example);
            if (_exampleHead != null) _exampleHead.gameObject.SetActive(hasExample);
            if (_noteText != null) _noteText.text = YakuExamples.NoteOf(entry);

            var type = HandArchetype.Of(yaku);
            if (_typeBadge != null) _typeBadge.Show(type);
            if (_typeText != null) _typeText.text = type == null ? "" : "相手がこの型なら、" + type.Tell;
        }

        /// <summary>例の牌を並べる。並べたら true。牌の絵はその場で作る（選ぶたびに入れ替わる）。</summary>
        private bool ShowExample(string example)
        {
            if (_tileRow == null) return false;
            for (int i = _tileRow.childCount - 1; i >= 0; i--)
            {
                GameObject old = _tileRow.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(old); else DestroyImmediate(old);
            }
            if (_tiles == null) return false;

            int winFrom;
            List<int> ids = YakuExamples.Parse(example, out winFrom);
            if (ids.Count == 0) return false;

            const float gap = 2f;
            const float winGap = 14f;
            float x = -DetailWidth * 0.5f + TileWidth * 0.5f;
            for (int i = 0; i < ids.Count; i++)
            {
                if (i == winFrom) x += winGap;

                var go = new GameObject("Tile" + i, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_tileRow, false);
                var img = go.GetComponent<Image>();
                img.sprite = _tiles.GetTileSprite(ids[i]);
                img.raycastTarget = false;
                Center(img.rectTransform, new Vector2(TileWidth, TileHeight));
                img.rectTransform.anchoredPosition = new Vector2(x, 0f);
                x += TileWidth + gap;
            }
            return true;
        }

        // ------------------------------------------------------------
        //  小物。どれも「シーンに置いてあればそれを使い、無ければ作る」
        // ------------------------------------------------------------

        private static Image Img(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
        {
            GameObject go = SceneFirst.Child(parent, name, out bool created, typeof(RectTransform), typeof(Image));
            var img = go.GetComponent<Image>();
            if (created)
            {
                Center(img.rectTransform, size);
                img.rectTransform.anchoredPosition = pos;
                img.color = color;
                img.raycastTarget = false;
            }
            return img;
        }

        private TextMeshProUGUI Txt(Transform parent, string name, string text, Vector2 pos, Vector2 size,
            float fontSize, TextAlignmentOptions align, Color color)
        {
            return Txt(parent, name, text, pos, size, fontSize, align, color, out bool _);
        }

        private TextMeshProUGUI Txt(Transform parent, string name, string text, Vector2 pos, Vector2 size,
            float fontSize, TextAlignmentOptions align, Color color, out bool created)
        {
            GameObject go = SceneFirst.Child(parent, name, out created, typeof(RectTransform), typeof(TextMeshProUGUI));
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (created)
            {
                Center(tmp.rectTransform, size);
                tmp.rectTransform.anchoredPosition = pos;
                if (_font != null) tmp.font = _font;
                tmp.text = text;
                tmp.fontSize = fontSize;
                tmp.alignment = align;
                tmp.color = color;
                tmp.raycastTarget = false;
            }
            return tmp;
        }

        private Button Btn(Transform parent, string name, string label, Vector2 pos, Vector2 size, float fontSize, Color color)
        {
            GameObject go = SceneFirst.Child(parent, name, out bool created,
                typeof(RectTransform), typeof(Image), typeof(Button));
            var button = go.GetComponent<Button>();
            if (created)
            {
                var rt = (RectTransform)go.transform;
                Center(rt, size);
                rt.anchoredPosition = pos;
                var img = go.GetComponent<Image>();
                img.color = color;
                img.raycastTarget = true;
                button.targetGraphic = img;
            }
            Txt(go.transform, "Text", label, Vector2.zero, size, fontSize, TextAlignmentOptions.Center, TextMain);
            return button;
        }

        /// <summary>行を、枠の上端から y の位置に置く（上端基準）。</summary>
        private static void PlaceRow(RectTransform rt, float y)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        /// <summary>
        /// シーンに既にある日本語フォントを借りる（コレクションと同じ手口）。
        /// 専用のフォントアセットを持たせると、差し替えたときにここだけ取り残される。
        /// </summary>
        private static TMP_FontAsset BorrowJapaneseFont()
        {
            var labels = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var label in labels)
            {
                if (label != null && label.font != null) return label.font;
            }
            return TMP_Settings.defaultFontAsset;
        }
    }
}
