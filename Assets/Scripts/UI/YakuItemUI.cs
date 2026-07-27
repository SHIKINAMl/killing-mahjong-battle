using UnityEngine;
using TMPro;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public class YakuItemUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI yakuNameText;
        [SerializeField] private TextMeshProUGUI localBoostText; // 味方の強化値（＋１など、色はUnity上で青等に設定）
        [SerializeField] private TextMeshProUGUI enemyBoostText; // 敵の強化値（＋１など、色はUnity上で赤等に設定）

        [Header("Optional")]
        [Tooltip("役の成立条件を出す専用テキスト。未設定なら役名の下に小さく差し込む。")]
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Tooltip("説明文の表示サイズ（役名に対する％）。専用テキスト未設定時のみ使う。")]
        [SerializeField, Range(30, 100)] private int inlineDescriptionSizePercent = 55;

        /// <summary>
        /// 役リストの1項目分をセットアップします
        /// </summary>
        /// <param name="yakuName">役の名前</param>
        /// <param name="localBoost">味方の強化値</param>
        /// <param name="enemyBoost">敵の強化値</param>
        public void Setup(string yakuName, int localBoost, int enemyBoost)
        {
            // 役名だけでは麻雀を知らないプレイヤーに何も伝わらないため、成立条件を併記する。
            string description = YakuInfo.GetDescription(yakuName);

            if (descriptionText != null)
            {
                descriptionText.text = description;
                if (yakuNameText != null) yakuNameText.text = yakuName;
            }
            else if (yakuNameText != null)
            {
                // 専用テキストがPrefabに用意されていない場合でも読めるように、
                // リッチテキストで役名の下へ小さく差し込む（Prefab改修なしで機能させるため）。
                yakuNameText.text = string.IsNullOrEmpty(description)
                    ? yakuName
                    : $"{yakuName}\n<size={inlineDescriptionSizePercent}%>{description}</size>";
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
