#!/usr/bin/env bash
# Builds all packages and publishes them as a GitHub release (needs the GitHub CLI, gh, signed in).
# Usage: scripts/release.sh [version]     e.g. scripts/release.sh 1.0.12
# Defaults to the <Version> in src/T8.Editor/T8.Editor.csproj. master is protected: changes (including the version
# bump) go in through a pull request with green CI. Run this afterwards, on a clean master that matches GitHub's.
set -euo pipefail

cd "$(dirname "$0")/.."
VERSION="${1:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' src/T8.Editor/T8.Editor.csproj)}"
TAG="v$VERSION"

if [ -n "$(git status --porcelain --untracked-files=no)" ]; then
  echo "Commit or stash your changes first." >&2
  exit 1
fi
if [ "$(git rev-parse --abbrev-ref HEAD)" != "master" ]; then
  echo "Run this on master, after the release pull request is merged." >&2
  exit 1
fi
git fetch --quiet origin master
if [ "$(git rev-parse HEAD)" != "$(git rev-parse origin/master)" ]; then
  echo "master isn't the same as GitHub's: git pull origin master first (changes go in through pull requests)." >&2
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
