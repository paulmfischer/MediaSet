#!/bin/bash
INPUT=$(cat)

# Prevent infinite loop if Claude is already attempting to fix failing tests
if [ "$(echo "$INPUT" | jq -r '.stop_hook_active')" = "true" ]; then
  exit 0
fi

# Collect changed files: unstaged, staged, and unpushed commits
REMOTE_BASE=$(git -C "$CLAUDE_PROJECT_DIR" rev-parse --abbrev-ref --symbolic-full-name @{upstream} 2>/dev/null || echo "origin/main")
CHANGED=$(
  git -C "$CLAUDE_PROJECT_DIR" diff --name-only 2>/dev/null
  git -C "$CLAUDE_PROJECT_DIR" diff --name-only --cached 2>/dev/null
  git -C "$CLAUDE_PROJECT_DIR" diff --name-only "$REMOTE_BASE"...HEAD 2>/dev/null
)

if [ -z "$CHANGED" ]; then
  exit 0
fi

FAILED=0

# Run backend tests if any .cs files changed
if echo "$CHANGED" | grep -qE '\.cs$'; then
  echo "Backend changes detected — running dotnet tests..." >&2
  OUTPUT=$(dotnet test "$CLAUDE_PROJECT_DIR/MediaSet.Api.Tests/MediaSet.Api.Tests.csproj" 2>&1)
  TEST_EXIT=$?
  echo "$OUTPUT" | tail -15 >&2
  if [ "$TEST_EXIT" -ne 0 ]; then
    echo "Backend tests FAILED." >&2
    FAILED=1
  else
    echo "Backend tests passed." >&2
  fi
fi

# Run frontend tests if any TS/JS files changed
if echo "$CHANGED" | grep -qE '\.(ts|tsx|js|jsx)$'; then
  echo "Frontend changes detected — running Vitest..." >&2
  OUTPUT=$(cd "$CLAUDE_PROJECT_DIR/MediaSet.Remix" && npm test 2>&1)
  TEST_EXIT=$?
  echo "$OUTPUT" | tail -15 >&2
  if [ "$TEST_EXIT" -ne 0 ]; then
    echo "Frontend tests FAILED." >&2
    FAILED=1
  else
    echo "Frontend tests passed." >&2
  fi
fi

if [ "$FAILED" -eq 1 ]; then
  echo "Fix failing tests before finishing." >&2
  exit 2
fi

exit 0
