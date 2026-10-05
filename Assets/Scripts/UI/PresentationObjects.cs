using UnityEngine;

namespace KillingMahjong.UI
{
    internal static class PresentationObjects
    {
        internal static GameObject Own(this PresentationScope scope, GameObject target)
        {
            scope.AddCleanup(() => {
                if (target == null) return;
                // Destroyはフレーム末尾なので、入力遮断と見た目は直ちに消す。
                target.SetActive(false);
                Object.Destroy(target);
            });
            return target;
        }
    }
}
