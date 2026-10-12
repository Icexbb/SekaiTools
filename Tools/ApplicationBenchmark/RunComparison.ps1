param(
    [Parameter(Mandatory)][string]$ReferenceDll,
    [Parameter(Mandatory)][string]$CandidateDll,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string[]]$Videos,
    [int]$Rounds = 3,
    [int]$Threads = 4,
    [int]$Buffer = 8
)
$ErrorActionPreference = 'Stop'
if (-not [string]::IsNullOrEmpty($env:SEKAI_BENCHMARK_RESUME_STATE)) { throw 'Clear external resume state before continuous comparison' }
if (-not [string]::IsNullOrEmpty($env:SEKAI_BENCHMARK_CANCEL_FRAME)) { throw 'Clear exact cancellation frame before continuous comparison' }
if ($Rounds -lt 3) { throw 'At least three rounds required' }
$binaries = @((Resolve-Path -LiteralPath $ReferenceDll).Path, (Resolve-Path -LiteralPath $CandidateDll).Path)
$inputs = @($Videos | ForEach-Object { (Resolve-Path -LiteralPath $_).Path })
if (@($inputs | Select-Object -Unique).Count -ne $inputs.Count) { throw 'Duplicate videos' }
if (@($inputs | ForEach-Object { [IO.Path]::GetFileName($_) } | Select-Object -Unique).Count -ne $inputs.Count) {
    throw 'Duplicate video names; use separate comparisons'
}
$root = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $root | Out-Null
$identity = [ordered]@{
    Rounds = $Rounds; Threads = $Threads; Buffer = $Buffer
    Binaries = @($binaries | ForEach-Object {
        $directory = Split-Path $_
        # Include managed and native dependencies, not just the entry assembly.
        [ordered]@{ Entry = $_; Files = @(Get-ChildItem $directory -Recurse -File |
            Where-Object { $_.Extension -in '.dll', '.json' } | Sort-Object FullName |
            ForEach-Object { [ordered]@{ Path = $_.FullName; Hash = (Get-FileHash -LiteralPath $_.FullName).Hash } }) }
    })
    Inputs = @($inputs | ForEach-Object {
        [ordered]@{ Video = $_; Files = @($_, [IO.Path]::ChangeExtension($_, '.json'),
            [IO.Path]::ChangeExtension($_, '.txt')) | ForEach-Object {
                [ordered]@{ Path = $_; Hash = (Get-FileHash -LiteralPath $_).Hash }
            } }
    })
} | ConvertTo-Json -Depth 10
$manifest = Join-Path $root 'manifest.json'
if (Test-Path -LiteralPath $manifest) {
    if ((Get-Content -LiteralPath $manifest -Raw).Trim() -cne $identity.Trim()) {
        throw 'Inputs, binaries or settings changed; use a new output directory'
    }
} else { Set-Content -LiteralPath $manifest -Value $identity -Encoding utf8 }
function Median($values) {
    $sorted = @($values | Sort-Object)
    $middle = [int][math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2) { return $sorted[$middle] }
    return ($sorted[$middle - 1] + $sorted[$middle]) / 2
}
$summary = @()
foreach ($video in $inputs) {
    $name = [IO.Path]::GetFileName($video)
    $reports = @{ reference = @(); candidate = @() }
    for ($round = 1; $round -le $Rounds; $round++) {
        # Alternate the leading configuration. Each run gets a fresh process.
        $order = if ($round % 2) { @(0, 1) } else { @(1, 0) }
        foreach ($index in $order) {
            $label = @('reference', 'candidate')[$index]
            $out = Join-Path $root "$name/round-$round/$label"
            $reportPath = Join-Path $out "$name/report.json"
            New-Item -ItemType Directory -Force $out | Out-Null
            if (-not (Test-Path -LiteralPath $reportPath)) {
                Write-Output "START $name round=$round $label"
                & dotnet $binaries[$index] $out $Threads $Buffer 0 $video *> (Join-Path $out 'process.log')
                if ($LASTEXITCODE) { throw "Failed: $out/process.log" }
            }
            $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
            if ($report.Video -cne $video -or $report.Threads -ne $Threads -or $report.Buffer -ne $Buffer -or
                $report.CancelFraction -ne 0 -or $null -ne $report.ResumeFrame -or $report.ElapsedSeconds -le 0 -or
                $report.PreviewCount -le 0 -or $report.SaveCount -le 0 -or
                $report.StopReason -notin 'Completed', 'EndOfStream') { throw "Invalid report: $reportPath" }
            # Check stored artifacts as well as the report's claimed hashes.
            foreach ($artifact in @(@('state.json', 'StateHash'), @('subtitle.ass', 'SubtitleHash'))) {
                if ((Get-FileHash -LiteralPath (Join-Path (Split-Path $reportPath) $artifact[0])).Hash -ne
                    $report.($artifact[1])) { throw "Artifact mismatch: $reportPath" }
            }
            $reports[$label] += $report
            $first = $reports.reference[0]
            if ($first -and ($report.StateHash -ne $first.StateHash -or $report.SubtitleHash -ne $first.SubtitleHash)) {
                throw "Output mismatch: $reportPath"
            }
            Write-Output "DONE $name round=$round $label seconds=$($report.ElapsedSeconds) previews=$($report.PreviewCount)"
        }
    }
    $paired = @(for ($i = 0; $i -lt $Rounds; $i++) {
        100 * (1 - $reports.candidate[$i].ElapsedSeconds / $reports.reference[$i].ElapsedSeconds)
    })
    $summary += [ordered]@{ Video = $video; Rounds = $Rounds
        ReferenceSeconds = Median $reports.reference.ElapsedSeconds
        CandidateSeconds = Median $reports.candidate.ElapsedSeconds
        PairedReductionPercent = Median $paired
        ReferencePreviewMilliseconds = Median $reports.reference.PreviewMilliseconds
        CandidatePreviewMilliseconds = Median $reports.candidate.PreviewMilliseconds
        ReferencePreviews = Median $reports.reference.PreviewCount
        CandidatePreviews = Median $reports.candidate.PreviewCount
        SameStateAndSubtitle = $true
    }
    $summary | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root 'summary.json') -Encoding utf8
}
