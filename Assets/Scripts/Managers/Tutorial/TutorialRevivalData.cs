using System;
using System.Collections.Generic;

namespace KillingMahjong.Managers
{
    /// <summary>マルチモードへの導入。フロー図の敗北後から復活までの台詞。</summary>
    [Serializable]
    public sealed class TutorialRevivalData
    {
        public List<TutorialLine> enemyLines = new List<TutorialLine>
        {
            new TutorialLine("<b>アッハッハッハ</b>"),
            new TutorialLine("いつやってもとどめの瞬間がたまらないわ"),
            new TutorialLine("じゃあ敗けたアナタの魂を頂こうかしらね"),
        };

        public List<TutorialLine> monologueLines = new List<TutorialLine>
        {
            new TutorialLine("目の前が真っ暗になった", TutorialSpeaker.System),
            new TutorialLine("もう体は動かない\nなにもできない", TutorialSpeaker.System),
            new TutorialLine("魂を奪われるとはきっとこういうことなのだろう", TutorialSpeaker.System),
            new TutorialLine("………………", TutorialSpeaker.System),
            new TutorialLine("………………", TutorialSpeaker.System),
            new TutorialLine("……………ん？", TutorialSpeaker.System),
        };

        public string skillName = "復活";

        public List<TutorialLine> beruruLines = new List<TutorialLine>
        {
            new TutorialLine("これで契約を果たした"),
            new TutorialLine("あとは後輩ちゃんが務めを果たせ"),
            new TutorialLine("アタシの代わりにあの吸血鬼を倒すんだ"),
        };
    }
}
