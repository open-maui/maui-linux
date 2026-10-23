#!/usr/bin/env bash
# Fetches the dotnet/maui test sources the conformance suite compiles in place.
# Usage: tests/Conformance/get-maui-sources.sh [dest] [tag]
set -euo pipefail
DEST="${1:-$HOME/.cache/openmaui-build/maui-src}"
TAG="${2:-10.0.120}"
if [ ! -d "$DEST/.git" ]; then
  git clone --depth 1 --branch "$TAG" --filter=blob:none --sparse https://github.com/dotnet/maui "$DEST"
elif [ "$(git -C "$DEST" describe --tags --exact-match 2>/dev/null)" != "$TAG" ]; then
  # An existing clone of another tag (the previous MAUI release) moves to this one.
  git -C "$DEST" fetch --depth 1 --filter=blob:none origin tag "$TAG"
  git -C "$DEST" checkout -q "$TAG"
fi
git -C "$DEST" sparse-checkout set \
  src/Core/tests/DeviceTests \
  src/Core/tests/DeviceTests.Shared \
  src/Controls/tests/DeviceTests \
  src/Essentials/test/DeviceTests \
  src/TestUtils
echo "MAUI test sources at $DEST ($(git -C "$DEST" describe --tags 2>/dev/null || echo "$TAG"))"
