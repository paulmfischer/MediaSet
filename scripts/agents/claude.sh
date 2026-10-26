#!/usr/bin/env bash
# Shared project settings are discovered from Claude's primary working directory.
set -euo pipefail
if ! command -v claude >/dev/null 2>&1; then
  echo 'FAILED: optional Claude Code runtime not found; see Development/AGENT_SETUP.md.' >&2
  exit 1
fi
SCRIPT_PATH=${BASH_SOURCE[0]}
[[ "$SCRIPT_PATH" == */* ]] || SCRIPT_PATH="./$SCRIPT_PATH"
SCRIPT_DIR=$(cd -- "${SCRIPT_PATH%/*}" && pwd)
cd -- "$SCRIPT_DIR/../.."
exec claude "$@"
