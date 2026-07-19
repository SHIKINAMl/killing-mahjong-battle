using UnityEngine;

namespace KillingMahjong.UI
{
    public class TutorialArrowUI : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private float bobbingAmount = 20f; // 上下に揺れる幅
        [SerializeField] private float bobbingSpeed = 5f;   // 揺れる速度
        [SerializeField] private Vector2 offset = new Vector2(0, 100f); // 牌の中心からのオフセット（上に表示）

        private RectTransform myRectTransform;
        private RectTransform targetRectTransform;
        private Vector2 basePosition;

        private void Awake()
        {
            myRectTransform = GetComponent<RectTransform>();
            gameObject.SetActive(false);
        }

        public void ShowAt(RectTransform target)
        {
            targetRectTransform = target;
            gameObject.SetActive(true);
            UpdatePosition();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            targetRectTransform = null;
        }

        private void Update()
        {
            if (targetRectTransform == null) return;
            UpdatePosition();
            Animate();
        }

        private void UpdatePosition()
        {
            // ターゲット（牌のUI）のスクリーン座標やワールド座標を、矢印の親Canvas内のローカル座標に変換して追従させる。
            // ターゲットが同じCanvas内にある前提なら、直接position（ワールド座標）を合わせるのが簡単。
            basePosition = (Vector2)targetRectTransform.position + offset;
            
            // LayoutGroup内で並び替えが発生しても自動で追従する
        }

        private void Animate()
        {
            float yOffset = Mathf.Sin(Time.time * bobbingSpeed) * bobbingAmount;
            myRectTransform.position = new Vector3(basePosition.x, basePosition.y + yOffset, myRectTransform.position.z);
        }
    }
}
