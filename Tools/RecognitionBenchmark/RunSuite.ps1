param(
    [Parameter(Mandatory)][string]$InputDirectory,
    [Parameter(Mandatory)][string]$BenchmarkDll,
    [Parameter(Mandatory)][string]$ReferenceDll,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateSet('screen', 'threads', 'buffers', 'decode', 'preview', 'verify')][string]$Stage = 'screen',
    [int]$Runs = 1,
    [string]$ReferenceResultsDirectory,
    [string[]]$VideoNames = @()
)
$ErrorActionPreference = 'Stop'
if ($Runs -lt 1) { throw 'Runs must be positive' }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $outputRoot | Out-Null
$videos = @(Get-ChildItem -LiteralPath $InputDirectory -File -Recurse |
    Where-Object { $_.Extension -in '.mp4', '.mkv', '.avi', '.mov', '.webm' } | Sort-Object FullName)
$manifest = @($videos | ForEach-Object {
    [pscustomobject]@{ Video = $_.FullName; Length = $_.Length;
        Script = Test-Path ([IO.Path]::ChangeExtension($_.FullName, '.json'));
        Translation = Test-Path ([IO.Path]::ChangeExtension($_.FullName, '.txt')) }
})
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRoot 'inputs.json') -Encoding utf8
function New-Config($label, $threads = 16, $buffer = 16, $hints = 'false', $preview = 'false') {
    [pscustomobject]@{ Label = $label; Threads = $threads; Buffer = $buffer; Hints = $hints; Preview = $preview }
}
$configs = switch ($Stage) {
    'screen' { New-Config reference; New-Config hints 16 16 true }
    'threads' { New-Config threads4 4; New-Config threads8 8; New-Config threads16 16 }
    'buffers' { New-Config buffer0 16 0; New-Config buffer4 16 4; New-Config buffer8 16 8; New-Config buffer16 16 16 }
    'decode' { New-Config decode 16 0 }
    'preview' { New-Config previewOff; New-Config previewOn 16 16 false true }
    'verify' { New-Config reference; New-Config hints 16 16 true }
}
foreach ($video in $videos) {
    if ($VideoNames.Count -and $video.BaseName -notin $VideoNames) { continue }
    $complete = (Test-Path ([IO.Path]::ChangeExtension($video.FullName, '.json'))) -and
        (Test-Path ([IO.Path]::ChangeExtension($video.FullName, '.txt')))
    if ($Stage -ne 'decode' -and -not $complete) { continue }
    $id = [IO.Path]::GetRelativePath($InputDirectory, $video.FullName).Replace('\', '__').Replace('/', '__')
    foreach ($cfg in $configs) {
        $label = $cfg.Label
        $threads = $cfg.Threads
        $buffer = $cfg.Buffer
        $hints = $cfg.Hints
        $preview = $cfg.Preview
        $out = Join-Path $outputRoot "$id/$label"
        New-Item -ItemType Directory -Force $out | Out-Null
        $lastRun = Join-Path $out "run-$Runs.json"
        if (-not (Test-Path $lastRun)) {
            $dll = if ($label -eq 'reference') { $ReferenceDll } else { $BenchmarkDll }
            $warmup = if ($Runs -gt 1) { 'true' } else { 'false' }
            $decode = if ($Stage -eq 'decode') { 'true' } else { 'false' }
            Write-Output "START $($video.BaseName) $label"
            & dotnet $dll $video.FullName $out $Runs $preview Automatic false $threads false $warmup $buffer $hints $decode *> (Join-Path $out 'process.log')
            if ($LASTEXITCODE) { throw "Benchmark failed; inspect $out/process.log" }
        }
        $r = Get-Content -LiteralPath $lastRun -Raw | ConvertFrom-Json
        $seconds = if ($Stage -eq 'decode') { $r.ElapsedMilliseconds / 1000 } else { [TimeSpan]::Parse($r.Performance.ElapsedTime).TotalSeconds }
        Write-Output "DONE $($video.BaseName) $label seconds=$seconds"
        if ($ReferenceResultsDirectory -and $Stage -ne 'decode') {
            $before = Get-Content -LiteralPath (Join-Path $ReferenceResultsDirectory "$id/reference/run-1.json") -Raw | ConvertFrom-Json
            for ($run = 1; $run -le $Runs; $run++) {
                $after = Get-Content -LiteralPath (Join-Path $out "run-$run.json") -Raw | ConvertFrom-Json
                if ($before.StateHash -ne $after.StateHash -or $before.SubtitleHash -ne $after.SubtitleHash) {
                    throw "Reference output mismatch: $($video.FullName), $label, run $run"
                }
            }
        }
        if ($Stage -in 'screen', 'verify' -and $label -eq 'hints') {
            for ($run = 1; $run -le $Runs; $run++) {
                $before = Get-Content -LiteralPath (Join-Path $outputRoot "$id/reference/run-$run.json") -Raw | ConvertFrom-Json
                $after = Get-Content -LiteralPath (Join-Path $out "run-$run.json") -Raw | ConvertFrom-Json
                if ($before.StateHash -ne $after.StateHash -or $before.SubtitleHash -ne $after.SubtitleHash) {
                    throw "Output mismatch: $($video.FullName), run $run"
                }
            }
        }
    }
}
