param(
    [Parameter(Mandatory)][string]$ReferenceDll,
    [Parameter(Mandatory)][string]$CandidateDll,
    [Parameter(Mandatory)][string]$Plugin,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string[]]$Videos
)
$ErrorActionPreference = 'Stop'
$reference = (Resolve-Path -LiteralPath $ReferenceDll).Path
$candidate = (Resolve-Path -LiteralPath $CandidateDll).Path
$pluginPath = (Resolve-Path -LiteralPath $Plugin).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh output directory' }
if ($Videos.Count -ne 5 -or @($Videos | Sort-Object -Unique).Count -ne 5) { throw 'Expected five groups' }
if ((Get-FileHash $reference).Hash -ne (Get-FileHash $candidate).Hash) { throw 'Use identical managed runners' }
if (Get-ChildItem Env: | Where-Object Name -Match '^(OPENCV_|SEKAI_BENCHMARK_)') { throw 'Unexpected environment' }
$experiment = Get-Content (Join-Path (Split-Path $candidate) 'native-experiment.json') -Raw | ConvertFrom-Json
$verifier = Join-Path $PSScriptRoot 'VerifyFactoryPair.py'
$paths = @($PSCommandPath,$verifier,$pluginPath)
foreach ($video in $Videos) { $paths += @($video,[IO.Path]::ChangeExtension($video,'.json'),[IO.Path]::ChangeExtension($video,'.txt')) }
foreach ($directory in @((Split-Path $reference),(Split-Path $candidate))) {
    $paths += @(Get-ChildItem $directory -Recurse -File | Where-Object Extension -In '.dll','.json' | Select-Object -ExpandProperty FullName)
}
$paths += @(Get-ChildItem (Join-Path ([Environment]::GetFolderPath('UserProfile')) 'SekaiTools/Resource/videoProcess') -Recurse -File | Select-Object -ExpandProperty FullName)
$manifest = @($paths | Sort-Object -Unique | ForEach-Object { @{Path=(Resolve-Path -LiteralPath $_).Path; Hash=(Get-FileHash -LiteralPath $_).Hash} })
New-Item -ItemType Directory $output | Out-Null
$manifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $output 'manifest-before.json') -Encoding utf8
$rows = @()
$stable = @{}
foreach ($round in 1..3) {
    foreach ($video in $Videos) {
        $name = [IO.Path]::GetFileNameWithoutExtension($video)
        $group = Join-Path $output "round-$round/$name"
        New-Item -ItemType Directory $group -Force | Out-Null
        $order = if ($round -eq 2) { @('factory','reference') } else { @('reference','factory') }
        $processTimes = @{}
        foreach ($mode in $order) {
            $dll = if ($mode -eq 'factory') { $candidate } else { $reference }
            $arguments = @($dll,$video,(Join-Path $group $mode),'1','true','Automatic','false','default','false','false','default','false','false','false')
            if ($mode -eq 'factory') { $arguments += @('16',$pluginPath,'true','true') }
            $timer = [Diagnostics.Stopwatch]::StartNew()
            & dotnet @arguments *> (Join-Path $group "$mode.log")
            $timer.Stop()
            if ($LASTEXITCODE) { throw "Run failed: $round $name $mode" }
            $processTimes[$mode] = $timer.Elapsed.TotalMilliseconds
        }
        $audit = & python $verifier (Join-Path $group 'reference') (Join-Path $group 'factory')
        if ($LASTEXITCODE) { throw "Output audit failed: $round $name" }
        $a = Get-Content (Join-Path $group 'reference/run-1.json') -Raw | ConvertFrom-Json
        $b = Get-Content (Join-Path $group 'factory/run-1.json') -Raw | ConvertFrom-Json
        $native = @($a.NativeLibraries | Where-Object { [IO.Path]::GetFileName($_.Path) -eq 'cvextern.dll' })
        if ($native.Count -ne 1 -or $native[0].Hash -ne '0A697D9F6CE35463ECC434CD881920EBDEB2E075F305BEE04685A44278B98D12' -or $b.PeriodicColorNativeHash -ne $experiment.NativeHash) { throw 'Unexpected native runtime' }
        foreach ($report in @($a,$b)) {
            if ($report.OpenCvThreads -ne 16 -or $null -ne $report.FrameBufferCapacity -or $report.DetailedProfile -or $report.ActualDecoderThreads -ne 16) { throw 'Unexpected defaults' }
        }
        $fingerprint = (Get-FileHash (Join-Path $group 'reference/state-1.json')).Hash
        if ($stable.ContainsKey($name) -and $stable[$name] -ne $fingerprint) { throw 'Reference state changed across rounds' }
        $stable[$name] = $fingerprint
        $rows += @{ Round=$round; Video=$video; VerificationPassed=$true;
            ReferenceMilliseconds=$a.WallMilliseconds; FactoryMilliseconds=$b.WallMilliseconds;
            ProcessingReductionPercent=(1-$b.WallMilliseconds/$a.WallMilliseconds)*100;
            ReferenceProcessMilliseconds=$processTimes.reference; FactoryProcessMilliseconds=$processTimes.factory;
            ProcessReductionPercent=(1-$processTimes.factory/$processTimes.reference)*100;
            ReferencePeakWorkingSet=$a.PeakWorkingSet; FactoryPeakWorkingSet=$b.PeakWorkingSet }
        $rows[-1] | ConvertTo-Json -Compress | Write-Output
        $rows | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $output 'completed-pairs.json') -Encoding utf8
    }
}
foreach ($entry in $manifest) { if ((Get-FileHash -LiteralPath $entry.Path).Hash -ne $entry.Hash) { throw "Manifest changed: $($entry.Path)" } }
@{ VerificationPassed=$true; ManifestUnchanged=$true; Pairs=$rows; LiveGuiQualified=$false } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $output 'summary.json') -Encoding utf8
