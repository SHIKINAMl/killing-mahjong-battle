using UnityEngine;
using TMPro;

namespace KillingMahjong.UI
{
    public partial class EnemyInfoUI
    {
        public void SetMaxHP(int max)
        {
            maxHp = max;
            // 同じ値で繰り返し呼ばれるので分母を引き下げない（PlayerInfoUI と同じ理由）
            hpPeak = HpMeterMath.RaisePeak(hpPeak, max);
        }

        /// <summary>新しい対局の開始時に呼ぶ。メーターの分母（到達最高HP）も引き直す。</summary>
        public void ResetHpMeter(int max)
        {
            maxHp = max;
            hpPeak = max;
        }

        public void SetHP(int hp)
        {
            // 初回セットアップ（0 → 初期HP）ではポップアップを出さない。PlayerInfoUI と同じ判定。
            bool isFirstSetup = (currentHp == 0 && hp > 0);
            int diff = hp - currentHp;

            currentHp = hp;
            // 誰の血かを添える。理由と大きさの根拠は PlayerInfoUI.HpOwnerLabelScale を参照
            if (hpText != null) hpText.text = $"<size={PlayerInfoUI.HpOwnerLabelScale}>相手 </size>{currentHp}";

            // 人型メーターの割合を更新する
            hpPeak = HpMeterMath.RaisePeak(hpPeak, hp);
            if (hpFillImage != null)
            {
                hpFillImage.fillAmount = (float)hp / MeterMax;
            }

            // 与えたダメージが敵側に一切表示されず、手応えが片側だけだったため追加。
            if (!isFirstSetup && diff != 0 && !SuppressHpPopup)
            {
                HpPopup.Report(diff, currentHp, maxHp);
            }
        }

        /// <summary>
        /// ロンの血の移動中だけ true にして、浮き数字とSEを止める。理由は
        /// <see cref="PlayerInfoUI.SuppressHpPopup"/> と同じ（同じ数字が同じ場所に二重に出る）。
        /// </summary>
        public bool SuppressHpPopup { get; set; }

        /// <summary>
        /// 打撃SEのピッチ判定に使う分母。理由は <see cref="PlayerInfoUI.MaxHp"/> と同じ。
        /// </summary>
        public int MaxHp => maxHp;

        /// <summary>
        /// HPが見えている場所（血袋）。**血の移動の着弾点と、増減ラベルの置き場所に使う。**
        /// 取り方は <see cref="HpPopup"/> と同じ。null は返さない。
        /// </summary>
        public RectTransform HpAnchor
        {
            get
            {
                if (damagePopupAnchor != null) return damagePopupAnchor;
                var zoom = zoomTarget as RectTransform;
                if (zoom != null) return zoom;
                if (enemyPanel != null) return enemyPanel.transform as RectTransform;
                return transform as RectTransform;
            }
        }

        private HpPopupPresenter HpPopup
        {
            get
            {
                if (hpPopup == null)
                {
                    // PlayerInfoUI と同様、transform は全画面のルートCanvas。
                    // HPが見えている血袋（EnemyPanel）を基準にする。
                    RectTransform anchor = damagePopupAnchor;
                    if (anchor == null) anchor = zoomTarget as RectTransform;
                    if (anchor == null && enemyPanel != null) anchor = enemyPanel.transform as RectTransform;

                    hpPopup = new HpPopupPresenter(this, transform as RectTransform, anchor,
                                                   damagePopupPrefab, new Vector2(0, 60f), isLocalPlayer: false);
                }
                return hpPopup;
            }
        }

        public void SetPanelVisible(bool visible)
        {
            if (enemyPanel != null)
            {
                enemyPanel.SetActive(visible);
            }
            if (!visible)
            {
                ShowReadyBox(false);
            }
        }
    }
}
