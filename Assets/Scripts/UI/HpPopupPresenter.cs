using System.Collections;
using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// HP増減のポップアップとSEを、プレイヤー側・敵側で共通に扱うためのヘルパー。
    ///
    /// RonAnimationUI は HP を 1.5 秒かけて Lerp で動かしながら毎フレーム SetHP を呼ぶ。
    /// 素直に SetHP のたびにポップアップを出すと、丸め値が変わるほぼ全フレームで生成され、
    /// 同じ座標に数十個の数字が積み重なってしまう（実際に PlayerInfoUI で起きていた）。
    ///
    /// そのため増減をいったん溜め込み、HPが動かなくなってから1つにまとめて表示する。
    /// SEも同じタイミングで1回だけ鳴らす。
    /// </summary>
    public class HpPopupPresenter
    {
        /// <summary>この時間だけHPが動かなければ「変化が落ち着いた」とみなす（実時間）。</summary>
        private const float SettleDelay = 0.12f;

        private readonly MonoBehaviour host;
        private readonly Transform parent;
        private readonly GameObject prefab;
        private readonly Vector2 spawnPosition;
        private readonly bool isLocalPlayer;

        private int pendingDiff;
        private int latestHp;
        private int latestMaxHp = 1;
        private Coroutine flushCoroutine;

        /// <param name="isLocalPlayer">
        /// 自分側なら被弾音（追い詰められる音）、相手側なら打撃音（当てた手応えの音）を鳴らす。
        /// </param>
        public HpPopupPresenter(MonoBehaviour host, Transform parent, GameObject prefab,
                                Vector2 spawnPosition, bool isLocalPlayer)
        {
            this.host = host;
            this.parent = parent;
            this.prefab = prefab;
            this.spawnPosition = spawnPosition;
            this.isLocalPlayer = isLocalPlayer;
        }

        /// <summary>HPの変化を報告する。実際の表示は変化が落ち着いてから1回だけ行われる。</summary>
        public void Report(int diff, int newHp, int maxHp)
        {
            if (diff == 0) return;

            pendingDiff += diff;
            latestHp = newHp;
            latestMaxHp = Mathf.Max(1, maxHp);

            if (host == null || !host.isActiveAndEnabled)
            {
                // コルーチンを回せない状況（非アクティブ時など）は即座に出す
                Flush();
                return;
            }

            if (flushCoroutine != null) host.StopCoroutine(flushCoroutine);
            flushCoroutine = host.StartCoroutine(FlushAfterSettle());
        }

        private IEnumerator FlushAfterSettle()
        {
            yield return new WaitForSecondsRealtime(SettleDelay);
            flushCoroutine = null;
            Flush();
        }

        private void Flush()
        {
            int amount = pendingDiff;
            pendingDiff = 0;
            if (amount == 0) return;

            PlaySound(amount);
            SpawnPopup(amount);
        }

        private void PlaySound(int amount)
        {
            var audio = Managers.AudioManager.Instance;
            if (audio == null) return;

            float ratio = (float)latestHp / latestMaxHp;

            if (amount < 0)
            {
                if (isLocalPlayer) audio.PlayDamageSE(ratio);
                else audio.PlayHitSE(ratio);
            }
            else
            {
                audio.PlayHealSE();
            }
        }

        private void SpawnPopup(int amount)
        {
            if (parent == null) return;

            GameObject popupObj;
            if (prefab != null)
            {
                popupObj = UnityEngine.Object.Instantiate(prefab, parent);
            }
            else
            {
                // プレハブが未設定の場合のフォールバック
                popupObj = new GameObject("DamagePopup");
                popupObj.transform.SetParent(parent, false);
                var rt = popupObj.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(300, 100);
                popupObj.AddComponent<DamagePopupUI>();
            }

            RectTransform popupRt = popupObj.GetComponent<RectTransform>();
            if (popupRt != null)
            {
                popupRt.anchoredPosition = spawnPosition;
            }

            DamagePopupUI popup = popupObj.GetComponent<DamagePopupUI>();
            if (popup != null)
            {
                Color c = amount > 0 ? Color.green : Color.red;
                popup.Setup(amount, c);
            }
        }
    }
}
