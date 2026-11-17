param([Parameter(Mandatory)][string]$SourceDirectory)
$ErrorActionPreference = 'Stop'
$taskRepository = Split-Path $PSScriptRoot -Parent
$taskSource = (Resolve-Path -LiteralPath $SourceDirectory).Path
$taskDestination = Join-Path $taskRepository '.codex-build/fluent-workbench/fixtures/vapourSynth'
$taskLock = Get-Content -Raw -Encoding UTF8 (Join-Path $taskRepository 'SekaiTools.UITests/TestData/media-resources.lock.json') | ConvertFrom-Json
foreach ($taskEntry in $taskLock) {
    $taskFile = [IO.Path]::GetFullPath((Join-Path $taskSource $taskEntry.Name))
    if (-not $taskFile.StartsWith($taskSource.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw '资源路径越界。' }
    if ((Get-FileHash -LiteralPath $taskFile -Algorithm SHA256).Hash -ne $taskEntry.Sha256) { throw "资源版本不匹配：$($taskEntry.Name)" }
}
foreach ($taskEntry in $taskLock) {
    $taskTarget = [IO.Path]::GetFullPath((Join-Path $taskDestination $taskEntry.Name))
    if (-not $taskTarget.StartsWith($taskDestination + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw '目标路径越界。' }
    New-Item -ItemType Directory -Force -Path (Split-Path $taskTarget -Parent) | Out-Null
    Copy-Item -LiteralPath (Join-Path $taskSource $taskEntry.Name) -Destination $taskTarget -Force
}
Write-Output "已准备固定版本压制 UI 测试资源：$taskDestination"
