param(
    [Parameter(Mandatory)][string]$ReferenceDll,
    [Parameter(Mandatory)][string]$CandidateDll,
    [Parameter(Mandatory)][string]$Plugin,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string[]]$Videos
)
$ErrorActionPreference = 'Stop'
if (Get-ChildItem Env: | Where-Object Name -Match '^(OPENCV_|SEKAI_BENCHMARK_)') { throw 'Unexpected experiment environment' }
$reference = (Resolve-Path -LiteralPath $ReferenceDll).Path
$candidate = (Resolve-Path -LiteralPath $CandidateDll).Path
$pluginPath = (Resolve-Path -LiteralPath $Plugin).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh output directory' }
if ($Videos.Count -ne 5 -or @($Videos | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_) } | Sort-Object -Unique).Count -ne 5) {
    throw 'Expected five distinct standard groups'
}
if ((Get-FileHash -LiteralPath $reference).Hash -ne (Get-FileHash -LiteralPath $candidate).Hash) { throw 'Use the same managed runner' }
$experiment = Get-Content -LiteralPath (Join-Path (Split-Path $candidate) 'native-experiment.json') -Raw | ConvertFrom-Json
$paths = @($pluginPath, $PSCommandPath, (Join-Path $PSScriptRoot 'VerifyPeriodicColorPair.py'))
foreach ($video in $Videos) { $paths += @($video, [IO.Path]::ChangeExtension($video, '.json'), [IO.Path]::ChangeExtension($video, '.txt')) }
foreach ($directory in @((Split-Path $reference), (Split-Path $candidate))) {
    $paths += @(Get-ChildItem -LiteralPath $directory -Recurse -File | Where-Object Extension -In '.dll','.json' | Select-Object -ExpandProperty FullName)
}
$paths += @(Get-ChildItem -LiteralPath (Join-Path ([Environment]::GetFolderPath('UserProfile')) 'SekaiTools/Resource/videoProcess') -Recurse -File | Select-Object -ExpandProperty FullName)
$manifest = @($paths | Sort-Object -Unique | ForEach-Object { @{ Path = (Resolve-Path -LiteralPath $_).Path; Hash = (Get-FileHash -LiteralPath $_).Hash } })
New-Item -ItemType Directory -Path $output | Out-Null
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'manifest-before.json') -Encoding utf8
$rows = @()
foreach ($video in $Videos) {
    $group = Join-Path $output ([IO.Path]::GetFileNameWithoutExtension($video))
    foreach ($mode in @('reference','gray')) {
        $dll = if ($mode -eq 'reference') { $reference } else { $candidate }
        $destination = Join-Path $group $mode
        $arguments = @($dll, $video, $destination, '1','true','Automatic','false','default','false','false','default','false','false','false')
        if ($mode -eq 'gray') { $arguments += @('16', $pluginPath, 'true') }
        & dotnet @arguments *> "$group-$mode.log"
        if ($LASTEXITCODE) { throw "Automatic defaults failed: $video $mode" }
    }
    $audit = & python (Join-Path $PSScriptRoot 'VerifyPeriodicColorPair.py') (Join-Path $group 'reference') (Join-Path $group 'gray') --require-native-identity
    if ($LASTEXITCODE) { throw "Complete defaults output differs: $video" }
    $a = Get-Content -LiteralPath (Join-Path $group 'reference/run-1.json') -Raw | ConvertFrom-Json
    $b = Get-Content -LiteralPath (Join-Path $group 'gray/run-1.json') -Raw | ConvertFrom-Json
    $capacities = @('reference','gray' | ForEach-Object {
        $matches = [regex]::Matches((Get-Content -LiteralPath "$group-$_.log" -Raw), '帧缓冲容量=(\d+)')
        if ($matches.Count -ne 1) { throw 'Cannot identify actual automatic buffer capacity' }
        [int]$matches[0].Groups[1].Value
    })
    if ($null -ne $a.FrameBufferCapacity -or $null -ne $b.FrameBufferCapacity) { throw 'Automatic buffer was overridden' }
    foreach ($report in @($a,$b)) {
        $expected = if ($report.NativePeriodicColorCapture) { $experiment.NativeHash } else { '0A697D9F6CE35463ECC434CD881920EBDEB2E075F305BEE04685A44278B98D12' }
        if (($report.NativeLibraries | Where-Object { [IO.Path]::GetFileName($_.Path) -eq 'cvextern.dll' }).Hash -ne $expected) {
            throw 'Unexpected actual runtime'
        }
    }
    $row = @{ Video=$video; CompleteStateAndAssEquivalent=$true; OpenCvThreads=$a.OpenCvThreads;
        AutomaticBuffer=$true; ReferenceActualBuffer=$capacities[0]; GrayActualBuffer=$capacities[1];
        ReferenceSeconds=$a.WallMilliseconds/1000; GraySeconds=$b.WallMilliseconds/1000;
        SingleRunReductionPercent=(1-$b.WallMilliseconds/$a.WallMilliseconds)*100;
        RepeatedPerformanceQualified=$false; GuiPreviewQualified=$false; ColorPreviewCallbacksQualified=$true }
    $rows += $row
    Write-Output ($row | ConvertTo-Json -Compress)
}
foreach ($entry in $manifest) { if ((Get-FileHash -LiteralPath $entry.Path).Hash -ne $entry.Hash) { throw "Manifest changed: $($entry.Path)" } }
$rows | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'summary.json') -Encoding utf8

