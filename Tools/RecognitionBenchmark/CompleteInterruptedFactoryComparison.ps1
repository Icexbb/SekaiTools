param(
    [Parameter(Mandatory)][string]$ComparisonDirectory,
    [Parameter(Mandatory)][string]$ReferenceDll,
    [Parameter(Mandatory)][string]$CandidateDll,
    [Parameter(Mandatory)][string]$Plugin
)
$ErrorActionPreference = 'Stop'
if (Get-ChildItem Env: | Where-Object Name -Match '^(OPENCV_|SEKAI_BENCHMARK_)') { throw 'Unexpected environment' }
$root = (Resolve-Path -LiteralPath $ComparisonDirectory).Path
if (Test-Path (Join-Path $root 'summary.json')) { throw 'Comparison already complete' }
$manifest = @(Get-Content (Join-Path $root 'manifest-before.json') -Raw | ConvertFrom-Json)
function VerifyManifest {
    foreach ($entry in $manifest) {
        if ((Get-FileHash -LiteralPath $entry.Path).Hash -ne $entry.Hash) { throw "Manifest changed: $($entry.Path)" }
    }
}
VerifyManifest
$reference = (Resolve-Path -LiteralPath $ReferenceDll).Path
$candidate = (Resolve-Path -LiteralPath $CandidateDll).Path
$pluginPath = (Resolve-Path -LiteralPath $Plugin).Path
foreach ($path in @($reference,$candidate,$pluginPath)) {
    if (@($manifest | Where-Object Path -eq $path).Count -ne 1) { throw 'Tool outside original manifest' }
}
$verifier = Join-Path $PSScriptRoot 'VerifyFactoryPair.py'
$rows = @(Get-Content (Join-Path $root 'completed-pairs.json') -Raw | ConvertFrom-Json)
$videos = @($rows.Video | Sort-Object -Unique)
if ($videos.Count -ne 5 -or $rows.Count -ne 13) { throw 'Expected thirteen completed pairs covering five groups' }
if (@($rows | ForEach-Object { "$($_.Round)|$($_.Video)" } | Sort-Object -Unique).Count -ne 13) { throw 'Duplicate completed pair' }
foreach ($row in $rows) {
    if ($row.Round -notin 1..3 -or -not $row.VerificationPassed) { throw 'Invalid completed row' }
    $group = Join-Path $root "round-$($row.Round)/$([IO.Path]::GetFileNameWithoutExtension($row.Video))"
    & python $verifier (Join-Path $group 'reference') (Join-Path $group 'factory') | Out-Null
    if ($LASTEXITCODE) { throw 'Existing pair audit failed' }
}
$continuation = Join-Path $root 'continuation'
if (Test-Path $continuation) { throw 'Inspect an existing continuation before proceeding' }
New-Item -ItemType Directory $continuation | Out-Null
Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $continuation 'runner.ps1')
foreach ($video in $videos) {
    if (@($rows | Where-Object { $_.Round -eq 3 -and $_.Video -eq $video }).Count) { continue }
    $group = Join-Path $continuation ([IO.Path]::GetFileNameWithoutExtension($video))
    $times = @{}
    foreach ($mode in @('reference','factory')) {
        $dll = if ($mode -eq 'reference') { $reference } else { $candidate }
        $arguments = @($dll,$video,(Join-Path $group $mode),'1','true','Automatic','false','default','false','false','default','false','false','false')
        if ($mode -eq 'factory') { $arguments += @('16',$pluginPath,'true','true') }
        New-Item -ItemType Directory -Force $group | Out-Null
        $timer = [Diagnostics.Stopwatch]::StartNew()
        & dotnet @arguments *> (Join-Path $group "$mode.log")
        $timer.Stop()
        if ($LASTEXITCODE) { throw 'Continuation recognition failed' }
        $times[$mode] = $timer.Elapsed.TotalMilliseconds
    }
    & python $verifier (Join-Path $group 'reference') (Join-Path $group 'factory') | Out-Null
    if ($LASTEXITCODE) { throw 'Continuation output differs' }
    $a = Get-Content (Join-Path $group 'reference/run-1.json') -Raw | ConvertFrom-Json
    $b = Get-Content (Join-Path $group 'factory/run-1.json') -Raw | ConvertFrom-Json
    foreach ($report in @($a,$b)) {
        if ($report.OpenCvThreads -ne 16 -or $null -ne $report.FrameBufferCapacity -or $report.DetailedProfile -or
            $report.ActualDecoderThreads -ne 16 -or $report.Warmup -or $report.HardwareDecode -or $report.PrepareGray) {
            throw 'Continuation defaults differ'
        }
        foreach ($library in $report.NativeLibraries) {
            if (@($manifest | Where-Object { $_.Path -eq $library.Path -and $_.Hash -eq $library.Hash }).Count -ne 1) {
                throw 'Actual library outside original manifest'
            }
        }
    }
    $baseline = Join-Path $root "round-1/$([IO.Path]::GetFileNameWithoutExtension($video))/reference/state-1.json"
    if ((Get-FileHash $baseline).Hash -ne (Get-FileHash (Join-Path $group 'reference/state-1.json')).Hash) { throw 'Reference state changed' }
    $rows += [pscustomobject]@{ Round=3; Video=$video; VerificationPassed=$true; ContinuedAfterInterruption=$true;
        EvidenceDirectory=$group; ReferenceMilliseconds=$a.WallMilliseconds; FactoryMilliseconds=$b.WallMilliseconds;
        ProcessingReductionPercent=(1-$b.WallMilliseconds/$a.WallMilliseconds)*100;
        ReferenceProcessMilliseconds=$times.reference; FactoryProcessMilliseconds=$times.factory;
        ProcessReductionPercent=(1-$times.factory/$times.reference)*100;
        ReferencePeakWorkingSet=$a.PeakWorkingSet; FactoryPeakWorkingSet=$b.PeakWorkingSet }
    $rows[-1] | ConvertTo-Json -Compress | Write-Output
    $rows | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $continuation 'completed-pairs.json') -Encoding utf8
}
VerifyManifest
if ($rows.Count -ne 15) { throw 'Incomplete comparison' }
@{ VerificationPassed=$true; ManifestUnchanged=$true; Pairs=$rows; LiveGuiQualified=$false;
    InterruptedAndContinued=$true; ContinuousMeasurement=$false; ContinuationScriptHash=(Get-FileHash $PSCommandPath).Hash
} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root 'summary.json') -Encoding utf8
