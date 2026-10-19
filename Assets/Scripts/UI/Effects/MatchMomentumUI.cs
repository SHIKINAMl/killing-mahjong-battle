using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

namespace KillingMahjong.UI.Effects
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
        [SerializeField, Tooltip("ロンした点に出す「ロン!」の吹き出し。子に Outline / Fill（Image）と Text（TMP）")]
        private GameObject ronCalloutPrefab;

        [Header("Settings")]
        [SerializeField] private float displayDuration = 2.0f;
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private Color playerColor = new Color(0.2f, 0.6f, 1f); // プレイヤー用（青系）
        [SerializeField] private Color enemyColor = new Color(1f, 0.3f, 0.3f);  // 敵用（赤系）

        private List<GameObject> spawnedUIElements = new List<GameObject>();
        private const float GrowthDuration = 1.5f;
        private const int GrowthPulseCount = 24;
        private readonly List<GraphStroke> strokes = new List<GraphStroke>();
        private readonly List<GraphMarker> markers = new List<GraphMarker>();
        public bool IsShowing { get; private set; }

        private struct GraphStroke
        {
            public RectTransform Rect;
            public float Length, Start, End;
        }

        private struct GraphMarker
        {
            public GameObject Object;
            public float Progress;
        }

        /// <summary>
        /// 履歴のどの点がロンだったか。流局や賭け金での増減と見分けるために、ロンの点にだけ付ける。
        /// Index は HP 履歴の添字（その点でロンの清算が済んでいる）。Rank は 満貫・跳満…役満。
        /// </summary>
        public struct RonMark
        {
            public int Index;
            public bool IsLocalWin;
            public string Rank;
        }

        // ロンの点では線の伸びをスローにして吹き出しを出す（2026-10-15 のユーザーの要望
        // 「ロン！したとき、その場所にロン！の吹き出しを出して少しだけ溜めが欲しい」。
        // 最初は止めていたが「止まるのではなく、少しスローに」と直された）。流局の点ではスローにしない。
        // RonSlowRange は進み具合（0〜1）で測った前後の幅、RonSlowSpeed は点の上での速さの倍率
        private const float RonSlowRange = 0.07f;
        private const float RonSlowSpeed = 0.18f;
        private const float RonPopSeconds = 0.16f;
        private readonly List<RonCallout> ronCallouts = new List<RonCallout>();
        private static Sprite burstSprite;

        private struct RonCallout
        {
            public RectTransform Rect;
            public float Progress;
            public float Scale;
        }

        /// <summary>
        /// 吹き出しの大きさをロンの強さで変える（2026-10-15 のユーザーの要望）。
        /// 本編のロンは満貫以上なので、満貫を基準の 1 にする。
        /// </summary>
        private static float RankScale(string rank)
        {
            switch (rank)
            {
                case "ダブル役満": return 1.9f;
                case "役満": return 1.7f;
                case "三倍満": return 1.45f;
                case "倍満": return 1.3f;
                case "跳満": return 1.15f;
                default: return 1f;
            }
        }

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
            if (!IsShowing) gameObject.SetActive(false);
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
        public void ShowMomentum(List<int> playerHpHistory, List<int> enemyHpHistory, IList<RonMark> ronMarks = null)
        {
            // シーン上で非アクティブに置かれていると Awake が未実行なので、ここで取得しておく
            EnsureCanvasGroup();
            StopAllCoroutines();
            canvasGroup.alpha = 0f;

            IsShowing = true;
            gameObject.SetActive(true);
            ClearGraph();

            StartCoroutine(DisplayRoutine(playerHpHistory, enemyHpHistory, ronMarks));
        }

        private IEnumerator DisplayRoutine(List<int> playerHpHistory, List<int> enemyHpHistory, IList<RonMark> ronMarks)
        {
            // 完成形を先に見せず、線の長さを0から始める。
            DrawGraph(playerHpHistory, enemyHpHistory, ronMarks);
            SetGraphProgress(0f);
            yield return StartCoroutine(FadeRoutine(1f));
            yield return StartCoroutine(GrowGraphRoutine());
            
            // テキスト演出の決定
            UpdateStatusText(playerHpHistory, enemyHpHistory);
            
            // 指定時間表示
            yield return new WaitForSeconds(displayDuration);
            
            // フェードアウト
            yield return StartCoroutine(FadeRoutine(0f));
            
            // グラフクリア & 非表示
            ClearGraph();
            IsShowing = false;
            gameObject.SetActive(false);
        }

        private IEnumerator GrowGraphRoutine()
        {
            if (strokes.Count == 0) yield break;
            ronCallouts.Sort((a, b) => a.Progress.CompareTo(b.Progress));
            int nextRon = 0;
            float progress = 0f;
            int lastPulse = -1;
            while (progress < 1f)
            {
                // ロンの点に近づくと線の伸びがスローになり、点を過ぎると元の速さへ戻る
                // （2026-10-15 のユーザーの指摘「止まるのではなく、ロンで少しスローになる感じに」）
                progress = Mathf.Min(1f, progress + Time.deltaTime / GrowthDuration * RonSpeedAt(progress));
                while (nextRon < ronCallouts.Count && progress >= ronCallouts[nextRon].Progress)
                    StartCoroutine(PopRonCallout(ronCallouts[nextRon++]));
                SetGraphProgress(progress);
                int pulse = Mathf.FloorToInt(progress * (GrowthPulseCount - 1));
                if (pulse != lastPulse)
                {
                    PlayGrowthPulse(pulse);
                    lastPulse = pulse;
                }
                yield return null;
            }
            // 吹き出しが弾け終わるまでは待つ（決着のロンは最後の点なので、伸び終わりと同時に出る）
            yield return new WaitForSeconds(RonPopSeconds);
        }

        /// <summary>
        /// 線の伸びる速さの倍率。ロンの点の前後 RonSlowRange の間だけ下げ、点の上で RonSlowSpeed になる。
        /// </summary>
        private float RonSpeedAt(float progress)
        {
            float speed = 1f;
            foreach (var callout in ronCallouts)
            {
                float distance = Mathf.Abs(progress - callout.Progress) / RonSlowRange;
                if (distance >= 1f) continue;
                float slow = Mathf.Lerp(RonSlowSpeed, 1f, Mathf.SmoothStep(0f, 1f, distance));
                speed = Mathf.Min(speed, slow);
            }
            return speed;
        }

        /// <summary>小さく出て一度ふくらみ、ロンの強さの大きさに落ち着く。</summary>
        private IEnumerator PopRonCallout(RonCallout callout)
        {
            if (callout.Rect == null) yield break;
            callout.Rect.gameObject.SetActive(true);
            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio != null)
            {
                // 強いロンほど低く太い音にする（ダブル役満で約1オクターブ下）。
                // 音は Freesound（CC0）のポップ音2つを重ねたもの（2026-10-19）
                audio.PlayStingerPitched("se_ron_pop", 1f / callout.Scale,
                    KillingMahjong.Managers.AudioManager.PitchJitter.None);
            }
            float t = 0f;
            while (t < RonPopSeconds && callout.Rect != null)
            {
                float k = t / RonPopSeconds;
                // 0 → 1.25倍 → 1倍
                float s = k < 0.6f ? Mathf.Lerp(0.3f, 1.25f, k / 0.6f) : Mathf.Lerp(1.25f, 1f, (k - 0.6f) / 0.4f);
                callout.Rect.localScale = Vector3.one * (s * callout.Scale);
                yield return null;
                t += Time.deltaTime;
            }
            if (callout.Rect != null) callout.Rect.localScale = Vector3.one * callout.Scale;
        }

        private static void PlayGrowthPulse(int pulse)
        {
            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio == null) return;
            float frequency = 220f * Mathf.Pow(2f, pulse / 12f);
            audio.PlaySynthSound(KillingMahjong.Managers.SynthWaveType.Triangle,
                frequency, frequency * 0.9f, 0.045f, 0.9f);
        }

        private void SetGraphProgress(float progress)
        {
            foreach (var stroke in strokes)
            {
                float amount = Mathf.InverseLerp(stroke.Start, stroke.End, progress);
                stroke.Rect.sizeDelta = new Vector2(stroke.Length * amount, 5f);
            }
            foreach (var marker in markers)
                marker.Object.SetActive(progress >= marker.Progress);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            IsShowing = false;
            ClearGraph();
        }

        private void DrawGraph(List<int> pHistory, List<int> eHistory, IList<RonMark> ronMarks)
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
            DrawLines(pPoints, playerColor, maxPoints);
            DrawLines(ePoints, enemyColor, maxPoints);
            
            // 交差（逆転）箇所のチェックとアイコン配置
            CheckIntersections(pHistory, eHistory, pPoints, ePoints);

            PlaceRonCallouts(ronMarks, pPoints, ePoints, maxPoints, height);
        }

        /// <summary>
        /// ロンの点ごとに吹き出しを置く（伸びる線が届くまで隠しておく）。
        /// 置くのはロンした側の線の上。枠の色はその側の線の色。
        /// </summary>
        private void PlaceRonCallouts(IList<RonMark> ronMarks, Vector2[] pPoints, Vector2[] ePoints, int maxPoints, float height)
        {
            if (ronMarks == null || ronCalloutPrefab == null || maxPoints < 2) return;
            if (burstSprite == null)
            {
                var texture = RonImpactEffect.CreateBurstTexture();
                burstSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
            }

            foreach (var mark in ronMarks)
            {
                var points = mark.IsLocalWin ? pPoints : ePoints;
                if (mark.Index < 1 || mark.Index >= points.Length) continue;

                GameObject go = Instantiate(ronCalloutPrefab, graphContainer);
                spawnedUIElements.Add(go);
                var rt = go.GetComponent<RectTransform>();
                float scale = RankScale(mark.Rank);

                // 点の少し上に出す。上に収まらないときは下に出す
                float lift = rt.rect.height * 0.5f * scale + 10f;
                Vector2 at = points[mark.Index];
                bool fitsAbove = at.y + lift + rt.rect.height * 0.5f * scale <= height * 0.5f + 20f;
                at.y += fitsAbove ? lift : -lift;
                rt.anchoredPosition = at;

                Color lineColor = mark.IsLocalWin ? playerColor : enemyColor;
                foreach (var image in go.GetComponentsInChildren<Image>(true))
                {
                    image.sprite = burstSprite;
                    if (image.name == "Outline") image.color = Color.Lerp(lineColor, Color.black, 0.45f);
                }

                go.SetActive(false);
                ronCallouts.Add(new RonCallout { Rect = rt, Scale = scale,
                    Progress = (float)mark.Index / (maxPoints - 1) });
            }
        }
        
        private void DrawLines(Vector2[] points, Color color, int maxPoints)
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
                
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = p1;
                rt.sizeDelta = new Vector2(0f, 5f);
                rt.localRotation = Quaternion.Euler(0, 0, angle);
                strokes.Add(new GraphStroke { Rect = rt, Length = dist,
                    Start = (float)i / (maxPoints - 1), End = (float)(i + 1) / (maxPoints - 1) });
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
                    markers.Add(new GraphMarker { Object = cross,
                        Progress = (float)i / (Mathf.Max(pHistory.Count, eHistory.Count) - 1) });
                    cross.SetActive(false);
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
            strokes.Clear();
            markers.Clear();
            ronCallouts.Clear();
            if (statusText != null) statusText.text = "";
        }
    }
}
