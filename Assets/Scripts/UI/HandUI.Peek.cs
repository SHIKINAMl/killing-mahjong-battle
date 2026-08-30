using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    public partial class HandUI
    {
        private void CreatePeekButton()
        {
            peekButton = Instantiate(decideButton, decideButton.transform.parent);
            peekButton.name = "HandPeekButton";
            peekButton.onClick.RemoveAllListeners();   // 押しっぱなしで見るので click は使わない

            // **「決定」と同じ高さには置けない。** 打牌フェイズでは山牌が下へ降りてくるので
            // （WallUI の discardContainerPos。手牌選択の -190 に対し -255）、複製したままの高さだと
            // 山牌の下段と 5px 重なり、絵としても牌にくっついて見える。その分だけ下げる。
            var peekRect = peekButton.GetComponent<RectTransform>();
            if (peekRect != null)
            {
                peekRect.anchoredPosition = new Vector2(
                    peekRect.anchoredPosition.x,
                    peekRect.anchoredPosition.y - PeekButtonDropY);
            }

            var tmp = peekButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = PeekButtonLabel;
                // 幅に収まるまで縮める設定は NormalizeActionButtons が複製元に入れてあるので、
                // ここでは触らない。**特に `fontSizeMax = tmp.fontSize` を書かないこと。**
                // 自動縮小が有効なとき `fontSize` は「今の縮んだ値」を返すので、
                // それを上限に代入すると小さいまま固定されてしまう。
            }
            var txt = peekButton.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = PeekButtonLabel;
                txt.resizeTextForBestFit = true;
            }

            // **Canvas を足したら GraphicRaycaster も要る。**
            // Graphic は最寄りの Canvas に登録されるため、Canvas を足した時点で
            // 親の GraphicRaycaster の探索対象から外れ、クリックが一切届かなくなる
            Ensure<Canvas>(peekButton.gameObject);
            Ensure<GraphicRaycaster>(peekButton.gameObject);

            var hold = peekButton.gameObject.AddComponent<HoldButton>();
            hold.onHoldStart.AddListener(() => SetPeek(true));
            hold.onHoldEnd.AddListener(() => SetPeek(false));

            peekButton.gameObject.SetActive(false);
        }

        /// <summary>打牌フェイズで手牌を隠す対象か。</summary>
        private bool IsPeekPhase(RoundStatus phaseStatus)
        {
            if (!HideHandUntilPeek) return false;
            if (phaseStatus != RoundStatus.Discard) return false;
            // チュートリアルも本編と同じ挙動にする。
            // 台本が手牌を見せるのは手牌フェイズ（能力の実演・透視の印）だけで、
            // 打牌フェイズで手牌を指す誘導は無いため、ここで隠しても台本は壊れない
            return true;
        }

        private void SetPeek(bool on)
        {
            if (isPeeking == on) return;
            isPeeking = on;
            ApplyPeekVisual(on);

            // 何度も手牌を覗いている（Tile_PeekHold）。開いたときだけ数える
            if (on)
            {
                var watcher = KillingMahjong.Managers.PlayerActivityWatcher.Instance;
                if (watcher != null) watcher.NotifyPeek();
            }

            // 手牌を実際に表示させるのは UpdateLayout。
            // **重なり順の設定も中央寄せも、その後でなければならない。**
            // 非アクティブなオブジェクトの Canvas は overrideSorting が入らず、
            // 表示前は子が非アクティブで牌の範囲も測れない
            if (gameUIManager != null) UpdateLayout(gameUIManager.CurrentPhaseStatus);
            ApplyPeekTilesOrder(on);
            ApplyPeekPlacement(on);
        }

        /// <summary>暗幕の表示と、手牌を前面へ出すかどうかを切り替える。</summary>
        private void ApplyPeekVisual(bool on)
        {
            if (PeekUseDimmer)
            {
                EnsurePeekDimmer();
                if (peekDimmer != null) peekDimmer.gameObject.SetActive(on);
            }
            else if (peekDimmer != null)
            {
                peekDimmer.gameObject.SetActive(false);
            }

        }

        /// <summary>
        /// 覗いている間だけ手牌を前面へ出す。
        ///
        /// **必ず手牌が表示された後に呼ぶこと。**
        /// 非アクティブな GameObject の Canvas に `overrideSorting` を代入しても効かず、
        /// 手牌は親の Canvas（order 1）のまま描かれて、山牌（WallCanvas order 2）に隠れる。
        /// 毎回入れ直しているのはそのため。
        /// </summary>
        private void ApplyPeekTilesOrder(bool on)
        {
            if (discardPhaseContainer == null) return;
            if (peekTilesCanvas == null) peekTilesCanvas = Ensure<Canvas>(discardPhaseContainer.gameObject);

            if (on)
            {
                peekTilesCanvas.enabled = true;
                peekTilesCanvas.overrideSorting = true;
                peekTilesCanvas.sortingOrder = UISortingOrders.HandPeekTiles;
            }
            else
            {
                // 覗いていない間は無効にしておく。有効なままだと和了・結果フェイズでも
                // 手牌が結果パネルより手前に出てしまう
                peekTilesCanvas.enabled = false;
            }
        }

        /// <summary>
        /// 覗いている間だけ手牌を画面中央へ寄せて拡大する。
        ///
        /// アンカーやピボットは触らない。コンテナの中の牌はコンテナ基準で配置されているので、
        /// アンカーを変えると牌の位置まで動いてしまう。
        /// 代わりに**牌全体の実際の範囲**（子の境界）の中心が目標へ来るよう平行移動する。
        /// コンテナの rect の中心は牌の見た目の中心とは限らないため。
        /// </summary>
        private void ApplyPeekPlacement(bool on)
        {
            if (!PeekCenterOverlay) return;

            var rt = discardPhaseContainer as RectTransform;
            if (rt == null) return;

            if (!on)
            {
                if (!peekTransformSaved) return;
                rt.localScale = peekOriginalScale;
                rt.anchoredPosition = peekOriginalPos;
                peekTransformSaved = false;
                return;
            }

            if (peekTransformSaved) return;   // 二重に保存しない

            // 表示前だと子が非アクティブで範囲を測れない。呼ぶ順番の間違いに気づけるようにする
            if (!rt.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[HandUI] 手牌が非表示のまま中央寄せを計算しようとした。UpdateLayout の後に呼ぶこと");
                return;
            }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var canvasRt = (canvas.rootCanvas != null ? canvas.rootCanvas : canvas).transform as RectTransform;
            if (canvasRt == null) return;

            peekOriginalPos = rt.anchoredPosition;
            peekOriginalScale = rt.localScale;

            // 先に拡大してから範囲を測る（拡大後の見た目で中心を合わせたいため）
            rt.localScale = Vector3.one * PeekOverlayScale;

            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRt, rt);
            if (bounds.size.x < 1f || bounds.size.y < 1f)
            {
                // 測れていない。ここで動かすと画面外へ飛ぶので、拡大だけに留める
                Debug.LogWarning("[HandUI] 手牌の範囲を測れなかったので中央寄せを見送った");
                peekTransformSaved = true;
                return;
            }

            Vector3 targetWorld = canvasRt.TransformPoint(new Vector3(PeekOverlayPos.x, PeekOverlayPos.y, 0f));
            Vector3 centerWorld = canvasRt.TransformPoint(bounds.center);
            rt.position += (targetWorld - centerWorld);
            peekTransformSaved = true;
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        private void EnsurePeekDimmer()
        {
            if (peekDimmer != null) return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var root = canvas.rootCanvas != null ? canvas.rootCanvas.transform : canvas.transform;

            var go = new GameObject("HandPeekDimmer", typeof(RectTransform));
            go.transform.SetParent(root, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var c = go.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = UISortingOrders.HandPeekDimmer;

            peekDimmer = go.AddComponent<Image>();
            peekDimmer.color = new Color(0f, 0f, 0f, PeekDimAlpha);
            // クリックを吸うと、押しっぱなしの解放（OnPointerUp）が
            // ボタンに届かなくなって手牌が出たまま固まる
            peekDimmer.raycastTarget = false;

            go.SetActive(false);
        }
    }
}
