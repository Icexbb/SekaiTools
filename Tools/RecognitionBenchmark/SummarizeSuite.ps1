param(
    [Parameter(Mandatory)][string]$ResultsDirectory,
    [string]$ReferenceResultsDirectory
)
$ErrorActionPreference = 'Stop'
function Get-Median($values) {
    $sorted = @($values | Sort-Object)
    $middle = [int][Math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2) { return $sorted[$middle] }
    return ($sorted[$middle - 1] + $sorted[$middle]) / 2
}
$files = @(Get-ChildItem -LiteralPath $ResultsDirectory -Filter 'run-*.json' -Recurse)
foreach ($group in ($files | Group-Object DirectoryName)) {
    $runs = @($group.Group | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json } |
        Where-Object { -not $_.Warmup })
    if (-not $runs.Count) { continue }
    $first = $runs[0]
    $relative = [IO.Path]::GetRelativePath($ResultsDirectory, $group.Name)
    $variant = Split-Path $relative -Leaf
    $id = Split-Path $relative -Parent
    $sameOutput = $null
    if ($first.DecodeOnly) {
        foreach ($r in $runs) {
            if ($r.DeclaredFrames -gt 0 -and $r.Frames -ne [math]::Round($r.DeclaredFrames)) {
                throw "Decoded frame count differs from declared count: $($r.Video)"
            }
        }
        $seconds = @($runs | ForEach-Object { $_.ElapsedMilliseconds / 1000 })
    } else {
        $seconds = @($runs | ForEach-Object { [TimeSpan]::Parse($_.Performance.ElapsedTime).TotalSeconds })
        $states = @($runs.StateHash | Sort-Object -Unique)
        $subtitles = @($runs.SubtitleHash | Sort-Object -Unique)
        $sameOutput = $states.Count -eq 1 -and $subtitles.Count -eq 1 -and
            -not [string]::IsNullOrEmpty($states[0]) -and -not [string]::IsNullOrEmpty($subtitles[0])
        if ($ReferenceResultsDirectory) {
            $reference = Get-Content -LiteralPath (Join-Path $ReferenceResultsDirectory "$id/reference/run-1.json") -Raw | ConvertFrom-Json
            $sameOutput = $sameOutput -and $reference.StateHash -eq $states[0] -and $reference.SubtitleHash -eq $subtitles[0]
        }
        if (-not $sameOutput) { throw "Recognition output mismatch: $($group.Name)" }
    }
    [pscustomobject]@{
        Video = $first.Video; Variant = $variant; Runs = $runs.Count
        MedianSeconds = Get-Median $seconds
        MinSeconds = ($seconds | Measure-Object -Minimum).Minimum
        MaxSeconds = ($seconds | Measure-Object -Maximum).Maximum
        PeakMiB = ($runs.PeakWorkingSet | Measure-Object -Maximum).Maximum / 1MB
        SameOutput = $sameOutput
    }
}
