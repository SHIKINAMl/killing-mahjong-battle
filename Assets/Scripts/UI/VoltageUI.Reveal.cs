using System.Collections;
using UnityEngine;

namespace KillingMahjong.UI
{
    public partial class VoltageUI
    {
        public const string RevealEffectId = "voltage.reveal";
        private const float BlocksReadyAt = .36f;
        private const float IgnitionSoundAt = BlocksReadyAt + .3f;
        private const float FlamesStartAt = IgnitionSoundAt + .1f;
        private const float RevealSeconds = 2.05f;
        private CanvasGroup[] _revealPips, _revealHearts;
        private CanvasGroup _revealCharge, _revealMultiplier;
        private bool _revealActive;

        private static CanvasGroup RevealGroup(Component target)
        {
            if (target == null) return null;
            var group = target.GetComponent<CanvasGroup>();
            if (group == null) group = target.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        private void PrepareReveal()
        {
            _revealActive = true;
            _revealPips = new CanvasGroup[PipCount];
            _revealHearts = new CanvasGroup[PipCount];
            for (int i = 0; i < PipCount; i++)
            {
                _revealPips[i] = RevealGroup(_pips[i]);
                _revealHearts[i] = RevealGroup(_hearts[i]);
                if (_flames[i] != null) _flames[i].BeginIgnition();
            }
            _revealCharge = RevealGroup(_chargeRow);
            _revealMultiplier = RevealGroup(_multiplierText);
            ApplyReveal(0f);
        }

        internal void CancelRevealPresentation()
        {
            if (!_revealActive) return;
            _revealActive = false;
            ApplyReveal(RevealSeconds);
            Managers.AudioManager.Instance?.StopVoltageIgnitionSE();
        }

        private void ApplyReveal(float elapsed)
        {
            float fire = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - FlamesStartAt) / .95f));
            for (int i = 0; i < PipCount; i++)
            {
                if (_revealPips[i] != null)
                    _revealPips[i].alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - i * .045f) / .22f));
                if (_revealHearts[i] != null) _revealHearts[i].alpha = fire;
                if (_flames[i] != null) _flames[i].SetIgnitionProgress(fire);
            }
            float body = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / BlocksReadyAt));
            if (_revealCharge != null) _revealCharge.alpha = body;
            if (_revealMultiplier != null) _revealMultiplier.alpha = body;
        }

        /// <summary>実際のポイントは変えず、左右の表示だけを順に点火する。</summary>
        public static IEnumerator RevealRoutine(System.Action onPrepared = null)
        {
            EnsureCreated();
            var views = _canvasObject.GetComponentsInChildren<VoltageUI>(true);
            foreach (var view in views)
            {
                view.Refresh();
                view.PrepareReveal();
            }
            onPrepared?.Invoke();
            SetCanvasVisible(true);
            bool completed = false;
            try
            {
                yield return AnimateReveal(views);
                completed = _canvasObject != null && _canvasObject.activeSelf;
            }
            finally
            {
                foreach (var view in views) if (view != null) view.CancelRevealPresentation();
                Managers.AudioManager.Instance?.StopVoltageIgnitionSE();
                if (!completed) SetCanvasVisible(false);
            }
        }

        internal IEnumerator RevealPresentation()
        {
            // 呼び出したフレームで炎を隠す。最初のyieldまで既存の炎を見せない。
            PrepareReveal();
            return AnimateReveal(new[] { this });
        }

        private static IEnumerator AnimateReveal(VoltageUI[] views)
        {
            bool playedSound = false;
            var audio = Managers.AudioManager.Instance;
            try
            {
                float elapsed = 0f;
                while (elapsed < RevealSeconds)
                {
                    bool visible = false;
                    foreach (var view in views)
                    {
                        if (view == null || !view.gameObject.activeInHierarchy) continue;
                        visible = true;
                        view.ApplyReveal(elapsed);
                    }
                    if (!visible) yield break;
                    if (!playedSound && elapsed >= IgnitionSoundAt)
                    {
                        playedSound = true;
                        audio?.PlayVoltageIgnitionSE();
                    }
                    // 重い開始フレームでも、ブロックだけの時間と点火前の間を飛ばさない。
                    elapsed += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                    yield return null;
                }
            }
            finally
            {
                foreach (var view in views) if (view != null) view.CancelRevealPresentation();
                audio?.StopVoltageIgnitionSE();
            }
        }
    }
}
