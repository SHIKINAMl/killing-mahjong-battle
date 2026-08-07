using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.Editor
{
    /// <summary>
    /// タイトル画面「じゃんぱいあ」を組み直すエディタ拡張。
    ///
    /// 構図はウディコン18の「あくま1/2」を参考にしている:
    /// キャラを大きく左へ寄せて画面外へ切れさせ、右上に装飾的なロゴ、右下にメニュー。
    ///
    /// **既存オブジェクトは作り直さず、位置と見た目だけを変える。**
    /// ボタンの onClick は TitleUIManager へシリアライズ済みなので、
    /// 作り直すと配線が消えて「押しても何も起きない」状態になる。
    ///
    /// 何度実行しても同じ結果になるようにしてある（名前で find-or-create）。
    /// 数値の調整は下の定数だけを触ればよい。
    /// </summary>
    public static class TitleScreenBuilder
    {
        // ---- 調整用の定数 ----

        private const string TitleText = "じゃんぱいあ";

        /// <summary>キャラの拡大率と位置。1を超えると画面外へ切れる（あくま1/2 の寄せ方）</summary>
        private static readonly float CharScale = 1.55f;
        private static readonly Vector2 CharPos = new Vector2(-215f, -150f);

        /// <summary>ロゴの位置・大きさ・傾き</summary>
        // 画面は 800x600。中央が原点なので右端は x=400。
        // 6文字 x LogoFontSize がはみ出さないよう、位置と幅は合わせて動かすこと
        private static readonly Vector2 LogoPos = new Vector2(150f, 170f);
        private static readonly Vector2 LogoSize = new Vector2(470f, 150f);
        private const float LogoFontSize = 70f;
        private const float LogoTilt = -4f;

        /// <summary>ロゴの縦グラデーション（上が白、下が深紅）</summary>
        private static readonly Color LogoTop = new Color32(255, 255, 255, 255);
        private static readonly Color LogoBottom = new Color32(214, 40, 62, 255);
        private static readonly Color LogoOutline = new Color32(26, 6, 12, 255);

        /// <summary>背景を落とす幕。右側ほど濃くしてロゴとメニューを読ませる</summary>
        private static readonly Color ScrimLeft = new Color(0f, 0f, 0f, 0.15f);
        private static readonly Color ScrimRight = new Color(0f, 0f, 0f, 0.72f);

        /// <summary>メニュー項目の色</summary>
        private static readonly Color MenuText = new Color32(240, 232, 236, 255);
        private static readonly Color MenuMarker = new Color32(214, 40, 62, 255);

        private const string SparkleAssetPath = "Assets/Resources/Title/TitleSparkle.png";

        [MenuItem("Tools/UI/タイトル画面を組み直す（じゃんぱいあ）")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("タイトル画面",
                    "Canvas が見つかりません。タイトルシーンを開いてから実行してください。", "OK");
                return;
            }

            var root = canvas.transform;
            var bg = root.Find("タイトル絵");
            var girl = root.Find("女の子");
            var silhouette = root.Find("女の子_Silhouette (白フチ)");
            var menu = root.Find("ボタン達");

            if (girl == null || menu == null)
            {
                EditorUtility.DisplayDialog("タイトル画面",
                    "「女の子」または「ボタン達」が見つかりません。開いているシーンを確認してください。", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "タイトル画面を組み直す");

            // ロゴのフォントはパスで決め打ちせず、既存のメニュー文字から借りる。
            // PixelMplus10_DynamicFixed という名前のアセットが複数あるうえ、
            // 指定を忘れると TMP 既定の LiberationSans になり、ひらがなが全部豆腐になる。
            var font = FindSceneFont(menu);
            if (font == null)
            {
                EditorUtility.DisplayDialog("タイトル画面",
                    "メニューから日本語フォントを見つけられませんでした。", "OK");
                return;
            }

            var sparkle = EnsureSparkleSprite();
            var scrim = BuildScrim(root);
            BuildCharacter(girl, silhouette);
            var logo = BuildLogo(root, font, sparkle);
            RestyleMenu(menu);

            // 描画順を決め直す。キャラを大きくするとボタンに被るため、
            // ボタンとロゴはキャラより後ろの兄弟（＝手前）に置く必要がある。
            int order = 0;
            if (bg != null) bg.SetSiblingIndex(order++);
            scrim.SetSiblingIndex(order++);
            if (silhouette != null) silhouette.SetSiblingIndex(order++);
            girl.SetSiblingIndex(order++);
            logo.SetSiblingIndex(order++);
            menu.SetSiblingIndex(order++);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[TitleScreenBuilder] タイトル画面を組み直しました。シーンを保存してください。");
        }

        // ------------------------------------------------------------------

        /// <summary>背景を落とす幕。左から右へ濃くなる横グラデーション。</summary>
        private static RectTransform BuildScrim(Transform root)
        {
            var go = FindOrCreate(root, "TitleScrim");
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = Ensure<Image>(go);
            img.sprite = null;
            img.color = Color.white;
            img.raycastTarget = false;

            // Image 単体では横グラデーションが作れないので、頂点色を書き換える小さな部品を付ける
            var grad = Ensure<UI.HorizontalGradient>(go);
            grad.left = ScrimLeft;
            grad.right = ScrimRight;
            img.SetVerticesDirty();   // 頂点色を作り直させる（Graphic 側のAPI）

            return rt;
        }

        /// <summary>キャラを大きくして左へ寄せる。シルエット（白フチ）も必ず同じ値にする。</summary>
        private static void BuildCharacter(Transform girl, Transform silhouette)
        {
            var rt = girl as RectTransform;
            rt.anchoredPosition = CharPos;
            rt.localScale = Vector3.one * CharScale;

            if (silhouette != null)
            {
                var srt = silhouette as RectTransform;
                srt.anchoredPosition = CharPos;
                // 白フチは本体より少しだけ大きいことでフチになる。元の比率を保つ
                srt.localScale = Vector3.one * (CharScale * 1.03f);
            }
        }

        /// <summary>シーンで実際に使われている日本語フォントを1つ拾う。</summary>
        private static TMP_FontAsset FindSceneFont(Transform menu)
        {
            var label = menu.GetComponentInChildren<TextMeshProUGUI>(true);
            return label != null ? label.font : null;
        }

        /// <summary>右上の装飾ロゴ。TMP の輪郭＋縦グラデーション＋周囲のきらめき。</summary>
        private static RectTransform BuildLogo(Transform root, TMP_FontAsset font, Sprite sparkle)
        {
            var go = FindOrCreate(root, "TitleLogo");
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = LogoPos;
            rt.sizeDelta = LogoSize;
            rt.localRotation = Quaternion.Euler(0f, 0f, LogoTilt);

            var tmp = Ensure<TextMeshProUGUI>(go);
            tmp.font = font;
            tmp.text = TitleText;
            tmp.fontSize = LogoFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;

            // 縦グラデーションは頂点色なのでコンポーネント側に持たせられる
            tmp.enableVertexGradient = true;
            tmp.colorGradient = new VertexGradient(LogoTop, LogoTop, LogoBottom, LogoBottom);

            // 輪郭と影はマテリアル側。共有マテリアルを直接触ると
            // 同じフォントを使う他の文字まで太くなるので、必ず専用のマテリアルを作る
            tmp.fontSharedMaterial = EnsureLogoMaterial(tmp.font);

            BuildSparkles(rt, sparkle);
            return rt;
        }

        /// <summary>ロゴ専用の TMP マテリアル（輪郭＋落ち影）をアセットとして用意する。</summary>
        private static Material EnsureLogoMaterial(TMP_FontAsset font)
        {
            const string dir = "Assets/Resources/Title";
            const string path = dir + "/JanpaiaTitle.mat";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // フォントのマテリアルから作り直す。アトラスの寸法や _GradientScale など
            // フォント固有の値が多く、テクスチャだけ差し替えても正しく描けないため、
            // 既存アセットがあっても中身を丸ごと上書きする。
            var fresh = new Material(font.material);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = fresh;
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                EditorUtility.CopySerialized(fresh, mat);
                Object.DestroyImmediate(fresh);
                // CopySerialized は名前まで複製してしまう。
                // インスペクタで元のフォントマテリアルと見分けが付かなくなるので戻す
                mat.name = "JanpaiaTitle";
            }

            // PixelMplus は線が細いので、輪郭を太くすると文字の面が食い潰されて
            // ほぼ輪郭色（＝ほぼ黒）の塊になる。面を少し太らせたうえで輪郭は薄く乗せる
            mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.10f);
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.12f);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, LogoOutline);

            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.85f));
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.15f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.35f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.25f);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.20f);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>ロゴの周りに散らすきらめき。位置は決め打ちで、あくま1/2 のように非対称に置く。</summary>
        private static void BuildSparkles(RectTransform logo, Sprite sparkle)
        {
            if (sparkle == null)
            {
                // sprite が null の Image は白い四角として描かれてしまう。
                // 中途半端に出すより、出さずに気づける方がよい
                Debug.LogWarning("[TitleScreenBuilder] きらめきのスプライトを取得できませんでした。"
                                 + "もう一度メニューから実行すると、生成済みのアセットを拾えます。");
                return;
            }

            // (x, y, 大きさ, 透明度)
            var spots = new Vector4[]
            {
                new Vector4(-205f,  52f, 34f, 1.00f),
                new Vector4( 196f,  40f, 26f, 0.85f),
                new Vector4( 150f, -52f, 20f, 0.70f),
                new Vector4(-160f, -50f, 16f, 0.60f),
                new Vector4(  15f,  66f, 14f, 0.55f),
            };

            for (int i = 0; i < spots.Length; i++)
            {
                var go = FindOrCreate(logo, "Sparkle" + i);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(spots[i].x, spots[i].y);
                rt.sizeDelta = new Vector2(spots[i].z, spots[i].z);
                rt.localRotation = Quaternion.Euler(0f, 0f, i * 17f);

                var img = Ensure<Image>(go);
                img.sprite = sparkle;
                img.color = new Color(1f, 1f, 1f, spots[i].w);
                img.raycastTarget = false;
            }
        }

        /// <summary>
        /// メニューを文字だけの見た目にする。
        /// 黒い長方形は背景の作り込みから浮くので消し、選択中を示す印を左に置く。
        /// **Button コンポーネント自体は残す**（onClick の配線を守るため）。
        /// </summary>
        private static void RestyleMenu(Transform menu)
        {
            for (int i = 0; i < menu.childCount; i++)
            {
                var child = menu.GetChild(i);
                var btn = child.GetComponent<Button>();
                if (btn == null) continue;   // MenuGap のような詰め物は触らない

                var img = child.GetComponent<Image>();
                if (img != null)
                {
                    // 当たり判定は残したいので、消さずに透明にする
                    img.color = new Color(0f, 0f, 0f, 0f);
                    img.raycastTarget = true;
                }

                var label = child.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    label.color = MenuText;
                    label.alignment = TextAlignmentOptions.Left;
                    label.margin = new Vector4(26f, 0f, 0f, 0f);
                    label.fontStyle = FontStyles.Bold;
                }

                // 左の印。ホバー時に出す想定だが、まずは常時薄く出して位置を確かめる
                var marker = FindOrCreate(child, "Marker");
                var mrt = marker.GetComponent<RectTransform>();
                mrt.anchorMin = new Vector2(0f, 0.5f);
                mrt.anchorMax = new Vector2(0f, 0.5f);
                mrt.pivot = new Vector2(0f, 0.5f);
                mrt.anchoredPosition = new Vector2(4f, 0f);
                mrt.sizeDelta = new Vector2(10f, 10f);
                mrt.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var mimg = Ensure<Image>(marker);
                mimg.sprite = null;
                mimg.color = MenuMarker;
                mimg.raycastTarget = false;
            }
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// きらめき用の4方向スターを PNG アセットとして作る。
        /// シーンに残す必要があるので、実行時生成ではなく本物のアセットにする。
        /// </summary>
        private static Sprite EnsureSparkleSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SparkleAssetPath);
            if (existing != null) return existing;

            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float sx = (x - c) / c;
                    float sy = (y - c) / c;
                    float dx = Mathf.Abs(sx);
                    float dy = Mathf.Abs(sy);
                    float r = Mathf.Sqrt(sx * sx + sy * sy);

                    // 先細りの4方向スター。太さも明るさも先端へ向かって落とす
                    float h = Spike(dx, dy);
                    float v = Spike(dy, dx);
                    float core = Mathf.Clamp01(1f - r * 4.5f);
                    float a = Mathf.Clamp01(Mathf.Max(h, v) + core);

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();

            var dir = Path.GetDirectoryName(SparkleAssetPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(SparkleAssetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(SparkleAssetPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SparkleAssetPath);
            importer.textureType = TextureImporterType.Sprite;
            // これを設定しないと Sprite のサブアセットが作られず、
            // LoadAssetAtPath<Sprite> が null を返し続ける（textureType だけでは足りない）
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            // 圧縮すると細いトゲがにじむ。64x64 と小さいので非圧縮のままでよい。
            // ドット絵に合わせて拡大は Point にする
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();

            // 作った直後は取得できないことがある。取れるまで待たないと
            // sprite が null のまま Image に入り、既定の「白い四角」が描かれる
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<Sprite>(SparkleAssetPath);
        }

        private static float Spike(float along, float across)
        {
            if (along >= 1f) return 0f;
            float halfWidth = Mathf.Max(0.13f * Mathf.Pow(1f - along, 1.1f), 0.012f);
            return Mathf.Clamp01(1f - across / halfWidth) * Mathf.Pow(1f - along, 0.6f);
        }

        private static GameObject FindOrCreate(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }
    }
}
