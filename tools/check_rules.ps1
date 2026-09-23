<#
  コミット前の決まりごと検査（2026-09-24）

  **散文で書いてあるだけのルールは守られない。** AGENTS.md に書いてあるもののうち、
  **文字で判定できて、実際に破った事故があるものだけ**を機械で弾く。

  **思いつきで増やさないこと。** 増やす前に2つ確かめる:
    1. その規則を破った事故が実際にあったか
    2. 手元の過去の資産に当ててみて、誤検出が出ないか

  実際、「MCP へ渡す C# の文字列に日本語を書かない」は入れようとして外した。
  過去の作業用スクリプトに当てたら 453 件引っかかり、**そのどれもが正常に動いていた**。
  当たらない警報は、無い方がましなので削った。

  使い方:
      powershell -NoProfile -File tools/check_rules.ps1            # HEAD との差分を見る
      powershell -NoProfile -File tools/check_rules.ps1 -Staged    # git add 済みの分だけ

  終了コード 0 = 問題なし / 1 = 違反あり。警告だけなら 0 で返る。
#>
[CmdletBinding()]
param(
    [switch]$Staged
)

$ErrorActionPreference = 'Stop'
$repo = (& git rev-parse --show-toplevel) -replace '/', '\'
Set-Location $repo

$errors = New-Object System.Collections.Generic.List[string]
$warns  = New-Object System.Collections.Generic.List[string]

function Add-Error($rule, $file, $detail) { $errors.Add("[$rule] $file`n        $detail") }
function Add-Warn($rule, $file, $detail)  { $warns.Add("[$rule] $file`n        $detail") }

# --- 対象の洗い出し ---------------------------------------------------------
$diffArgs = if ($Staged) { @('diff', '--cached', '--name-status', '--diff-filter=ACMR') }
            else         { @('diff', 'HEAD',     '--name-status', '--diff-filter=ACMR') }
$changed = @(& git @diffArgs)
# 追跡されていない新規ファイルも見る。新しく足した .py などを見落とさないため
$changed += @(& git ls-files --others --exclude-standard | ForEach-Object { "A`t$_" })

$files = @()
foreach ($line in $changed) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $parts = $line -split "`t"
    $files += [pscustomobject]@{ Status = $parts[0].Substring(0, 1); Path = $parts[-1] }
}

# --- 1. 別の人の担当範囲 ----------------------------------------------------
# mahjong_engine/ 配下と、すべての .py。読むのと外から呼ぶのは自由、編集は不可。
foreach ($f in $files) {
    if ($f.Path -like 'mahjong_engine/*' -or $f.Path -like '*.py') {
        Add-Error '別担当の範囲' $f.Path 'mahjong_engine/ と .py は別の人の担当。読むのと外から呼ぶのは自由だが、編集しない。'
    }
}

# --- 2. BOM の有無を変えていないか ------------------------------------------
# utf-8-sig で読んで utf-8 で書き戻すと BOM が勝手に付く／消える。過去に事故あり。
$textExt = @('.cs', '.ps1', '.md', '.json', '.txt', '.shader', '.asmdef')
foreach ($f in $files) {
    if ($f.Status -ne 'M') { continue }
    if ($textExt -notcontains [System.IO.Path]::GetExtension($f.Path)) { continue }
    if (-not (Test-Path -LiteralPath $f.Path)) { continue }

    $nowBytes = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $f.Path))
    $nowBom = ($nowBytes.Length -ge 3 -and $nowBytes[0] -eq 0xEF -and $nowBytes[1] -eq 0xBB -and $nowBytes[2] -eq 0xBF)

    # **git の中身は必ず cmd 経由でファイルへ落とす。**
    # PowerShell のパイプや `>` はいったん文字列にするので、見たい BOM そのものが
    # 落ちたり付いたりする（PS 5.1 の `>` は UTF-8 BOM 付きで書く）。
    $tmp = [System.IO.Path]::GetTempFileName()
    cmd /c "git show `"HEAD:$($f.Path)`" > `"$tmp`" 2>NUL" | Out-Null
    $oldBytes = [System.IO.File]::ReadAllBytes($tmp)
    Remove-Item $tmp -Force -ErrorAction SilentlyContinue
    if ($oldBytes.Length -lt 3) { continue }
    $oldBom = ($oldBytes[0] -eq 0xEF -and $oldBytes[1] -eq 0xBB -and $oldBytes[2] -eq 0xBF)

    if ($nowBom -ne $oldBom) {
        $was = if ($oldBom) { 'あり' } else { 'なし' }
        $now = if ($nowBom) { 'あり' } else { 'なし' }
        Add-Error 'BOM' $f.Path "BOM が $was から $now に変わっている。元に戻すこと。"
    }
}

# --- 3〜4. 足した行の中身 ---------------------------------------------------
# 参考曲・参考動画・ライセンスのURLはリポジトリに書かない（置き場所は Music フォルダ）。
# フォントの OFL.txt だけは例外。
$refHosts = 'youtube\.com|youtu\.be|soundcloud\.com|open\.spotify\.com|x\.com/|twitter\.com/'
$diffText = if ($Staged) { & git diff --cached -U0 } else { & git diff HEAD -U0 }
$currentFile = ''
foreach ($line in $diffText) {
    if ($line -match '^\+\+\+ b/(.+)$') { $currentFile = $Matches[1]; continue }
    if ($line -notmatch '^\+' -or $line -match '^\+\+\+') { continue }
    $added = $line.Substring(1)

    # AIで画像を生成しない
    if ($added -match 'generate_image|imagegen|image_gen') {
        Add-Error 'AI画像生成' $currentFile "AIで画像を生成しない。承認用の絵は実機のスクショか録画で。`n        > $added"
    }

    # 参考曲・参考動画・ライセンスの情報
    if ($currentFile -notlike '*OFL.txt' -and $added -match $refHosts) {
        Add-Error '参考資料のURL' $currentFile "参考曲・参考動画のURLはリポジトリに書かない。Music フォルダへ置く（OFL.txt は例外）。`n        > $added"
    }
}

# --- 5. シーン・プレハブ・設定を触っていないか（警告） ----------------------
# 多くの指示で「C# のみ。シーンは触らない」と決めている。意図した変更なら無視してよい。
foreach ($f in $files) {
    if ($f.Path -match '\.(unity|prefab)$' -or $f.Path -like 'ProjectSettings/*') {
        if ($f.Path -like 'Assets/_Recovery/*') { continue }   # Unity が自動で残す控え
        Add-Warn 'シーン変更' $f.Path 'シーン・プレハブ・設定に差分がある。意図したものか確かめること。'
    }
}

# --- 結果 -------------------------------------------------------------------
if ($warns.Count -gt 0) {
    Write-Host ""
    Write-Host "警告 $($warns.Count) 件" -ForegroundColor Yellow
    foreach ($w in $warns) { Write-Host "  $w" -ForegroundColor Yellow }
}

if ($errors.Count -gt 0) {
    Write-Host ""
    Write-Host "違反 $($errors.Count) 件" -ForegroundColor Red
    foreach ($e in $errors) { Write-Host "  $e" -ForegroundColor Red }
    Write-Host ""
    exit 1
}

Write-Host ""
Write-Host "検査ずみ: $($files.Count) ファイル。違反なし。" -ForegroundColor Green
exit 0
