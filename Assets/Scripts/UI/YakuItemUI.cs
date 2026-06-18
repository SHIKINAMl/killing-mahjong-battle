using UnityEngine;
using TMPro;

namespace KillingMahjong.UI
{
    public class YakuItemUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI yakuNameText;
        [SerializeField] private TextMeshProUGUI localBoostText; // 味方の強化値（＋１など、色はUnity上で青等に設定）
        [SerializeField] private TextMeshProUGUI enemyBoostText; // 敵の強化値（＋１など、色はUnity上で赤等に設定）

        /// <summary>
        /// 役リストの1項目分をセットアップします
        /// </summary>
        /// <param name="yakuName">役の名前</param>
        /// <param name="localBoost">味方の強化値</param>
        /// <param name="enemyBoost">敵の強化値</param>
        public void Setup(string yakuName, int localBoost, int enemyBoost)
        {
            if (yakuNameText != null)
            {
                yakuNameText.text = yakuName;
            }

            if (localBoostText != null)
            {
                // 0より大きければ "+1" のように表示し、0なら空文字（何も表示しない）
                localBoostText.text = localBoost > 0 ? $"+{localBoost}" : "";
            }

            if (enemyBoostText != null)
            {
                enemyBoostText.text = enemyBoost > 0 ? $"+{enemyBoost}" : "";
            }
        }
    }
}
