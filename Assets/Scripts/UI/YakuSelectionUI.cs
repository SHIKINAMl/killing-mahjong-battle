using UnityEngine;
using UnityEngine.UI;
using System;

namespace KillingMahjong.UI
{
    public class YakuSelectionUI : MonoBehaviour
    {
        private GameObject uiPanel;

        public void Show(Action<string> onSelected, Action onCanceled)
        {
            if (uiPanel == null)
            {
                CreateUI();
            }
            
            // Re-assign listeners each time we show it (to avoid memory leaks / multiple fires)
            // But actually we create the buttons once and just store the callbacks in local fields.
            this.onSelectedAction = onSelected;
            this.onCanceledAction = onCanceled;

            uiPanel.SetActive(true);
        }

        private Action<string> onSelectedAction;
        private Action onCanceledAction;

        private void CreateUI()
        {
            // Background
            uiPanel = new GameObject("YakuSelectionPanel");
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                uiPanel.transform.SetParent(canvas.transform, false);
            }

            var bg = uiPanel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.8f);
            var rt = uiPanel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            // Make sure it catches clicks
            uiPanel.AddComponent<GraphicRaycaster>();
            var uiCanvas = uiPanel.AddComponent<Canvas>();
            uiCanvas.overrideSorting = true;
            uiCanvas.sortingOrder = 10000;

            // Container
            GameObject container = new GameObject("Container");
            container.transform.SetParent(uiPanel.transform, false);
            var containerRt = container.AddComponent<RectTransform>();
            containerRt.anchorMin = new Vector2(0.1f, 0.1f);
            containerRt.anchorMax = new Vector2(0.9f, 0.9f);
            containerRt.offsetMin = Vector2.zero; containerRt.offsetMax = Vector2.zero;
            var img = container.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(container.transform, false);
            var titleTxt = titleObj.AddComponent<Text>();
            titleTxt.text = "強化する役を選んでください";
            titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleTxt.fontSize = 40;
            titleTxt.color = Color.white;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            var titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 0.85f); titleRt.anchorMax = new Vector2(1, 1);
            titleRt.offsetMin = Vector2.zero; titleRt.offsetMax = Vector2.zero;

            // Grid Container
            GameObject gridObj = new GameObject("Grid");
            gridObj.transform.SetParent(container.transform, false);
            var gridRt = gridObj.AddComponent<RectTransform>();
            gridRt.anchorMin = new Vector2(0.05f, 0.1f);
            gridRt.anchorMax = new Vector2(0.95f, 0.85f);
            gridRt.offsetMin = Vector2.zero; gridRt.offsetMax = Vector2.zero;

            var grid = gridObj.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(200, 60);
            grid.spacing = new Vector2(20, 20);
            grid.padding = new RectOffset(20, 20, 20, 20);

            string[] yakus = { "立直", "断幺九", "平和", "役牌", "一盃口", "対々和", "三暗刻", "混一色", "清一色", "七対子" };
            Font arial = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            foreach (var y in yakus)
            {
                GameObject btnObj = new GameObject("Btn_" + y);
                btnObj.transform.SetParent(gridObj.transform, false);
                var btnImg = btnObj.AddComponent<Image>();
                btnImg.color = Color.white;
                var btn = btnObj.AddComponent<Button>();
                
                GameObject txtObj = new GameObject("Text");
                txtObj.transform.SetParent(btnObj.transform, false);
                var txt = txtObj.AddComponent<Text>();
                txt.text = y;
                txt.font = arial;
                txt.fontSize = 28;
                txt.color = Color.black;
                txt.alignment = TextAnchor.MiddleCenter;
                var txtRt = txtObj.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;

                string yakuName = y;
                btn.onClick.AddListener(() => {
                    uiPanel.SetActive(false);
                    onSelectedAction?.Invoke(yakuName);
                });
            }

            // Cancel Button
            GameObject cancelObj = new GameObject("Btn_Cancel");
            cancelObj.transform.SetParent(gridObj.transform, false);
            var cancelImg = cancelObj.AddComponent<Image>();
            cancelImg.color = new Color(0.8f, 0.2f, 0.2f);
            var cancelBtn = cancelObj.AddComponent<Button>();
            
            GameObject cTxtObj = new GameObject("Text");
            cTxtObj.transform.SetParent(cancelObj.transform, false);
            var cTxt = cTxtObj.AddComponent<Text>();
            cTxt.text = "キャンセル";
            cTxt.font = arial;
            cTxt.fontSize = 28;
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
