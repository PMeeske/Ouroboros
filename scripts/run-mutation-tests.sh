#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
CONFIG_PATH="${1:-stryker-config.json}"
shift || true

pushd "${REPO_ROOT}" > /dev/null

echo "Restoring local dotnet tools..."
dotnet tool restore

echo "Running mutation tests: dotnet stryker --config-file ${CONFIG_PATH} $*"
dotnet stryker --config-file "${CONFIG_PATH}" "$@"

popd > /dev/null
