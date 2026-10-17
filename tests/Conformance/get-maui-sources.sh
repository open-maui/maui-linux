#!/usr/bin/env bash
# Fetches the dotnet/maui sources the conformance suites compile in place (a shallow,
# sparse, blob-less checkout of one revision).
# Usage: tests/Conformance/get-maui-sources.sh [dest] [revision]
#   revision: a commit SHA (fetched directly: init + fetch --depth 1), a tag or a branch.
#   Default: d32b79928709cf53b671336dabd0caef5a679fc2 (release/11.0.1xx-rc2), the commit
#   the Microsoft.Maui.* 11.0.0-rc.2.26505.5 packages were built from.
set -euo pipefail
DEST="${1:-$HOME/.cache/openmaui-build/maui-src-11}"
REV="${2:-d32b79928709cf53b671336dabd0caef5a679fc2}"
REPO=https://github.com/dotnet/maui

# The suites compile the test folders; the product folders are read when porting
# (comparing OpenMaui with MAUI's handlers, mappers and Controls behaviour).
PATHS=(
  src/Core/tests/DeviceTests
  src/Core/tests/DeviceTests.Shared
  src/Controls/tests/DeviceTests
  src/Essentials/test/DeviceTests
  src/TestUtils
  src/Controls/src/Core
  src/Core/src
  src/Essentials/src
  src/SingleProject/Resizetizer/src
)

if [ ! -d "$DEST/.git" ]; then
  mkdir -p "$DEST"
  git -C "$DEST" init -q
  git -C "$DEST" remote add origin "$REPO"
  git -C "$DEST" config remote.origin.promisor true
  git -C "$DEST" config remote.origin.partialclonefilter blob:none
  git -C "$DEST" sparse-checkout init --cone
fi
git -C "$DEST" sparse-checkout set "${PATHS[@]}"
if [[ "$REV" =~ ^[0-9a-f]{40}$ ]]; then
  git -C "$DEST" fetch -q --depth 1 --filter=blob:none origin "$REV"
else
  # A tag or a branch name.
  git -C "$DEST" fetch -q --depth 1 --filter=blob:none origin "+refs/tags/$REV:refs/tags/$REV" 2>/dev/null \
    || git -C "$DEST" fetch -q --depth 1 --filter=blob:none origin "$REV"
fi
git -C "$DEST" checkout -q --detach FETCH_HEAD
echo "MAUI sources at $DEST ($REV, $(git -C "$DEST" rev-parse HEAD))"
