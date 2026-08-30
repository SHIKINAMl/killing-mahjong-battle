using UnityEngine;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    public partial class HandUI
    {
        private TMPro.TextMeshProUGUI EnsurePhaseGuide()
        {
            if (_phaseGuide != null) return _phaseGuide;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return null;

            var go = new GameObject("PhaseGuideText", typeof(RectTransform));
            go.transform.SetParent(canvas.rootCanvas.transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(560f, 26f);
            rt.anchoredPosition = new Vector2(0f, PhaseGuideFallbackY);

            var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.fontSize = PhaseGuideFontSize;
            tmp.color = PhaseGuideColor;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.overflowMode = TMPro.TextOverflowModes.Overflow;
            // 卓の緑にも暗い床にも載るので、縁を付けて背景から切り離す。
            //
            // **SDF の輪郭（OUTLINE_ON）はこの大きさでは黒画素が1つも出ない。**
            // 輪郭の太さは padding × (fontSize / pointSize) に比例し、
            // pointSize 90 / padding 9 のアトラスを 15px で使うと padding 全体でも 1.5px しかない。
            // outlineWidth 0.25 では 0.4px ＝ 1画素も塗られない（実測で暗い画素 0 個）。
            // 効くのは Underlay なので、オフセット 0 のまま全周へ膨らませて縁取りにする
            // （同条件で暗い画素 221 個。`UnityEngine.UI.Outline` は TMP には最初から効かない）。
            var guideMat = tmp.fontMaterial;
            guideMat.EnableKeyword("UNDERLAY_ON");
            guideMat.SetColor(TMPro.ShaderUtilities.ID_UnderlayColor, Color.black);
            guideMat.SetFloat(TMPro.ShaderUtilities.ID_UnderlayOffsetX, 0f);
            guideMat.SetFloat(TMPro.ShaderUtilities.ID_UnderlayOffsetY, 0f);
            guideMat.SetFloat(TMPro.ShaderUtilities.ID_UnderlayDilate, 1f);
            guideMat.SetFloat(TMPro.ShaderUtilities.ID_UnderlaySoftness, 0f);

            _phaseGuide = tmp;
            return _phaseGuide;
        }

        /// <summary>
        /// 「いま何をすればいいか」を1行で出す。手牌選択では選んだ枚数も添える。
        /// **チュートリアルでは出さない**（台本が同じことを順番に喋るため）。
        /// </summary>
        private void UpdatePhaseGuide(RoundStatus phaseStatus)
        {
            if (!ShowPhaseGuide) return;

            bool tutorial = gameUIManager != null && gameUIManager.IsTutorialMode;

            // **「山牌から1枚切る」は自分の手番だけ。**
            // 打牌フェイズは相手の番でも続いているので、条件を phaseStatus だけにすると
            // 自分では何もできない間ずっと「切れ」と言い続けることになる。
            // 手牌選択は**両者が同時に選ぶ**ので手番の概念が無く、ここでは見ない。
            bool localTurn = Managers.BoardStateManager.Instance != null &&
                             Managers.BoardStateManager.Instance.IsLocalTurn;

            bool wanted = !tutorial &&
                (phaseStatus == RoundStatus.HandSelection ||
                 (phaseStatus == RoundStatus.Discard && localTurn));

            if (!wanted)
            {
                if (_phaseGuide != null) _phaseGuide.gameObject.SetActive(false);
                _lastGuidePhase = (RoundStatus)(-1);
                _lastGuideHandCount = -1;
                return;
            }

            var guide = EnsurePhaseGuide();
            if (guide == null) return;

            int handCount = (Managers.BoardStateManager.Instance != null &&
                             Managers.BoardStateManager.Instance.CurrentHandTiles != null)
                            ? Managers.BoardStateManager.Instance.CurrentHandTiles.Count : 0;

            // 毎フレーム text を代入するとそのたびに文字が組み直されるので、変わったときだけ
            if (phaseStatus != _lastGuidePhase || handCount != _lastGuideHandCount)
            {
                if (phaseStatus == RoundStatus.HandSelection)
                {
                    string count = (handCount == HandSize)
                        ? $"<color=#7CE07C>{handCount} / {HandSize}</color>"
                        : $"<color=#FFD24A>{handCount} / {HandSize}</color>";
                    // **短く保つこと。** 卓の右手前に置物があり、長いと右端が隠れる
                    guide.text = $"山牌から{HandSize}枚えらぶ　{count}";
                }
                else
                {
                    guide.text = "山牌から1枚切る";
                }

                _lastGuidePhase = phaseStatus;
                _lastGuideHandCount = handCount;
            }

            PlaceGuideAboveWall(guide);
            guide.gameObject.SetActive(true);
        }

        /// <summary>手牌の枚数。ルール上13枚で固定</summary>
        /// <summary>
        /// 案内を山牌のすぐ上に置く。
        ///
        /// 案内は ScreenSpace-Overlay の Canvas に下端中央アンカーで置いてあるので、
        /// 画面座標を scaleFactor で割れば、そのまま anchoredPosition.y になる。
        /// </summary>
        private void PlaceGuideAboveWall(TMPro.TextMeshProUGUI guide)
        {
            if (_wallRect == null)
            {
                var go = GameObject.Find("WallContainer");
                if (go != null) _wallRect = go.transform as RectTransform;
            }
            if (_wallRect == null) return;

            var canvas = guide.canvas;
            if (canvas == null) return;

            var corners = new Vector3[4];
            _wallRect.GetWorldCorners(corners);
            float topScreenY = RectTransformUtility.WorldToScreenPoint(null, corners[1]).y;

            float scale = Mathf.Approximately(canvas.scaleFactor, 0f) ? 1f : canvas.scaleFactor;
            float y = topScreenY / scale + PhaseGuideLift;

            var rt = guide.rectTransform;
            if (!Mathf.Approximately(rt.anchoredPosition.y, y))
            {
                rt.anchoredPosition = new Vector2(0f, y);
            }
        }
    }
}
