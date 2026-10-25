#!/usr/bin/env bash
# No startup registration: run this explicitly from any working directory.
set -euo pipefail
if ! command -v python3 >/dev/null 2>&1; then
  echo 'FAILED: Python >=3.11 required. On Debian/Ubuntu: sudo apt-get install python3' >&2
  echo 'For other Linux distributions, see Development/AGENT_SETUP.md#prerequisites.' >&2
  exit 1
fi
if ! python3 -c 'import sys; sys.exit(0 if sys.version_info >= (3, 11) else 1)'; then
  echo 'FAILED: Python >=3.11 required; see Development/AGENT_SETUP.md#prerequisites.' >&2
  exit 1
fi
SCRIPT_PATH=${BASH_SOURCE[0]}
[[ "$SCRIPT_PATH" == */* ]] || SCRIPT_PATH="./$SCRIPT_PATH"
SCRIPT_DIR=$(cd -- "${SCRIPT_PATH%/*}" && pwd)
exec python3 -B "$SCRIPT_DIR/setup.py" "$@"
