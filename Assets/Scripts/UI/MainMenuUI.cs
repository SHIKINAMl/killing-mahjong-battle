using KillingMahjong.Network;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// メインメニュー（書斎の部屋。2026-11-21 ユーザーのデザイン mainmyuI*.png）。
    ///
    /// 絵は3枚を重ねている（どれも 400x300 のドット絵を 2 倍で 800x600 に）:
    ///   背景（本棚・窓・燭台）／ 手前の物（紫の結晶・椅子・電話の台）／ 文字
    /// 押せる場所は4つ。絵の上に透明なボタンを置いている（位置は絵から測った）:
    ///   ・「さまよえるだれかと」（紫の結晶）… 野良の対局
    ///   ・「よび出した友達と」（電話）… 友達との対局（部屋を作る／あいことばで入る）
    ///   ・「ギャラリー」（右上の棚の小物）… コレクション
    ///   ・「やめる」… タイトルへ戻る
    /// CPU 対戦の入口は置かない（ユーザーの判断）。マウスを乗せたときの演出は、いったん無し（ユーザーの判断）。
    ///
    /// **部品はシーン（メインメニューシーン）に置いてある。ここは押したときの動きだけを再生時につなぐ**
    /// （<see cref="TitleMultiMenuUI"/> と同じ理由。シーンに保存した配線は外れやすい）。
    /// 対局への入り方（名前の確認 → <see cref="MatchJoinRequest"/> → 対局シーン）は、タイトル・部屋と同じ。
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Button strangerButton;
        [SerializeField] private Button friendButton;
        [SerializeField] private Button galleryButton;
        [SerializeField] private Button quitButton;
        [Tooltip("開いている間は下のボタンを押せなくする（友達の入口・コレクション・名前の入力）")]
        [SerializeField] private CanvasGroup menuGroup;

        [SerializeField] private string matchSceneName = "UIテストシーン";
        [SerializeField] private string titleSceneName = "タイトルシーン";

        private TitleMultiMenuUI _multiMenu;
        private CollectionUI _collection;
        private bool _isLoadingScene;

        private void Start()
        {
            Bind(strangerButton, OnStranger);
            Bind(friendButton, OnFriend);
            Bind(galleryButton, OnGallery);
            Bind(quitButton, OnQuit);

            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio != null) audio.PlayRoomBgm();
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }


        private void SetMenuInteractable(bool on)
        {
            if (menuGroup == null) return;
            menuGroup.interactable = on;
            menuGroup.blocksRaycasts = on;
        }

        // ---- さまよえるだれかと（野良） ----

        private void OnStranger()
        {
            if (_isLoadingScene) return;
            if (!EnsureName(OnStranger)) return;
            MatchJoinRequest.RequestPublic();
            LoadScene(matchSceneName);
        }

        // ---- よび出した友達と ----

        private void OnFriend()
        {
            if (_isLoadingScene) return;
            if (!EnsureName(OnFriend)) return;
            if (_multiMenu == null) _multiMenu = GetComponent<TitleMultiMenuUI>();
            if (_multiMenu == null) _multiMenu = gameObject.AddComponent<TitleMultiMenuUI>();
            if (_isOpeningFriend) return;
            SetMenuInteractable(false);
            StartCoroutine(OpenFriendOverBlurredRoom());
        }

        /// <summary>
        /// 友達対戦の画面の後ろに、いまの部屋をぼかして敷く（2026-11-27 プランナーの案
        /// 「UIとか全部消したメインメニュー背景を表示 → ぼかし → 半透明の黒い膜 → 電話の絵を上から置く」）。
        /// 部屋の上の字（Labels）と押す所（Buttons）を1コマだけ消して画面を写し、すぐ戻す。
        /// </summary>
        private System.Collections.IEnumerator OpenFriendOverBlurredRoom()
        {
            _isOpeningFriend = true;
            var hidden = new System.Collections.Generic.List<GameObject>();
            var room = menuGroup != null ? menuGroup.transform.parent : null; // menuGroup は Room/Buttons に付いている
            if (room != null)
                foreach (var name in RoomOverlayNames)
                {
                    var part = room.Find(name);
                    if (part != null && part.gameObject.activeSelf) { part.gameObject.SetActive(false); hidden.Add(part.gameObject); }
                }
            yield return new WaitForEndOfFrame();
            Texture2D blurred = null;
            try { blurred = ScreenBlurCapture.Capture(); }
            catch (System.Exception e) { Debug.LogWarning("[MainMenu] 部屋の絵を写せませんでした: " + e.Message); }
            foreach (var part in hidden) if (part != null) part.SetActive(true);
            _isOpeningFriend = false;

            _multiMenu.OpenFriend(mode =>
            {
                Debug.Log($"[MainMenu] 友達と対局（{mode}）。{matchSceneName} へ");
                LoadScene(matchSceneName);
            }, () =>
            {
                if (!_isLoadingScene) SetMenuInteractable(true);
            }, blurred);
        }

        /// <summary>ぼかした背景に写さない物（部屋の上の字と、透明なボタン）。シーンの Room の下の名前。</summary>
        private static readonly string[] RoomOverlayNames = { "Labels", "Buttons" };
        private bool _isOpeningFriend;

        // ---- ギャラリー ----

        private void OnGallery()
        {
            if (_isLoadingScene) return;
            if (_collection == null) _collection = GetComponent<CollectionUI>();
            if (_collection == null) _collection = gameObject.AddComponent<CollectionUI>();
            SetMenuInteractable(false);
            _collection.Open(() =>
            {
                SetMenuInteractable(true);
                // コレクションは閉じるときにタイトル曲を流すので、メニューの曲へ戻す
                var audio = KillingMahjong.Managers.AudioManager.Instance;
                if (audio != null) audio.PlayRoomBgm();
            });
        }

        // ---- やめる ----

        private void OnQuit()
        {
            LoadScene(titleSceneName);
        }

        /// <summary>
        /// 名前が無ければ、先に書いてもらう（タイトル・部屋の「対局へ」と同じ。2026-10-23 ユーザー指示）。
        /// 書けたら <paramref name="then"/> をもう一度呼ぶ。名前があれば true。
        /// </summary>
        private bool EnsureName(System.Action then)
        {
            if (KillingMahjong.Core.PlayerProfile.HasName) return true;
            var prompt = MatchNamePromptUI.Ensure();
            SceneFirst.Report(nameof(MatchNamePromptUI));
            SetMenuInteractable(false);
            prompt.Open(() =>
            {
                SetMenuInteractable(true);
                then();
            }, () =>
            {
                SetMenuInteractable(true);
            });
            return false;
        }

        private void LoadScene(string sceneName)
        {
            if (_isLoadingScene) return;
            _isLoadingScene = true;
            SetMenuInteractable(false);
            var loading = LoadingManager.Instance;
            if (loading != null) loading.FadeOutScreen(() => SceneManager.LoadSceneAsync(sceneName));
            else SceneManager.LoadSceneAsync(sceneName);
        }
    }
}
