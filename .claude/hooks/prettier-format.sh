#!/bin/bash
FILE=$(jq -r '.tool_input.file_path' <<< "$(cat)")

if [[ "$FILE" == *.ts || "$FILE" == *.tsx || "$FILE" == *.js || "$FILE" == *.jsx ]]; then
  cd "$CLAUDE_PROJECT_DIR/MediaSet.Remix" && npx prettier --write "$FILE" 2>/dev/null
fi

exit 0
