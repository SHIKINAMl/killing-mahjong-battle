using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;
using KillingMahjong.Managers;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 透視スキルの画面演出（2026-10-03）。Canva の提案スライド
    /// 「透視スキル ①発動／②1枚ずつ透視する／③フラッシュ／④もとに戻る」をそのまま組んだもの。
    ///
    /// 流れ:
    ///   ① 発動      … その瞬間の画面を1枚撮り、青くして周りに重ねる。
    ///                  敵の山牌以外を暗く落とし、集中線を山牌へ集める。BGMが沈む
    ///   ② 1枚ずつ    … 牌をめくるのは <see cref="ExposedTileEffectPlayer"/> 側。
    ///                  この演出は出たまま待っている
    ///   ③ フラッシュ … 白く光らせ、その瞬間に重ねと集中線を消す
    ///   ④ もとに戻る … 心音とともに BGM の沈みを抜く。めくった牌は見えたまま
    ///
    /// **シーンには置かない。** 呼ばれるたびに自前の Canvas を作り、終わったら自分を消す。
    /// 対局シーンが `UIテストシーン` と `OpeningScene` の2つあるので、シーンに置くと
    /// 片方にだけ入れる事故が起きる（<see cref="ScreenFlash"/> と同じ理由）。
    ///
    /// **AI画像生成は使っていない。** 出しているのは実機の画面そのものの複製と、
    /// 頂点色で描いた図形だけ。
    /// </summary>
    public class PerspectiveSkillEffect : MonoBehaviour
    {
        // ------------------------------------------------------------
        //  重ねる位置。画面の幅・高さに対する割合で持つ
        //
        //  **主画面は縮めない。** 原寸のまま少しずつずらして重ねるので、
        //  画面の大きさは変わらず、外側だけが青く覆われて見える。
        // ------------------------------------------------------------
        private static readonly Vector2[] GhostOffsets =
        {
            new Vector2(-0.10f,  0.07f),
            new Vector2( 0.10f,  0.07f),
            new Vector2( 0.00f, -0.11f),
            new Vector2(-0.07f, -0.05f),
            new Vector2( 0.07f, -0.05f),
        };

        /// <summary>
        /// 重ねる絵の濃さ。**色は付けない。**
        ///
        /// UI の色は元の絵との**乗算**なので、ここで青を入れても「青くする」ことはできず、
        /// 青以外が削られて暗くなるだけになる（2026-10-03 に実機で確認。
        /// 元の盤面が暗いので、重ねたのがほとんど見えなくなっていた）。
        /// 青へ寄せるのは <see cref="TintBlue"/> が撮った絵そのものに対して行う。
        /// </summary>
        private static readonly Color GhostTint = new Color(1f, 1f, 1f, 0.62f);

        // 撮った画面を青へ寄せる式の係数。意味は <see cref="TintBlue"/> に書いた
        private const float TintMulR = 0.10f, TintAddR = 16f;
        private const float TintMulG = 0.14f, TintAddG = 32f;
        private const float TintMulB = 0.22f, TintAddB = 96f;

        /// <summary>
        /// 青を抜く楕円の何倍の所で、青が乗りきるか。
        /// 小さいほど急に青くなり、画面の周りがはっきり青く見える。
        /// </summary>
        private const float GhostOuterScale = 1.55f;

        /// <summary>出きるまでの秒数。</summary>
        public const float EnterDuration = 0.45f;

        /// <summary>フラッシュの長さ。<see cref="ScreenFlash"/> の短い合図より少し長くする。</summary>
        private const float FlashDuration = 0.35f;

        /// <summary>心音3拍の間合い（秒）。「どくっ　どくっ　どく」。SE素材が無いときの代替。</summary>
        private static readonly float[] HeartbeatGaps = { 0.00f, 0.42f, 0.36f };

        /// <summary>
        /// 白フラッシュの「さーーーーっ」から、心音3拍を鳴らし始めるまでの間（秒）。
        /// 受け取った連結プレビュー（`clairvoyance_sequence_preview_v1.wav`）を測ると、
        /// 抜ける音が 0.00 秒、最初の「どくっ」が 0.95 秒だった。
        /// </summary>
        private const float FlashToHeartbeat = 0.95f;

        /// <summary>
        /// 心音と一緒に BGM を戻すのにかける秒数。
        /// 心音の素材は 1.84 秒・3拍なので、だいたい最後の拍で戻りきる長さにする。
        /// </summary>
        private const float BgmReturnDuration = 1.7f;

        private RectTransform _root;
        private Texture2D _shot;
        private PerspectiveGhostLayer[] _ghosts;
        private PerspectiveDarkenLayer _darken;
        private PerspectiveFocusLines _lines;
        private bool _released;

        /// <summary>演出の入れ物を作る。まだ何も出さない。</summary>
        public static PerspectiveSkillEffect Create()
        {
            if (!Application.isPlaying) return null;

            var go = new GameObject("PerspectiveSkillEffect");
            return go.AddComponent<PerspectiveSkillEffect>();
        }

        /// <summary>
        /// ①発動。画面を撮って、青い重ね・暗落とし・集中線を出す。BGMを沈める。
        /// </summary>
        /// <param name="focusScreenRect">
        /// 敵の山牌の画面上の範囲（px）。集中線の集まる先と、暗く落とさない穴に使う。
        /// 空なら画面中央あたりを使う。
        /// </param>
        public IEnumerator Enter(Rect focusScreenRect)
        {
            // **撮るのはフレームの終わりでなければならない。** 途中で呼ぶと
            // 描き終わっていない画面が返る。重ねが真っ黒になって原因が見えにくい
            yield return new WaitForEndOfFrame();

            Texture2D captured = ScreenCapture.CaptureScreenshotAsTexture();
            if (captured != null)
            {
                _shot = TintBlue(captured);
                Destroy(captured);
            }

            Build(focusScreenRect);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetBgmDeepMuffle(true);

                // 集中しているあいだ、深く沈んだ音を流し続ける。
                // BGMのこもりだけだと「沈んだ」が音として立たなかった
                AudioManager.Instance.StartClairvoyanceLoop(EnterDuration);
            }

            float t = 0f;
            while (t < EnterDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / EnterDuration);

                // 端をなめらかに。パッと出すと「絵が差し替わった」ように見える
                float eased = u * u * (3f - 2f * u);
                ApplyStrength(eased);

                yield return null;
            }
            ApplyStrength(1f);
        }

        /// <summary>
        /// ③フラッシュ → ④もとに戻る。光った瞬間に重ねと集中線を消し、
        /// 心音とともに BGM の沈みを抜く。終わったら自分を片付ける。
        /// </summary>
        public IEnumerator Release()
        {
            if (_released) yield break;
            _released = true;

            var audio = AudioManager.Instance;
            bool hasSe = audio != null && audio.HasClairvoyanceSe;

            // **光ると同時に、集中が抜ける音を鳴らして沈んだループを切る。**
            // ループを先に切ると、無音の一拍があってから光ることになる
            // 透視解除は専用の「さーーーーっ」を持つため、共通のキーンと重ねない。
            ScreenFlash.Play(FlashDuration, 0.85f, playSound: !hasSe);
            if (audio != null)
            {
                audio.StopClairvoyanceLoop();
                if (hasSe) audio.PlayClairvoyanceFlashReturn();
            }

            // 光が乗りきってから消す。先に消すと、戻った画面が一瞬だけ見えてしまう
            yield return new WaitForSeconds(0.06f);
            ApplyStrength(0f);

            if (hasSe)
            {
                // 抜ける音が鳴っているあいだは待つ。素材の連結プレビューで
                // 「さー」から最初の「どくっ」までが 0.95 秒だった
                yield return new WaitForSeconds(FlashToHeartbeat - 0.06f);
                audio.PlayClairvoyanceHeartbeat();
                audio.SetBgmDeepMuffle(false, BgmReturnDuration);
                yield return new WaitForSeconds(BgmReturnDuration);
            }
            else
            {
                // SE素材が挿さっていないときの段取り。合成の心音で間に合わせる
                if (audio != null) audio.SetBgmDeepMuffle(false);

                var strengths = new[]
                {
                    HeartbeatStrength.Medium,
                    HeartbeatStrength.Medium,
                    HeartbeatStrength.Strong,
                };
                for (int i = 0; i < strengths.Length; i++)
                {
                    if (HeartbeatGaps[i] > 0f) yield return new WaitForSeconds(HeartbeatGaps[i]);
                    if (audio != null) audio.PlayHeartbeat(strengths[i], HeartbeatSpacing.Compact);
                }
                yield return new WaitForSeconds(0.25f);
            }

            Dispose();
        }

        /// <summary>途中で止めたいときに。音も画面も元へ戻す。</summary>
        public void Dispose()
        {
            if (this == null || gameObject == null) return;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnDisable() { RestoreAudio(); }

        /// <summary>
        /// **BGM を戻すのはここでやる。** 途中でシーンが変わっても、
        /// 対局をやめても、この入れ物が消えれば必ず沈みが抜ける。
        /// <see cref="Release"/> の中だけで戻していると、演出の途中で
        /// 画面を離れたときに BGM がこもったまま残る。
        /// </summary>
        private void OnDestroy()
        {
            RestoreAudio();
            if (_shot != null)
            {
                Destroy(_shot);
                _shot = null;
            }
        }

        private void RestoreAudio()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetBgmDeepMuffle(false);
                AudioManager.Instance.StopClairvoyanceLoop();
            }

        }

        // ------------------------------------------------------------

        /// <summary>
        /// 撮った画面そのものを青へ寄せる。
        ///
        /// **色相を回すのではなく、青の板と混ぜる。** ドット絵なので、
        /// 色数が増えすぎない混ぜ方のほうが元の形が残る。混ぜたあと少し暗くして、
        /// 主画面より後ろに見えるようにする。
        ///
        /// **寄せる先は水色ではなく、濃い青（2026-10-07 のユーザー指摘「もっと青く」）。**
        /// 前の式（r' = 0.164r + 31 ／ g' = 0.173g + 77 ／ b' = 0.194b + 175）は
        /// 緑が多くて水色に寄り、上から掛ける黒と合わさって灰色がかって見えていた。
        /// 提案の絵の周りを測ると、集中線を除いて (33, 50, 105) あたり。
        /// 青に対して赤が 0.3、緑が 0.5 ほどしか無い。それに合わせたのが今の式:
        ///
        ///     r' = TintMulR·r + TintAddR （緑・青も同じ形）
        ///
        /// **青くした絵は、新しく作ったテクスチャに入れて返す（2026-10-07）。**
        /// `CaptureScreenshotAsTexture` が返すテクスチャにそのまま書き戻すと、
        /// このプロジェクト（リニア色空間）では中身が「リニアの値」として読まれ、
        /// 画面に出るときにもう一度明るく持ち上げられる。式で (29, 48, 112) を
        /// 狙っても実機では (93, 108, 164) の白っぽい水色になっていた。
        /// 「青が薄い」の本当の原因はこれだった。sRGB として作ったテクスチャなら、
        /// 入れた色がそのまま画面に出る。
        ///
        /// 1920x1080 で約 200 万画素ぶん回す。カットインの直後に1回だけなので
        /// ここで止まっても対局の操作には掛からないが、**毎フレームやらないこと。**
        /// </summary>
        /// <returns>青くした絵。読めなかったときは null（重ねを出さずに演出は続ける）。</returns>
        private static Texture2D TintBlue(Texture2D tex)
        {
            Color32[] pixels;
            try
            {
                pixels = tex.GetPixels32();
            }
            catch (UnityException e)
            {
                Debug.LogWarning("[Perspective] 撮った画面を読めませんでした: " + e.Message);
                return null;
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                c.r = (byte)(c.r * TintMulR + TintAddR);
                c.g = (byte)(c.g * TintMulG + TintAddG);
                c.b = (byte)(c.b * TintMulB + TintAddB);
                pixels[i] = c;
            }

            // 最後の引数 linear: false が「sRGB として扱う」の指定
            var tinted = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false, false);
            tinted.wrapMode = TextureWrapMode.Clamp;
            tinted.SetPixels32(pixels);
            tinted.Apply(false, true);
            return tinted;
        }

        private void Build(Rect focusScreenRect)
        {
            float w = Screen.width;
            float h = Screen.height;
            float minSide = Mathf.Min(w, h);

            if (focusScreenRect.width <= 1f || focusScreenRect.height <= 1f)
            {
                // 山牌の場所が取れなかったときの逃げ道。画面の上寄り中央を狙う
                focusScreenRect = new Rect(w * 0.28f, h * 0.60f, w * 0.44f, h * 0.10f);
            }

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UISortingOrders.PerspectiveOverlay;

            // GraphicRaycaster は**わざと付けない**。付けると演出中だけ全画面の
            // クリックを吸ってしまう（ScreenFlash と同じ）
            var rootGo = new GameObject("Overlay", typeof(RectTransform));
            _root = (RectTransform)rootGo.transform;
            _root.SetParent(transform, false);
            Stretch(_root);

            // 画面座標 → このレイヤーのローカル座標。Overlay なので中心が原点
            Vector2 center = new Vector2(w * 0.5f, h * 0.5f);
            Vector2 wallCenter = focusScreenRect.center - center;
            Vector2 wallExtent = focusScreenRect.size * 0.5f;

            // 青を抜く穴。画面中央と山牌の間に置き、山牌がすっぽり入る大きさにする。
            // **中央に固定してはいけない。** 16:9 など縦が短い画面では山牌が
            // 穴からはみ出して、めくった牌が青くかぶってしまう。
            //
            // **丸ではなく楕円で抜く（2026-10-03）。** 丸だと左右が先に青くなって
            // 上下が残り、「画面の周りが青い」に見えなかった（ユーザー指摘）。
            // 縦をきつめに取ると、上下の帯もちゃんと青くなる
            //
            // **横も欲張らない（2026-10-07）。** 横半径を広く取っていたときは、
            // 青が乗りきる所が画面の外に出てしまい、左右の端が薄いままだった
            Vector2 holeCenter = Vector2.Lerp(Vector2.zero, wallCenter, 0.45f);
            //
            // 縦は少しだけ広げた。青が濃くなったぶん、手牌の下の段の端が
            // 読めなくなっていたため（提案の絵でも手牌は青の外にある）
            Vector2 ghostHole = new Vector2(minSide * 0.30f + wallExtent.x * 0.55f,
                                            minSide * 0.34f);

            // 山牌のまわりだけ残して暗く落とす。横に長く縦に薄いので横長の楕円で抜く。
            //
            // **青くない所もはっきり暗くする（2026-10-03 のユーザー指摘
            // 「普段の画面全体ももう少し暗く」）。** 楕円を小さめにして
            // 暗さが山牌の近くから立ち上がるようにし、いちばん暗い所も濃くした
            _darken = NewGraphic<PerspectiveDarkenLayer>("Darken");
            _darken.color = new Color(0f, 0f, 0.02f, 1f);
            _darken.CenterLocal = wallCenter;
            _darken.RadiusX = wallExtent.x + w * 0.06f;
            _darken.RadiusY = wallExtent.y + h * 0.10f;
            _darken.MaxAlpha = 0.72f;
            _darken.Strength = 0f;

            // **青い重ねは、暗落としの上に乗せる（2026-10-07）。**
            // 逆だと青の上から黒が掛かって、青が濁って灰色に見える。
            // 青の濃さは <see cref="TintBlue"/> の式だけで決まるようにしておく
            BuildGhosts(w, h, holeCenter, ghostHole);

            // 集中線。**空ける穴は山牌に沿った横長の楕円にする。**
            // 丸で空けると、横に長い山牌の上下だけ線が遠くなって締まらない。
            //
            // **縦は広めに取ること。** 山牌のすぐ上に相手のキャラが居るので、
            // 山牌にぴったり沿わせると線が顔を塗りつぶしてしまう
            // （2026-10-03、録画で確認。相手がまったく見えなくなった）
            _lines = NewGraphic<PerspectiveFocusLines>("FocusLines");
            Vector2 lineHole = new Vector2(wallExtent.x * 1.05f + minSide * 0.10f,
                                           wallExtent.y + minSide * 0.33f);
            _lines.Setup(wallCenter, lineHole, w / 800f);
            _lines.Progress = 0f;
        }

        private void BuildGhosts(float w, float h, Vector2 holeCenter, Vector2 hole)
        {
            _ghosts = new PerspectiveGhostLayer[GhostOffsets.Length];
            if (_shot == null) return;

            for (int i = 0; i < GhostOffsets.Length; i++)
            {
                var ghost = NewGraphic<PerspectiveGhostLayer>("Ghost" + i);
                ghost.texture = _shot;
                ghost.color = GhostTint;

                Vector2 offset = new Vector2(GhostOffsets[i].x * w, GhostOffsets[i].y * h);
                ghost.rectTransform.anchoredPosition = offset;

                // 穴は**画面に対して**空ける。板をずらした分だけ、穴も逆にずらす
                ghost.CenterLocal = holeCenter - offset;
                ghost.RadiusX = hole.x;
                ghost.RadiusY = hole.y;
                ghost.OuterScale = GhostOuterScale;
                ghost.Strength = 0f;

                _ghosts[i] = ghost;
            }
        }

        private T NewGraphic<T>(string name) where T : MaskableGraphic
        {
            // CanvasRenderer は自分で付ける。new GameObject 経由だと RequireComponent が
            // 効かず、絵が一切出ない（TileSparkleEffect / VoltageFlame と同じ）
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_root, false);
            Stretch(rect);

            var graphic = go.GetComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private void ApplyStrength(float strength)
        {
            if (_ghosts != null)
            {
                foreach (var ghost in _ghosts)
                {
                    if (ghost != null) ghost.Strength = strength;
                }
            }
            if (_darken != null) _darken.Strength = strength;
            if (_lines != null) _lines.Progress = strength;
        }

        // ------------------------------------------------------------

        /// <summary>
        /// RectTransform の並びから、画面上の囲み（px）を求める。
        /// 集中線の集まる先と、暗く落とさない穴を**実際の牌の位置**から決めるために使う。
        /// 解像度や画面比が変わっても付いてくる。
        /// </summary>
        public static Rect GetScreenRect(System.Collections.Generic.IList<RectTransform> targets)
        {
            if (targets == null || targets.Count == 0) return new Rect();

            float xMin = float.MaxValue, yMin = float.MaxValue;
            float xMax = float.MinValue, yMax = float.MinValue;
            var corners = new Vector3[4];
            bool any = false;

            foreach (var rt in targets)
            {
                if (rt == null) continue;

                Canvas canvas = rt.GetComponentInParent<Canvas>();
                Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    ? canvas.worldCamera
                    : null;

                rt.GetWorldCorners(corners);
                for (int i = 0; i < 4; i++)
                {
                    Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
                    if (sp.x < xMin) xMin = sp.x;
                    if (sp.y < yMin) yMin = sp.y;
                    if (sp.x > xMax) xMax = sp.x;
                    if (sp.y > yMax) yMax = sp.y;
                    any = true;
                }
            }

            if (!any) return new Rect();
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
