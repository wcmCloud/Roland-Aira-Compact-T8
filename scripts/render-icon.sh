#!/usr/bin/env bash
# Renders assets/icon/app-icon.svg to the app icon files: assets/icon/app-icon.ico (Windows),
# assets/icon/app-icon.icns (macOS) and assets/icon/png/app-icon-<size>.png (Linux, window icon).
# Usage: scripts/render-icon.sh     (run after editing the SVG, and commit the results)
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet run --project tools/IconRenderer -c Release -- assets/icon
