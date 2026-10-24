param(
    [string]$HostDll = (Join-Path $PSScriptRoot '../SekaiToolsHost/bin/Debug/net10.0/SekaiToolsHost.dll')
)
$ErrorActionPreference = 'Stop'
$HostDll = (Resolve-Path -LiteralPath $HostDll).Path
$probeDirectory = Join-Path ([IO.Path]::GetTempPath()) ('SekaiToolsHostProbe-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $probeDirectory | Out-Null

function Invoke-HostRequest([string]$Command, [hashtable]$Arguments, [int]$ExpectedExit = 0) {
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.ArgumentList.Add($HostDll)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardInputEncoding = [Text.UTF8Encoding]::new($false)
    $start.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.StandardInput.WriteLine((@{ version = 1; command = $Command; args = $Arguments } | ConvertTo-Json -Depth 10 -Compress))
        $process.StandardInput.Flush()
        if (-not $process.WaitForExit(30000)) {
            $process.Kill($true)
            throw 'Host protocol probe timed out'
        }
        $lines = @($stdout.GetAwaiter().GetResult() -split '\r?\n' | Where-Object { $_ })
        if ($process.ExitCode -ne $ExpectedExit) { throw $stderr.GetAwaiter().GetResult() }
        if ($lines.Count -ne 1) { throw "Expected one JSON response; received $($lines.Count) lines" }
        $response = $lines[0] | ConvertFrom-Json
        if ($response.version -ne 1) { throw 'Incorrect response version' }
        return $response
    }
    finally { $process.Dispose() }
}

try {
    $capabilities = Invoke-HostRequest 'capabilities' @{}
    if ($capabilities.type -ne 'result' -or $capabilities.data.sources.Count -eq 0) { throw 'Capabilities failed' }
    $bad = Invoke-HostRequest 'unsupported' @{} 1
    if ($bad.type -ne 'error') { throw 'Missing structured error' }
    $script = Join-Path $probeDirectory '中文剧本.json'
    @{
        ScenarioId = 'host-probe'
        Snippets = @(@{ Action = 1 })
        TalkData = @(@{ WindowDisplayName = '原名'; Body = '原文'; TalkCharacters = @(); Voices = @() })
        SpecialEffectData = @()
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $script -Encoding utf8
    $loaded = Invoke-HostRequest 'translation.load' @{ script = $script }
    $rows = @($loaded.data)
    if ($rows.Count -ne 1 -or $rows[0].original -ne '原文') { throw 'Script loading failed' }
    $rows[0].speaker = '译名'
    $rows[0].content = "第一行`n@第二行"
    $translation = Join-Path $probeDirectory '译文.txt'
    $saved = Invoke-HostRequest 'translation.save' @{ script = $script; output = $translation; rows = $rows }
    if ($saved.type -ne 'result') { throw 'Translation saving failed' }
    $reloaded = Invoke-HostRequest 'translation.load' @{ script = $script; translation = $translation }
    if ($reloaded.data[0].speaker -ne '译名' -or $reloaded.data[0].content -ne "第一行`n@第二行") { throw 'Unicode/multiline roundtrip failed' }
    Write-Output 'PASS: capabilities, structured failure, Unicode paths, translation load/save/import, clean JSON stdout'
}
finally {
    # This invocation created the exact GUID directory; remove only its files, then the empty directory.
    Get-ChildItem -LiteralPath $probeDirectory -File | Remove-Item -Force
    Remove-Item -LiteralPath $probeDirectory
}
