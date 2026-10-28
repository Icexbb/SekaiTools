@echo off
setlocal
where pwsh >nul 2>nul
if errorlevel 1 (
    echo PowerShell 7 ^(pwsh^) is required. Install it and try again.
    pause
    exit /b 1
)
pwsh -NoProfile -File "%~dp0Tools\CleanBuildArtifacts.ps1" -Interactive
set "cleanupExitCode=%errorlevel%"
pause
exit /b %cleanupExitCode%
