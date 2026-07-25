using UnityEngine;
using System.Collections.Generic;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    public class HandBaseUI : MonoBehaviour
    {
        [Header("Hand Slots")]
        [SerializeField] protected Transform handSlotContainer; // For Dealing / Hand Selection Phase
        [SerializeField] protected Transform discardPhaseContainer; // For Discard Phase (New 2-row layout)
        
        protected List<RectTransform> handSlots = new List<RectTransform>();
        public List<RectTransform> GetHandSlots() 
        {
            if (handSlots == null) handSlots = new List<RectTransform>();
            return handSlots;
        }

        public RectTransform GetTileSlotRectTransform(int tileId)
        {
            if (handSlots == null) return null;
            foreach (var slot in handSlots)
            {
                var interaction = slot.GetComponent<TileInteraction>();
                if (interaction != null && interaction.TileId == tileId)
                {
                    return slot;
                }
            }
            return null;
        }

        [SerializeField] protected TileResourceManager tileResourceManager;

        [Header("Layout Settings")]
        [SerializeField] protected float tileSpacingX = 50f;
        [SerializeField] protected float tileSpacingY = 70f;
        
        protected GameUIManager gameUIManager;

        public virtual void Setup(GameUIManager manager)
        {
            this.gameUIManager = manager;
        }

        public virtual void AddTileToHand(RectTransform tileTransform, int tileId)
        {
            if (tileTransform == null) return;
            
            handSlots.Add(tileTransform);

            tileTransform.SetParent(handSlotContainer, false);
            tileTransform.SetAsLastSibling();
            tileTransform.localPosition = Vector3.zero;
            
            RectTransform rt = tileTransform.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition3D = Vector3.zero;
            }
            
            tileTransform.localRotation = Quaternion.identity;
            tileTransform.localScale = Vector3.one;
            tileTransform.gameObject.SetActive(true);

            var interaction = tileTransform.GetComponent<TileInteraction>();
            if (interaction != null && gameUIManager != null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                interaction.Initialize(tileId, true, gameUIManager, canvas);
            }

            var visual = tileTransform.GetComponent<TileVisual>();
            if (visual != null)
            {
                visual.SetFuritenHighlight(false);
            }

            if (gameUIManager != null)
            {
                UpdateLayout(gameUIManager.CurrentPhaseStatus);
                if (KillingMahjong.Managers.AudioManager.Instance != null)
                {
                    KillingMahjong.Managers.AudioManager.Instance.PlayDrawTileSE();
                }
            }
            else
            {
                UpdateLayout(RoundStatus.None); // Fallback
            }
        }

        public virtual void RemoveTileFromHand(RectTransform tileTransform, int tileId)
        {
            if (handSlots.Contains(tileTransform))
            {
                handSlots.Remove(tileTransform);
                var interaction = tileTransform.GetComponent<TileInteraction>();
                if (interaction != null && gameUIManager != null)
                {
                    Canvas canvas = GetComponentInParent<Canvas>();
                    interaction.Initialize(tileId, false, gameUIManager, canvas);
                }
                
                // 牌が抜けた後に、残りの手牌のレイアウト（目標座標）を更新して詰めるアニメーションを発動
                if (gameUIManager != null)
                {
                    UpdateLayout(gameUIManager.CurrentPhaseStatus);
                }
                else
                {
                    UpdateLayout(RoundStatus.None);
                }
            }
        }


        private Dictionary<RectTransform, Vector2> targetPositions = new Dictionary<RectTransform, Vector2>();
        [SerializeField] protected float animationSpeed = 15f; // 追従スピード

        protected virtual void Update()
        {
            // 毎フレーム、目標座標に向けて滑らかに追従移動する
            foreach (var rt in handSlots)
            {
                if (rt == null) continue;
                if (targetPositions.TryGetValue(rt, out Vector2 targetPos))
                {
                    rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, targetPos, Time.deltaTime * animationSpeed);
                }
            }
        }

        public virtual void SortHandSlots()
        {
        }

        public virtual void UpdateLayout(RoundStatus phaseStatus)
        {
            if (handSlotContainer == null) return;

            bool isGameEndPhase = phaseStatus == RoundStatus.Agari || 
                                  phaseStatus == RoundStatus.Ron || 
                                  phaseStatus == RoundStatus.Result || 
                                  phaseStatus == RoundStatus.Draw;

            // Bettingフェーズではコンテナ切り替え／レイアウト更新を行わない。
            // どの呼び出し元経由でも牌が消えるバグの原因となるため、発生源で一括ガードする。
            if (phaseStatus == RoundStatus.Betting)
            {
                return;
            }

            bool isBoardActivePhase = phaseStatus == RoundStatus.Discard || isGameEndPhase;

            var layoutGroup = handSlotContainer.GetComponent<UnityEngine.UI.LayoutGroup>();
            Transform activeContainer = (isBoardActivePhase && discardPhaseContainer != null) 
                                        ? discardPhaseContainer : handSlotContainer;
            
            if (discardPhaseContainer != null)
            {
                discardPhaseContainer.gameObject.SetActive(activeContainer == discardPhaseContainer);
            }
            if (handSlotContainer != null)
            {
                handSlotContainer.gameObject.SetActive(activeContainer == handSlotContainer);
            }

            // LayoutGroupは完全に使用しない（スクリプトでの座標管理に移行）
            if (layoutGroup != null) layoutGroup.enabled = false;

            foreach (var st in handSlots)
            {
                if (st.parent != activeContainer)
                {
                    st.SetParent(activeContainer, false);
                }
                st.localPosition = new Vector3(st.localPosition.x, st.localPosition.y, 0f);
                st.localScale = Vector3.one;
                st.localRotation = Quaternion.identity;

                // 初めて登録される牌なら、初期位置は現在の位置にするか、山牌側にする等の工夫ができるが
                // ここではとりあえず現在位置を維持しつつ、後でUpdateで引っ張る
                if (!targetPositions.ContainsKey(st))
                {
                    targetPositions[st] = st.anchoredPosition;
                }
            }

            // ソート処理（同じルール）
            handSlots.Sort((a, b) => 
            {
                var intA = a.GetComponent<TileInteraction>();
                var intB = b.GetComponent<TileInteraction>();
                if (intA == null || intB == null) return 0;

                int baseA = intA.TileId & 0x1F;
                int baseB = intB.TileId & 0x1F;
                if (baseA != baseB) return baseA.CompareTo(baseB);
                
                return intA.TileId.CompareTo(intB.TileId);
            });

            if (handSlots.Count == 0) return;

            if (!isBoardActivePhase)
            {
                // 通常フェイズ（横一列）の座標計算
                float totalWidth = (handSlots.Count - 1) * tileSpacingX;
                // コンテナ内で中央寄せ等したい場合は調整するが、ここでは左寄せ(x=0から)とする
                for (int i = 0; i < handSlots.Count; i++)
                {
                    RectTransform rt = handSlots[i];
                    rt.anchorMin = new Vector2(0, 0.5f);
                    rt.anchorMax = new Vector2(0, 0.5f);
                    rt.pivot = new Vector2(0, 0.5f);
                    
                    Vector2 targetPos = new Vector2(i * tileSpacingX, 0);
                    targetPositions[rt] = targetPos;
                    
                    rt.SetSiblingIndex(i);
                }
            }
            else
            {
                // Discard Phase Layout（2段）
                List<RectTransform> row0 = new List<RectTransform>();
                List<RectTransform> row1 = new List<RectTransform>();

                for (int i = 0; i < handSlots.Count; i++)
                {
                    int rowIndex = i / 7; 
                    int colIndex = i % 7; 

                    float w = tileSpacingX;
                    float h = tileSpacingY;

                    float targetY = -rowIndex * h;
                    float targetX = colIndex * w;

                    RectTransform rt = handSlots[i];
                    rt.anchorMin = new Vector2(0, 1);
                    rt.anchorMax = new Vector2(0, 1);
                    rt.pivot = new Vector2(0, 1);
                    
                    Vector2 targetPos = new Vector2(targetX, targetY);
                    targetPositions[rt] = targetPos;

                    if (rowIndex == 0) row0.Add(rt);
                    else row1.Add(rt);
                }

                // SiblingIndex設定
                int sibIdx = 0;
                for (int i = 0; i < row0.Count; i++) row0[i].SetSiblingIndex(sibIdx++);
                for (int i = 0; i < row1.Count; i++) row1[i].SetSiblingIndex(sibIdx++);
            }
        }
    }
}
