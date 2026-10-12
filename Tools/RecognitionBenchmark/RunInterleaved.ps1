param(
    [Parameter(Mandatory)][string]$InputDirectory,
    [Parameter(Mandatory)][string]$BenchmarkDll,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [int]$Rounds = 3
)
$ErrorActionPreference = 'Stop'
if ($Rounds -lt 3) { throw 'At least three rounds required' }
$dll = (Resolve-Path -LiteralPath $BenchmarkDll).Path
$root = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $root | Out-Null
$videos = @(Get-ChildItem -LiteralPath $InputDirectory -Recurse -File | Where-Object {
    $_.Extension -in '.mp4', '.mkv', '.avi', '.mov', '.webm' -and
    (Test-Path ([IO.Path]::ChangeExtension($_.FullName, '.json'))) -and
    (Test-Path ([IO.Path]::ChangeExtension($_.FullName, '.txt')))
} | Sort-Object FullName)
if (-not $videos.Count) { throw 'No complete input groups' }
$configs = @(
    [pscustomobject]@{ Label = 'threads16buffer16'; Threads = 16; Buffer = 16 },
    [pscustomobject]@{ Label = 'threads4buffer16'; Threads = 4; Buffer = 16 },
    [pscustomobject]@{ Label = 'threads4buffer8'; Threads = 4; Buffer = 8 }
)
$identity = [ordered]@{
    Binary = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
    Core = (Get-FileHash -LiteralPath (Join-Path (Split-Path $dll) 'SekaiToolsCore.dll') -Algorithm SHA256).Hash
    Configurations = $configs
    Inputs = @($videos | ForEach-Object {
        [ordered]@{ Video = $_.FullName; Files = @($_.FullName,
            [IO.Path]::ChangeExtension($_.FullName, '.json'), [IO.Path]::ChangeExtension($_.FullName, '.txt')) |
            ForEach-Object { [ordered]@{ Path = $_; Hash = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash } } }
    })
} | ConvertTo-Json -Depth 8
$manifest = Join-Path $root 'manifest.json'
if (Test-Path $manifest) {
    if ((Get-Content -LiteralPath $manifest -Raw).Trim() -ne $identity.Trim()) {
        throw 'Inputs or binaries changed; use a new output directory'
    }
} else { Set-Content -LiteralPath $manifest -Value $identity -Encoding utf8 }
foreach ($video in $videos) {
    $id = [IO.Path]::GetRelativePath($InputDirectory, $video.FullName).Replace('\', '__').Replace('/', '__')
    for ($round = 1; $round -le $Rounds; $round++) {
        for ($index = 0; $index -lt $configs.Count; $index++) {
            $cfg = $configs[($index + $round - 1) % $configs.Count]
            $out = Join-Path $root "$id/$($cfg.Label)/round-$round"
            New-Item -ItemType Directory -Force $out | Out-Null
            $report = Join-Path $out 'run-1.json'
            if (-not (Test-Path $report)) {
                Write-Output "START $($video.BaseName) round=$round $($cfg.Label)"
                & dotnet $dll $video.FullName $out 1 false Automatic false $cfg.Threads false false $cfg.Buffer false false *> (Join-Path $out 'process.log')
                if ($LASTEXITCODE) { throw "Failed: $out/process.log" }
            }
            $r = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
            $reference = Get-Content -LiteralPath (Join-Path $root "$id/threads16buffer16/round-1/run-1.json") -Raw | ConvertFrom-Json
            if ($r.StateHash -ne $reference.StateHash -or $r.SubtitleHash -ne $reference.SubtitleHash) {
                throw "Output mismatch: $out"
            }
            Write-Output "DONE $($video.BaseName) round=$round $($cfg.Label) seconds=$([TimeSpan]::Parse($r.Performance.ElapsedTime).TotalSeconds) peakMiB=$([math]::Round($r.PeakWorkingSet/1MB,1))"
        }
    }
}
