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
    /// **役の並び順・例の牌・翻数・説明は、ここでは持たない。**
    ///   並び順と例の牌 … <see cref="YakuExamples"/>（役強化で強める役を選ぶ画面と共用）
    ///   翻数 … <see cref="GameRules.GetBaseHan"/>
    ///   説明 … <see cref="YakuInfo.GetDescription"/>
    /// </summary>
    public sealed partial class CollectionUI
    {
        private static YakuExamples.Entry[] YakuEntries { get { return YakuExamples.Entries; } }

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

        private static string YakuDisplayName(string name) { return YakuExamples.DisplayName(name); }

        private static string YakuSectionName(int han) { return YakuExamples.SectionName(han); }

        private static string YakuHanText(int han) { return YakuExamples.HanText(han); }

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

            YakuExamples.Entry entry = YakuEntries[index];
            int han = GameRules.GetBaseHan(entry.Name);

            if (yakuNameText != null) yakuNameText.text = YakuDisplayName(entry.Name);
            if (yakuHanText != null) yakuHanText.text = han > 0 ? YakuHanText(han) : "";
            if (yakuDescText != null) yakuDescText.text = YakuInfo.GetDescription(entry.Name);

            bool hasExample = ShowYakuExample(entry.Example);
            if (yakuExampleHead != null) yakuExampleHead.gameObject.SetActive(hasExample);
            if (yakuNoteText != null)
            {
                yakuNoteText.text = YakuExamples.NoteOf(entry);

                // 例が無い役は、ひとことを例の位置まで上げる
                yakuNoteText.rectTransform.anchoredPosition =
                    new Vector2(YakuDetailCenterX, hasExample ? -58f : 14f);
            }
        }

        /// <summary>例の牌を並べる。並べたら true。書式は <see cref="YakuExamples.Entry.Example"/>。</summary>
        private bool ShowYakuExample(string example)
        {
            if (yakuTileRow == null) return false;
            for (int i = yakuTileRow.childCount - 1; i >= 0; i--) Destroy(yakuTileRow.GetChild(i).gameObject);
            if (string.IsNullOrEmpty(example)) return false;

            TileResourceManager tiles = GetYakuTiles();
            if (tiles == null) return false;

            int winFrom;
            List<int> ids = YakuExamples.Parse(example, out winFrom);
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
