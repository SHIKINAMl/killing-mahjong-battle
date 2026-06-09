using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace KillingMahjong.UI
{
    public class GameResultUI : MonoBehaviour
    {
        private GameObject uiPanel;

        public void Show(bool isWin)
        {
            if (uiPanel == null)
            {
                CreateUI(isWin);
            }
            uiPanel.SetActive(true);
        }

        private void CreateUI(bool isWin)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null) canvas = canvas.rootCanvas;

            uiPanel = new GameObject("GameResultPanel");
            if (canvas != null) uiPanel.transform.SetParent(canvas.transform, false);

            var rt = uiPanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var bg = uiPanel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.85f);

            GameObject textObj = new GameObject("ResultText");
            textObj.transform.SetParent(uiPanel.transform, false);
            var resultTxt = textObj.AddComponent<Text>();
            resultTxt.text = isWin ? "勝！" : "負け！";
            resultTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            resultTxt.fontSize = 120;
            resultTxt.color = isWin ? new Color(1f, 0.8f, 0.2f) : new Color(0.5f, 0.6f, 0.8f);
            resultTxt.alignment = TextAnchor.MiddleCenter;

            var txtRt = textObj.GetComponent<RectTransform>();
            txtRt.anchorMin = new Vector2(0, 0.4f); txtRt.anchorMax = new Vector2(1, 0.8f);
            txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;

            var shadow = textObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(4, -4);

            GameObject btnObj = new GameObject("Btn_Title");
            btnObj.transform.SetParent(uiPanel.transform, false);
            var btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.3f, 0.2f);
            btnRt.anchorMax = new Vector2(0.7f, 0.35f);
            btnRt.offsetMin = Vector2.zero; btnRt.offsetMax = Vector2.zero;

            var btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            var btn = btnObj.AddComponent<Button>();

            GameObject btnTxtObj = new GameObject("Text");
            btnTxtObj.transform.SetParent(btnObj.transform, false);
            var btnTxt = btnTxtObj.AddComponent<Text>();
            btnTxt.text = "タイトルに戻る";
            btnTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnTxt.fontSize = 40;
            btnTxt.color = Color.white;
            btnTxt.alignment = TextAnchor.MiddleCenter;
            
            var btnTxtRt = btnTxtObj.GetComponent<RectTransform>();
            btnTxtRt.anchorMin = Vector2.zero; btnTxtRt.anchorMax = Vector2.one;
            btnTxtRt.offsetMin = Vector2.zero; btnTxtRt.offsetMax = Vector2.zero;

            btn.onClick.AddListener(() => {
                Destroy(uiPanel);
                SceneManager.LoadScene("タイトルシーン");
            });
            
            var uiCanvas = uiPanel.AddComponent<Canvas>();
            uiCanvas.overrideSorting = true;
            uiCanvas.sortingOrder = 20000;
            uiPanel.AddComponent<GraphicRaycaster>();
        }
    }
}
