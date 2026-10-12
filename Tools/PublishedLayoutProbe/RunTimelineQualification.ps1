param(
    [Parameter(Mandatory)][string]$ReferencePackage,
    [Parameter(Mandatory)][string]$CandidatePackage,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string[]]$Videos
)
$ErrorActionPreference = 'Stop'
$reference = (Resolve-Path -LiteralPath $ReferencePackage).Path
$candidate = (Resolve-Path -LiteralPath $CandidatePackage).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use fresh output' }
$inputs = @($Videos | ForEach-Object { (Resolve-Path -LiteralPath $_).Path })
if ($inputs.Count -ne 5 -or @($inputs | Sort-Object -Unique).Count -ne 5) { throw 'Five distinct videos required' }
if ((Get-FileHash "$reference/PublishedLayoutProbe.dll").Hash -ne (Get-FileHash "$candidate/PublishedLayoutProbe.dll").Hash) { throw 'Use identical bootstrap' }
$experiment = Get-Content "$candidate/native-experiment.json" -Raw | ConvertFrom-Json
$paths = @($inputs) + @($PSCommandPath, (Join-Path $PSScriptRoot 'Program.cs'))
foreach ($package in @($reference,$candidate)) {
    $paths += @(Get-ChildItem -LiteralPath $package -Recurse -File | Where-Object Extension -In '.dll','.json' | Select-Object -ExpandProperty FullName)
}
$manifest = @($paths | Sort-Object -Unique | ForEach-Object { @{Path=$_; Hash=(Get-FileHash -LiteralPath $_).Hash} })
New-Item -ItemType Directory -Path $output | Out-Null
$manifest | ConvertTo-Json -Depth 5 | Set-Content "$output/manifest-before.json" -Encoding utf8
$rows = @()
for ($i=0; $i -lt $inputs.Count; $i++) {
    $video = $inputs[$i]
    foreach ($mode in @('reference','candidate')) {
        $package = if ($mode -eq 'reference') { $reference } else { $candidate }
        & dotnet exec --depsfile "$package/SekaiToolsGUI.deps.json" --runtimeconfig "$package/SekaiToolsGUI.runtimeconfig.json" "$package/PublishedLayoutProbe.dll" $video "$output/$i-$mode.json" timeline *> "$output/$i-$mode.log"
        if ($LASTEXITCODE) { throw "Product timeline failed: $video $mode" }
    }
    $a = Get-Content "$output/$i-reference.json" -Raw | ConvertFrom-Json
    $b = Get-Content "$output/$i-candidate.json" -Raw | ConvertFrom-Json
    foreach ($report in @($a,$b)) {
        if (-not $report.CaptureDisposed -or -not $report.CancellationChecked -or -not $report.InvalidFrameChecked -or
            $report.Video -ne $video -or $report.InputHash -ne (Get-FileHash -LiteralPath $video).Hash -or $report.Images.Count -ne 9) { throw 'Incomplete timeline report' }
    }
    if ($a.ProductAssemblyHash -ne $b.ProductAssemblyHash -or
        ($a.Images | ConvertTo-Json -Depth 8 -Compress) -ne ($b.Images | ConvertTo-Json -Depth 8 -Compress)) { throw 'Product timeline pixels differ' }
    if ($a.NativeHash -ne '0A697D9F6CE35463ECC434CD881920EBDEB2E075F305BEE04685A44278B98D12' -or
        $b.NativeHash -ne $experiment.NativeHash) { throw 'Unexpected actual runtime' }
    $rows += @{Video=$video; TimelineImagesEquivalent=$true; NativePointerReleased=$true;
        ReferenceReportHash=(Get-FileHash "$output/$i-reference.json").Hash; CandidateReportHash=(Get-FileHash "$output/$i-candidate.json").Hash; LiveGuiQualified=$false}
    Write-Output "Verified product timeline: $video"
}
foreach ($entry in $manifest) { if ((Get-FileHash -LiteralPath $entry.Path).Hash -ne $entry.Hash) { throw "Manifest changed: $($entry.Path)" } }
$rows | ConvertTo-Json -Depth 5 | Set-Content "$output/summary.json" -Encoding utf8
