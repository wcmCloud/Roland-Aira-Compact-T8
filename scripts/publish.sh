#!/usr/bin/env bash
# Builds self-contained T-8 Editor packages (no .NET install needed to run them).
# Usage: scripts/publish.sh [rid ...]   e.g. scripts/publish.sh osx-arm64
# Default: osx-arm64 osx-x64 win-x64 linux-x64. Output goes to artifacts/.
set -euo pipefail

cd "$(dirname "$0")/.."
RIDS=("$@")
if [ ${#RIDS[@]} -eq 0 ]; then
  RIDS=(osx-arm64 osx-x64 win-x64 linux-x64)
fi
VERSION="${VERSION:-0.1.0}"
OUT=artifacts
mkdir -p "$OUT"

for rid in "${RIDS[@]}"; do
  echo "== $rid"
  dir="$OUT/publish/$rid"
  rm -rf "$dir"
  dotnet publish src/T8.Editor/T8.Editor.csproj -c Release -r "$rid" --self-contained \
    -p:Version="$VERSION" -o "$dir"

  case "$rid" in
    osx-*)
      # Wrap in a macOS .app bundle.
      app="$OUT/T8Editor-$rid/T-8 Editor.app"
      rm -rf "$OUT/T8Editor-$rid"
      mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
      cp -R "$dir/." "$app/Contents/MacOS/"
      sed "s/__VERSION__/$VERSION/g" scripts/Info.plist > "$app/Contents/Info.plist"
      chmod +x "$app/Contents/MacOS/T8Editor"
      (cd "$OUT" && rm -f "T8Editor-$rid.zip" && zip -qry "T8Editor-$rid.zip" "T8Editor-$rid")
      ;;
    win-*)
      (cd "$dir" && rm -f "../../T8Editor-$rid.zip" && zip -qr "../../T8Editor-$rid.zip" .)
      ;;
    *)
      chmod +x "$dir/T8Editor"
      tar -czf "$OUT/T8Editor-$rid.tar.gz" -C "$OUT/publish" "$rid"
      ;;
  esac
done

echo "Packages:"
ls -1 "$OUT" | grep -E '\.(zip|tar\.gz)$'
