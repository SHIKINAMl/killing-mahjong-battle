using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    /// <summary>
    /// コレクションの「役」タブ（2026-10-09。それまでは「準備中」とだけ出ていた）。
    ///
    /// 左に役の一覧（翻数ごとに区切る）、右に選んだ役の説明と、牌で組んだ例を出す。
    ///
    /// **役の名前と翻数は、ここでは持たない。**
    ///   翻数 … <see cref="GameRules.GetBaseHan"/>（サーバーの `mahjong_engine/engine/yaku.py` と同じ27役）
    ///   説明 … <see cref="YakuInfo.GetDescription"/>（対局中の役一覧と同じ文）
    /// ここにあるのは「並べる順番」と「例の牌」だけ。役が増えたら <see cref="YakuEntries"/> に1行足す。
    ///
    /// **例の牌は、このゲームの牌だけで組む。** 字牌は東と西の2種しか無い（發・中・白などは無い）。
    /// 手牌13枚＋ロンする1枚。鳴きは無いので、どの例も門前の形。
    /// </summary>
    public sealed partial class CollectionUI
    {
        /// <summary>一覧に並べる1役。</summary>
        private readonly struct YakuEntry
        {
            public readonly string Name;

            /// <summary>
            /// 例の牌。「m123 p456 s789 E3 m5 + m5」のように書く
            /// （m=萬子 p=筒子 s=索子、後ろの数字が1枚ずつ。E=東 W=西 は後ろの数字が枚数。
            /// r を頭に付けると赤ドラ。「+」より後ろがロンする牌）。空なら例を出さない。
            /// </summary>
            public readonly string Example;

            /// <summary>例の下に出すひとこと。空なら既定の文。</summary>
            public readonly string Note;

            public YakuEntry(string name, string example, string note = "")
            {
                Name = name;
                Example = example;
                Note = note;
            }
        }

        /// <summary>
        /// 並べる順番と、例の牌。翻数の小さい順（サーバーの yaku.py と同じ並び）。
        /// 区切りの見出しは、翻数が変わる所に自動で入る。
        /// </summary>
        private static readonly YakuEntry[] YakuEntries =
        {
            // ---- 1翻 ----
            new YakuEntry("立直",     "", "形は問わない"),
            new YakuEntry("断么九",   "m234 m567 p345 s456 s8 + s8"),
            new YakuEntry("平和",     "m123 p567 s234 s67 p88 + s8", "順子4組。待ちは両側（5索か8索）"),
            new YakuEntry("一盃口",   "m223344 p567 s789 E1 + E1"),
            new YakuEntry("東",       "E3 m234 p567 s345 s9 + s9"),
            new YakuEntry("西",       "W3 m345 p234 s678 p9 + p9"),
            new YakuEntry("ドラ",     "", "形は問わない"),
            new YakuEntry("赤ドラ",   "rm5 rp5 rs5", "赤い5はこの3種類"),
            new YakuEntry("一発",     "", "形は問わない"),
            new YakuEntry("河底撈魚", "", "形は問わない"),

            // ---- 2翻 ----
            new YakuEntry("三色同順", "m345 m789 p345 s345 p1 + p1"),
            new YakuEntry("三色同刻", "m222 p222 s222 m678 W1 + W1"),
            new YakuEntry("三暗刻",   "m111 p444 s777 m456 p8 + p8"),
            new YakuEntry("対々和",   "m222 p555 s888 E2 W2 + E1"),
            new YakuEntry("混老頭",   "m111 m999 p111 s999 E1 + E1"),
            new YakuEntry("混全帯么九", "m123 m789 p123 E3 s9 + s9"),
            new YakuEntry("七対子",   "m11 m44 p22 p77 s33 s66 W1 + W1"),
            new YakuEntry("一気通貫", "m123 m456 m789 p234 s5 + s5"),

            // ---- 3翻 ----
            new YakuEntry("二盃口",   "m223344 p556677 s9 + s9"),
            new YakuEntry("混一色",   "m111 m345 m678 E3 m9 + m9"),
            new YakuEntry("純全帯么九", "m123 m789 p123 s789 s1 + s1"),

            // ---- 6翻 ----
            new YakuEntry("清一色",   "m123 m345 m567 m789 m9 + m9"),

            // ---- 役満 ----
            new YakuEntry("九蓮宝燈", "m1112245678999 + m3"),
            new YakuEntry("緑一色",   "s234 s234 s666 s888 s4 + s4", "2・3・4・6・8索だけ。このゲームに發は無い"),
            new YakuEntry("清老頭",   "m111 m999 p111 s11 s99 + s9"),
            new YakuEntry("四暗刻",   "m222 p444 p777 s333 E1 + E1"),

            // ---- ダブル役満 ----
            new YakuEntry("純正九蓮宝燈", "m1112345678999 + m5", "1から9のどれが来てもアガれる形"),
        };

        // 割り付け。プレイヤーバー（試聴の棒）が出ないタブなので、一覧は下まで使う
        private const float YakuListCenterX = -252f;
        private const float YakuListWidth = 200f;
        private const float YakuListTop = 150f;
        private const float YakuListBottom = -252f;
        private const float YakuRowPitch = 21f;
        private const float YakuDetailCenterX = 112f;
        private const float YakuDetailWidth = 480f;
        private const float YakuTileWidth = 28f;
        private const float YakuTileHeight = 37f;

        private readonly List<Image> yakuRowBgs = new List<Image>();
        private readonly List<int> yakuRowEntry = new List<int>();   // 行 → YakuEntries の番号（見出しは -1）
        private TextMeshProUGUI yakuNameText;
        private TextMeshProUGUI yakuHanText;
        private TextMeshProUGUI yakuDescText;
        private TextMeshProUGUI yakuExampleHead;
        private TextMeshProUGUI yakuNoteText;
        private RectTransform yakuTileRow;
        private TileResourceManager yakuTiles;
        private bool yakuTilesTried;
        private int yakuSelected = -1;

        private void BuildYakuPage(Transform parent)
        {
            yakuRowBgs.Clear();
            yakuRowEntry.Clear();

            BuildYakuList(parent);
            BuildYakuDetail(parent);

            SelectYaku(1);   // 立直は形の例が無いので、最初は例のある断么九を見せる
        }

        // ------------------------------------------------------------
        //  左: 役の一覧
        // ------------------------------------------------------------

        private void BuildYakuList(Transform parent)
        {
            float height = YakuListTop - YakuListBottom;
            float centerY = (YakuListTop + YakuListBottom) * 0.5f;

            // 枠。透明でも raycastTarget は要る（切るとホイールを拾えない。音楽の列と同じ）
            var viewport = NewImage(parent, "YakuViewport", new Color(0f, 0f, 0f, 0f));
            Center(viewport.rectTransform, new Vector2(YakuListWidth, height));
            viewport.rectTransform.anchoredPosition = new Vector2(YakuListCenterX, centerY);
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            var contentGo = NewEmpty(viewport.transform, "Content");
            var content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);

            float y = -4f;
            int lastHan = int.MinValue;
            for (int i = 0; i < YakuEntries.Length; i++)
            {
                int han = GameRules.GetBaseHan(YakuEntries[i].Name);
                if (han != lastHan)
                {
                    // 翻数が変わる所に見出しを入れる
                    lastHan = han;
                    if (i > 0) y -= 6f;
                    var head = Label(content, "Section_" + han, YakuSectionName(han), Vector2.zero,
                        new Vector2(YakuListWidth - 8f, 20f), 13f, TextAlignmentOptions.Left, Marker);
                    PlaceRow(head.rectTransform, y);
                    yakuRowBgs.Add(null);
                    yakuRowEntry.Add(-1);
                    y -= YakuRowPitch;
                }

                int index = i;
                var row = NewImage(content, "Yaku_" + i, new Color(0f, 0f, 0f, 0f));
                row.rectTransform.sizeDelta = new Vector2(YakuListWidth, 20f);
                PlaceRow(row.rectTransform, y);
                row.raycastTarget = true;
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = row;
                btn.onClick.AddListener(() => SelectYaku(index));
                yakuRowBgs.Add(row);
                yakuRowEntry.Add(i);

                Label(row.transform, "Name", YakuDisplayName(YakuEntries[i].Name), new Vector2(4f, 0f),
                    new Vector2(YakuListWidth - 24f, 18f), 14f, TextAlignmentOptions.Left, TextMain);
                y -= YakuRowPitch;
            }

            content.sizeDelta = new Vector2(YakuListWidth, -y + 6f);
            content.anchoredPosition = Vector2.zero;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 18f;
            scroll.inertia = false;

            // はみ出すときだけ、右端に細いつまみを出す（無いとスクロールできると気づけない）
            if (content.sizeDelta.y > height)
            {
                var track = NewImage(parent, "YakuBar", new Color32(40, 24, 34, 255));
                Center(track.rectTransform, new Vector2(4f, height));
                track.rectTransform.anchoredPosition = new Vector2(YakuListCenterX + YakuListWidth * 0.5f + 4f, centerY);
                var area = NewEmpty(track.transform, "SlidingArea");
                Stretch(area.GetComponent<RectTransform>());
                var handle = NewImage(area.transform, "Handle", Marker);
                handle.rectTransform.anchorMin = new Vector2(0f, 0f);
                handle.rectTransform.anchorMax = new Vector2(1f, 1f);
                handle.rectTransform.sizeDelta = Vector2.zero;
                var bar = track.gameObject.AddComponent<Scrollbar>();
                bar.handleRect = handle.rectTransform;
                bar.targetGraphic = handle;
                bar.direction = Scrollbar.Direction.BottomToTop;
                scroll.verticalScrollbar = bar;
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }

            content.anchoredPosition = Vector2.zero;
            scroll.verticalNormalizedPosition = 1f;

            // 一覧と説明を分ける細い線
            var divider = NewImage(parent, "YakuDivider", PanelEdge);
            Center(divider.rectTransform, new Vector2(1f, height));
            divider.rectTransform.anchoredPosition = new Vector2(YakuListCenterX + YakuListWidth * 0.5f + 16f, centerY);
            divider.raycastTarget = false;
        }

        /// <summary>行を、枠の上端から y の位置に置く（上端基準）。</summary>
        private static void PlaceRow(RectTransform rt, float y)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        /// <summary>
        /// 画面に出す役名。**サーバーの表記のまま出してはいけない。**
        /// 「断么九」などの「么」はフォントに無く、□ になる（最初に作ったとき実際に □ が出た）。
        /// 対局中の画面と同じ関数を通して、収録済みの「幺」に置き換える。
        /// </summary>
        private static string YakuDisplayName(string name)
        {
            return YakuNameUtil.ToDisplayText(new YakuNameUtil.Entry { BaseName = name, Boost = 0, Count = 1 });
        }

        private static string YakuSectionName(int han)
        {
            if (han >= 26) return "ダブル役満";
            if (han >= 13) return "役満";
            return han + "翻";
        }

        private static string YakuHanText(int han)
        {
            if (han >= 26) return "ダブル役満（26翻）";
            if (han >= 13) return "役満（13翻）";
            return han + "翻";
        }

        // ------------------------------------------------------------
        //  右: 選んだ役の説明と例
        // ------------------------------------------------------------

        private void BuildYakuDetail(Transform parent)
        {
            float left = YakuDetailCenterX - YakuDetailWidth * 0.5f;

            yakuNameText = Label(parent, "YakuName", "", new Vector2(YakuDetailCenterX, 126f),
                new Vector2(YakuDetailWidth, 40f), 28f, TextAlignmentOptions.Left, TextMain);
            yakuHanText = Label(parent, "YakuHan", "", new Vector2(YakuDetailCenterX, 124f),
                new Vector2(YakuDetailWidth, 30f), 17f, TextAlignmentOptions.Right, Marker);

            var rule = NewImage(parent, "YakuNameRule", PanelEdge);
            Center(rule.rectTransform, new Vector2(YakuDetailWidth, 1f));
            rule.rectTransform.anchoredPosition = new Vector2(YakuDetailCenterX, 102f);
            rule.raycastTarget = false;

            yakuDescText = Label(parent, "YakuDesc", "", new Vector2(YakuDetailCenterX, 70f),
                new Vector2(YakuDetailWidth, 48f), 16f, TextAlignmentOptions.TopLeft, TextMain);
            yakuDescText.textWrappingMode = TextWrappingModes.Normal;

            yakuExampleHead = Label(parent, "YakuExampleHead", "例", new Vector2(YakuDetailCenterX, 18f),
                new Vector2(YakuDetailWidth, 20f), 13f, TextAlignmentOptions.Left, Marker);

            var rowGo = NewEmpty(parent, "YakuTiles");
            yakuTileRow = rowGo.GetComponent<RectTransform>();
            Center(yakuTileRow, new Vector2(YakuDetailWidth, YakuTileHeight));
            yakuTileRow.anchoredPosition = new Vector2(YakuDetailCenterX, -18f);

            yakuNoteText = Label(parent, "YakuNote", "", new Vector2(YakuDetailCenterX, -58f),
                new Vector2(YakuDetailWidth, 36f), 12.5f, TextAlignmentOptions.TopLeft, TextDim);
            yakuNoteText.textWrappingMode = TextWrappingModes.Normal;

            // 翻数と倍率の表。値はサーバーの精算（game_engine.py の _get_liquidation_multiplier）と同じ
            var tableRule = NewImage(parent, "YakuTableRule", PanelEdge);
            Center(tableRule.rectTransform, new Vector2(YakuDetailWidth, 1f));
            tableRule.rectTransform.anchoredPosition = new Vector2(YakuDetailCenterX, -150f);
            tableRule.raycastTarget = false;

            Label(parent, "YakuTableHead", "翻数の合計と倍率", new Vector2(YakuDetailCenterX, -166f),
                new Vector2(YakuDetailWidth, 20f), 13f, TextAlignmentOptions.Left, Marker);

            string[] ranks = { "満貫", "跳満", "倍満", "三倍満", "役満", "ダブル役満" };
            int[] hans = { 4, 6, 8, 11, 13, 26 };
            float cell = YakuDetailWidth / ranks.Length;
            for (int i = 0; i < ranks.Length; i++)
            {
                float x = left + cell * (i + 0.5f);
                Label(parent, "YakuRank_" + i, ranks[i], new Vector2(x, -190f), new Vector2(cell, 20f),
                    13f, TextAlignmentOptions.Center, TextMain);
                Label(parent, "YakuRankHan_" + i, hans[i] + "翻から", new Vector2(x, -208f), new Vector2(cell, 18f),
                    12f, TextAlignmentOptions.Center, TextDim);
                Label(parent, "YakuRankRate_" + i, "×" + GameRules.GetMultiplier(hans[i]).ToString("0.#"),
                    new Vector2(x, -226f), new Vector2(cell, 18f), 12f, TextAlignmentOptions.Center, TextDim);
            }

            Label(parent, "YakuTableNote", "合計が4翻（満貫）に届かない手では、ロンできない",
                new Vector2(YakuDetailCenterX, -248f), new Vector2(YakuDetailWidth, 18f),
                11.5f, TextAlignmentOptions.Left, TextDim);
        }

        private void SelectYaku(int index)
        {
            if (index < 0 || index >= YakuEntries.Length) return;
            yakuSelected = index;

            for (int i = 0; i < yakuRowBgs.Count; i++)
            {
                if (yakuRowBgs[i] == null) continue;
                yakuRowBgs[i].color = (yakuRowEntry[i] == index) ? RowSelected : new Color(0f, 0f, 0f, 0f);
            }

            YakuEntry entry = YakuEntries[index];
            int han = GameRules.GetBaseHan(entry.Name);

            if (yakuNameText != null) yakuNameText.text = YakuDisplayName(entry.Name);
            if (yakuHanText != null) yakuHanText.text = han > 0 ? YakuHanText(han) : "";
            if (yakuDescText != null) yakuDescText.text = YakuInfo.GetDescription(entry.Name);

            bool hasExample = ShowYakuExample(entry.Example);
            if (yakuExampleHead != null) yakuExampleHead.gameObject.SetActive(hasExample);
            if (yakuNoteText != null)
            {
                bool wins = entry.Example.Contains("+");
                string note = entry.Note;
                if (string.IsNullOrEmpty(note) && wins) note = "右に離した1枚でロン";
                else if (!string.IsNullOrEmpty(note) && wins) note = "右に離した1枚でロン。" + note;
                yakuNoteText.text = note;

                // 例が無い役は、ひとことを例の位置まで上げる
                yakuNoteText.rectTransform.anchoredPosition =
                    new Vector2(YakuDetailCenterX, hasExample ? -58f : 14f);
            }
        }

        /// <summary>例の牌を並べる。並べたら true。書式は <see cref="YakuEntry.Example"/>。</summary>
        private bool ShowYakuExample(string example)
        {
            if (yakuTileRow == null) return false;
            for (int i = yakuTileRow.childCount - 1; i >= 0; i--) Destroy(yakuTileRow.GetChild(i).gameObject);
            if (string.IsNullOrEmpty(example)) return false;

            TileResourceManager tiles = GetYakuTiles();
            if (tiles == null) return false;

            // 「+」の前が手牌、後ろがロンする牌
            var ids = new List<int>();
            int winFrom = -1;
            foreach (string token in example.Split(' '))
            {
                if (token.Length == 0) continue;
                if (token == "+") { winFrom = ids.Count; continue; }
                AddExampleTiles(token, ids);
            }
            if (ids.Count == 0) return false;

            const float gap = 2f;
            const float winGap = 14f;
            float x = -YakuDetailWidth * 0.5f + YakuTileWidth * 0.5f;
            for (int i = 0; i < ids.Count; i++)
            {
                if (i == winFrom) x += winGap;

                var img = NewImage(yakuTileRow, "Tile" + i, Color.white);
                img.sprite = tiles.GetTileSprite(ids[i]);
                img.raycastTarget = false;
                Center(img.rectTransform, new Vector2(YakuTileWidth, YakuTileHeight));
                img.rectTransform.anchoredPosition = new Vector2(x, 0f);
                x += YakuTileWidth + gap;
            }
            return true;
        }

        /// <summary>「m123」「E3」「rp5」のような1かたまりを牌の番号にして足す。</summary>
        private static void AddExampleTiles(string token, List<int> ids)
        {
            bool red = token[0] == 'r';
            if (red) token = token.Substring(1);
            if (token.Length < 2) return;

            char kind = token[0];
            string digits = token.Substring(1);

            if (kind == 'E' || kind == 'W')
            {
                int count = digits[0] - '0';
                int honor = kind == 'E' ? TutorialTiles.Ton : TutorialTiles.Sha;
                for (int i = 0; i < count; i++) ids.Add(TutorialTiles.Encode(honor));
                return;
            }

            foreach (char d in digits)
            {
                int number = d - '0';
                if (number < 1 || number > 9) continue;
                int baseId = kind == 'm' ? TutorialTiles.Man(number)
                           : kind == 'p' ? TutorialTiles.Pin(number)
                           : TutorialTiles.Sou(number);
                ids.Add(TutorialTiles.Encode(baseId, false, red));
            }
        }

        /// <summary>
        /// 牌の絵の表。**演出の試写の舞台（EffectPreviewRig）が持っているものを借りる。**
        /// 表そのもの（`Assets/Preafb/麻雀牌リスト.asset`）は Resources の外にあって、ここからは直接読めない。
        /// </summary>
        private TileResourceManager GetYakuTiles()
        {
            if (yakuTiles != null || yakuTilesTried) return yakuTiles;
            yakuTilesTried = true;

            var prefab = Resources.Load<GameObject>("Presentation/EffectPreviewRig");
            var rig = prefab != null ? prefab.GetComponent<EffectPreviewRig>() : null;
            yakuTiles = rig != null ? rig.tiles : null;
            if (yakuTiles == null)
                Debug.LogWarning("[CollectionUI] 牌の絵の表が読めませんでした。役の例は出しません");
            return yakuTiles;
        }
    }
}
