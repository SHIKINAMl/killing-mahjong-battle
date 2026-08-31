# 対戦ボットを常駐させる見張り役。
#
# opponent_bot.py は duration 秒で自分から終了する作りなので、
# 終わるたびに起動し直す。落ちた場合も同じ経路で拾い直す。
#
# 起動:   powershell -ExecutionPolicy Bypass -File bot_supervisor.ps1
# 止める: Stop-Process -Id (Get-Content bot_supervisor.pid)
#
# ログ:
#   bot_supervisor.log  … 見張り役自身の記録（何回目を起動したか、終了コード）
#   bot_YYYYMMDD.txt    … ボット本体のログ（日付ごと）

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$python = "$env:LOCALAPPDATA\Programs\Python\Python312\python.exe"
if (-not (Test-Path $python)) {
    $found = Get-Command python -ErrorAction SilentlyContinue
    if ($found -and (Get-Item $found.Source).Length -gt 0) { $python = $found.Source }
    else { "python.exe が見つかりません: $python" | Out-File "$root\bot_supervisor.log" -Append -Encoding UTF8; exit 1 }
}

# 1回あたりの走行時間。長すぎると状態が濁るので1時間で切って入れ直す。
$duration = 3600
# 終了から次の起動までの待ち。連続で失敗したときに回りすぎないための間。
$restartDelay = 5

$PID | Out-File "$root\bot_supervisor.pid" -Encoding ASCII
$run = 0

function Write-Log($msg) {
    "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $msg |
        Out-File "$root\bot_supervisor.log" -Append -Encoding UTF8
}

Write-Log "見張り開始 (PID $PID) python=$python duration=${duration}s"

while ($true) {
    $run++
    $log = "bot_{0}.txt" -f (Get-Date -Format "yyyyMMdd")
    Write-Log "$run 回目を起動 → $log"

    try {
        & $python "$root\opponent_bot.py" $duration --log $log
        $code = $LASTEXITCODE
    } catch {
        $code = "例外: $_"
    }

    Write-Log "$run 回目が終了 (終了コード $code)。${restartDelay}秒後に入れ直す"
    Start-Sleep -Seconds $restartDelay
}
