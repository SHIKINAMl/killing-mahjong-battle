# ボルテージの点火音

`se_voltage_ignite.wav` は、下記2素材の公開HQプレビューを加工して重ねた音。
新たな発振音や音声は足していない。いずれも取得時点でCC0。

| 素材 | 作者 | 素材ページ | 使用したプレビュー |
| --- | --- | --- | --- |
| flameclic.wav | j1987 | https://freesound.org/people/j1987/sounds/111331/ | https://cdn.freesound.org/previews/111/111331_367313-hq.mp3 |
| Bonfire Being Lit | samararaine | https://freesound.org/people/samararaine/sounds/186374/ | https://cdn.freesound.org/previews/186/186374_2421128-hq.mp3 |

ライセンス: https://creativecommons.org/publicdomain/zero/1.0/

加工: 焚き火の0.94〜1.84秒とライターの0.10〜0.68秒を切り出し、
速度を0.8倍にして、低域・高域を整理。フェードを付け、ライターを90ms遅らせて小さく重ねた。
48kHz、モノラル、16bit PCM、1.35秒。ピーク約-6.5dBFS、平均約-24.8dBFS。
ゲーム内ではSE設定に追従し、音量をさらに0.7倍にする。

取得・加工日: 2026-10-29。

再現用FFmpegフィルター（入力0=Bonfire、入力1=flameclic）:

```text
[0:a]atrim=start=0.94:end=1.84,asetpts=PTS-STARTPTS,atempo=0.8,highpass=f=75,lowpass=f=1900,afade=t=in:st=0:d=0.18,afade=t=out:st=0.55:d=0.575,volume=0.85[a];[1:a]atrim=start=0.10:end=0.68,asetpts=PTS-STARTPTS,atempo=0.8,highpass=f=180,lowpass=f=2300,afade=t=in:st=0:d=0.10,afade=t=out:st=0.26:d=0.465,volume=0.22,adelay=90:all=1[b];[a][b]amix=inputs=2:normalize=0,apad,atrim=duration=1.35,alimiter=limit=0.50:level=0[out]
```
