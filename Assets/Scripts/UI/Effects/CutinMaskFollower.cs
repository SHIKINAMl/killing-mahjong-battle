using UnityEngine;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 帯の絵（<see cref="CutinPixelBackgroundUI"/>）と同じ場所・向き・大きさに、毎フレーム重なり続ける（2026-10-21）。
    ///
    /// 相手のスキルのカットインで、立ち絵を「帯の絵の形」で切り抜く型に使う。型を帯の子にすると、
    /// 帯のすぐ上に描く血しぶきが立ち絵の上に乗ってしまうので、型は帯の外に置いてこれで追わせる。
    /// 帯の絵は自分の LateUpdate で位置を決めるので、こちらはその後に動く。
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class CutinMaskFollower : MonoBehaviour
    {
        private RectTransform target;
        private RectTransform self;

        public void Follow(RectTransform picture)
        {
            target = picture;
            self = (RectTransform)transform;
            self.anchorMin = self.anchorMax = self.pivot = new Vector2(.5f, .5f);
            self.sizeDelta = picture.sizeDelta;
            self.localScale = Vector3.one;
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (target == null || self == null) return;
            self.position = target.position;
            self.rotation = target.rotation;
        }
    }
}
