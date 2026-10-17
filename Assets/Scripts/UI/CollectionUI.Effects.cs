using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    public sealed partial class CollectionUI
    {
        /// <summary>
        /// 演出の一覧1枚ぶん。**「演出」タブと「没案」タブで同じ作りを使い回す**（2026-10-07）。
        /// 違うのは、一覧表のどの行を並べるか（<see cref="Filter"/>）だけ。
        /// </summary>
        private sealed class EffectListPage
        {
            public string Title;
            public Func<GameEffectCatalog.Entry, bool> Filter;
            public int Total;
            public RectTransform Content;
            public TextMeshProUGUI Name, Details, Count;
            public GameEffectCatalog.Entry Selected;
            /// <summary>フェイズ別タブで選んでいるもの（<see cref="GameEffectCatalog.Phases"/>）。</summary>
            public string Phase = GameEffectCatalog.AllPhases;
            public string Query = "";
            public readonly List<(string phase, Image image)> PhaseTabs = new List<(string, Image)>();
            public readonly List<Image> Rows = new List<Image>();
            public readonly List<GameEffectCatalog.Entry> Visible = new List<GameEffectCatalog.Entry>();
        }

        private GameObject effectsPage;

        /// <summary>「演出」タブ。没案は並べない（「没案」タブ＝<see cref="BuildUnusedPage"/> の側に出す）。</summary>
        private void BuildEffectsPage(Transform parent)
        {
            BuildEffectList(parent, "演出", "名前・分類・IDで検索（例：透視／画面効果／ron）",
                entry => !entry.IsUnused, "skill.perspective");
        }

        private void BuildEffectList(Transform parent, string title, string searchHint,
            Func<GameEffectCatalog.Entry, bool> filter, string firstSelectedId)
        {
            var page = new EffectListPage { Title = title, Filter = filter };
            foreach (var entry in GameEffectCatalog.Entries)
            {
                if (!filter(entry)) continue;
                page.Total++;
                // 最初に選んでおく行。指定が無いか、この一覧に無ければ先頭にする
                if (page.Selected == null || entry.Id == firstSelectedId) page.Selected = entry;
            }

            // フェイズ別のタブ。1局の流れの順に横へ並べる（件数の行があった場所）
            const float tabW = 82f, tabGap = 3f;
            var phases = GameEffectCatalog.Phases;
            float tabsLeft = -(phases.Length * tabW + (phases.Length - 1) * tabGap) / 2f + tabW / 2f;
            for (int i = 0; i < phases.Length; i++)
            {
                string phase = phases[i];
                string tabName = "Phase_" + i;
                Button(parent, tabName, phase, new Vector2(tabsLeft + i * (tabW + tabGap), 164),
                    new Vector2(tabW, 24), 13, () => { page.Phase = phase; RebuildEffectRows(page, page.Query); });
                var tab = parent.Find(tabName);
                var tabLabel = tab.GetComponentInChildren<TextMeshProUGUI>();
                // 「チュートリアル」は幅に入りきらないので、折り返さずに縮める
                tabLabel.textWrappingMode = TextWrappingModes.NoWrap;
                tabLabel.enableAutoSizing = true; tabLabel.fontSizeMin = 9; tabLabel.fontSizeMax = 13;
                int inPhase = 0;
                foreach (var entry in GameEffectCatalog.Entries)
                    if (filter(entry) && (phase == GameEffectCatalog.AllPhases || GameEffectCatalog.PhaseOf(entry) == phase)) inPhase++;
                // 没案タブでは、そのフェイズに1件も無いタブは押せなくする
                if (inPhase == 0) tab.GetComponent<Button>().interactable = false;
                page.PhaseTabs.Add((phase, tab.GetComponent<Image>()));
            }

            var inputObject = NewImage(parent, "Search", new Color32(48, 32, 42, 255));
            Center(inputObject.rectTransform, new Vector2(540, 30));
            inputObject.rectTransform.anchoredPosition = new Vector2(-70, 132);
            var text = Label(inputObject.transform, "Text", "", Vector2.zero, new Vector2(520, 28),
                15, TextAlignmentOptions.Left, TextMain);
            var hint = Label(inputObject.transform, "Hint", searchHint, Vector2.zero,
                new Vector2(520, 28), 14, TextAlignmentOptions.Left, TextDim);
            var input = inputObject.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = inputObject.rectTransform; input.textComponent = text; input.placeholder = hint;
            input.onValueChanged.AddListener(query => { page.Query = query; RebuildEffectRows(page, query); });
            page.Count = Label(parent, "Count", "", new Vector2(275, 132), new Vector2(130, 30),
                13, TextAlignmentOptions.Right, TextDim);

            var view = NewImage(parent, "EffectsViewport", new Color32(20, 12, 18, 255));
            Center(view.rectTransform, new Vector2(420, 350));
            view.rectTransform.anchoredPosition = new Vector2(-130, -66);
            view.gameObject.AddComponent<RectMask2D>();
            page.Content = NewEmpty(view.transform, "Content").GetComponent<RectTransform>();
            page.Content.anchorMin = new Vector2(0, 1); page.Content.anchorMax = new Vector2(1, 1);
            page.Content.pivot = new Vector2(.5f, 1);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view.rectTransform; scroll.content = page.Content;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;

            page.Name = Label(parent, "EffectName", "", new Vector2(220, 68), new Vector2(230, 65),
                18, TextAlignmentOptions.TopLeft, TextMain);
            page.Details = Label(parent, "EffectDetails", "", new Vector2(220, -45), new Vector2(230, 155),
                13, TextAlignmentOptions.TopLeft, TextDim);
            Button(parent, "PlayEffect", "再生", new Vector2(220, -157), new Vector2(230, 36), 18,
                () => PlaySelectedEffect(page, false));
            Button(parent, "LoopEffect", "繰り返し再生", new Vector2(220, -201), new Vector2(230, 36), 18,
                () => PlaySelectedEffect(page, true));
            RebuildEffectRows(page, "");
        }

        private void RebuildEffectRows(EffectListPage page, string query)
        {
            // **親から外してから消す。** Destroy はフレームの終わりまで残るので、外さないと
            // 下の Find が同じ名前の古い行を拾い、新しい行が並ばない（フェイズを切り替えたときに起きた）
            var old = new List<Transform>();
            foreach (Transform child in page.Content) old.Add(child);
            foreach (var child in old) { child.gameObject.SetActive(false); child.SetParent(null, false); Destroy(child.gameObject); }
            page.Rows.Clear(); page.Visible.Clear();
            query = (query ?? "").Trim();
            foreach (var entry in GameEffectCatalog.Entries)
            {
                if (!page.Filter(entry)) continue;
                if (page.Phase != GameEffectCatalog.AllPhases && GameEffectCatalog.PhaseOf(entry) != page.Phase) continue;
                string searchable = entry.Id + " " + entry.Group + " " + entry.Name;
                if (query.Length > 0 && searchable.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                int index = page.Visible.Count; page.Visible.Add(entry);
                string rowName = "Effect_" + entry.Id;
                Button(page.Content, rowName, entry.Group + "／" + entry.Name + "\n" + entry.Id,
                    Vector2.zero, new Vector2(414, 49), 14, () => SelectEffect(page, entry));
                var row = page.Content.Find(rowName).GetComponent<RectTransform>();
                row.anchorMin = row.anchorMax = new Vector2(.5f, 1);
                row.anchoredPosition = new Vector2(0, -25 - index * 51);
                page.Rows.Add(row.GetComponent<Image>());
            }
            page.Content.sizeDelta = new Vector2(0, Mathf.Max(350, page.Visible.Count * 51));
            page.Content.anchoredPosition = Vector2.zero;
            page.Count.text = $"{page.Visible.Count} / {page.Total} 件";
            foreach (var (phase, image) in page.PhaseTabs)
                image.color = phase == page.Phase ? (Color)RowSelected : new Color32(52, 30, 42, 255);
            // 選んでいた演出がこのタブに無ければ、先頭を選ぶ
            if (page.Selected == null || !page.Visible.Contains(page.Selected))
                page.Selected = page.Visible.Count > 0 ? page.Visible[0] : null;
            SelectEffect(page, page.Selected);
        }

        private void SelectEffect(EffectListPage page, GameEffectCatalog.Entry entry)
        {
            page.Selected = entry;
            // 一覧表にこの分類の行が1つも無いとき（没案を全部外したとき）は、選ぶものが無い
            page.Name.text = entry == null ? "" : entry.Name;
            page.Details.text = entry == null ? "" :
                entry.Group + "\nID: " + entry.Id + "\n\n試写用の局面で演出のみ再生。\n戦績・対局の状態は変更しません。\n\n実装: " + entry.Source;
            for (int i = 0; i < page.Rows.Count; i++)
                page.Rows[i].color = page.Visible[i] == entry ? RowSelected : new Color32(42, 27, 37, 255);
        }

        private void PlaySelectedEffect(EffectListPage page, bool loop)
        {
            if (page.Selected == null) return;
            StopPreview();
            EffectPreviewPlayer.Open(page.Selected.Id, loop);
        }
    }
}
