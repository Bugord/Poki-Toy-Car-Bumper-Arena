#!/usr/bin/env bash
# Builds the Poki release (WebGL, POKI_ENABLED) into Builds/WebGL in batch mode and prints the size report.
# The Unity Editor must not have this project open. Super Console leaves itself out of release builds. The build is
# numbered with the commit count of HEAD (ToyCarArena.Editor.BuildNumber), which the upload puts in the version's name.
# Usage: Tools/build-webgl.sh [--development] [path to Unity.exe]; --development makes a development build, whose
# exceptions name their lines (set Enable Exceptions to Full With Stacktrace too for the stack) and which runs the console.
set -euo pipefail
cd "$(dirname "$0")/.."

DEVELOPMENT=
if [ "${1:-}" = "--development" ]; then
    DEVELOPMENT=-development
    shift
fi
UNITY="${1:-C:/Program Files/Unity/Hub/Editor/$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)/Editor/Unity.exe}"

mkdir -p Builds
"$UNITY" -batchmode -quit -nographics -projectPath . -buildTarget WebGL \
    -executeMethod ToyCarArena.Editor.WebGLBuild.Build $DEVELOPMENT -logFile Builds/WebGL-build.log
cat Builds/WebGL-report.txt
