using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

namespace KillingMahjongBattle.UI.Effects
{
    [RequireComponent(typeof(CanvasGroup))]
    public class MatchMomentumUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField, Tooltip("グラフを描画するコンテナ")] 
        private RectTransform graphContainer;
        [SerializeField, Tooltip("演出テキスト（例: 圧倒的優勢）")] 
        private TextMeshProUGUI statusText;
        private CanvasGroup canvasGroup;
        
        [Header("Prefabs & Assets")]
        [SerializeField, Tooltip("線を表現する細長いImageプレハブ")] 
        private GameObject lineSegmentPrefab; 
        [SerializeField, Tooltip("交差（逆転）時の★マークプレハブ")] 
        private GameObject crossPointPrefab;  
        
        [Header("Settings")]
        [SerializeField] private float displayDuration = 2.0f;
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private Color playerColor = new Color(0.2f, 0.6f, 1f); // プレイヤー用（青系）
        [SerializeField] private Color enemyColor = new Color(1f, 0.3f, 0.3f);  // 敵用（赤系）

        private List<GameObject> spawnedUIElements = new List<GameObject>();

        private void Awake()
        {
            EnsureCanvasGroup();
            canvasGroup.alpha = 0f;
        }

        private void Start()
        {
            // 初期表示を隠すのは Awake ではなく Start で行う。
            // Awake で SetActive(false) すると、シーン上で非アクティブに置かれていた場合に
            // ShowMomentum() の SetActive(true) が Awake を誘発し、その場で自分を非アクティブへ
            // 戻してしまうため、直後の StartCoroutine が "game object is inactive" で失敗する。
            gameObject.SetActive(false);
        }

        private void EnsureCanvasGroup()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

            // 戦況グラフは見せるだけの演出なので、裏のUIの入力を奪わないようにする
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        /// <summary>
        /// HPの推移を受け取り、モメンタムグラフを描画・表示する
        /// </summary>
        public void ShowMomentum(List<int> playerHpHistory, List<int> enemyHpHistory)
        {
            // シーン上で非アクティブに置かれていると Awake が未実行なので、ここで取得しておく
            EnsureCanvasGroup();
            canvasGroup.alpha = 0f;

            gameObject.SetActive(true);
            ClearGraph();

            StartCoroutine(DisplayRoutine(playerHpHistory, enemyHpHistory));
        }

        private IEnumerator DisplayRoutine(List<int> playerHpHistory, List<int> enemyHpHistory)
        {
            // フェードイン
            yield return StartCoroutine(FadeRoutine(1f));
            
            // グラフの描画
            DrawGraph(playerHpHistory, enemyHpHistory);
            
            // テキスト演出の決定
            UpdateStatusText(playerHpHistory, enemyHpHistory);
            
            // 指定時間表示
            yield return new WaitForSeconds(displayDuration);
            
            // フェードアウト
            yield return StartCoroutine(FadeRoutine(0f));
            
            // グラフクリア & 非表示
            ClearGraph();
            gameObject.SetActive(false);
        }

        private void DrawGraph(List<int> pHistory, List<int> eHistory)
        {
            if (pHistory == null || eHistory == null || pHistory.Count == 0 || eHistory.Count == 0 || graphContainer == null) return;

            // 非アクティブ状態から出した直後はレイアウトが未確定なことがあるので確定させてから測る
            Canvas.ForceUpdateCanvases();

            float width = graphContainer.rect.width;
            float height = graphContainer.rect.height;

            if (width <= 0f || height <= 0f)
            {
                Debug.LogWarning($"[MatchMomentumUI] {graphContainer.name} のサイズが 0 です。" +
                                 "Layout Group / Content Size Fitter を外して固定サイズにしてください。");
                return;
            }
            int maxPoints = Mathf.Max(pHistory.Count, eHistory.Count);
            float xStep = maxPoints > 1 ? width / (maxPoints - 1) : width;
            
            // スケール計算用の最大HPを検索
            int maxHp = 1;
            foreach (int hp in pHistory) if (hp > maxHp) maxHp = hp;
            foreach (int hp in eHistory) if (hp > maxHp) maxHp = hp;

            Vector2[] pPoints = new Vector2[pHistory.Count];
            Vector2[] ePoints = new Vector2[eHistory.Count];
            
            // 座標計算
            for (int i = 0; i < pHistory.Count; i++)
            {
                float x = (i * xStep) - (width / 2f); // アンカー中央を想定
                float y = (((float)pHistory[i] / maxHp) * height) - (height / 2f);
                pPoints[i] = new Vector2(x, y);
            }
            for (int i = 0; i < eHistory.Count; i++)
            {
                float x = (i * xStep) - (width / 2f);
                float y = (((float)eHistory[i] / maxHp) * height) - (height / 2f);
                ePoints[i] = new Vector2(x, y);
            }
            
            // 線の描画
            DrawLines(pPoints, playerColor);
            DrawLines(ePoints, enemyColor);
            
            // 交差（逆転）箇所のチェックとアイコン配置
            CheckIntersections(pHistory, eHistory, pPoints, ePoints);
        }
        
        private void DrawLines(Vector2[] points, Color color)
        {
            if (lineSegmentPrefab == null) return;

            for (int i = 0; i < points.Length - 1; i++)
            {
                GameObject segment = Instantiate(lineSegmentPrefab, graphContainer);
                spawnedUIElements.Add(segment);
                
                RectTransform rt = segment.GetComponent<RectTransform>();
                Image img = segment.GetComponent<Image>();
                if (img != null) img.color = color;
                
                Vector2 p1 = points[i];
                Vector2 p2 = points[i + 1];
                Vector2 dir = p2 - p1;
                float dist = dir.magnitude;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                
                rt.anchoredPosition = p1 + dir / 2;
                rt.sizeDelta = new Vector2(dist, 5f); // 線の太さ(5f)
                rt.localRotation = Quaternion.Euler(0, 0, angle);
            }
        }
        
        private void CheckIntersections(List<int> pHistory, List<int> eHistory, Vector2[] pPoints, Vector2[] ePoints)
        {
            if (crossPointPrefab == null) return;

            int minLen = Mathf.Min(pHistory.Count, eHistory.Count);
            for (int i = 1; i < minLen; i++)
            {
                bool playerWasHigher = pHistory[i-1] > eHistory[i-1];
                bool playerIsHigher = pHistory[i] > eHistory[i];
                
                // 上下が入れ替わっている、もしくは同値になったタイミングを交差（逆転）とする
                if ((playerWasHigher != playerIsHigher && pHistory[i] != eHistory[i]) || 
                    (pHistory[i-1] != eHistory[i-1] && pHistory[i] == eHistory[i]))
                {
                    GameObject cross = Instantiate(crossPointPrefab, graphContainer);
                    spawnedUIElements.Add(cross);
                    RectTransform rt = cross.GetComponent<RectTransform>();
                    
                    // 中間地点にアイコンを配置
                    Vector2 midPoint = (pPoints[i] + ePoints[i] + pPoints[i-1] + ePoints[i-1]) / 4f;
                    rt.anchoredPosition = midPoint;
                }
            }
        }

        private void UpdateStatusText(List<int> pHistory, List<int> eHistory)
        {
            if (statusText == null || pHistory.Count == 0 || eHistory.Count == 0) return;
            
            int lastP = pHistory[pHistory.Count - 1];
            int lastE = eHistory[eHistory.Count - 1];
            
            if (lastP > lastE * 2)
            {
                statusText.text = "圧倒的優勢";
                statusText.color = new Color(1f, 0.9f, 0.2f); // 黄色系
            }
            else if (lastP < lastE / 2)
            {
                statusText.text = "絶体絶命";
                statusText.color = new Color(1f, 0.3f, 0.3f); // 赤系
            }
            else if (lastP > lastE)
            {
                statusText.text = "優勢";
                statusText.color = Color.white;
            }
            else if (lastP < lastE)
            {
                statusText.text = "劣勢";
                statusText.color = Color.gray;
            }
            else
            {
                statusText.text = "互角";
                statusText.color = Color.white;
            }
        }

        private IEnumerator FadeRoutine(float targetAlpha)
        {
            if (canvasGroup == null) yield break;
            
            float startAlpha = canvasGroup.alpha;
            float time = 0;
            while (time < fadeDuration)
            {
                time += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / fadeDuration);
                yield return null;
            }
            canvasGroup.alpha = targetAlpha;
        }
        
        private void ClearGraph()
        {
            foreach (var element in spawnedUIElements)
            {
                if (element != null) Destroy(element);
            }
            spawnedUIElements.Clear();
            if (statusText != null) statusText.text = "";
        }
    }
}
