using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>ロンの対象の背面だけで再生する、透過ドット絵の靄。</summary>
    public sealed class RonAuraUI : MonoBehaviour
    {
        public const string EnemyEffectId = "ron.aura_enemy";
        public const string LocalEffectId = "ron.aura_local";
        private const int Columns = 4, Rows = 5, FrameCount = 20;
        private RawImage image;
        private RectTransform target;
        private bool followCharacter;
        private float elapsed;

        public static RonAuraUI BehindCharacter(RectTransform character)
        {
            var aura = Create(character.parent, true);
            if (aura == null) return null;
            aura.target = character;
            aura.followCharacter = true;
            aura.transform.SetSiblingIndex(character.GetSiblingIndex());
            aura.SyncCharacter();
            return aura;
        }

        public static RonAuraUI BehindCallout(RectTransform callout)
        {
            var aura = Create(callout, false);
            if (aura == null) return null;
            var rt = (RectTransform)aura.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(480f, 384f);
            rt.SetAsFirstSibling();
            return aura;
        }

        private static RonAuraUI Create(Transform parent, bool enemy)
        {
            var texture = Resources.Load<Texture2D>(enemy ? "UI/RonAura/enemy_red" : "UI/RonAura/local_blue");
            if (texture == null)
            {
                Debug.LogError("[RonAuraUI] Ron aura texture is missing.");
                return null;
            }
            var go = new GameObject(enemy ? "EnemyRonAura" : "LocalRonAura", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var aura = go.AddComponent<RonAuraUI>();
            aura.image = go.GetComponent<RawImage>();
            aura.image.texture = texture;
            aura.image.raycastTarget = false;
            aura.SetFrame(0);
            return aura;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            SetFrame(Mathf.FloorToInt(PresentationTiming.Sample(elapsed) / .065f) % FrameCount);
            // 親のカットインや吹き出しと同時に終わる。独自の待機時間は増やさない。
            image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(PresentationTiming.Sample(elapsed) / .10f));
        }

        private void SetFrame(int frame)
        {
            image.uvRect = new Rect((frame % Columns) / (float)Columns,
                (Rows - 1 - frame / Columns) / (float)Rows, 1f / Columns, 1f / Rows);
        }

        private void LateUpdate()
        {
            if (followCharacter && target != null) SyncCharacter();
        }

        private void SyncCharacter()
        {
            var rt = (RectTransform)transform;
            rt.anchorMin = target.anchorMin;
            rt.anchorMax = target.anchorMax;
            rt.pivot = target.pivot;
            rt.sizeDelta = target.sizeDelta * (240f / 270f);
            rt.localRotation = target.localRotation;
            rt.localScale = target.localScale;
            rt.anchoredPosition = target.anchoredPosition +
                (Vector2)(target.localRotation * new Vector3(0f, target.sizeDelta.y / 30f * target.localScale.y, 0f));
        }
    }
}
