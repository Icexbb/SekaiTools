param(
    [Parameter(Mandatory)][string]$ReferenceDll,
    [Parameter(Mandatory)][string]$CandidateDll,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string[]]$Videos,
    [Parameter(Mandatory)][string]$ResourceDirectory,
    [int]$Rounds = 3,
    [int]$Threads = 4,
    [int]$Buffer = 8
)
$ErrorActionPreference = 'Stop'
if ($Rounds -lt 3 -or $Threads -lt 1 -or $Buffer -lt 1) { throw 'Require at least three rounds and positive thread/buffer counts' }
$binaries = @((Resolve-Path -LiteralPath $ReferenceDll).Path, (Resolve-Path -LiteralPath $CandidateDll).Path)
$inputs = @($Videos | ForEach-Object { (Resolve-Path -LiteralPath $_).Path })
if (@($inputs | Select-Object -Unique).Count -ne $inputs.Count) { throw 'Duplicate videos' }
if (@($inputs | ForEach-Object { [IO.Path]::GetFileName($_) } | Select-Object -Unique).Count -ne $inputs.Count) { throw 'Duplicate names; use separate comparisons' }
$resourceRoot = (Resolve-Path -LiteralPath $ResourceDirectory).Path
$actualResources = [IO.Path]::Combine([Environment]::GetFolderPath('UserProfile'), 'SekaiTools', 'Resource', 'videoProcess')
if ($resourceRoot.TrimEnd('\') -ine $actualResources.TrimEnd('\')) { throw 'ResourceDirectory must fingerprint the actual ResourceManager videoProcess directory' }
$root = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $root | Out-Null
function Fingerprints($paths) {
    @($paths | Sort-Object | ForEach-Object { [ordered]@{ Path=$_; Hash=(Get-FileHash -LiteralPath $_).Hash } })
}
$identity = [ordered]@{
    Rounds=$Rounds; Threads=$Threads; Buffer=$Buffer
    Preview=$false; Profile=$false; Warmup=$false; Mode='Automatic'
    Binaries=@($binaries | ForEach-Object { [ordered]@{ Entry=$_; Files=Fingerprints @(Get-ChildItem (Split-Path $_) -Recurse -File | Where-Object { $_.Extension -in '.dll','.json' } | Select-Object -ExpandProperty FullName) } })
    Inputs=@($inputs | ForEach-Object { [ordered]@{ Video=$_; Files=Fingerprints @($_,[IO.Path]::ChangeExtension($_,'.json'),[IO.Path]::ChangeExtension($_,'.txt')) } })
    Resources=Fingerprints @(Get-ChildItem $resourceRoot -Recurse -File | Select-Object -ExpandProperty FullName)
    CpuDisable=$env:OPENCV_CPU_DISABLE
} | ConvertTo-Json -Depth 10
$manifest = Join-Path $root 'manifest.json'
if (Test-Path -LiteralPath $manifest) {
    if ((Get-Content -LiteralPath $manifest -Raw).Trim() -cne $identity.Trim()) { throw 'Inputs, binaries, resources or settings changed; use a new directory' }
} else { Set-Content -LiteralPath $manifest -Value $identity -Encoding utf8 }
function Median($values) {
    $sorted=@($values | Sort-Object)
    $middle=[int][Math]::Floor($sorted.Count/2)
    if ($sorted.Count % 2) { return $sorted[$middle] }
    ($sorted[$middle-1]+$sorted[$middle])/2
}
$summary=@()
foreach ($video in $inputs) {
    $name=[IO.Path]::GetFileName($video)
    $reports=@{ reference=@(); candidate=@() }
    for ($round=1; $round -le $Rounds; $round++) {
        $order=if ($round % 2) { @(0,1) } else { @(1,0) }
        foreach ($index in $order) {
            $label=@('reference','candidate')[$index]
            $out=Join-Path $root "$name/round-$round/$label"
            $reportPath=Join-Path $out 'run-1.json'
            if (-not (Test-Path -LiteralPath $reportPath)) {
                if (Test-Path -LiteralPath $out) { throw "Incomplete output exists: $out; inspect before restarting" }
                New-Item -ItemType Directory -Force $out | Out-Null
                Write-Output "START $name round=$round $label"
                & dotnet $binaries[$index] $video $out 1 false Automatic false $Threads false false $Buffer false false false *> (Join-Path $out 'process.log')
                if ($LASTEXITCODE) { throw "Recognition failed: $out/process.log" }
            }
            $report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
            if ($report.Video -cne $video -or $report.Run -ne 1 -or $report.Warmup -or $report.Preview -or $report.DetailedProfile -or $report.PrepareGray -or $report.ContentPositionHints -or $report.HardwareDecode -or $report.Mode -ne 'Automatic' -or $report.OpenCvThreads -ne $Threads -or $report.FrameBufferCapacity -ne $Buffer -or $report.StopReason -notin 'Completed','EndOfStream') { throw "Invalid report: $reportPath" }
            $elapsed=[TimeSpan]::Parse($report.Performance.ElapsedTime).TotalSeconds
            if ($elapsed -le 0) { throw "Invalid time: $reportPath" }
            foreach ($artifact in @(@('state-1.json','StateHash'),@('subtitle-1.ass','SubtitleHash'))) {
                if ((Get-FileHash -LiteralPath (Join-Path $out $artifact[0])).Hash -ne $report.($artifact[1])) { throw "Artifact mismatch: $reportPath" }
            }
            $report | Add-Member -NotePropertyName ElapsedSeconds -NotePropertyValue $elapsed
            $first=$reports.reference[0]
            if ($first -and ($report.StateHash -ne $first.StateHash -or $report.SubtitleHash -ne $first.SubtitleHash -or $report.StopReason -ne $first.StopReason)) { throw "Output mismatch: $reportPath" }
            $reports[$label]+=$report
            Write-Output "DONE $name round=$round $label seconds=$elapsed"
        }
    }
    $paired=@(for ($i=0; $i -lt $Rounds; $i++) { 100*(1-$reports.candidate[$i].ElapsedSeconds/$reports.reference[$i].ElapsedSeconds) })
    $summary += [ordered]@{ Video=$video; Rounds=$Rounds; ReferenceSeconds=Median $reports.reference.ElapsedSeconds; CandidateSeconds=Median $reports.candidate.ElapsedSeconds; PairedReductionPercent=Median $paired; PairedReductions=$paired; ReferencePeakMiB=Median @($reports.reference | ForEach-Object { $_.PeakWorkingSet/1048576 }); CandidatePeakMiB=Median @($reports.candidate | ForEach-Object { $_.PeakWorkingSet/1048576 }); SameFullStateAndAss=$true; StopReason=$reports.reference[0].StopReason }
    $summary | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root 'summary.json') -Encoding utf8
}

