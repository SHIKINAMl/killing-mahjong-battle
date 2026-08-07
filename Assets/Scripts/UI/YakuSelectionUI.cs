using UnityEngine;
using UnityEngine.UI;
using System;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public class YakuSelectionUI : MonoBehaviour
    {
        public Font customFont;
        private GameObject uiPanel;

        private Action<string> onSelectedAction;
        private Action onCanceledAction;

        public void Show(Action<string> onSelected, Action onCanceled)
        {
            if (uiPanel == null)
            {
                CreateUI();
            }
            
            this.onSelectedAction = onSelected;
            this.onCanceledAction = onCanceled;

            uiPanel.SetActive(true);
        }

        private void CreateUI()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null) canvas = canvas.rootCanvas;

            uiPanel = new GameObject("YakuSelectionPanel");
            if (canvas != null) uiPanel.transform.SetParent(canvas.transform, false);

            var rt = uiPanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var bg = uiPanel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.8f);

            var uiCanvas = uiPanel.AddComponent<Canvas>();
            uiCanvas.overrideSorting = true;
            uiCanvas.sortingOrder = UISortingOrders.YakuSelection;
            uiPanel.AddComponent<GraphicRaycaster>();

            Font fontToUse = customFont != null ? customFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(uiPanel.transform, false);
            var titleTxt = titleObj.AddComponent<Text>();
            titleTxt.text = "強化する役を選んでください";
            titleTxt.font = fontToUse;
            titleTxt.fontSize = (int)KillingMahjong.Common.UITypography.BodyLarge;
            titleTxt.color = Color.white;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            var titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 0.85f); titleRt.anchorMax = new Vector2(1, 1);
            titleRt.offsetMin = Vector2.zero; titleRt.offsetMax = Vector2.zero;

            GameObject scrollObj = new GameObject("ScrollView");
            scrollObj.transform.SetParent(uiPanel.transform, false);
            var scrollRt = scrollObj.AddComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.1f, 0.15f);
            scrollRt.anchorMax = new Vector2(0.9f, 0.85f);
            scrollRt.offsetMin = Vector2.zero; scrollRt.offsetMax = Vector2.zero;
            var scrollImg = scrollObj.AddComponent<Image>();
            scrollImg.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            var scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f;

            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(scrollObj.transform, false);
            var viewportRt = viewportObj.AddComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero; viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero; viewportRt.offsetMax = Vector2.zero;
            viewportObj.AddComponent<Image>();
            viewportObj.AddComponent<Mask>().showMaskGraphic = false;

            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);
            var contentRt = contentObj.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.offsetMin = Vector2.zero; contentRt.offsetMax = Vector2.zero;

            var grid = contentObj.AddComponent<GridLayoutGroup>();
            // 役名の下に成立条件を1行入れるため、以前の (160, 50) から高さを広げている
            grid.cellSize = new Vector2(190, 72);
            grid.spacing = new Vector2(8, 12);
            grid.padding = new RectOffset(8, 8, 16, 16);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3; // 3列に固定

            var fitter = contentObj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRt;
            scrollRect.viewport = viewportRt;

            string[] yakus = {
                "断么九", "平和", "一盃口", "東", "西", "一発", "河底撈魚",
                "三色同順", "三色同刻", "三暗刻", "対々和", "混老頭", "混全帯么九", "七対子",
                "二盃口", "混一色", "純全帯么九",
                "清一色",
                "九蓮宝燈", "緑一色", "清老頭", "四暗刻", "純正九蓮宝燈"
            };

            foreach (var y in yakus)
            {
                GameObject btnObj = new GameObject("Btn_" + y);
                btnObj.transform.SetParent(contentObj.transform, false);
                var btnImg = btnObj.AddComponent<Image>();
                btnImg.color = Color.white;
                var btn = btnObj.AddComponent<Button>();
                
                GameObject txtObj = new GameObject("Text");
                txtObj.transform.SetParent(btnObj.transform, false);
                var txt = txtObj.AddComponent<Text>();
                txt.text = y;
                txt.font = fontToUse;
                txt.fontSize = (int)KillingMahjong.Common.UITypography.BodySmall; // 枠に合わせて少し文字を小さく
                txt.color = Color.black;
                txt.alignment = TextAnchor.LowerCenter;
                var txtRt = txtObj.GetComponent<RectTransform>();
                // 上側に役名、下側に成立条件を置く
                txtRt.anchorMin = new Vector2(0, 0.55f); txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;

                // 役名だけでは「どれを強化すべきか」が初見で判断できないため、条件を併記する
                string yakuDesc = KillingMahjong.Common.YakuInfo.GetDescription(y);
                if (!string.IsNullOrEmpty(yakuDesc))
                {
                    GameObject descObj = new GameObject("Desc");
                    descObj.transform.SetParent(btnObj.transform, false);
                    var descTxt = descObj.AddComponent<Text>();
                    descTxt.text = yakuDesc;
                    descTxt.font = fontToUse;
                    descTxt.fontSize = Mathf.Max(9, (int)(KillingMahjong.Common.UITypography.BodySmall * 0.6f));
                    descTxt.color = new Color(0.25f, 0.25f, 0.25f);
                    descTxt.alignment = TextAnchor.UpperCenter;
                    descTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
                    descTxt.verticalOverflow = VerticalWrapMode.Truncate;
                    descTxt.raycastTarget = false; // ボタンのクリック判定を奪わない
                    var descRt = descObj.GetComponent<RectTransform>();
                    descRt.anchorMin = new Vector2(0, 0); descRt.anchorMax = new Vector2(1, 0.55f);
                    descRt.offsetMin = new Vector2(4, 2); descRt.offsetMax = new Vector2(-4, 0);
                }

                string yakuName = y;
                btn.onClick.AddListener(() => {
                    uiPanel.SetActive(false);
                    onSelectedAction?.Invoke(yakuName);
                });
            }

            GameObject cancelObj = new GameObject("Btn_Cancel");
            cancelObj.transform.SetParent(uiPanel.transform, false);
            var cancelRt = cancelObj.AddComponent<RectTransform>();
            cancelRt.anchorMin = new Vector2(0.3f, 0.02f);
            cancelRt.anchorMax = new Vector2(0.7f, 0.1f);
            cancelRt.offsetMin = Vector2.zero; cancelRt.offsetMax = Vector2.zero;
            
            var cancelImg = cancelObj.AddComponent<Image>();
            cancelImg.color = new Color(0.8f, 0.2f, 0.2f);
            var cancelBtn = cancelObj.AddComponent<Button>();
            
            GameObject cTxtObj = new GameObject("Text");
            cTxtObj.transform.SetParent(cancelObj.transform, false);
            var cTxt = cTxtObj.AddComponent<Text>();
            cTxt.text = "キャンセル";
            cTxt.font = fontToUse;
            cTxt.fontSize = (int)KillingMahjong.Common.UITypography.Body;
            cTxt.color = Color.white;
            cTxt.alignment = TextAnchor.MiddleCenter;
            var cTxtRt = cTxtObj.GetComponent<RectTransform>();
            cTxtRt.anchorMin = Vector2.zero; cTxtRt.anchorMax = Vector2.one;
            cTxtRt.offsetMin = Vector2.zero; cTxtRt.offsetMax = Vector2.zero;

            cancelBtn.onClick.AddListener(() => {
                uiPanel.SetActive(false);
                onCanceledAction?.Invoke();
            });
        }
    }
}
