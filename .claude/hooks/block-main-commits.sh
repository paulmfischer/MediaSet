#!/bin/bash
INPUT=$(cat)
CMD=$(echo "$INPUT" | jq -r '.tool_input.command')

# Block git commit when on main branch
if echo "$CMD" | grep -qE "git commit"; then
  BRANCH=$(git -C "$CLAUDE_PROJECT_DIR" branch --show-current 2>/dev/null)
  if [ "$BRANCH" = "main" ]; then
    echo "Blocked: Cannot commit directly to 'main'. Create a feature branch first (git checkout -b feature/your-branch)." >&2
    exit 2
  fi
fi

# Block force push to any branch
if echo "$CMD" | grep -qE "git push.*(--force| -f(\s|$))"; then
  echo "Blocked: Force push is not allowed." >&2
  exit 2
fi

exit 0
