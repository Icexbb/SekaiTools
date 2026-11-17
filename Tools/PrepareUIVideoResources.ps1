param([Parameter(Mandatory)][string]$SourceDirectory)
$ErrorActionPreference = 'Stop'
$taskRepository = Split-Path $PSScriptRoot -Parent
$taskSource = (Resolve-Path -LiteralPath $SourceDirectory).Path
$taskDestination = Join-Path $taskRepository '.codex-build/fluent-workbench/fixtures/videoProcess'
$taskLock = Get-Content -Raw -Encoding UTF8 (Join-Path $taskRepository 'SekaiTools.UITests/TestData/video-resources.lock.json') | ConvertFrom-Json
# Validate the complete pack before replacing any fixture file. Never modify the source.
foreach ($taskEntry in $taskLock) {
    if ([IO.Path]::GetFileName($taskEntry.Name) -ne $taskEntry.Name) { throw '资源锁定文件含越界名称。' }
    $taskFile = Join-Path $taskSource $taskEntry.Name
    if ((Get-FileHash -LiteralPath $taskFile -Algorithm SHA256).Hash -ne $taskEntry.Sha256) {
        throw "资源版本不匹配：$($taskEntry.Name)"
    }
}
New-Item -ItemType Directory -Force -Path $taskDestination | Out-Null
foreach ($taskEntry in $taskLock) {
    Copy-Item -LiteralPath (Join-Path $taskSource $taskEntry.Name) -Destination (Join-Path $taskDestination $taskEntry.Name) -Force
}
Write-Output "已准备固定版本字幕 UI 测试资源：$taskDestination"
