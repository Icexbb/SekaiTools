param(
    [ValidateSet('Smoke', 'Functional', 'Visual', 'CaptureOnly', 'All')][string]$Mode = 'Smoke',
    [string]$Filter,
    [switch]$Publish,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$taskRepository = Split-Path $PSScriptRoot -Parent
$taskOutput = Join-Path $taskRepository '.codex-build/fluent-workbench'
$taskApp = Join-Path $taskOutput $(if ($Publish) { 'published-app' } else { 'app' })
$taskRunId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
$taskArtifacts = Join-Path $taskOutput "artifacts/$taskRunId"
New-Item -ItemType Directory -Force -Path $taskArtifacts | Out-Null

function Invoke-CheckedDotnet {
    param([string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet 失败，退出码 $LASTEXITCODE。产物：$taskArtifacts" }
}

Push-Location $taskRepository
$taskEnvironmentNames = @('SEKAI_TOOLS_RUN_UI_TESTS', 'SEKAI_TOOLS_UI_EXE', 'SEKAI_TOOLS_UI_ARTIFACTS')
$taskPreviousEnvironment = @{}
foreach ($taskName in $taskEnvironmentNames) {
    $taskPreviousEnvironment[$taskName] = [Environment]::GetEnvironmentVariable($taskName, 'Process')
}
try {
    if (-not $SkipBuild) {
        $taskBuildAction = if ($Publish) { 'publish' } else { 'build' }
        Invoke-CheckedDotnet -Arguments @($taskBuildAction, 'SekaiToolsGUI/SekaiToolsGUI.csproj', '-c', 'Release', '-o', $taskApp, '--no-restore', '-m:1')
        Invoke-CheckedDotnet -Arguments @('build', 'SekaiTools.UITests/SekaiTools.UITests.csproj', '-c', 'Release', '--no-restore')
    }
    $taskExecutable = Join-Path $taskApp 'SekaiToolsGUI.exe'
    if (-not (Test-Path -LiteralPath $taskExecutable)) { throw "缺少测试应用：$taskExecutable，请先构建。" }
    if ($Publish) {
        foreach ($taskRelativePath in @('libs/Wpf.Ui.dll', 'tools/updater/Updater.exe', 'tools/updater/7zr.exe', 'network-endpoints.json', 'Configuration/settings-ui.json')) {
            if (-not (Test-Path -LiteralPath (Join-Path $taskApp $taskRelativePath) -PathType Leaf)) {
                throw "发布目录缺少 $taskRelativePath。"
            }
        }
    }
    $env:SEKAI_TOOLS_RUN_UI_TESTS = '1'
    $env:SEKAI_TOOLS_UI_EXE = $taskExecutable
    $env:SEKAI_TOOLS_UI_ARTIFACTS = $taskArtifacts
    $taskArguments = @('test', 'SekaiTools.UITests/SekaiTools.UITests.csproj', '-c', 'Release', '--no-build', '--no-restore',
        '--logger', 'trx;LogFileName=ui-tests.trx', '--results-directory', $taskArtifacts)
    $taskSelectedFilter = if ($Filter) { $Filter }
        elseif ($Mode -eq 'CaptureOnly') { 'FullyQualifiedName~WorkbenchDisplayTests' }
        elseif ($Mode -ne 'All') { "Category=$Mode" }
    if ($taskSelectedFilter) { $taskArguments += @('--filter', $taskSelectedFilter) }
    Invoke-CheckedDotnet -Arguments $taskArguments
    [xml]$taskTrx = Get-Content -Raw -LiteralPath (Join-Path $taskArtifacts 'ui-tests.trx')
    if ([int]$taskTrx.TestRun.ResultSummary.Counters.executed -lt 1 -or
        [int]$taskTrx.TestRun.ResultSummary.Counters.notExecuted -gt 0) {
        throw "UI 验收未完成：没有实际执行的测试或存在未执行用例。报告：$taskArtifacts"
    }
    Write-Output "UI 测试报告与截图：$taskArtifacts"
}
finally {
    foreach ($taskName in $taskEnvironmentNames) {
        [Environment]::SetEnvironmentVariable($taskName, $taskPreviousEnvironment[$taskName], 'Process')
    }
    Pop-Location
    $taskIndex = [ordered]@{
        RunId = $taskRunId
        Mode = $Mode
        Filter = $taskSelectedFilter
        Published = $Publish.IsPresent
        Files = @(Get-ChildItem -LiteralPath $taskArtifacts -Recurse -File |
            Where-Object { $_.Name -match '\.(trx|png|log)$|^(report|environment|display|display-captures|.*-diagnostic)\.json$|^uia-tree\.txt$' } |
            ForEach-Object { [IO.Path]::GetRelativePath($taskArtifacts, $_.FullName).Replace('\', '/') } |
            Where-Object { $_ -notlike '*/sandbox/*' } |
            Sort-Object)
    }
    $taskIndex | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskArtifacts 'index.json') -Encoding utf8
}
