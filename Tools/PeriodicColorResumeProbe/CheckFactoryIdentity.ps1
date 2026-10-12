param(
    [Parameter(Mandatory)][string]$ToolDll,
    [Parameter(Mandatory)][string]$Plugin,
    [Parameter(Mandatory)][string]$CancelDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$dll = (Resolve-Path -LiteralPath $ToolDll).Path
$pluginPath = (Resolve-Path -LiteralPath $Plugin).Path
$cancel = (Resolve-Path -LiteralPath $CancelDirectory).Path
$report = Get-Content (Join-Path $cancel 'report.json') -Raw | ConvertFrom-Json
if (-not $report.ProcessorFactoryCapture -or -not $report.CheckpointEquivalent) { throw 'Factory checkpoint required' }
$checkpoint = $report.CheckpointPath
$checkpointHash = (Get-FileHash -LiteralPath $checkpoint).Hash
$original = Get-Content -LiteralPath $checkpoint -Raw
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh output directory' }
New-Item -ItemType Directory $output | Out-Null
$rows = @()
foreach ($kind in @('missing-identity','wrong-runtime','wrong-period')) {
    $state = $original | ConvertFrom-Json
    if ($kind -eq 'missing-identity') { $state.Metadata.CaptureIdentity = $null }
    elseif ($kind -eq 'wrong-runtime') {
        $state.Metadata.CaptureIdentity = $state.Metadata.CaptureIdentity.Replace($report.NativeHash, ('0' * 64))
    } else {
        $state.Metadata.CaptureIdentity = $state.Metadata.CaptureIdentity.Replace(':16:12:', ':16:13:')
        if ($state.Metadata.CaptureIdentity -eq $report.CaptureIdentity) { throw 'Expected interval 12 fixture' }
    }
    $fixture = Join-Path $output "$kind.json"
    $state | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $fixture -Encoding utf8
    $destination = Join-Path $output "$kind-output"
    $log = Join-Path $output "$kind.log"
    & dotnet $dll $report.Video $pluginPath $destination resume $fixture default default true *> $log
    if ($LASTEXITCODE -eq 0 -or (Get-Content -LiteralPath $log -Raw) -notmatch 'Factory checkpoint identity differs') {
        throw "Wrong identity did not fail at compatibility validation: $kind"
    }
    if (Test-Path -LiteralPath (Join-Path $destination 'state.json')) { throw 'Rejected state produced output' }
    $rows += @{ Kind=$kind; Rejected=$true; FixtureHash=(Get-FileHash -LiteralPath $fixture).Hash }
}
if ((Get-FileHash -LiteralPath $checkpoint).Hash -ne $checkpointHash) { throw 'Source checkpoint changed' }
@{ VerificationPassed=$true; OriginalCheckpointUnchanged=$true; Results=$rows } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $output 'summary.json') -Encoding utf8
