# 【対局プロシージャル漫画生成エンジン】設計・評価報告書
**プロジェクト**: Killing Mahjong Battle「じゃんぱいあ」  
**専門領域**: プロシージャル・コミック・ジェネレーション / 劇画調レンダリング / ナラティブ・アーカイブ / SNSバイラル拡散  
**対象**: 対局終了時の勝負分岐点（クライマックス・ブラフ・大逆転）を1ページの劇画調Web漫画（マンガコマ割り・フキダシ・効果線）としてプロシージャル自動生成・画像出力するシステム

---

## 1. エグゼクティブ・サマリー

対戦ゲームにおける「勝敗の記憶」は、通常テキストの戦績ログや数値データとして保存されますが、プレイヤーの脳裏に残る「あの時の一打」「相手のブラフを見抜いた瞬間」「大逆転の恐怖と快楽」という**ナラティブ（劇的文脈）**を最も感情豊かに保存・共有できるメディアは**「日本のマンガ（劇画）」**の表現形式です。

本報告書では、対局中の全イベントログ（打牌・スキル・思考時間・HP変動・Live2D感情・和了）を解析し、勝負の決定的瞬間を**「伝統的劇画マンガの文法（変則多角形コマ割り・集中線・描き文字オノマトペ・フキダシ・白黒スクリーントーン）」**へと自動変換して1枚の縦型画像（PNG/WebP）として出力する**「対局プロシージャル漫画生成エンジン（Procedural Manga Generator）」**のアーキテクチャを定義・設計します。

---

## 2. 漫画生成パイプライン・アーキテクチャ

```
[ 対局終了 (VictoryUI / DefeatUI) ]
         │ (全対局イベントログ・タイムスタンプ・感情データ)
         ▼
[ Step 1: ドラマツルギー解析器 (Narrative Dramaturgy Parser) ]
 ├── 局面の感情テンション曲線 (Tension Curve) 算出
 └── 4大ハイライトシーン選定 (起・承・転・結)
         │
         ▼
[ Step 2: 動的コマ割りレイアウター (Dynamic Panel Layout Engine) ]
 ├── 黄金比・白銀比に基づく変則多角形コマ割り (Voronoi / BSP分割)
 └── 視線誘導（Zの法則 / 右上➔左下）の最適化
         │
         ▼
[ Step 3: 劇画レンダリング＆マテリアル合成 (Gekiga Rendering Engine) ]
 ├── 3D/2Dアセットの線画抽出 (Sobel Edge + Kuwahara Filter)
 ├── プロシージャル・スクリーントーン (SDF Halftone Dot Shader)
 └── 動的効果線（集中線・流線・スピード線）生成
         │
         ▼
[ Step 4: フキダシ＆描き文字オノマトペ合成 (Lettering & Onomatopoeia) ]
 ├── キャラクターセリフの自動選定・フォント最適化
 └── 衝撃度に連動した巨大描き文字（「ドォォン！」「ゴゴゴ…」）配置
         │
         ▼
[ 1ページ完成漫画 (1080x1920 PNG/WebP) ] ──► [ SNS共有 / プレイヤーアーカイブ ]
```

---

## 3. ドラマツルギー解析と4大コマ構成（起・承・転・結）

対局ログから以下のアルゴリズムで最もドラマ性の高い4つの瞬間を抽出し、標準4〜5コマの1ページ漫画を構築します。

| コマ | 劇的役割 | 抽出トリガー条件 | マンガ演出・構図 |
| :--- | :--- | :--- | :--- |
| **第1コマ (起)** | **対峙・命の賭託** | 開幕ベット額決定、またはオールイン宣言時 | **【引きの構図】** 薄暗い地下賭場、対峙する2人。重苦しい空気（縦ベタ・点描トーン）。フキダシ:「命を賭ける覚悟はあるのかしら…」 |
| **第2コマ (承)** | **疑心・ブラフの応酬** | 危険牌ホバー、スキル（透視・強襲）発動、長考時 | **【斜め分割コマ】** サキュバスの妖しい冷笑アップ ＆ プレイヤーの冷や汗。効果線:「ざわ…ざわ…」 |
| **第3コマ (転)** | **運命の打牌・決断** | 放銃牌の手離れ、またはアタリ牌ツモの瞬間 | **【鋭角の突き刺しコマ】** 牌を叩きつける手元の超クローズアップ。強烈な放射状集中線（Speedlines）。 |
| **第4コマ (結)** | **爆砕和了・勝負決着** | ロン成立、役満コール、HP全損KOの瞬間 | **【最大面積の大ゴマ（画面下半分）】** 勝者の圧倒的ドヤ顔/敗者の崩壊 ＋ 役満手牌 ＋ 巨大描き文字「ロンッ！！」「倍満 16000！！」 |

---

## 4. 劇画調レンダリング＆プロシージャル要素技術

