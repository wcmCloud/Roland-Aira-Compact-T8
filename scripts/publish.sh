#!/usr/bin/env bash
# Builds self-contained Roland Aira Compact T-8 Editor packages (no .NET install needed to run them).
# Usage: scripts/publish.sh [rid ...]          e.g. scripts/publish.sh osx-arm64
#        VERSION=1.0.4 scripts/publish.sh      (defaults to the <Version> in src/T8.Editor/T8.Editor.csproj)
# Default: macOS, Windows and Linux, each for Apple Silicon/ARM64 and Intel/AMD64.
# Output: artifacts/T8Editor-<version>-<rid>.zip (macOS, Windows) or .tar.gz (Linux).
set -euo pipefail

cd "$(dirname "$0")/.."
RIDS=("$@")
if [ ${#RIDS[@]} -eq 0 ]; then
  RIDS=(osx-arm64 osx-x64 win-arm64 win-x64 linux-arm64 linux-x64)
fi
VERSION="${VERSION:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' src/T8.Editor/T8.Editor.csproj)}"
APP_NAME="Roland Aira Compact T-8 Editor"
OUT=artifacts
mkdir -p "$OUT"

for rid in "${RIDS[@]}"; do
  echo "== $rid ($VERSION)"
  dir="$OUT/publish/$rid"
  name="T8Editor-$VERSION-$rid"
  rm -rf "$dir" "$OUT/$name" "$OUT/$name.zip" "$OUT/$name.tar.gz"
  dotnet publish src/T8.Editor/T8.Editor.csproj -c Release -r "$rid" --self-contained \
    -p:Version="$VERSION" -p:DebugType=None -o "$dir"
  cp LICENSE-MIT LICENSE-GPL-3.0 "$dir/"

  case "$rid" in
    osx-*)
      # Wrap in a macOS .app bundle.
      app="$OUT/$name/$APP_NAME.app"
      mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
      cp -R "$dir/." "$app/Contents/MacOS/"
      sed "s/__VERSION__/$VERSION/g" scripts/Info.plist > "$app/Contents/Info.plist"
      cp assets/icon/app-icon.icns "$app/Contents/Resources/"
      chmod +x "$app/Contents/MacOS/T8Editor"
      # Apple Silicon only runs signed code: sign every file, then the bundle, ad hoc (no Apple
      # Developer ID needed). ditto keeps the signatures of the .dll files, which live in extended attributes.
      if command -v codesign > /dev/null; then
        find "$app/Contents/MacOS" -type f -exec codesign --force --sign - {} \;
        codesign --force --sign - "$app"
        codesign --verify --deep --strict "$app"
        (cd "$OUT" && ditto -c -k --keepParent "$name" "$name.zip")
      else
        (cd "$OUT" && zip -qry "$name.zip" "$name")
      fi
      rm -rf "$OUT/$name"
      ;;
    win-*)
      (cd "$dir" && zip -qr "../../$name.zip" .)
      ;;
    *)
      chmod +x "$dir/T8Editor"
      # Icons in the freedesktop layout, and a .desktop file for app menus.
      for size in 16 24 32 48 64 128 256 512; do
        mkdir -p "$dir/icons/hicolor/${size}x${size}/apps"
        cp "assets/icon/png/app-icon-$size.png" "$dir/icons/hicolor/${size}x${size}/apps/t8editor.png"
      done
      mkdir -p "$dir/icons/hicolor/scalable/apps"
      cp assets/icon/app-icon.svg "$dir/icons/hicolor/scalable/apps/t8editor.svg"
      cp scripts/T8Editor.desktop "$dir/"
      rm -rf "$OUT/$name" && cp -R "$dir" "$OUT/$name"
      tar -czf "$OUT/$name.tar.gz" -C "$OUT" "$name"
      rm -rf "$OUT/$name"
      ;;
  esac
done

echo "Packages:"
ls -1 "$OUT" | grep -E "^T8Editor-$VERSION-.*\.(zip|tar\.gz)$"
