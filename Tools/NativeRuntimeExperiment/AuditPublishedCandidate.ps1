param(
    [Parameter(Mandatory)][string]$CandidateDirectory,
    [Parameter(Mandatory)][string]$AuditCopyDirectory,
    [Parameter(Mandatory)][string]$FactoryReport,
    [Parameter(Mandatory)][string]$OutputReport,
    [ValidateSet('Limited','Expanded')][string]$NativeVariant = 'Limited'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $CandidateDirectory).Path
$copy = (Resolve-Path -LiteralPath $AuditCopyDirectory).Path
$destination = [IO.Path]::GetFullPath($OutputReport)
if (Test-Path -LiteralPath $destination) { throw 'Use a new audit report' }
$required = @('SekaiToolsGUI.exe','SekaiToolsGUI.dll','SekaiToolsGUI.deps.json',
    'SekaiToolsGUI.runtimeconfig.json','Updater.exe','7zr.exe','network-endpoints.json',
    'libs/Emgu.CV.dll','libs/Emgu.CV.Wpf.dll','libs/SekaiToolsCore.dll',
    'libs/SekaiToolsInfrastructure.dll','libs/cvextern.dll','libs/opencv_videoio_ffmpeg500_64.dll')
foreach ($relative in $required) {
    $path = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) {
        throw "Missing package file: $relative"
    }
}
$files = @(Get-ChildItem -LiteralPath $root -Recurse -File)
if ($files | Where-Object { $_.Extension -eq '.pdb' -or $_.Name -like '*Probe*' }) { throw 'Debug or probe files in candidate' }
if (Get-ChildItem -LiteralPath (Join-Path $root 'runtimes') -Directory | Where-Object Name -ne 'win-x64') {
    throw 'Unexpected runtime architecture'
}
$expectedNative = if ($NativeVariant -eq 'Expanded') {
    'EC795EC4496E2DAB48A593A3E3FC94DEDC35FFB05404BF6A3D9DA7CCBFDB35D1'
} else {
    '5B13F57426105BABBFD02395D4F6AC5F9D622FAE9FD0B9C81C08CA9FD287B984'
}
$nativeFiles = @($files | Where-Object Name -eq 'cvextern.dll')
if (-not $nativeFiles.Count) { throw 'Native runtime missing' }
foreach ($file in $nativeFiles) {
    if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne $expectedNative) { throw 'Unexpected native runtime' }
}
$evidencePath = (Resolve-Path -LiteralPath $FactoryReport).Path
$evidence = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json
if (-not $evidence.VerificationPassed -or -not $evidence.SavedIdentitySelectedFactory -or
    -not $evidence.CaptureDisposed -or $evidence.FramesChecked -ne 13 -or $evidence.NativeHash -ne $expectedNative -or
    $evidence.ProductAssemblyHash -ne (Get-FileHash -LiteralPath (Join-Path $root 'SekaiToolsGUI.dll')).Hash -or
    -not $evidence.NativePath.StartsWith($copy + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Published-layout qualification does not match candidate'
}
$manifest = foreach ($file in $files) {
    $relative = [IO.Path]::GetRelativePath($root, $file.FullName)
    $hash = (Get-FileHash -LiteralPath $file.FullName).Hash
    if ((Get-FileHash -LiteralPath (Join-Path $copy $relative)).Hash -ne $hash) { throw "Audit copy differs: $relative" }
    [ordered]@{ Path=$relative; Bytes=$file.Length; Sha256=$hash }
}
[ordered]@{
    VerificationPassed=$true; CandidateRoot=$root; NativeHash=$expectedNative; NativeVariant=$NativeVariant
    FactoryReport=$evidencePath; FactoryReportHash=(Get-FileHash -LiteralPath $evidencePath).Hash
    AuditCopyMatchesCandidate=$true; Files=@($manifest | Sort-Object Path)
    GuiFileVersion=(Get-Item -LiteralPath (Join-Path $root 'SekaiToolsGUI.dll')).VersionInfo.FileVersion
    FormalReleaseApproved=$false; FullNativeSdkQualified=$false; VisibleGuiQualified=$false
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $destination -Encoding utf8
