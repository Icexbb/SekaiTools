param(
    [Parameter(Mandatory)][string]$VcpkgExecutable,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [switch]$ResumeFailed
)
$ErrorActionPreference = 'Stop'
$vcpkg = (Resolve-Path -LiteralPath $VcpkgExecutable).Path
$root = [IO.Path]::GetFullPath($OutputDirectory)
$manifest = [ordered]@{
    name='sekai-native-expanded-dependencies'; 'version-string'='1.0.0'
    'builtin-baseline'='b1e15efef6758eaa0beb0a8732cfa66f6a68a81d'
    dependencies=@([ordered]@{ name='sqlite3'; features=@('tool') }, 'freetype', 'harfbuzz', 'hdf5', 'eigen3')
}
$manifestPath = Join-Path $root 'vcpkg.json'
if (Test-Path -LiteralPath $root) {
    if (-not $ResumeFailed -or (Test-Path -LiteralPath (Join-Path $root 'dependency-manifest.json'))) {
        throw 'Use a fresh dependency directory or explicitly resume an incomplete build'
    }
    $existing = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -AsHashtable
    if (($existing | ConvertTo-Json -Depth 6 -Compress) -cne ($manifest | ConvertTo-Json -Depth 6 -Compress)) {
        throw 'Pinned dependency manifest changed'
    }
} else {
    if ($ResumeFailed) { throw 'No incomplete dependency build to resume' }
    New-Item -ItemType Directory -Path $root | Out-Null
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding utf8
}
$previousDownloads = $env:VCPKG_DOWNLOADS
$previousCache = $env:VCPKG_DEFAULT_BINARY_CACHE
try {
    $env:VCPKG_DOWNLOADS = Join-Path $root 'downloads'
    $env:VCPKG_DEFAULT_BINARY_CACHE = Join-Path $root 'binary-cache'
    New-Item -ItemType Directory -Force -Path $env:VCPKG_DOWNLOADS,$env:VCPKG_DEFAULT_BINARY_CACHE | Out-Null
    & $vcpkg install --triplet=x64-windows-static-md "--x-manifest-root=$root" "--x-install-root=$(Join-Path $root 'installed')"
    if ($LASTEXITCODE) { throw 'Pinned dependency build failed; retain logs and directory for diagnosis' }
} finally {
    $env:VCPKG_DOWNLOADS = $previousDownloads
    $env:VCPKG_DEFAULT_BINARY_CACHE = $previousCache
}
$installedFiles = @(Get-ChildItem -LiteralPath (Join-Path $root 'installed/x64-windows-static-md') -Recurse -File)
$installedFiles += Get-Item -LiteralPath (Join-Path $root 'installed/vcpkg/status')
$files = @($installedFiles | ForEach-Object {
    [ordered]@{ Path=[IO.Path]::GetRelativePath($root, $_.FullName); Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash }
})
[ordered]@{
    ManifestHash=(Get-FileHash -LiteralPath $manifestPath).Hash
    VcpkgPath=$vcpkg; VcpkgHash=(Get-FileHash -LiteralPath $vcpkg).Hash
    Triplet='x64-windows-static-md'; Files=$files
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root 'dependency-manifest.json') -Encoding utf8
