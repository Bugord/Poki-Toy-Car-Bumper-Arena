#!/usr/bin/env bash
# Uploads Builds/WebGL to Poki for Developers as a new version named after the build's product name, version and build
# number (Player Settings > Product Name and Version, and the commit count of the commit built, from the build's
# build.json: e.g. Bonk Arena v0.1.0 (36), or
# Bonk Arena v0.1.0 (36) dev for a development build, which has the cheat console:
# test it, never release it), with the commit built and its message as notes (see Docs/poki-upload.md).
# Checks the build first: it must exist, be uncompressed and carry its build number. Build it with
# Tools/build-webgl.sh [--development]. It signs in with the upload token in POKI_UPLOAD_TOKEN, or else in
# UserSettings/poki-upload-token.txt (ignored by git); with neither, the browser opens to sign in to Poki for Developers.
# Usage: Tools/upload-poki.sh [--dry-run]; --dry-run makes the checks and prints the name and notes, without uploading.
set -euo pipefail
cd "$(dirname "$0")/.."

DRY_RUN=
if [ "${1:-}" = "--dry-run" ]; then
    DRY_RUN=1
fi

BUILD=Builds/WebGL
fail() { echo "upload-poki: $*" >&2; exit 1; }
# The value of a "key": "value" line of a JSON file the build wrote.
field() { sed -n "s/.*\"$2\": \"\([^\"]*\)\".*/\1/p" "$1"; }

[ -f poki.json ] || fail "poki.json is missing; run: npx @poki/cli init --game <game id> --build-dir $BUILD"
[ -f "$BUILD/index.html" ] || fail "no build in $BUILD; run Tools/build-webgl.sh"
if ls "$BUILD"/Build/*.br "$BUILD"/Build/*.gz >/dev/null 2>&1; then
    fail "$BUILD is compressed; Poki wants compression Disabled"
fi

PRODUCT=$(field "$BUILD/index.json" product_name)
[ -n "$PRODUCT" ] || fail "no product_name in $BUILD/index.json"
VERSION=$(field "$BUILD/index.json" product_version)
[ -n "$VERSION" ] || fail "no product_version in $BUILD/index.json"
[ -f "$BUILD/build.json" ] || fail "no $BUILD/build.json (the build number); build again with Tools/build-webgl.sh"
BUILD_NUMBER=$(field "$BUILD/build.json" build_number)
COMMIT=$(field "$BUILD/build.json" commit)
[ -n "$BUILD_NUMBER" ] && [ -n "$COMMIT" ] || fail "no build_number or commit in $BUILD/build.json"
NAME="$PRODUCT v$VERSION ($BUILD_NUMBER)"
if ! grep -q '"development_player": "false"' "$BUILD/index.json"; then
    NAME="$NAME dev"
fi

case "$(git rev-parse HEAD)" in
    "$COMMIT"*) ;;
    *) echo "upload-poki: warning: the build is of $COMMIT, not of the commit checked out ($(git rev-parse --short HEAD))" >&2 ;;
esac
if ! git diff --quiet HEAD -- Assets Packages ProjectSettings; then
    echo "upload-poki: warning: Assets, Packages or ProjectSettings have uncommitted changes, which the build may hold" >&2
fi
if MESSAGE=$(git log -1 --pretty=%B "$COMMIT" 2>/dev/null); then
    NOTES="$COMMIT: $MESSAGE"
else
    NOTES="$COMMIT"
fi

if [ -n "$DRY_RUN" ]; then
    echo "upload-poki: would upload $BUILD as: $NAME"
    echo "upload-poki: notes: $NOTES"
    exit 0
fi

# Poki's CLI takes an upload token over its browser login; the token stays out of git, in UserSettings.
TOKEN_FILE=UserSettings/poki-upload-token.txt
if [ -z "${POKI_UPLOAD_TOKEN:-}" ] && [ -s "$TOKEN_FILE" ]; then
    POKI_UPLOAD_TOKEN=$(tr -d '
' < "$TOKEN_FILE")
    export POKI_UPLOAD_TOKEN
fi

echo "upload-poki: uploading $NAME ($COMMIT)"
node Tools/poki-auth.js
npx --yes @poki/cli upload --name "$NAME" --notes "$NOTES"
