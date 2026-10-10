using UnityEngine;

namespace KillingMahjong.Managers
{
    public partial class AudioManager
    {
        // ------------------------------------------------------------
        //  どのシーンから始めても音が鳴るようにする（2026-10-04）
        //
        //  **以前は OpeningScene にしか置いていなかった。**
        //  ビルドは先頭が OpeningScene なので本番では鳴っていたが、
        //  エディタでタイトルシーンを直接再生すると AudioManager がどこにも
        //  居ないため、まるごと無音になっていた（ユーザー報告）。
        //  同じ理由で UIテストシーンも無音で、演出の録画が音無しで出ていた。
        //
        //  **シーンごとに置いて回る形にはしない。** 差し込むクリップが
        //  30個近くあり、置き場所が増えるとそのぶん挿し忘れが起きる
        //  （実際、透視スキルのSEを足したときに踏んだ）。
        //  プレハブ1つを本物として、居なければそれを出す。
        //
        //  `AfterSceneLoad` なので、シーンに実体が置いてある場合は
        //  そちらの `Awake` が先に走って `Instance` が埋まり、ここは何もしない。
        //  逆にここで出したあとにシーンの実体が現れても、`Awake` の
        //  重複ガードが後から来たほうを捨てるので二重には鳴らない。
        // ------------------------------------------------------------

        /// <summary>`Resources` から見たプレハブの場所。</summary>
        private const string BootstrapPrefabPath = "AudioManager";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;

            var prefab = Resources.Load<GameObject>(BootstrapPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[AudioManager] Resources/{BootstrapPrefabPath} が見つかりません。" +
                                 "このシーンでは音が鳴りません。");
                return;
            }

            // 名前から "(Clone)" を落とす。ヒエラルキーで実体と見分けが付かなくてよい
            var go = Instantiate(prefab);
            go.name = prefab.name;
        }
    }
}
