param([Parameter(Mandatory)][string]$ResultsDirectory)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($ResultsDirectory)
$reports = @(Get-ChildItem -LiteralPath $root -Recurse -Filter run-1.json | ForEach-Object {
    $parts = [IO.Path]::GetRelativePath($root, $_.FullName).Split([IO.Path]::DirectorySeparatorChar)
    [pscustomobject]@{ Video = $parts[0]; Configuration = $parts[1]; Round = [int]$parts[2].Replace('round-', '');
        Report = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json }
})
function Median($values) {
    $sorted = @($values | Sort-Object)
    $middle = [int][math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2) { return $sorted[$middle] }
    return ($sorted[$middle - 1] + $sorted[$middle]) / 2
}
foreach ($video in ($reports | Group-Object Video)) {
    $states = @($video.Group.Report.StateHash | Sort-Object -Unique)
    $subtitles = @($video.Group.Report.SubtitleHash | Sort-Object -Unique)
    if ($states.Count -ne 1 -or $subtitles.Count -ne 1 -or -not $states[0] -or -not $subtitles[0]) { throw "Output mismatch: $($video.Name)" }
    $baseline = @($video.Group | Where-Object Configuration -eq threads16buffer16)
    foreach ($configuration in ($video.Group | Group-Object Configuration)) {
        if ($configuration.Count -lt 3) { throw "Incomplete rounds: $($video.Name)/$($configuration.Name)" }
        $seconds = @($configuration.Group | ForEach-Object { [TimeSpan]::Parse($_.Report.Performance.ElapsedTime).TotalSeconds })
        $paired = @($configuration.Group | ForEach-Object {
            $current = $_
            $reference = @($baseline | Where-Object Round -eq $current.Round)
            if ($reference.Count -ne 1) { throw 'Missing reference round' }
            $before = [TimeSpan]::Parse($reference[0].Report.Performance.ElapsedTime).TotalSeconds
            $after = [TimeSpan]::Parse($current.Report.Performance.ElapsedTime).TotalSeconds
            100 * ($before - $after) / $before
        })
        [pscustomobject]@{ Video = $video.Name; Configuration = $configuration.Name; Rounds = $configuration.Count;
            MedianSeconds = Median $seconds; PairedMedianReductionPercent = Median $paired;
            MinSeconds = ($seconds | Measure-Object -Minimum).Minimum; MaxSeconds = ($seconds | Measure-Object -Maximum).Maximum;
            MedianPeakMiB = Median @($configuration.Group | ForEach-Object { $_.Report.PeakWorkingSet / 1MB }); SameOutput = $true }
    }
}
