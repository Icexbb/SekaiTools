param(
    [Parameter(Mandatory)][string]$Baseline,
    [Parameter(Mandatory)][string]$Candidate
)
$ErrorActionPreference = 'Stop'
function Read-Runs([string]$Directory) {
    $runs = @(Get-ChildItem -LiteralPath $Directory -Filter 'run-*.json' |
        ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json } |
        Where-Object { -not $_.Warmup })
    if ($runs.Count -lt 3) { throw "At least three measured runs required: $Directory" }
    return $runs
}
function Get-Median($Runs) {
    $values = @($Runs | ForEach-Object { [TimeSpan]::Parse($_.Performance.ElapsedTime).TotalMilliseconds } | Sort-Object)
    $middle = [int][Math]::Floor($values.Count / 2)
    if ($values.Count % 2) { return $values[$middle] }
    return ($values[$middle - 1] + $values[$middle]) / 2
}
$before = @(Read-Runs $Baseline)
$after = @(Read-Runs $Candidate)
$stateHashes = @(@($before) + @($after) | ForEach-Object { $_.StateHash } | Sort-Object -Unique)
$subtitleHashes = @(@($before) + @($after) | ForEach-Object { $_.SubtitleHash } | Sort-Object -Unique)
$sameState = $stateHashes.Count -eq 1
$sameSubtitle = $subtitleHashes.Count -eq 1 -and -not [string]::IsNullOrEmpty($subtitleHashes[0])
$baselineMs = Get-Median $before
$candidateMs = Get-Median $after
[pscustomobject]@{
    BaselineMilliseconds = $baselineMs
    CandidateMilliseconds = $candidateMs
    ReductionPercent = 100 * ($baselineMs - $candidateMs) / $baselineMs
    SameState = $sameState
    SameSubtitle = $sameSubtitle
} | ConvertTo-Json
if (-not $sameState -or -not $sameSubtitle) { throw 'Recognition state or ASS output differs; inspect before accepting.' }
