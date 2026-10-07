using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    public sealed partial class CollectionUI
    {
        private GameObject effectsPage;
        private RectTransform effectsContent;
        private TextMeshProUGUI effectName, effectDetails, effectCount;
        private GameEffectCatalog.Entry selectedEffect;
        private readonly List<Image> effectRows = new List<Image>();
        private readonly List<GameEffectCatalog.Entry> visibleEffects = new List<GameEffectCatalog.Entry>();

        private void BuildEffectsPage(Transform parent)
        {
            effectCount = Label(parent, "Count", "", new Vector2(0, 164), new Vector2(680, 24),
                14, TextAlignmentOptions.Left, TextDim);
            var inputObject = NewImage(parent, "Search", new Color32(48, 32, 42, 255));
            Center(inputObject.rectTransform, new Vector2(680, 30));
            inputObject.rectTransform.anchoredPosition = new Vector2(0, 132);
            var text = Label(inputObject.transform, "Text", "", Vector2.zero, new Vector2(660, 28),
                15, TextAlignmentOptions.Left, TextMain);
            var hint = Label(inputObject.transform, "Hint", "名前・分類・IDで検索（例：透視／画面効果／ron）", Vector2.zero,
                new Vector2(660, 28), 14, TextAlignmentOptions.Left, TextDim);
            var input = inputObject.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = inputObject.rectTransform; input.textComponent = text; input.placeholder = hint;
            input.onValueChanged.AddListener(RebuildEffectRows);

            var view = NewImage(parent, "EffectsViewport", new Color32(20, 12, 18, 255));
            Center(view.rectTransform, new Vector2(420, 350));
            view.rectTransform.anchoredPosition = new Vector2(-130, -66);
            view.gameObject.AddComponent<RectMask2D>();
            effectsContent = NewEmpty(view.transform, "Content").GetComponent<RectTransform>();
            effectsContent.anchorMin = new Vector2(0, 1); effectsContent.anchorMax = new Vector2(1, 1);
            effectsContent.pivot = new Vector2(.5f, 1);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view.rectTransform; scroll.content = effectsContent;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;

            effectName = Label(parent, "EffectName", "", new Vector2(220, 68), new Vector2(230, 65),
                18, TextAlignmentOptions.TopLeft, TextMain);
            effectDetails = Label(parent, "EffectDetails", "", new Vector2(220, -45), new Vector2(230, 155),
                13, TextAlignmentOptions.TopLeft, TextDim);
            Button(parent, "PlayEffect", "再生", new Vector2(220, -157), new Vector2(230, 36), 18,
                () => PlaySelectedEffect(false));
            Button(parent, "LoopEffect", "繰り返し再生", new Vector2(220, -201), new Vector2(230, 36), 18,
                () => PlaySelectedEffect(true));
            selectedEffect = GameEffectCatalog.Find("skill.perspective");
            RebuildEffectRows("");
        }

        private void RebuildEffectRows(string query)
        {
            foreach (Transform child in effectsContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            effectRows.Clear(); visibleEffects.Clear();
            query = (query ?? "").Trim();
            foreach (var entry in GameEffectCatalog.Entries)
            {
                string searchable = entry.Id + " " + entry.Group + " " + entry.Name;
                if (query.Length > 0 && searchable.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                int index = visibleEffects.Count; visibleEffects.Add(entry);
                string rowName = "Effect_" + entry.Id;
                Button(effectsContent, rowName, entry.Group + "／" + entry.Name + "\n" + entry.Id,
                    Vector2.zero, new Vector2(414, 49), 14, () => SelectEffect(entry));
                var row = effectsContent.Find(rowName).GetComponent<RectTransform>();
                row.anchorMin = row.anchorMax = new Vector2(.5f, 1);
                row.anchoredPosition = new Vector2(0, -25 - index * 51);
                effectRows.Add(row.GetComponent<Image>());
            }
            effectsContent.sizeDelta = new Vector2(0, Mathf.Max(350, visibleEffects.Count * 51));
            effectsContent.anchoredPosition = Vector2.zero;
            effectCount.text = $"演出 {visibleEffects.Count} / {GameEffectCatalog.Entries.Count} 件　スクロールして選択";
            SelectEffect(selectedEffect);
        }

        private void SelectEffect(GameEffectCatalog.Entry entry)
        {
            selectedEffect = entry;
            effectName.text = entry.Name;
            effectDetails.text = entry.Group + "\nID: " + entry.Id + "\n\n試写用の局面で演出のみ再生。\n戦績・対局の状態は変更しません。\n\n実装: " + entry.Source;
            for (int i = 0; i < effectRows.Count; i++)
                effectRows[i].color = visibleEffects[i] == entry ? RowSelected : new Color32(42, 27, 37, 255);
        }

        private void PlaySelectedEffect(bool loop)
        {
            StopPreview();
            EffectPreviewPlayer.Open(selectedEffect.Id, loop);
        }
    }
}
