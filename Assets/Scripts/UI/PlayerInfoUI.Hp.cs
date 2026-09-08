using UnityEngine;
using TMPro;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public partial class PlayerInfoUI
    {
        public void SetMaxHP(int max)
        {
            maxHp = max;
            // TutorialManager.ApplyHpToUI から同じ値で何度も呼ばれるので、
            // ここで到達最高HPを引き下げてはいけない（メーターの分母が戻ってしまう）。
            hpPeak = HpMeterMath.RaisePeak(hpPeak, max);
            // 新しい対局が始まるのでビネットの抑止を解除する
            heartbeatSuppressed = false;
        }

        /// <summary>新しい対局の開始時に呼ぶ。メーターの分母（到達最高HP）も引き直す。</summary>
        public void ResetHpMeter(int max)
        {
            maxHp = max;
            hpPeak = max;
            heartbeatSuppressed = false;
        }

        public void SetHP(int hp)
        {
            // 初回セットアップ時はポップアップを出さないように、現在値が0かつhpがmaxHpと同じならスキップするなどの制御が必要ですが、
            // 今回は通信で更新されたときだけ出したいので、初期化かどうかの簡易判定を入れます。
            bool isFirstSetup = (currentHp == 0 && hp > 0);
            int diff = hp - currentHp;

            currentHp = hp;
            if (hpText != null)
            {
                // 自分側はスマホの位置そのものが識別子なので、狭い数値欄には HP の値だけを置く。
                // 値の供給元や更新タイミングは変えず、表示文字列だけを整える。
                hpText.text = currentHp.ToString();
            }
            
            // 人型メーターの割合を更新する
            hpPeak = HpMeterMath.RaisePeak(hpPeak, hp);
            if (hpFillImage != null)
            {
                hpFillImage.fillAmount = (float)hp / MeterMax;
            }

            if (!isFirstSetup && diff != 0 && !SuppressHpPopup)
            {
                HpPopup.Report(diff, currentHp, maxHp);
            }

            // 減ったときだけノイズを走らせる。増えた（勝った）ときは出さない
            if (!isFirstSetup && diff < 0)
            {
                HpDamageGlitch.Play(diff, maxHp);
            }

            // --- 瀕死ハートビートエフェクトの更新 ---
            if (heartbeatEffect != null && !heartbeatSuppressed)
            {
                heartbeatEffect.UpdateHeartbeat(currentHp, maxHp);
            }
        }

        /// <summary>
        /// 決着時など、HPに関係なくビネットを消したいときに呼ぶ。
        /// ビネット(91)は勝敗Canvas(55)より手前なので、消さないと結果画面に被る。
        /// ロン演出中は SetHP が毎フレーム呼ばれるため、次の SetMaxHP まで再開を抑止する。
        /// </summary>
        public void StopHeartbeatEffect()
        {
            heartbeatSuppressed = true;
            if (heartbeatEffect != null)
            {
                heartbeatEffect.StopEffect();
            }
        }

        private HpPopupPresenter HpPopup
        {
            get
            {
                if (hpPopup == null)
                {
                    // このコンポーネントは全画面のルートCanvasに付いているので、
                    // transform を基準にすると相手側と同じ画面中央に出てしまう。
                    // HPが見えているスマホ（HPPanel）を基準にする。
                    RectTransform anchor = damagePopupAnchor != null
                        ? damagePopupAnchor
                        : zoomTarget as RectTransform;

                    hpPopup = new HpPopupPresenter(this, transform as RectTransform, anchor,
                                                   damagePopupPrefab, new Vector2(0, 60f), isLocalPlayer: true);
                }
                return hpPopup;
            }
        }
    }
}
