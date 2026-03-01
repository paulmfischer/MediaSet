#!/bin/bash
INPUT=$(cat)
FILE=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')

if [ -z "$FILE" ]; then
  exit 0
fi

FILENAME=$(basename "$FILE")

# Block writes to .env files (e.g. .env, .env.local, .env.production)
if [[ "$FILENAME" == ".env" || "$FILENAME" == .env.* ]]; then
  echo "Protected: Cannot write to '$FILE'. Edit .env files manually." >&2
  exit 2
fi

exit 0
