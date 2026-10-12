param([Parameter(Mandatory)][string]$BuildDirectory)
$ErrorActionPreference = 'Stop'
$build = (Resolve-Path -LiteralPath $BuildDirectory).Path
if (-not (Test-Path -LiteralPath (Join-Path $build 'experiment.json'))) { throw 'Experiment configuration missing' }
$identity = Get-Content -LiteralPath (Join-Path $build 'experiment.json') -Raw | ConvertFrom-Json
if ($identity.DependencyBootstrapHash -and
    (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'ExpandedDependencies.cmake')).Hash -ne $identity.DependencyBootstrapHash) {
    throw 'Expanded dependency configuration changed; configure a new experiment'
}
if ($identity.DiagnosticsHash -and (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Diagnostics.cpp')).Hash -ne $identity.DiagnosticsHash) {
    throw 'Diagnostic source changed; configure a new experiment'
}
$previousCompilerOptions = $env:_CL_
if ($identity.PeriodicColorCaptureHash -and (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'PeriodicColorCapture.cpp')).Hash -ne $identity.PeriodicColorCaptureHash) {
    throw 'Periodic color source changed; configure a new experiment'
}
if ($identity.GrayCaptureHash -and (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'GrayCapture.cpp')).Hash -ne $identity.GrayCaptureHash) {
    throw 'Gray capture source changed; configure a new experiment'
}
if ((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'IsolateOutputs.cmake')).Hash -ne $identity.IsolationHash) {
    throw 'Isolation configuration changed; configure a new experiment'
}
try {
    # One MSBuild project, four compiler workers; avoid nested /MP fan-out.
    $env:_CL_ = "$previousCompilerOptions /MP4"
    & cmake --build $build --config Release --target cvextern --parallel 1
    if ($LASTEXITCODE) { throw 'Native build failed' }
} finally { $env:_CL_ = $previousCompilerOptions }
$binary = Join-Path $build 'native/cvextern.dll'
if (-not (Test-Path -LiteralPath $binary)) { throw 'Isolated native output missing' }
[ordered]@{ NativeDll=$binary; NativeHash=(Get-FileHash -LiteralPath $binary).Hash
    ExperimentHash=(Get-FileHash -LiteralPath (Join-Path $build 'experiment.json')).Hash
    Configuration='Release'; Target='cvextern'; BuildParallelism=1; CompilerParallelism=4
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $build 'build-result.json') -Encoding utf8
