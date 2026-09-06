using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    public partial class PlayerInfoUI
    {
        /// <summary>「ばー」の正体。黒い横線が3本だけ描かれた覆い。</summary>
        private const string CoverSpriteName = "HPUCOVER";

        /// <summary>袋を血より手前に描くために作る複製の名前。</summary>
        private const string BagFrontName = "袋（手前）";

        /// <summary>
        /// 自分の体力表示の重なりを直す。ユーザーの指示（2026-09-06）。
        ///
        /// 1. **血を袋より奥に描く。** uGUI は「親の絵 → 子の絵」の順に描くので、
        ///    血（`hpFillImage`）が袋（親「メーター」の Image）の子である限り、
        ///    血が袋の枠に必ずかぶる。そこで**袋の絵を、血より後ろの兄弟として
        ///    作り直し**、親側の Image は消す。血の RectTransform は触らないので、
        ///    位置も伸縮もこれまでどおり。
        /// 2. **黒い横線3本の覆い（HPUCOVER）を出さない。** 不要になったため。
        ///
        /// **シーンではなく実行時に組み替える。** 対局シーンが2つ（UIテストシーン /
        /// OpeningScene）あり、シーンを直すと片方だけ直る事故が起きるため（AGENTS.md §2）。
        /// 戻すときは <c>Awake</c> からこの呼び出しを外すだけでよい。
        /// </summary>
        private void FixHpMeterLayering()
        {
            if (hpFillImage == null) return;

            Transform meter = hpFillImage.transform.parent;
            if (meter == null) return;

            HideCoverBars(meter);
            MoveBagBehindBlood(meter);
        }

        /// <summary>黒い横線3本の覆いを見えなくする。</summary>
        private static void HideCoverBars(Transform meter)
        {
            // 名前ではなく絵で見分ける。名前（「ばー」）は付け替えられうるが、
            // 消したいのは HPUCOVER が貼られたものだから。
            for (int i = 0; i < meter.childCount; i++)
            {
                var image = meter.GetChild(i).GetComponent<Image>();
                if (image == null || image.sprite == null) continue;
                if (image.sprite.name == CoverSpriteName)
                {
                    image.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>袋の絵を、血より後ろの兄弟として作り直す。</summary>
        private void MoveBagBehindBlood(Transform meter)
        {
            var bag = meter.GetComponent<Image>();
            if (bag == null || bag.sprite == null) return;

            // 二度作らない。OnEnable などで複数回呼ばれても増えないようにする。
            if (meter.Find(BagFrontName) != null) return;

            var front = new GameObject(BagFrontName, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)front.transform;
            rect.SetParent(meter, worldPositionStays: false);

            // 親いっぱいに広げる。親の Image が描いていた範囲をそのまま引き継ぐ。
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            var frontImage = front.GetComponent<Image>();
            frontImage.sprite = bag.sprite;
            frontImage.type = bag.type;
            frontImage.color = bag.color;
            frontImage.material = bag.material;
            frontImage.preserveAspect = bag.preserveAspect;
            frontImage.fillCenter = bag.fillCenter;
            // 牌のクリック判定を吸わないようにする（PlayerInfoUI の他の要素と同じ扱い）。
            frontImage.raycastTarget = false;

            // 血より後ろ＝最後に描く。血は兄弟の先頭にいる想定だが、
            // 位置を決め打ちにせず末尾に置くことで並びが変わっても手前に来る。
            rect.SetAsLastSibling();

            // 親の絵は消す。残すと血の奥にも袋が描かれて、枠が二重に見える。
            bag.enabled = false;
        }
    }
}
