param(
    [Parameter(Mandatory)][string]$SourceDirectory,
    [Parameter(Mandatory)][string]$BuildDirectory,
    [ValidateSet('Sse3', 'Avx2', 'IppAvx2')][string]$Variant = 'Sse3',
    [switch]$RefreshFailedConfiguration,
    [switch]$GrayCapture,
    [switch]$PeriodicColorCapture,
    [string]$ModuleComparisonPath,
    [string]$DependencyDirectory
)
$ErrorActionPreference = 'Stop'
if ($GrayCapture -and $PeriodicColorCapture) { throw 'Select one capture prototype' }
$source = (Resolve-Path -LiteralPath $SourceDirectory).Path
$build = [IO.Path]::GetFullPath($BuildDirectory)
if ($build -eq $source) { throw 'Use an out-of-tree build directory' }
$revisions = [ordered]@{
    Emgu = '4f8dc1bf4bb562e396dedf9badaa1856deb68c29'
    OpenCv = '2b862f17fc625948d75840300a3837c4d0defa7a'
    Contrib = '7bd9df363342be59052b29e8e33e42440b7ddc2e'
}
foreach ($item in @(@($source, $revisions.Emgu), @((Join-Path $source 'opencv'), $revisions.OpenCv),
    @((Join-Path $source 'opencv_contrib'), $revisions.Contrib))) {
    $actual = & git -C $item[0] rev-parse HEAD
    if ($LASTEXITCODE -or $actual -cne $item[1]) { throw "Unexpected source revision: $($item[0])" }
}
$dispatch = if ($Variant -ne 'Sse3') { 'SSE4_1;SSE4_2;AVX;AVX2' } else { '' }
$ipp = if ($Variant -eq 'IppAvx2') { 'ON' } else { 'OFF' }
$isolation = (Join-Path $PSScriptRoot 'IsolateOutputs.cmake').Replace('\', '/')
$flags = @('-G', 'Visual Studio 18 2026', '-A', 'x64', '-S', $source, '-B', $build,
    "-DCMAKE_PROJECT_emgucv_INCLUDE=$isolation", '-DCMAKE_CONFIGURATION_TYPES=Release',
    '-DBUILD_LIST=core,imgproc,imgcodecs,videoio,geometry,highgui', '-DBUILD_SHARED_LIBS=OFF',
    '-DBUILD_WITH_STATIC_CRT=OFF',
    '-DBUILD_TESTS=OFF', '-DBUILD_PERF_TESTS=OFF', '-DBUILD_EXAMPLES=OFF',
    '-DBUILD_opencv_apps=OFF', '-DBUILD_opencv_java=OFF', '-DBUILD_opencv_python3=OFF',
    "-DWITH_IPP=$ipp", '-DWITH_EIGEN=OFF', '-DWITH_CUDA=OFF', '-DWITH_OPENCL=ON',
    '-DWITH_FFMPEG=ON', '-DWITH_MSMF=OFF', '-DWITH_DSHOW=OFF',
    '-DWITH_OPENEXR=OFF', '-DWITH_TIFF=OFF', '-DWITH_WEBP=OFF', '-DWITH_JASPER=OFF',
    '-DWITH_AVIF=OFF', '-DWITH_JPEGXL=OFF', '-DWITH_GDAL=OFF',
    '-DEMGU_CV_WITH_FREETYPE=OFF', '-DEMGU_CV_WITH_TESSERACT=OFF', '-DEMGU_CV_WITH_TIFF=OFF', '-DEMGU_ENABLE_SSE=ON',
    # The upstream MAUI target requires Emgu.CV to exist during generation.
    # Only cvextern will be built; managed/MAUI targets are not executed.
    '-DEMGU_CV_BUILD=ON', '-DEMGU_CV_EXAMPLE_BUILD=OFF',
    '-DCV_ENABLE_INTRINSICS=ON', '-DCPU_BASELINE=SSE3', "-DCPU_DISPATCH=$dispatch")
$diagnostics = $null
$moduleComparisonHash = $null
$dependencyManifestHash = $null
$dependencyBootstrapHash = $null
if ($DependencyDirectory) {
    $dependencyRoot = (Resolve-Path -LiteralPath $DependencyDirectory).Path
    $dependencyManifestPath = Join-Path $dependencyRoot 'dependency-manifest.json'
    $dependencyManifest = Get-Content -LiteralPath $dependencyManifestPath -Raw | ConvertFrom-Json
    if ($dependencyManifest.Triplet -ne 'x64-windows-static-md') { throw 'Wrong dependency triplet' }
    if (-not @($dependencyManifest.Files).Count -or
        (Get-FileHash -LiteralPath (Join-Path $dependencyRoot 'vcpkg.json')).Hash -ne $dependencyManifest.ManifestHash) {
        throw 'Incomplete or changed dependency manifest'
    }
    foreach ($entry in $dependencyManifest.Files) {
        $path = [IO.Path]::GetFullPath((Join-Path $dependencyRoot $entry.Path))
        if (-not $path.StartsWith($dependencyRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
            (Get-FileHash -LiteralPath $path).Hash -ne $entry.Sha256) { throw 'Dependency artifact changed' }
    }
    $prefix = (Join-Path $dependencyRoot 'installed/x64-windows-static-md').Replace('\','/')
    $vcpkg = (Resolve-Path -LiteralPath $dependencyManifest.VcpkgPath).Path
    if ((Get-FileHash -LiteralPath $vcpkg).Hash -ne $dependencyManifest.VcpkgHash) { throw 'Dependency tool changed' }
    $toolchain = (Join-Path (Split-Path $vcpkg) 'scripts/buildsystems/vcpkg.cmake').Replace('\','/')
    if (-not (Test-Path -LiteralPath $toolchain)) { throw 'Dependency toolchain missing' }
    $flags += @("-DCMAKE_PREFIX_PATH=$prefix", "-DSQLite3_INCLUDE_DIR=$prefix/include",
        "-DSQLite3_LIBRARY=$prefix/lib/sqlite3.lib", "-DEXE_SQLITE3=$prefix/tools/sqlite3/sqlite3.exe",
        "-DCMAKE_TOOLCHAIN_FILE=$toolchain", "-DVCPKG_INSTALLED_DIR=$($dependencyRoot.Replace('\','/'))/installed",
        '-DVCPKG_TARGET_TRIPLET=x64-windows-static-md', '-DVCPKG_MANIFEST_MODE=OFF')
    $bootstrap = Join-Path $PSScriptRoot 'ExpandedDependencies.cmake'
    $flags += @("-DSEKAI_DEPENDENCY_PREFIX=$prefix", "-DCMAKE_PROJECT_OpenCV_INCLUDE=$($bootstrap.Replace('\','/'))")
    $dependencyBootstrapHash = (Get-FileHash -LiteralPath $bootstrap).Hash
    $dependencyManifestHash = (Get-FileHash -LiteralPath $dependencyManifestPath).Hash
}
if ($ModuleComparisonPath) {
    $comparisonPath = (Resolve-Path -LiteralPath $ModuleComparisonPath).Path
    $comparison = Get-Content -LiteralPath $comparisonPath -Raw | ConvertFrom-Json
    if ($comparison.Original.NativeHash -ne '0A697D9F6CE35463ECC434CD881920EBDEB2E075F305BEE04685A44278B98D12') {
        throw 'Original package module inventory required'
    }
    $modules = @($comparison.Original.Modules)
    if (-not $modules.Count -or @($modules | Where-Object { $_ -notmatch '^[a-z0-9_]+$' }).Count -or
        @($modules | Sort-Object -Unique).Count -ne $modules.Count) { throw 'Invalid module inventory' }
    $flags = @($flags | Where-Object { $_ -notlike '-DBUILD_LIST=*' })
    $flags += "-DBUILD_LIST=$($modules -join ',')"
    $flags += "-DOPENCV_EXTRA_MODULES_PATH=$((Join-Path $source 'opencv_contrib/modules').Replace('\','/'))"
    $flags += @('-DWITH_MSMF=ON','-DWITH_DSHOW=ON','-DWITH_TIFF=ON','-DWITH_WEBP=ON',
        '-DWITH_EIGEN=ON','-DEMGU_CV_WITH_FREETYPE=ON','-DEMGU_CV_WITH_TIFF=ON')
    $moduleComparisonHash = (Get-FileHash -LiteralPath $comparisonPath).Hash
}
if ($Variant -eq 'IppAvx2') {
    $diagnostics = Join-Path $PSScriptRoot 'Diagnostics.cpp'
    $flags += "-DSEKAI_NATIVE_DIAGNOSTICS=$($diagnostics.Replace('\','/'))"
} else { $flags += '-DSEKAI_NATIVE_DIAGNOSTICS=' }
if ($PeriodicColorCapture) {
    $flags += "-DSEKAI_GRAY_CAPTURE=$((Join-Path $PSScriptRoot 'PeriodicColorCapture.cpp').Replace('\','/'))"
} elseif ($GrayCapture) {
    $flags += "-DSEKAI_GRAY_CAPTURE=$((Join-Path $PSScriptRoot 'GrayCapture.cpp').Replace('\','/'))"
} else { $flags += '-DSEKAI_GRAY_CAPTURE=' }
New-Item -ItemType Directory -Force $build | Out-Null
$identity = [ordered]@{ Revisions=$revisions; Variant=$Variant; Source=$source; Flags=$flags
    IsolationHash=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'IsolateOutputs.cmake')).Hash } | ConvertTo-Json -Depth 8
if ($diagnostics) {
    $record = $identity | ConvertFrom-Json -AsHashtable
    $record.DiagnosticsHash = (Get-FileHash -LiteralPath $diagnostics).Hash
    $identity = $record | ConvertTo-Json -Depth 8
}
$manifest = Join-Path $build 'experiment.json'
if ($dependencyManifestHash) {
    $record = $identity | ConvertFrom-Json -AsHashtable
    $record.DependencyManifestHash = $dependencyManifestHash
    $record.DependencyBootstrapHash = $dependencyBootstrapHash
    $identity = $record | ConvertTo-Json -Depth 8
}
if ($moduleComparisonHash) {
    $record = $identity | ConvertFrom-Json -AsHashtable
    $record.ModuleComparisonHash = $moduleComparisonHash
    $record.RequestedModules = $modules
    $identity = $record | ConvertTo-Json -Depth 8
}
if ($PeriodicColorCapture) {
    $record = $identity | ConvertFrom-Json -AsHashtable
    $record.PeriodicColorCaptureHash = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'PeriodicColorCapture.cpp')).Hash
    $identity = $record | ConvertTo-Json -Depth 8
}
if ($GrayCapture) {
    $record = $identity | ConvertFrom-Json -AsHashtable
    $record.GrayCaptureHash = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'GrayCapture.cpp')).Hash
    $identity = $record | ConvertTo-Json -Depth 8
}
if (Test-Path -LiteralPath $manifest) {
    if ((Get-Content -LiteralPath $manifest -Raw).Trim() -cne $identity.Trim()) {
        if (-not $RefreshFailedConfiguration -or (Test-Path -LiteralPath (Join-Path $build 'build-result.json'))) {
            throw 'Changed build inputs; use another build directory'
        }
        $previousHash = (Get-FileHash -LiteralPath $manifest).Hash
        Copy-Item -LiteralPath $manifest -Destination (Join-Path $build "experiment.previous-$previousHash.json")
        Set-Content -LiteralPath $manifest -Value $identity -Encoding utf8
    }
} else { Set-Content -LiteralPath $manifest -Value $identity -Encoding utf8 }
& cmake @flags
if ($LASTEXITCODE) { throw 'Native configure failed' }
