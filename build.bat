set VERSION=1.4.3.1026
set PROJECT_NAME=SekaiToolsGUI
set BUILD_DIR=Build

del /s /q %BUILD_DIR%
dotnet publish %PROJECT_NAME% -c Release -o %BUILD_DIR%
@REM 7z a SekaiTools-%VERSION%.7z %BUILD_DIR%/*
