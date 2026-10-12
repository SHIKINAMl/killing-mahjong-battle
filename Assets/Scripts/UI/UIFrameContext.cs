using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>子UIの持ち主。将来、相手側UIには相手のプレイヤーIDを明示して割り当てる。</summary>
    public sealed class UIFrameContext : MonoBehaviour
    {
        [SerializeField] private string playerKey = UIFrameSelection.LocalPlayer;
        public string PlayerKey => playerKey;
        public void SetPlayer(string key) { playerKey = key ?? UIFrameSelection.LocalPlayer; }
    }
}
