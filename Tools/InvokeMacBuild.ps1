param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z0-9_.-]+@[A-Za-z0-9_.:-]+$')]
    [string]$Mac,
    [ValidatePattern('^(~/|/)[A-Za-z0-9_./-]+$')]
    [string]$Repository = '~/dev/SekaiTools'
)
$ErrorActionPreference = 'Stop'
# Build the already checked-out remote revision. This helper never resets or syncs files.
$remoteCommand = "set -e; cd $Repository; bash SekaiToolsMac/build-dev.sh; python3 Tools/verify-macos-host.py artifacts/macos/SekaiTools.app/Contents/Helpers/SekaiToolsHost"
& ssh -o BatchMode=yes -o ConnectTimeout=10 $Mac $remoteCommand
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
