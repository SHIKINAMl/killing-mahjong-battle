# 牙の噛みつき音（8bit版・2026-11-08）

Freesoundで公開されている以下のHQプレビューを使用。ライセンスはいずれも [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)。

| 素材 | 作者 | 素材ページ | 使用したプレビュー |
| --- | --- | --- | --- |
| Scraping Stone | dslrguide | https://freesound.org/people/dslrguide/sounds/321488/ | https://cdn.freesound.org/previews/321/321488_5485024-hq.mp3 |
| Ominous Rumbling.mp3 | Mendenhall02 | https://freesound.org/people/Mendenhall02/sounds/521871/ | https://cdn.freesound.org/previews/521/521871_10058132-hq.mp3 |
| monster bite | LucasDuff | https://freesound.org/people/LucasDuff/sounds/467701/ | https://cdn.freesound.org/previews/467/467701_8272463-hq.mp3 |
| Teeth clack 2 | hollandm | https://freesound.org/people/hollandm/sounds/691909/ | https://cdn.freesound.org/previews/691/691909_5309408-hq.mp3 |

各素材をモノラル化・帯域調整し、個別に8kHzのサンプルホールドと8bit振幅量子化を施してから合成。
合成後も8bit振幅に量子化。Unityへは48kHz・16bit PCMの容器で渡すが、音の振幅解像度は8bitのまま。

- `se_fang_anticipation.wav`：0.48秒。石を擦る音と低い唸りを、牙の出現から突進まで次第に強くする。
- `se_fang_bite.wav`：0.66秒。噛む音に14ms遅れで歯の硬い衝突を重ね、74ms後に小さな噛み合わせを追加。
- 接触音は従来どおり牌に噛みついた瞬間に呼ぶ。表示時間・動き・点数計算は変更していない。

再生成：`C:/Unity/km-bridge/audio_review/fang_8bit_20261108/render.cjs`
同じ場所に元のHQプレビュー・加工済み4素材・連続試聴用WAV・差し替え前の音を保存。
