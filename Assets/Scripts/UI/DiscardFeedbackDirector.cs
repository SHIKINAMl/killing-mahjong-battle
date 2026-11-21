using UnityEngine;
using UnityEngine.SceneManagement;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    /// <summary>公開されている打牌数だけで、卓の小反応と終盤の静けさを進める。入力や判定は持たない。</summary>
    public sealed class DiscardFeedbackDirector : MonoBehaviour
    {
        public const string EffectId = "discard.table_feedback";
        private int localCount, enemyCount;
        private float currentTension;
        private EnemyInfoUI enemy;
        private GameUIManager manager;
        private bool preview, active;

        public static void Notify(RiverUI river, bool isEnemy, int count)
        {
            var scene = river.gameObject.scene;
            DiscardFeedbackDirector director = null;
            foreach (var item in FindObjectsByType<DiscardFeedbackDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (item.gameObject.scene == scene) { director = item; break; }
            if (director == null)
            {
                var go = new GameObject("DiscardFeedbackDirector");
                SceneManager.MoveGameObjectToScene(go, scene);
                director = go.AddComponent<DiscardFeedbackDirector>();
                foreach (var ui in FindObjectsByType<GameUIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (ui.gameObject.scene == scene) { director.manager = ui; break; }
                foreach (var ui in FindObjectsByType<EnemyInfoUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (ui.gameObject.scene == scene) { director.enemy = ui; break; }
            }
            if (director.manager != null && director.manager.CurrentPhaseStatus != EngineData.RoundStatus.Discard) return;
            director.Present(isEnemy, count);
        }

        public void ConfigurePreview(EnemyInfoUI target) { preview = true; enemy = target; }

        public void Present(bool isEnemy, int publicDiscardCount)
        {
            if (isEnemy) enemyCount = publicDiscardCount; else localCount = publicDiscardCount;
            active = true;
            enemy?.PlayDiscardAttention(Mathf.Max(localCount, enemyCount) >= 14);
        }

        private void Update()
        {
            if (!active) return;
            if (!preview && manager != null && manager.CurrentPhaseStatus != EngineData.RoundStatus.Discard)
            { ResetPresentation(); return; }
            float target = Mathf.InverseLerp(13f, 17f, Mathf.Max(localCount, enemyCount));
            currentTension = Mathf.MoveTowards(currentTension, target, Time.unscaledDeltaTime * .8f);
            AudioManager.Instance?.SetDiscardTension(this, currentTension);
        }

        public void ResetPresentation()
        {
            active = false;
            currentTension = 0f;
            localCount = enemyCount = 0;
            enemy?.CancelDiscardAttention();
            AudioManager.Instance?.ReleaseDiscardTension(this);
        }

        public static void ResetForScene(Scene scene)
        {
            foreach (var item in FindObjectsByType<DiscardFeedbackDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (item.gameObject.scene == scene) item.ResetPresentation();
        }

        private void OnDisable() => ResetPresentation();
        private void OnDestroy() => ResetPresentation();
    }
}
