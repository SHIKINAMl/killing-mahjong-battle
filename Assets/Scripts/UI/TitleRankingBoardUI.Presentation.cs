using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    public sealed partial class TitleRankingBoardUI
    {
        private readonly List<TMP_Text> hiddenStartLabels = new List<TMP_Text>();

        private void HideUnderlyingStartLabel()
        {
            foreach (var label in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (label.gameObject.scene != gameObject.scene || !label.isActiveAndEnabled
                    || (label.text ?? "").Trim() != "ゲーム開始" || label.GetComponentInParent<Button>() == null) continue;
                hiddenStartLabels.Add(label);
                label.enabled = false;
            }
        }

        private void RestoreUnderlyingStartLabel()
        {
            foreach (var label in hiddenStartLabels) if (label != null) label.enabled = true;
            hiddenStartLabels.Clear();
        }
    }
}
