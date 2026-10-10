using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    public class RiverUI : MonoBehaviour
    {
        [Header("River Configuration")]
        [SerializeField] private Transform riverContainer;
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private TileResourceManager tileResourceManager;
        [SerializeField] private TMPro.TextMeshProUGUI turnText; // 左上の打目表示用UI

        [Header("Layout Settings")]
        [SerializeField] private float tileWidth = 50.0f;
        [SerializeField] private float tileHeight = 70.0f;
        [SerializeField] private float tileScale = 0.9f;

        [SerializeField] private bool isEnemyRiver = false; // 相手の河として扱う場合、180度回転させる
        [SerializeField] private float enemyOffsetX = 350f; // 敵の河の位置手前寄せ調整X（ユーザー要望: 350）
        [SerializeField] private float enemyOffsetY = -200f; // 敵の河の位置手前寄せ調整Y（ユーザー要望: -200）

        /// <summary>
        /// 1行に並べる枚数。
        ///
        /// **9 は仕様から決まる値で、好みではない。** 河は1人あたり最大17牌なので、
        /// 9 なら 9+8 でちょうど2行に収まる。8 以下だと17牌目だけが3行目に落ちる。
        ///
        /// **ここは意図的に SerializeField にしていない。**
        /// 対局シーンが UIテストシーン と OpeningScene の2つあり、河も自分・相手で
        /// 2つずつあるので、シーンに持たせると4か所を直すことになる
        /// （実際、コードの既定値 6 に対してシーンには 8 が焼かれていた）。
        /// </summary>
        private const int MaxPerRow = 9;

        private List<Transform> discardedTiles = new List<Transform>();

        /// <summary>
        /// いちばん最後に置いた牌。**ロン演出で「その牌だけ光らせる」ために使う**（2026-10-04）。
        /// まだ1枚も置いていなければ null。
        /// </summary>
        public RectTransform LastTile
        {
            get
            {
                for (int i = discardedTiles.Count - 1; i >= 0; i--)
                {
                    var rt = discardedTiles[i] as RectTransform;
                    if (rt != null) return rt;
                }
                return null;
            }
        }

        private RectTransform _tilesBoundsProxy;

        /// <summary>
        /// いま置いてある牌ぜんぶを囲む矩形（2026-10-07）。
        /// **チュートリアルで「河をハイライト」するときの的に使う。**
        ///
        /// 河の入れ物（riverContainer）そのものは的にできない。牌は自前の座標で
        /// 並べていて、入れ物の矩形は牌の並びと大きさが合っていないので、
        /// そこを囲むと何も無い所に枠が出る。実際に置いてある牌の四隅から測る。
        ///
        /// 返すのは**この河の直下に置いた、見えない目印**。牌の親（riverContainer）の
        /// 下には置かない。あちらは並び順や枚数を子の並びで見ているので、
        /// 牌でないものを混ぜると数え違いの元になる。
        ///
        /// 1枚も置いていなければ null。
        /// </summary>
        public RectTransform GetTilesBoundsRect()
        {
            var self = transform as RectTransform;
            if (self == null) return null;

            bool any = false;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            var corners = new Vector3[4];

            foreach (var t in discardedTiles)
            {
                var rt = t as RectTransform;
                if (rt == null || !rt.gameObject.activeInHierarchy) continue;

                // 敵の河は180度回してあるので、ローカルの大きさではなく世界座標の四隅で測る
                rt.GetWorldCorners(corners);
                for (int i = 0; i < 4; i++)
                {
                    Vector2 p = self.InverseTransformPoint(corners[i]);
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                    any = true;
                }
            }

            if (!any) return null;

            if (_tilesBoundsProxy == null)
            {
                var go = new GameObject("TilesBounds", typeof(RectTransform));
                _tilesBoundsProxy = (RectTransform)go.transform;
                _tilesBoundsProxy.SetParent(self, false);
            }

            // 親の矩形の大きさに引きずられないよう、アンカーは親のピボットの位置に寄せる。
            // こうすると anchoredPosition が、そのまま親のローカル座標になる
            Vector2 pivot = self.pivot;
            _tilesBoundsProxy.anchorMin = pivot;
            _tilesBoundsProxy.anchorMax = pivot;
            _tilesBoundsProxy.pivot = new Vector2(0.5f, 0.5f);
            _tilesBoundsProxy.localRotation = Quaternion.identity;
            _tilesBoundsProxy.localScale = Vector3.one;
            _tilesBoundsProxy.sizeDelta = max - min;
            _tilesBoundsProxy.anchoredPosition = (min + max) * 0.5f;

            return _tilesBoundsProxy;
        }

        // ボルテージのゲージはここでは作らない。**GameUIManager.Start が作る。**
        // 河から作ると河のキャンバスの下にぶら下がり、手牌を選んでいる間は
        // 河ごと消えてゲージまで見えなくなる（実際そうなった）。

        // ---- 2つの河を同じ中心線に乗せる（2026-08-24）----
        //
        // 同じ11枚でも、敵 x332..511（幅179）／自分 x339..543（幅204）と揃っていなかった。
        // 送り幅 `tileWidth` が **敵18 / 自分21** で 14% 違うのが原因で、牌の絵の大きさは
        // 35.1 と 36.0 でほとんど同じ。つまり遠近感ではなく、敵側だけ詰まって並んでいた。
        //
        // **幅の差はそのまま残す**（奥の河が少し短いのは卓の遠近に合う）。
        // 揃えるのは**中心線と、縦の重なり**の2つだけ。
        //
        // 縦は 敵141..190 / 自分97..148 で **7px 重なっていた**（敵の河は上へ、
        // 自分の河は下へ伸びるので、境目でぶつかる）。同じ 7px を隙間にして離す。

        /// <summary>敵の河と自分の河のあいだに空ける隙間。</summary>
        private const float RiverGapY = 7f;

        /// <summary>
        /// 敵の河を自分の河と同じ中心線に乗せ、縦の重なりを隙間に変える。
        ///
        /// **シーンの `enemyOffsetX` / `enemyOffsetY` を直値で書き換えない。**
        /// 対局シーンが2つあるうえ、片方だけ直すと次に牌の大きさを変えたときに
        /// また食い違う。自分の河の実寸から毎回計算する。
        ///
        /// `GameUIManager` の初期化から1回だけ呼ぶ。
        /// </summary>
        public void AlignToOpponentRiver(RiverUI localRiver)
        {
            if (!isEnemyRiver || localRiver == null) return;
            if (riverContainer == null || localRiver.riverContainer == null) return;

            var selfRect = riverContainer as RectTransform;
            var otherRect = localRiver.riverContainer as RectTransform;
            if (selfRect == null || otherRect == null) return;

            float selfTileW = TileVisualWidth();
            float otherTileW = localRiver.TileVisualWidth();

            // 牌のピボットは左上なので、1行はどちらも「先頭の位置」から右へ伸びる。
            // 自分: [left, left + 8*tileWidth + 牌幅]
            // 敵  : [left + offsetX - 8*tileWidth, left + offsetX + 牌幅]
            float otherLeft = otherRect.anchoredPosition.x - otherRect.rect.width * otherRect.pivot.x;
            float otherCenter = otherLeft + (RowSpan(localRiver.tileWidth, otherTileW)) * 0.5f;

            float selfLeft = selfRect.anchoredPosition.x - selfRect.rect.width * selfRect.pivot.x;
            enemyOffsetX = otherCenter - selfLeft + (8f * tileWidth - selfTileW) * 0.5f;

            // **縦は動かさない。**
            //
            // 一度、自分の河との重なり(7px)を隙間に変えようとして敵の河を13px上げたが、
            // **敵の手牌に8px食い込んだ**（2026-08-24 に実測して差し戻し）。盤面の縦はこう詰まっている:
            //
            //   自分の山牌 y 10..128 ／ 自分の河 97..148 ／ 敵の河 141..190 ／ 敵の手牌 195..235
            //
            // 自分の河の上端(148)から敵の手牌の下端(195)までは **47px しかなく、敵の河は48px**。
            // どこかと必ず重なるので、**重なる相手を選ぶ**しかない。牌が伏せてある敵の手牌より、
            // 同じ河同士で少し重なる方が読み違えが少ないため、縦はシーンの値のまま置く。
            //
            // 直すなら河ではなく、自分の山牌・河・敵の手牌の縦位置を引き直す話になる。

            // すでに並んでいる牌にも当て直す（局の途中で呼ばれても崩れないように）
            for (int i = 0; i < discardedTiles.Count; i++)
            {
                var rt = discardedTiles[i] as RectTransform;
                if (rt != null) ApplyRiverLayoutAt(rt, i);
            }
        }

        /// <summary>1行（MaxPerRow 枚）が占める横幅。</summary>
        private static float RowSpan(float step, float tileVisualWidth)
        {
            return (MaxPerRow - 1) * step + tileVisualWidth;
        }

        /// <summary>牌1枚の見た目の幅。プレハブの矩形に `tileScale` を掛けたもの。</summary>
        private float TileVisualWidth()
        {
            var prefabRect = tilePrefab != null ? tilePrefab.GetComponent<RectTransform>() : null;
            float w = prefabRect != null && prefabRect.sizeDelta.x > 0f ? prefabRect.sizeDelta.x : 45f;
            return w * tileScale;
        }

        // 既存のタイル(Wallなどから)をRiverに入れるための新しいメソッド
        public void AddExistingTile(RectTransform rt, int tileId)
        {
            if (rt == null || riverContainer == null) return;

            // **並べる前に数える。** 並べてから呼ぶと、今捨てた牌を
            // 「すでに出ている」と数えてしまう。AddTile 側も同じ。
            bool willFly = BeginVoltage(tileId);

            rt.SetParent(riverContainer, true);
            rt.SetAsLastSibling();

            ApplyRiverLayout(rt);

            // Visual Setup
            TileVisual visual = rt.GetComponent<TileVisual>();
            if (visual != null && tileResourceManager != null)
            {
                visual.SetTile(tileId, tileResourceManager.GetDiscardTileSprite(tileId, isEnemyRiver));
                visual.SetExposed(false);
                visual.SetHoverHighlight(false);
            }

            TileInteraction interaction = rt.GetComponent<TileInteraction>();
            if (interaction != null)
            {
                interaction.Initialize(tileId, false, FindFirstObjectByType<GameUIManager>(), FindFirstObjectByType<Canvas>());
                interaction.enabled = false;
            }

            discardedTiles.Add(rt);
            UpdateTurnText();
            UpdateSiblingOrder();

            PlayPlacedHeartbeat();
            if (willFly) FinishVoltage(rt);
        }

        public void AddTile(int tileId)
        {
            if (tilePrefab == null || riverContainer == null) return;

            bool willFly = BeginVoltage(tileId);

            GameObject obj = Instantiate(tilePrefab, riverContainer);
            RectTransform rt = obj.GetComponent<RectTransform>();
            if (rt == null) rt = obj.transform as RectTransform;

            ApplyRiverLayout(rt);
            
            // Visual
            TileVisual visual = obj.GetComponent<TileVisual>();
            if (visual != null && tileResourceManager != null)
            {
                visual.SetTile(tileId, tileResourceManager.GetDiscardTileSprite(tileId, isEnemyRiver));
                visual.SetExposed(false);
                visual.SetHoverHighlight(false);
            }

            TileInteraction interaction = obj.GetComponent<TileInteraction>();
            if (interaction != null)
            {
                interaction.Initialize(tileId, false, FindFirstObjectByType<GameUIManager>(), FindFirstObjectByType<Canvas>());
                interaction.enabled = false;
            }

            discardedTiles.Add(obj.transform);
            UpdateTurnText();
            UpdateSiblingOrder();

            PlayPlacedHeartbeat();
            if (willFly) FinishVoltage(rt);
        }

        /// <summary>
        /// 牌が河に置かれた直後の心音（弱・詰まった1拍）。
        ///
        /// **ここ1か所だけで鳴らす。** <see cref="AddTile"/> と
        /// <see cref="AddExistingTile"/> は自分・相手・チュートリアルの
        /// すべての打牌が通る場所なので、`GameUIManager.HandleDiscardEvent` や
        /// `TutorialManager` 側にも足すと二重に鳴る。
        ///
        /// **鳴らすのは自分の打牌だけ**（2026-10-01 の指示）。
        /// 相手の打牌でも鳴らしていたが、自分が切る緊張を表す音なので、
        /// 両方で鳴ると打牌のたびに鳴り続けて意味が薄れる。
        /// </summary>
        private void PlayPlacedHeartbeat()
        {
            if (isEnemyRiver) return;

            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio != null) audio.PlayDiscardHeartbeat();
        }

        /// <summary>
        /// 牌を1枚置く前に呼ぶ。まだ出ていない牌なら、**記録だけ先に済ませて
        /// 段を上げるのは後回しにする**（光が着いてから上げるため）。
        /// すでに出ている牌なら、その場で段を落とす。
        ///
        /// **自分・相手の両方で動く**（2026-09-13 のユーザー指示）。
        /// 以前はチュートリアルの自分の打牌だけの試作だった。
        /// </summary>
        /// <returns>光を飛ばす牌なら true。</returns>
        private bool BeginVoltage(int tileId)
        {
            if (!KillingMahjong.Managers.VoltageSystem.IsNewTileType(tileId))
            {
                KillingMahjong.Managers.VoltageSystem.NotifyDiscard(isEnemyRiver, tileId);
                return false;
            }

            KillingMahjong.Managers.VoltageSystem.MarkSeen(tileId);
            return true;
        }

        /// <summary>
        /// 置いた牌からゲージへ光を飛ばし、**着いた瞬間に段を上げる。**
        /// ゲージが出ていない場面では光を飛ばせないので、その場で上げる。
        /// </summary>
        private void FinishVoltage(RectTransform rt)
        {
            bool enemy = isEnemyRiver;
            // **光が出せない場面でも同じ包みを通す。** 直に
            // `ApplyDiscardResult` を呼ぶと、そちらの経路だけ心音が鳴らなくなる。
            if (!VoltageTileFlightEffect.TryPlay(rt, enemy, () => ApplyVoltageWithHeartbeat(enemy)))
            {
                ApplyVoltageWithHeartbeat(enemy);
            }
        }

        /// <summary>
        /// ゲージへ反映し、**実際にポイントが増えたときだけ**心音を鳴らす。
        ///
        /// 増えたかどうかを前後で比べているのは、鳴らしてはいけない場合が
        /// 3つあるから。既出牌で途切れたとき（ここは `NotifyDiscard` 側を通るので
        /// そもそも来ない）、最大値（14点）に張り付いていて増えないとき、
        /// そして将来 `ApplyDiscardResult` の条件が増えたとき。
        /// **「呼んだから鳴らす」ではなく「増えたから鳴らす」にしておく。**
        ///
        /// 段が上がった回（2/5/9/14 に届いた回）だけ強い方にする。
        /// </summary>
        private static void ApplyVoltageWithHeartbeat(bool enemy)
        {
            int beforePoints = KillingMahjong.Managers.VoltageSystem.GetPoints(enemy);
            int beforeLevel = KillingMahjong.Managers.VoltageSystem.GetLevel(enemy);

            KillingMahjong.Managers.VoltageSystem.ApplyDiscardResult(enemy, true);

            int afterPoints = KillingMahjong.Managers.VoltageSystem.GetPoints(enemy);
            int afterLevel = KillingMahjong.Managers.VoltageSystem.GetLevel(enemy);

            if (afterPoints <= beforePoints) return;

            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio != null) audio.PlayVoltageHeartbeat(afterLevel > beforeLevel);
        }

        private void ApplyRiverLayout(RectTransform rt)
        {
            // 追加する牌は、いま入っている枚数がそのまま並び順になる
            ApplyRiverLayoutAt(rt, discardedTiles.Count);
        }

        private void ApplyRiverLayoutAt(RectTransform rt, int index)
        {
            int row = index / MaxPerRow;
            int col = index % MaxPerRow;

            float targetX = col * tileWidth;
            float targetY = -row * tileHeight; 
            
            if (isEnemyRiver)
            {
                targetX = -targetX;
                targetY = -targetY;
                
                targetX += enemyOffsetX;
                targetY += enemyOffsetY;
            }

            if (rt != null)
            {
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(targetX, targetY);
                rt.localScale = new Vector3(tileScale, tileScale, 1f);

                rt.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// 行ごとのSiblingIndex（描画順）を再設定する。
        /// 手前（自分）: 2列目が1列目より前面（上レイヤー）
        /// 奥（敵）: 2列目が1列目より背面（下レイヤー）
        /// </summary>
        private void UpdateSiblingOrder()
        {
            if (discardedTiles.Count == 0) return;

            List<List<Transform>> rows = new List<List<Transform>>();

            for (int i = 0; i < discardedTiles.Count; i++)
            {
                int rowIdx = i / MaxPerRow;
                while (rows.Count <= rowIdx)
                {
                    rows.Add(new List<Transform>());
                }
                rows[rowIdx].Add(discardedTiles[i]);
            }

            int sibIdx = 0;
            if (isEnemyRiver)
            {
                // 敵の河: 奥の行（rowIdxが大きい方）から先に描画する（背面にいく）
                // 3列目 -> 2列目 -> 1列目 の順で描画（1列目が一番上にくる）
                for (int i = rows.Count - 1; i >= 0; i--)
                {
                    foreach (var t in rows[i]) t.SetSiblingIndex(sibIdx++);
                }
            }
            else
            {
                // 自分の河: 手前の行（rowIdxが小さい方）から先に描画する（背面にいく）
                // 1列目 -> 2列目 -> 3列目 の順で描画（3列目が一番上にくる）
                for (int i = 0; i < rows.Count; i++)
                {
                    foreach (var t in rows[i]) t.SetSiblingIndex(sibIdx++);
                }
            }
        }

        /// <summary>
        /// 打牌数（「後：五打目」）の置き場所。チュートリアルで囲んで見せるのに使う（2026-10-08）。
        /// この河が打牌数を持っていなければ null。
        /// </summary>
        public RectTransform TurnTextRect
        {
            get { return turnText != null ? turnText.rectTransform : null; }
        }

        public void UpdateTurnText()
        {
            if (turnText != null)
            {
                int turnCount = discardedTiles.Count;
                int displayTurn = turnCount + 1; // これから打つ牌が何巡目か
                
                bool isFirst = KillingMahjong.Managers.BoardStateManager.Instance.IsLocalPlayerFirstRound;
                string prefix = isFirst ? "先：" : "後：";
                
                turnText.text = $"{prefix}{ToKanji(displayTurn)}打目";
                turnText.gameObject.SetActive(true);
            }
        }

        private string ToKanji(int number)
        {
            string[] kanjiNumbers = { "", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
            if (number <= 0) return "零";
            if (number <= 10) return kanjiNumbers[number];
            if (number < 20) return "十" + kanjiNumbers[number % 10];
            if (number < 100)
            {
                int tens = number / 10;
                int ones = number % 10;
                return kanjiNumbers[tens] + "十" + kanjiNumbers[ones];
            }
            return number.ToString(); // Fallback for >= 100
        }

        public void Clear()
        {
            // 局が切り替わって河が空になるので、ボルテージも履歴ごと捨てる。
            // 自分・相手の河は続けて Clear されるが、二度呼んでも害はない。
            KillingMahjong.Managers.VoltageSystem.ResetAll();

            var uiManager = FindFirstObjectByType<GameUIManager>();
            foreach (var t in discardedTiles)
            {
                if (t != null && uiManager != null && uiManager.VisualController != null)
                {
                    uiManager.VisualController.poolManager.ReturnTileToPool(t.gameObject);
                }
                else if (t != null)
                {
                    Destroy(t.gameObject);
                }
            }
            discardedTiles.Clear();
        }

        public void SetRiver(List<int> tileIds)
        {
            Clear();
            if (tileIds != null)
            {
                foreach(int tileId in tileIds)
                {
                    AddTile(tileId);
                }
            }
            UpdateTurnText();
        }
    }
}
