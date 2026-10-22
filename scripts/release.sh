#!/usr/bin/env bash
# Builds all packages and publishes them as a GitHub release (needs the GitHub CLI, gh, signed in).
# Usage: scripts/release.sh [version]     e.g. scripts/release.sh 1.0.8
# Defaults to the <Version> in src/T8.Editor/T8.Editor.csproj. Run from a clean, pushed master.
set -euo pipefail

cd "$(dirname "$0")/.."
VERSION="${1:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' src/T8.Editor/T8.Editor.csproj)}"
TAG="v$VERSION"

if [ -n "$(git status --porcelain)" ]; then
  echo "Commit or stash your changes first." >&2
  exit 1
fi
if gh release view "$TAG" > /dev/null 2>&1; then
  echo "Release $TAG already exists." >&2
  exit 1
fi

VERSION="$VERSION" scripts/publish.sh
files=(artifacts/T8Editor-"$VERSION"-*.zip artifacts/T8Editor-"$VERSION"-*.tar.gz)

git tag -a "$TAG" -m "Roland Aira Compact T-8 Editor $VERSION"
git push origin "$TAG"
notes="$(mktemp)"
sed "s/<version>/$VERSION/g" "${RELEASE_NOTES:-scripts/release-notes.md}" > "$notes"
gh release create "$TAG" "${files[@]}" --title "Roland Aira Compact T-8 Editor $VERSION" --notes-file "$notes"
rm -f "$notes"
