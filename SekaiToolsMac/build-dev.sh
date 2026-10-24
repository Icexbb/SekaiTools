#!/bin/bash
set -euo pipefail

if [[ "$(uname -s)" != Darwin || "$(uname -m)" != arm64 ]]; then
  echo 'This development build requires an Apple Silicon Mac.' >&2
  exit 1
fi
root="$(cd "$(dirname "$0")/.." && pwd)"
app="$root/artifacts/macos/SekaiTools.app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Helpers"
dotnet publish "$root/SekaiToolsHost/SekaiToolsHost.csproj" -c Release -r osx-arm64 --self-contained true -o "$app/Contents/Helpers"
swift build --package-path "$root/SekaiToolsMac" -c release --arch arm64
bin="$(swift build --package-path "$root/SekaiToolsMac" -c release --arch arm64 --show-bin-path)"
cp "$bin/SekaiToolsMac" "$app/Contents/MacOS/SekaiToolsMac"
cp "$root/SekaiToolsMac/Info.plist" "$app/Contents/Info.plist"
echo "Development app: $app"
echo 'Not signed or notarized. OpenCV on macOS 15/26 requires a compatible native runtime.'
