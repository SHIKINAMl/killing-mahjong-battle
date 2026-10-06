using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    public sealed partial class CollectionUI
    {
        private void BuildUnusedPage(Transform parent)
        {
            Label(parent, "UnusedHeading", "赤い目の敗北演出", new Vector2(0f, 135f),
                new Vector2(600f, 36f), 24f, TextAlignmentOptions.Center, TextMain);
            var thumb = NewEmpty(parent, "RedDefeatThumbnail").AddComponent<RawImage>();
            Center(thumb.rectTransform, new Vector2(320f, 240f));
            thumb.rectTransform.anchoredPosition = new Vector2(-155f, -28f);
            thumb.texture = Resources.Load<Texture2D>("UnusedEndings/RedDefeat/open");
            thumb.raycastTarget = false;
            Label(parent, "UnusedDescription", "没案\n\n赤い目が開く\n笑い声 → 傾き → 暗転\n\n終了後、タイトルに戻ります",
                new Vector2(170f, 10f), new Vector2(290f, 180f), 19f, TextAlignmentOptions.Left, TextMain);
            Label(parent, "LaughPending", "笑い声は後日追加", new Vector2(170f, -98f),
                new Vector2(290f, 28f), 17f, TextAlignmentOptions.Left, TextDim);
            Button(parent, "PlayRedDefeat", "再生", new Vector2(170f, -155f), new Vector2(180f, 40f), 21f,
                () => { StopPreview(); RedDefeatPrototypeUI.Play(); });
        }
    }
}
