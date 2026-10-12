param(
    [Parameter(Mandatory)][string]$ReferenceDll,
    [Parameter(Mandatory)][string]$CandidateDll,
    [Parameter(Mandatory)][string]$Plugin,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string[]]$Videos,
    [ValidateRange(3,3)][int]$Rounds = 3
)
$ErrorActionPreference = 'Stop'
if (Get-ChildItem Env: | Where-Object Name -Match '^(OPENCV_|SEKAI_BENCHMARK_)') { throw 'Unexpected experiment environment' }
$reference = (Resolve-Path -LiteralPath $ReferenceDll).Path
$candidate = (Resolve-Path -LiteralPath $CandidateDll).Path
$pluginPath = (Resolve-Path -LiteralPath $Plugin).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh output directory; inspect an existing job before continuing it' }
if ($Videos.Count -ne 5) { throw 'Expected five standard video groups' }
$inputVideos = @($Videos | ForEach-Object { (Resolve-Path -LiteralPath $_).Path })
if (@($inputVideos | Sort-Object -Unique).Count -ne 5 -or
    @($inputVideos | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_) } | Sort-Object -Unique).Count -ne 5) {
    throw 'Distinct video paths and basenames required'
}
if ((Get-FileHash -LiteralPath $reference).Hash -ne (Get-FileHash -LiteralPath $candidate).Hash) { throw 'Use the same managed runner' }
$nativeExperiment = Get-Content -LiteralPath (Join-Path (Split-Path $candidate) 'native-experiment.json') -Raw | ConvertFrom-Json
$paths = @($pluginPath, $PSCommandPath, (Join-Path $PSScriptRoot 'VerifyGrayPair.py'), (Join-Path $PSScriptRoot 'SummarizeGrayComparison.py'))
foreach ($video in $inputVideos) {
    $paths += @($video, [IO.Path]::ChangeExtension($video, '.json'), [IO.Path]::ChangeExtension($video, '.txt'))
}
foreach ($directory in @((Split-Path $reference), (Split-Path $candidate))) {
    $paths += @(Get-ChildItem -LiteralPath $directory -Recurse -File | Where-Object Extension -In '.dll','.json' | Select-Object -ExpandProperty FullName)
}
$resource = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'SekaiTools/Resource/videoProcess'
$paths += @(Get-ChildItem -LiteralPath $resource -Recurse -File | Select-Object -ExpandProperty FullName)
$manifest = @($paths | Sort-Object -Unique | ForEach-Object { @{ Path = $_; Hash = (Get-FileHash -LiteralPath $_).Hash } })
New-Item -ItemType Directory -Path $output | Out-Null
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'manifest-before.json') -Encoding utf8
@{ ReferenceDll = $reference; CandidateDll = $candidate; Plugin = $pluginPath; Videos = $inputVideos; Rounds = $Rounds;
    ExpectedReferenceNativeHash = '0A697D9F6CE35463ECC434CD881920EBDEB2E075F305BEE04685A44278B98D12';
    ExpectedCandidateNativeHash = $nativeExperiment.NativeHash;
    ReferenceIdentity = 'OriginalPackage'; CandidateIdentity = 'IppNativeGray'; NoGuiPreviewQualification = $true
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'schedule.json') -Encoding utf8
for ($round = 1; $round -le $Rounds; $round++) {
    foreach ($video in $inputVideos) {
        $pair = Join-Path $output ([IO.Path]::GetFileNameWithoutExtension($video) + "/round-$round")
        $order = if ($round % 2) { @('reference','gray') } else { @('gray','reference') }
        foreach ($mode in $order) {
            $dll = if ($mode -eq 'reference') { $reference } else { $candidate }
            $destination = Join-Path $pair $mode
            New-Item -ItemType Directory -Path $destination -Force | Out-Null
            $arguments = @($dll, $video, $destination, '1','false','Automatic','false','4','false','false','8','false','false','false')
            if ($mode -eq 'gray') { $arguments += @('16', $pluginPath) }
            & dotnet @arguments *> "$destination.log"
            if ($LASTEXITCODE) { throw "Recognition failed: $video round $round $mode" }
        }
        & python (Join-Path $PSScriptRoot 'VerifyGrayPair.py') (Join-Path $pair 'reference') (Join-Path $pair 'gray') --require-native-identity
        if ($LASTEXITCODE) { throw "Full gray verification failed: $video round $round" }
    }
}
& python (Join-Path $PSScriptRoot 'SummarizeGrayComparison.py') $output
if ($LASTEXITCODE) { throw 'Gray comparison audit failed' }
