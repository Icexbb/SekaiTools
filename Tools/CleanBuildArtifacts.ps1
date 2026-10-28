param(
    [switch]$Apply,
    [switch]$Interactive,
    [ValidateRange(1, 365)][int]$KeepDays = 7,
    [ValidateRange(1, 168)][int]$ProtectHours = 24,
    [ValidateRange(1, 1024)][int]$MaxGiB = 20
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$root = [IO.Path]::GetFullPath((Join-Path $repo '.codex-build'))
if (-not (Test-Path -LiteralPath $root)) { Write-Output 'No .codex-build directory'; return }
foreach ($path in @($repo, $root)) {
    if ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Refusing redirected cleanup root: $path"
    }
}
$tracked = @(& git -C $repo ls-files -- .codex-build)
if ($LASTEXITCODE -ne 0 -or $tracked.Count) { throw 'Cannot clean a tracked or unverified output directory' }
function Get-BuildProcesses {
    Get-CimInstance Win32_Process | Where-Object {
    ($_.ExecutablePath -and $_.ExecutablePath.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) -or
    ($_.Name -match '^(dotnet|MSBuild|testhost)\.exe$' -and
        $_.CommandLine -match '(?i)(build|publish|test|benchmark|probe|\.csproj|\.sln)')
    }
}
$processes = @(Get-BuildProcesses)
if ($processes.Count) {
    [pscustomobject]@{ Status = 'SkippedActiveProcesses'; ProcessIds = @($processes.ProcessId) } | ConvertTo-Json
    return
}
$mutex = [Threading.Mutex]::new($false, 'Local\SekaiToolsBuildArtifactCleanup')
if (-not $mutex.WaitOne(0)) { $mutex.Dispose(); Write-Output 'Cleanup already running'; return }
try {
    $now = [DateTime]::UtcNow
    $extensions = @('.dll', '.exe', '.pdb', '.lib', '.obj', '.so', '.dylib', '.ilk', '.pch')
    $files = [Collections.Generic.List[object]]::new()
    $groups = @{}
    $queue = [Collections.Generic.Queue[string]]::new()
    $queue.Enqueue($root)
    while ($queue.Count) {
        $directory = $queue.Dequeue()
        foreach ($item in Get-ChildItem -LiteralPath $directory -Force) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
            if ($item.PSIsContainer) { $queue.Enqueue($item.FullName); continue }
            $relative = [IO.Path]::GetRelativePath($root, $item.FullName)
            $group = ($relative -split '[\\/]')[0]
            if (-not $groups.ContainsKey($group)) {
                $groups[$group] = @{ Latest = $item.LastWriteTimeUtc; Protected = $group -in @('native', 'native-dependencies-pinned'); Candidates = [Collections.Generic.List[object]]::new() }
            }
            if ($item.LastWriteTimeUtc -gt $groups[$group].Latest) { $groups[$group].Latest = $item.LastWriteTimeUtc }
            if ($item.Name -eq '.keep-build') { $groups[$group].Protected = $true }
            if ($item.Extension.ToLowerInvariant() -in $extensions) { $groups[$group].Candidates.Add($item) }
            $files.Add([pscustomobject]@{ File = $item; Group = $group })
        }
    }
    if (Test-Path -LiteralPath (Join-Path $root '.keep-build')) { throw 'Root is protected by .keep-build' }
    $before = [long](($files | ForEach-Object { $_.File.Length } | Measure-Object -Sum).Sum)
    $remaining = $before
    $selected = [Collections.Generic.List[object]]::new()
    foreach ($group in $groups.Keys | Sort-Object { $groups[$_].Latest }) {
        $policy = $groups[$group]
        if ($policy.Protected -or $policy.Latest -gt $now.AddHours(-$ProtectHours)) { continue }
        $expired = $policy.Latest -lt $now.AddDays(-$KeepDays)
        foreach ($entry in $policy.Candidates) {
            if (-not $expired -and $remaining -le $MaxGiB * 1GB) { break }
            $selected.Add($entry)
            $remaining -= $entry.Length
        }
    }
    $freed = [long]0
    $failed = 0
    $checked = 0
    $interrupted = $false
    foreach ($file in $selected) {
        if (-not $Apply) { continue }
        if ($checked++ % 100 -eq 0 -and @(Get-BuildProcesses).Count -gt 0) {
            $interrupted = $true
            break
        }
        $full = [IO.Path]::GetFullPath($file.FullName)
        if (-not $full.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Path escaped cleanup root' }
        $ancestor = Split-Path $full -Parent
        while ($ancestor.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
            if ((Get-Item -LiteralPath $ancestor).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Redirected cleanup path' }
            if (Test-Path -LiteralPath (Join-Path $ancestor '.keep-build')) { throw 'Cleanup protection appeared during run' }
            if ($ancestor -eq $root) { break }
            $ancestor = Split-Path $ancestor -Parent
        }
        try {
            $current = Get-Item -LiteralPath $full
            if (($current.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
                $current.LastWriteTimeUtc -ne $file.LastWriteTimeUtc -or $current.Length -ne $file.Length) { continue }
            Remove-Item -LiteralPath $full -Force
            $freed += $file.Length
        }
        catch { $failed++; Write-Warning "Skipped locked/unavailable file: $full" }
    }
    [pscustomobject]@{
        Status = $(if ($interrupted) { 'StoppedActiveProcesses' } elseif ($Apply) { 'Applied' } else { 'Preview' })
        Root = $root
        BeforeGiB = [math]::Round($before / 1GB, 2)
        CandidateFiles = $selected.Count
        PlannedGiB = [math]::Round(($before - $remaining) / 1GB, 2)
        FreedGiB = [math]::Round($freed / 1GB, 2)
        RemainingGiB = [math]::Round(($before - $freed) / 1GB, 2)
        FailedFiles = $failed
    } | ConvertTo-Json
}
finally { $mutex.ReleaseMutex(); $mutex.Dispose() }

if ($Interactive -and -not $Apply -and $selected.Count -gt 0) {
    $answer = Read-Host 'Apply this cleanup? Type YES to continue; Enter cancels'
    if ($answer -ceq 'YES') {
        & $PSCommandPath -Apply -KeepDays $KeepDays -ProtectHours $ProtectHours -MaxGiB $MaxGiB
    }
    else { Write-Output 'Canceled. No files deleted.' }
}
