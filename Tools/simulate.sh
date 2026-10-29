#!/usr/bin/env bash
# Measures novice rounds in the actual Game scene using editor PlayMode tests.
# The Unity Editor must not have this project open. Reports and test results go to Logs/.
# Usage: SIM_ROUNDS=5 SIM_LEVELS=1,7 SIM_SEED=1 Tools/simulate.sh [path to Unity.exe]
# Defaults: 40 runs per visit round, levels 1,3,5,7, base seed 1. This measures outcomes, not balance thresholds.
set -euo pipefail
cd "$(dirname "$0")/.."

UNITY="${1:-C:/Program Files/Unity/Hub/Editor/$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt | tr -d '\r')/Editor/Unity.exe}"
export SIM_ROUNDS="${SIM_ROUNDS:-40}"
export SIM_LEVELS="${SIM_LEVELS:-1,3,5,7}"
export SIM_SEED="${SIM_SEED:-1}"

mkdir -p Logs
# Remove only the previous report so a failed run cannot print old measurements as its own.
rm -f Logs/simulation-report.md
STATUS=0
"$UNITY" -batchmode -nographics -projectPath . \
    -runTests -testPlatform PlayMode -testFilter ToyCarArena.Tests.PlayMode.RoundSimulationTests.SimulateNoviceRounds \
    -testResults Logs/simulation-results.xml -logFile Logs/simulation.log || STATUS=$?
if [ -f Logs/simulation-report.md ]; then
    cat Logs/simulation-report.md
else
    echo "Simulation report was not written. See Logs/simulation.log and Logs/simulation-results.xml." >&2
    if [ "$STATUS" -eq 0 ]; then STATUS=1; fi
fi
exit "$STATUS"
