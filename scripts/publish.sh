#!/usr/bin/env bash
# Builds self-contained Roland Aira Compact T-8 Editor packages (no .NET install needed to run them).
# Usage: scripts/publish.sh [rid ...]          e.g. scripts/publish.sh osx-arm64
#        VERSION=1.0.5 scripts/publish.sh      (defaults to the <Version> in src/T8.Editor/T8.Editor.csproj)
# Default: macOS, Windows and Linux, each for Apple Silicon/ARM64 and Intel/AMD64.
# Output: artifacts/T8Editor-<version>-<rid>.zip (macOS, Windows) or .tar.gz (Linux).
#
# macOS: when this Mac has a "Developer ID Application" certificate, the app is signed with it (hardened
# runtime, scripts/Entitlements.plist) and notarized with the notarytool keychain profile in
# NOTARY_PROFILE (default "maXfolders"), and the ticket is stapled, so Gatekeeper opens it without warnings.
# Set it up once with: xcrun notarytool store-credentials "<profile>" --apple-id ... --team-id ...
# Without the certificate (or with SIGN_IDENTITY=-) the app is signed ad hoc, as before.
set -euo pipefail

cd "$(dirname "$0")/.."
RIDS=("$@")
if [ ${#RIDS[@]} -eq 0 ]; then
  RIDS=(osx-arm64 osx-x64 win-arm64 win-x64 linux-arm64 linux-x64)
fi
VERSION="${VERSION:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' src/T8.Editor/T8.Editor.csproj)}"
APP_NAME="Roland Aira Compact T-8 Editor"
SIGN_IDENTITY="${SIGN_IDENTITY:-$(security find-identity -v -p codesigning 2>/dev/null | sed -n 's/.*"\(Developer ID Application: [^"]*\)".*/\1/p' | head -1)}"
SIGN_IDENTITY="${SIGN_IDENTITY:--}"
NOTARY_PROFILE="${NOTARY_PROFILE:-maXfolders}"
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
      # Apple Silicon only runs signed code: sign every file, then the bundle. ditto keeps the signatures
      # of the .dll files, which live in extended attributes.
      if command -v codesign > /dev/null; then
        if [ "$SIGN_IDENTITY" = "-" ]; then
          echo "Signing ad hoc (no Developer ID Application certificate found)"
          find "$app/Contents/MacOS" -type f -exec codesign --force --sign - {} \;
          codesign --force --sign - "$app"
        else
          echo "Signing with $SIGN_IDENTITY"
          find "$app/Contents/MacOS" -type f -exec codesign --force --timestamp --options runtime \
            --entitlements scripts/Entitlements.plist --sign "$SIGN_IDENTITY" {} \;
          codesign --force --timestamp --options runtime --entitlements scripts/Entitlements.plist \
            --sign "$SIGN_IDENTITY" "$app"
        fi
        codesign --verify --deep --strict "$app"
        if [ "$SIGN_IDENTITY" != "-" ] && [ "${NOTARIZE:-1}" != "0" ]; then
          echo "Notarizing with the notarytool profile $NOTARY_PROFILE"
          (cd "$OUT" && ditto -c -k --keepParent "$name/$APP_NAME.app" "$name-notarize.zip")
          xcrun notarytool submit "$OUT/$name-notarize.zip" --keychain-profile "$NOTARY_PROFILE" --wait \
            | tee "$OUT/$name-notarize.log"
          rm -f "$OUT/$name-notarize.zip"
          if ! grep -q "status: Accepted" "$OUT/$name-notarize.log"; then
            id=$(sed -n 's/^ *id: //p' "$OUT/$name-notarize.log" | head -1)
            [ -n "$id" ] && xcrun notarytool log "$id" --keychain-profile "$NOTARY_PROFILE" || true
            echo "Notarization failed for $rid" >&2
            exit 1
          fi
          rm -f "$OUT/$name-notarize.log"
          xcrun stapler staple "$app"
          spctl --assess --type execute --verbose "$app"
        fi
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
