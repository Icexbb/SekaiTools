param(
    [Parameter(Mandatory)][string]$PublishedToolDirectory,
    [Parameter(Mandatory)][string]$NativeBuildDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$tool = (Resolve-Path -LiteralPath $PublishedToolDirectory).Path
$build = (Resolve-Path -LiteralPath $NativeBuildDirectory).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new staging directory' }
if ($output.StartsWith($tool.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Staging directory must be outside the published tool directory'
}
$result = Get-Content -LiteralPath (Join-Path $build 'build-result.json') -Raw | ConvertFrom-Json
$binary = Join-Path $build 'native/cvextern.dll'
if ((Get-FileHash -LiteralPath $binary).Hash -ne $result.NativeHash) { throw 'Native artifact changed' }
if ((Get-FileHash -LiteralPath (Join-Path $build 'experiment.json')).Hash -ne $result.ExperimentHash) { throw 'Build identity changed' }
New-Item -ItemType Directory -Force $output | Out-Null
foreach ($item in Get-ChildItem -LiteralPath $tool -Force) {
    Copy-Item -LiteralPath $item.FullName -Destination $output -Recurse
}
$replacements = @(Get-ChildItem -LiteralPath $output -Recurse -File -Filter cvextern.dll | Where-Object {
    $_.DirectoryName -eq $output -or
    $_.DirectoryName -eq (Join-Path $output 'libs') -or
    $_.FullName -like '*\runtimes\win-x64\native\cvextern.dll'
})
if (-not $replacements.Count) { throw 'Published tool has no Windows x64 native runtime' }
foreach ($item in $replacements) { Copy-Item -LiteralPath $binary -Destination $item.FullName -Force }
[ordered]@{ PublishedTool=$tool; NativeBuild=$build; NativeHash=$result.NativeHash
    ReplacedPaths=@($replacements.FullName); ExperimentHash=$result.ExperimentHash
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'native-experiment.json') -Encoding utf8
