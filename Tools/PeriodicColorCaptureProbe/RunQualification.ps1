param(
    [Parameter(Mandatory)][string]$ProbeDll,
    [Parameter(Mandatory)][string]$Plugin,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string[]]$Videos,
    [switch]$PublicBinding
)
$ErrorActionPreference = 'Stop'
if (Get-ChildItem Env: | Where-Object Name -Match '^(OPENCV_|SEKAI_BENCHMARK_)') { throw 'Unexpected experiment environment' }
$probe = (Resolve-Path -LiteralPath $ProbeDll).Path
$pluginPath = (Resolve-Path -LiteralPath $Plugin).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh output directory' }
$inputs = @($Videos | ForEach-Object { (Resolve-Path -LiteralPath $_).Path })
if ($inputs.Count -ne 8 -or @($inputs | Sort-Object -Unique).Count -ne 8 -or
    @($inputs | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_) } | Sort-Object -Unique).Count -ne 8) {
    throw 'Expected eight distinct video paths and basenames'
}
$experiment = Get-Content -LiteralPath (Join-Path (Split-Path $probe) 'native-experiment.json') -Raw | ConvertFrom-Json
$paths = @($inputs) + @($pluginPath, $PSCommandPath)
$paths += @((Join-Path $experiment.NativeBuild 'experiment.json'),
    (Join-Path $experiment.NativeBuild 'build-result.json'),
    (Join-Path $PSScriptRoot '../NativeRuntimeExperiment/PeriodicColorCapture.cpp'))
$paths += @(Get-ChildItem -LiteralPath (Split-Path $probe) -Recurse -File | Where-Object Extension -In '.dll','.json' | Select-Object -ExpandProperty FullName)
$paths += @(Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object Extension -In '.cs','.csproj' | Select-Object -ExpandProperty FullName)
$manifest = @($paths | Sort-Object -Unique | ForEach-Object { @{Path=$_; Hash=(Get-FileHash -LiteralPath $_).Hash} })
New-Item -ItemType Directory -Path $output | Out-Null
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'manifest-before.json') -Encoding utf8
$rows = @()
foreach ($video in $inputs) {
    $name = [IO.Path]::GetFileNameWithoutExtension($video)
    $report = Join-Path $output "$name.json"
    $arguments = @($probe, $video, $pluginPath, $report)
    if ($PublicBinding) { $arguments += 'public-bind' }
    & dotnet @arguments *> (Join-Path $output "$name.log")
    if ($LASTEXITCODE) { throw "Periodic color qualification failed: $video" }
    $row = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if ($row.PublicCaptureBinding -ne [bool]$PublicBinding -or
        ($PublicBinding -and -not $row.FailedBindingsPreservedCapture)) { throw 'Binding qualification missing' }
    foreach ($library in @(@($row.NativePath,$row.NativeHash), @($row.PluginPath,$row.PluginHash))) {
        $qualified = @($manifest | Where-Object {
            [IO.Path]::GetFullPath($_.Path) -eq [IO.Path]::GetFullPath($library[0]) -and $_.Hash -eq $library[1]
        })
        if ($qualified.Count -ne 1) { throw 'Actual runtime outside qualified manifest' }
    }
    if (-not $row.VerificationPassed -or -not $row.CaptureDisposed -or
        -not $row.RetainedColorAndGrayFramesUnchanged -or $row.ColorFramesVerified -le 0 -or
        $row.NativeHash -ne $experiment.NativeHash -or $row.Video -ne $video -or
        $row.PluginHash -ne (Get-FileHash -LiteralPath $pluginPath).Hash -or
        $row.InputHash -ne (Get-FileHash -LiteralPath $video).Hash) { throw 'Incomplete or mismatched probe report' }
    $rows += $row
    Write-Output ($row | ConvertTo-Json -Compress)
}
foreach ($entry in $manifest) { if ((Get-FileHash -LiteralPath $entry.Path).Hash -ne $entry.Hash) { throw "Manifest changed: $($entry.Path)" } }
$rows | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'summary.json') -Encoding utf8
