param(
    [Parameter(Mandatory)][string]$BenchmarkDll,
    [Parameter(Mandatory)][string]$Video,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [int[]]$CancelFrames = @(4095, 4096, 4097),
    [int]$Threads = 4,
    [int]$Buffer = 8,
    [switch]$RequirePackedCancellation,
    [switch]$ForcePacks
)
$ErrorActionPreference = 'Stop'
if ($CancelFrames.Count -eq 0 -or @($CancelFrames | Where-Object { $_ -le 0 }).Count) { throw 'Positive cancellation frames required' }
if (@($CancelFrames | Select-Object -Unique).Count -ne $CancelFrames.Count) { throw 'Duplicate cancellation frames' }
$dll = (Resolve-Path -LiteralPath $BenchmarkDll).Path
$inputVideo = (Resolve-Path -LiteralPath $Video).Path
$root = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $root) { throw 'Use a new output directory; previous reports are never reused' }
$inputs = @($inputVideo, [IO.Path]::ChangeExtension($inputVideo, '.json'), [IO.Path]::ChangeExtension($inputVideo, '.txt'))
$templateDirectory = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'SekaiTools/Resource/videoProcess'
if (-not (Test-Path -LiteralPath $templateDirectory -PathType Container)) { throw 'Recognition templates missing' }
$identityFiles = @(Get-ChildItem -LiteralPath $templateDirectory -Recurse -File | Select-Object -ExpandProperty FullName) + @($inputs) + @(Get-ChildItem -LiteralPath (Split-Path $dll) -File -Recurse |
    Where-Object { $_.Extension -in '.dll', '.json' } | Select-Object -ExpandProperty FullName)
function Fingerprint {
    @($identityFiles | Sort-Object | ForEach-Object { [ordered]@{ Path = $_; Hash = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash } }) | ConvertTo-Json -Depth 4
}
$identity = Fingerprint
New-Item -ItemType Directory -Path $root | Out-Null
Set-Content -LiteralPath (Join-Path $root 'identity.json') -Value $identity -Encoding utf8
$priorChunked = $env:SEKAI_BENCHMARK_CHUNKED
$priorCancel = $env:SEKAI_BENCHMARK_CANCEL_FRAME
$priorResume = $env:SEKAI_BENCHMARK_RESUME_STATE
$priorForcePacks = $env:SEKAI_BENCHMARK_FORCE_PACKS
$reports = @()
try {
    $env:SEKAI_BENCHMARK_CHUNKED = 'true'
    $env:SEKAI_BENCHMARK_RESUME_STATE = $null
    $env:SEKAI_BENCHMARK_FORCE_PACKS = if ($ForcePacks) { 'true' } else { $null }
    foreach ($frame in @($null) + @($CancelFrames)) {
        $label = if ($null -eq $frame) { 'continuous' } else { "frame-$frame" }
        $env:SEKAI_BENCHMARK_CANCEL_FRAME = if ($null -eq $frame) { $null } else { [string]$frame }
        $out = Join-Path $root $label
        New-Item -ItemType Directory -Path $out | Out-Null
        Write-Output "START $label"
        & dotnet $dll $out $Threads $Buffer 0 $inputVideo *> (Join-Path $out 'process.log')
        if ($LASTEXITCODE) { throw "Benchmark failed: $out/process.log" }
        $resultDirectory = Join-Path $out ([IO.Path]::GetFileName($inputVideo))
        $report = Get-Content -LiteralPath (Join-Path $resultDirectory 'report.json') -Raw | ConvertFrom-Json
        if (($report.ForcedChunkedCheckpoint -eq $true) -ne $ForcePacks.IsPresent) { throw 'Forced pack mode differs from request' }
        if ($report.ChunkedCheckpoint -ne $true) { throw 'Chunked checkpoint mode was not exercised' }
        if ($null -ne $frame -and ($report.CancelFrame -ne $frame -or $report.ResumeFrame -ne $frame)) { throw 'Exact cancellation/resume frame differs' }
        if ($null -ne $frame -and $RequirePackedCancellation) {
            $manifest = Get-Content -LiteralPath (Join-Path $resultDirectory 'canceled-manifest.json') -Raw | ConvertFrom-Json
            if ($manifest.CheckpointStorage -ne 2 -or $manifest.Version -notlike 'chunks2:*') { throw 'Cancellation checkpoint did not use v2 storage' }
        }
        $files = @('state.json', 'subtitle.ass' | ForEach-Object {
            $path = Join-Path $resultDirectory $_
            [ordered]@{ Name = $_; Length = (Get-Item -LiteralPath $path).Length; Hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
        })
        if ($null -eq $frame) { $reference = $files | ConvertTo-Json }
        elseif (($files | ConvertTo-Json) -cne $reference) { throw "State or ASS differs at frame $frame" }
        $reports += [ordered]@{ Label = $label; Report = $report; Files = $files }
        Write-Output "PASS $label"
    }
    if ((Fingerprint) -cne $identity) { throw 'Inputs or binaries changed during validation' }
    [ordered]@{ Video = $inputVideo; Threads = $Threads; Buffer = $Buffer; CancelFrames = $CancelFrames; ForcePacks = $ForcePacks.IsPresent; RequirePackedCancellation = $RequirePackedCancellation.IsPresent; AllEquivalent = $true; Runs = $reports } |
        ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $root 'verification.json') -Encoding utf8
} finally {
    $env:SEKAI_BENCHMARK_CHUNKED = $priorChunked
    $env:SEKAI_BENCHMARK_CANCEL_FRAME = $priorCancel
    $env:SEKAI_BENCHMARK_RESUME_STATE = $priorResume
    $env:SEKAI_BENCHMARK_FORCE_PACKS = $priorForcePacks
}
