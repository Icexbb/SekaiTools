param(
    [Parameter(Mandatory)][string]$CandidateDll,
    [Parameter(Mandatory)][string]$Plugin,
    [Parameter(Mandatory)][string]$ReferenceRoot,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string[]]$Videos
)
$ErrorActionPreference = 'Stop'
$candidate = (Resolve-Path -LiteralPath $CandidateDll).Path
$pluginPath = (Resolve-Path -LiteralPath $Plugin).Path
$references = (Resolve-Path -LiteralPath $ReferenceRoot).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh output directory' }
if ($Videos.Count -ne 5 -or @($Videos | Sort-Object -Unique).Count -ne 5) { throw 'Expected five groups' }
if (Get-ChildItem Env: | Where-Object Name -Match '^(OPENCV_|SEKAI_BENCHMARK_)') { throw 'Unexpected experiment environment' }
$experiment = Get-Content (Join-Path (Split-Path $candidate) 'native-experiment.json') -Raw | ConvertFrom-Json
$verifier = Join-Path $PSScriptRoot 'VerifyFactoryPair.py'
$paths = @($PSCommandPath, $verifier, $pluginPath)
$paths += @(Get-ChildItem (Split-Path $candidate) -Recurse -File | Where-Object Extension -In '.dll','.json' | Select-Object -ExpandProperty FullName)
$paths += @(Get-ChildItem (Join-Path ([Environment]::GetFolderPath('UserProfile')) 'SekaiTools/Resource/videoProcess') -Recurse -File | Select-Object -ExpandProperty FullName)
foreach ($video in $Videos) {
    $paths += @($video, [IO.Path]::ChangeExtension($video, '.json'), [IO.Path]::ChangeExtension($video, '.txt'))
    $reference = Join-Path $references ([IO.Path]::GetFileNameWithoutExtension($video) + '/reference')
    $paths += @('run-1.json','state-1.json','subtitle-1.ass' | ForEach-Object { Join-Path $reference $_ })
}
$manifest = @($paths | Sort-Object -Unique | ForEach-Object { @{ Path=(Resolve-Path -LiteralPath $_).Path; Hash=(Get-FileHash -LiteralPath $_).Hash } })
New-Item -ItemType Directory $output | Out-Null
$manifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $output 'manifest-before.json') -Encoding utf8
$rows = @()
foreach ($video in $Videos) {
    $name = [IO.Path]::GetFileNameWithoutExtension($video)
    $destination = Join-Path $output $name
    & dotnet $candidate $video $destination 1 true Automatic false default false false default false false false 16 $pluginPath true true *> (Join-Path $output "$name.log")
    if ($LASTEXITCODE) { throw "Factory recognition failed: $name" }
    $audit = & python $verifier (Join-Path $references "$name/reference") $destination
    if ($LASTEXITCODE) { throw "Factory output differs: $name" }
    $report = Get-Content (Join-Path $destination 'run-1.json') -Raw | ConvertFrom-Json
    if ($report.PeriodicColorNativeHash -ne $experiment.NativeHash) { throw 'Unexpected runtime' }
    foreach ($library in $report.NativeLibraries) {
        if (@($manifest | Where-Object { $_.Path -eq $library.Path -and $_.Hash -eq $library.Hash }).Count -ne 1) { throw 'Actual library outside manifest' }
    }
    $rows += @{ Video=$video; Audit=($audit | ConvertFrom-Json); ReportHash=(Get-FileHash (Join-Path $destination 'run-1.json')).Hash }
    Write-Output $audit
}
foreach ($entry in $manifest) { if ((Get-FileHash -LiteralPath $entry.Path).Hash -ne $entry.Hash) { throw "Manifest changed: $($entry.Path)" } }
@{ VerificationPassed=$true; ManifestUnchanged=$true; Results=$rows; NoPerformanceConclusion=$true } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $output 'summary.json') -Encoding utf8
