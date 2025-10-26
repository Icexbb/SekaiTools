set VERSION=1.4.3.1026
set PROJECT_NAME=SekaiToolsGUI
set BUILD_DIR=Build

dotnet publish %PROJECT_NAME% -c Release -o %BUILD_DIR%
7z a SekaiTools-%VERSION%.7z %BUILD_DIR%/*
