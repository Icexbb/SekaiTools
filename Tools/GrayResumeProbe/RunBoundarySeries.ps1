param(
    [Parameter(Mandatory)][string]$ToolDll,
    [Parameter(Mandatory)][string]$Plugin,
    [Parameter(Mandatory)][string]$ReferenceDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [int[]]$Frames = @(1028,1029,1030)
)
$ErrorActionPreference = 'Stop'
if (Get-ChildItem Env: | Where-Object Name -Match '^(OPENCV_|SEKAI_BENCHMARK_)') { throw 'Unexpected experiment environment' }
$dll = (Resolve-Path -LiteralPath $ToolDll).Path
$pluginPath = (Resolve-Path -LiteralPath $Plugin).Path
$reference = (Resolve-Path -LiteralPath $ReferenceDirectory).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh output directory' }
if ($Frames.Count -lt 1 -or @($Frames | Sort-Object -Unique).Count -ne $Frames.Count -or
    @($Frames | Where-Object { $_ -le 0 }).Count) { throw 'Distinct positive frame indices required' }
$baseline = Get-Content -LiteralPath (Join-Path $reference 'run-1.json') -Raw | ConvertFrom-Json
if (-not $baseline.NativeGrayCapture -or $baseline.OpenCvThreads -ne 4 -or $baseline.FrameBufferCapacity -ne 8 -or
    $baseline.Preview -or $baseline.PrepareGray -or $baseline.HardwareDecode -or $baseline.ActualDecoderThreads -ne 16) {
    throw 'Unexpected continuous reference configuration'
}
$video = $baseline.Video
$paths = @($video, [IO.Path]::ChangeExtension($video, '.json'), [IO.Path]::ChangeExtension($video, '.txt'),
    $pluginPath, $PSCommandPath, (Join-Path $PSScriptRoot 'VerifyGrayResume.py'))
$paths += @(Get-ChildItem -LiteralPath (Split-Path $dll) -Recurse -File | Where-Object Extension -In '.dll','.json' | Select-Object -ExpandProperty FullName)
$paths += @(Get-ChildItem -LiteralPath $reference -File | Select-Object -ExpandProperty FullName)
$resource = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'SekaiTools/Resource/videoProcess'
$paths += @(Get-ChildItem -LiteralPath $resource -Recurse -File | Select-Object -ExpandProperty FullName)
$manifest = @($paths | Sort-Object -Unique | ForEach-Object { @{ Path = $_; Hash = (Get-FileHash -LiteralPath $_).Hash } })
New-Item -ItemType Directory -Path $output | Out-Null
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'manifest-before.json') -Encoding utf8
$rows = @()
foreach ($frame in $Frames) {
    $canceled = Join-Path $output "frame-$frame-cancel"
    $resumed = Join-Path $output "frame-$frame-resume"
    & dotnet $dll $video $pluginPath $canceled cancel $frame 4 8 *> "$canceled.log"
    if ($LASTEXITCODE) { throw "Cancellation failed at $frame" }
    $cancelReport = Get-Content -LiteralPath (Join-Path $canceled 'report.json') -Raw | ConvertFrom-Json
    & dotnet $dll $video $pluginPath $resumed resume $cancelReport.CheckpointPath 4 8 *> "$resumed.log"
    if ($LASTEXITCODE) { throw "Restore failed at $frame" }
    $audit = & python (Join-Path $PSScriptRoot 'VerifyGrayResume.py') $reference $canceled $resumed
    if ($LASTEXITCODE) { throw "Complete restored output audit failed at $frame" }
    $rows += $audit | ConvertFrom-Json
    Write-Output $audit
}
foreach ($entry in $manifest) {
    if ((Get-FileHash -LiteralPath $entry.Path).Hash -ne $entry.Hash) { throw "Manifest changed: $($entry.Path)" }
}
$rows | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'summary.json') -Encoding utf8
