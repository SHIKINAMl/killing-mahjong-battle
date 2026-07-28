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
        private readonly RectTransform canvasRoot;
        private readonly RectTransform anchor;
        private readonly GameObject prefab;
        private readonly Vector2 offset;
        private readonly bool isLocalPlayer;

        private int pendingDiff;
        private int latestHp;
        private int latestMaxHp = 1;
        private Coroutine flushCoroutine;

        /// <param name="canvasRoot">ポップアップの親。Canvas のルートを渡す。</param>
        /// <param name="anchor">
        /// どこに出すかの基準。自分のスマホ、相手の血袋など「HPが見えている場所」を渡す。
        /// null や canvasRoot と同じ場合は Canvas 中央基準になる。
        /// </param>
        /// <param name="offset">anchor の中心からのずらし量。</param>
        /// <param name="isLocalPlayer">
        /// 自分側なら被弾音（追い詰められる音）、相手側なら打撃音（当てた手応えの音）を鳴らす。
        /// </param>
        public HpPopupPresenter(MonoBehaviour host, RectTransform canvasRoot, RectTransform anchor,
                                GameObject prefab, Vector2 offset, bool isLocalPlayer)
        {
            this.host = host;
            this.canvasRoot = canvasRoot;
            this.anchor = anchor;
            this.prefab = prefab;
            this.offset = offset;
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

        /// <summary>
        /// 出現位置を Canvas ローカル座標で返す。
        ///
        /// PlayerInfoUI / EnemyInfoUI はどちらも全画面サイズのルートCanvasなので、
        /// その transform を基準にすると自分も相手も画面中央に出てしまう。
        /// HPが見えている実際のパネル（スマホ・血袋）を基準にする。
        /// </summary>
        private Vector2 ResolveSpawnPosition()
        {
            if (anchor == null || anchor == canvasRoot) return offset;

            Vector3[] corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            Vector3 center = (corners[0] + corners[2]) * 0.5f;

            return (Vector2)canvasRoot.InverseTransformPoint(center) + offset;
        }

        private void SpawnPopup(int amount)
        {
            if (canvasRoot == null) return;

            GameObject popupObj;
            if (prefab != null)
            {
                popupObj = UnityEngine.Object.Instantiate(prefab, canvasRoot);
            }
            else
            {
                // プレハブが未設定の場合のフォールバック
                popupObj = new GameObject("DamagePopup");
                popupObj.transform.SetParent(canvasRoot, false);
                var rt = popupObj.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(300, 100);
                popupObj.AddComponent<DamagePopupUI>();
            }

            RectTransform popupRt = popupObj.GetComponent<RectTransform>();
            if (popupRt != null)
            {
                // アンカーはCanvas中央に固定してから座標を入れる。
                // プレハブ側のアンカー設定に結果が左右されないようにするため。
                popupRt.anchorMin = new Vector2(0.5f, 0.5f);
                popupRt.anchorMax = new Vector2(0.5f, 0.5f);
                popupRt.pivot = new Vector2(0.5f, 0.5f);
                popupRt.anchoredPosition = ResolveSpawnPosition();
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