### 4.1 プロシージャル・集中線＆流線ジェネレータ
数式により、任意の消失点 $(x_0, y_0)$ から放射状に伸びる劇画集中線をGPU上で直接描画：

```hlsl
// プロシージャル集中線シェーダー (HLSL)
float ProceduralSpeedlines(float2 uv, float2 center, float time, float density, float innerRadius) {
    float2 d = uv - center;
    float r = length(d);
    float angle = atan2(d.y, d.x);
    
    // 極座標での高周波ノイズ
    float lineNoise = frac(sin(floor(angle * density)) * 43758.5453);
    float lineMask = step(0.5, lineNoise);
    
    // 内側マスク（中心部をくり抜いてキャラクターの顔を見せる）
    float radiusMask = smoothstep(innerRadius, innerRadius + 0.15, r);
    
    // 線の鋭利なフェード
    float stroke = sin(angle * density * 2.0) * 0.5 + 0.5;
    return stroke * lineMask * radiusMask;
}
```

### 4.2 ハーフトーン・スクリーントーン（Halftone Dot SDF）
グレースケール輝度値 $L \in [0, 1]$ を、印刷物特有のドット網点（印刷角度45度）へ変換：

$$r_{\text{dot}} = \sqrt{1.0 - L} \cdot \frac{\sqrt{2}}{2}$$

---

## 5. C# 漫画生成エンジン実装 (`ProceduralMangaDirector.cs`)

```csharp
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;

namespace KillingMahjong.Manga
{
    public class ProceduralMangaDirector : MonoBehaviour
    {
        [Header("Manga Render Canvas (1080x1920)")]
        [SerializeField] private Canvas mangaCanvas;
        [SerializeField] private Camera mangaOffscreenCam;
        [SerializeField] private RenderTexture mangaRenderTexture;

        [Header("Panel Containers")]
        [SerializeField] private Image panel1_Intro;
        [SerializeField] private Image panel2_Clash;
        [SerializeField] private Image panel3_Decision;
        [SerializeField] private Image panel4_Climax;

        [Header("Manga Elements")]
        [SerializeField] private Text bubbleText1;
        [SerializeField] private Text bubbleText4;
        [SerializeField] private Image onomatopoeiaImage; // 巨大描き文字
        [SerializeField] private Material speedlineMaterial;

        /// <summary>
        /// 対局ログから1ページ漫画を生成し、PNGとして保存・URL発行
        /// </summary>
        public string GenerateMangaPage(
            string winnerName, 
            string loserName, 
            string yakuName, 
            int finalScore, 
            Sprite winnerCutin, 
            Sprite loserDefeat)
        {
            // 1. 各コマの素材・画像をバインド
            panel1_Intro.sprite = loserDefeat; // 導入
            panel4_Climax.sprite = winnerCutin; // 決着大ゴマ

            // 2. セリフ・オノマトペの設定
            bubbleText1.text = "この血は……私のものよ！";
            bubbleText4.text = $"{yakuName}！！\n{finalScore:#,0} 点強奪ッ！！";

            // 3. 集中線の消失点を大ゴマの和了牌位置に合わせる
            speedlineMaterial.SetVector("_Center", new Vector4(0.5f, 0.35f, 0, 0));

            // 4. オフスクリーンレンダリング実行
            mangaOffscreenCam.targetTexture = mangaRenderTexture;
            mangaOffscreenCam.Render();

            // 5. Texture2D へ読み込み & PNG保存
            RenderTexture.active = mangaRenderTexture;
            Texture2D mangaTex = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
            mangaTex.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
            mangaTex.Apply();

            byte[] pngData = mangaTex.EncodeToPNG();
            string filePath = Path.Combine(Application.temporaryCachePath, $"KMB_Manga_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(filePath, pngData);

            Debug.Log($"[MangaGenerator] 1-Page Gekiga Manga generated successfully at: {filePath}");
            return filePath;
        }
    }
}
```

---

## 6. バイラル拡散・ユーザー体験（UX）

1. **勝利・敗北画面（`VictoryUI`）での「劇画コミック・プレビュー」**:
   - リザルト画面で「本日の死闘録（コミックを読む）」ボタンを押すと、和紙のページめくりアニメーションとともに漫画がズームアップ表示。
2. **X（Twitter）/ Web共有**:
   - 1タップで「#じゃんぱいあ #役満死闘録」のタグと共に漫画画像が添付投稿され、プレイヤー自身のプレイスタイルやドラマがフォロワーへ即座に伝達。

---

## 7. 結論

本プロシージャル漫画生成エンジンにより、対局の興奮が単なるログデータから**「プレイヤー自身が主人公となる伝説の闘牌マンガの1ページ」**へと昇華され、極めて高いバイラル性とナラティブな愛着をユーザーに提供します。
