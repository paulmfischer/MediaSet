# Existing agent hook inventory

Update for #636: the tracked Claude `block-main-commits.sh` registration/script
below is replaced by the [shared Git guard](AGENT_GIT_GUARD.md). Codex adds only
that guard in tracked inline `.codex/config.toml`; its legacy untracked candidate
is preserved. The tables below retain the #635 legacy inventory for migration.

Inspected 2026-10-25 for [#635](https://github.com/paulmfischer/MediaSet/issues/635).
This records source behavior and harmless direct-input fixtures; it is **not**
evidence of live native execution. See [activation and migration](AGENT_SETUP.md).

Tracked Claude provenance: [#520](https://github.com/paulmfischer/MediaSet/pull/520),
commit `eb1fbb2`; main guard correction in
[#533](https://github.com/paulmfischer/MediaSet/pull/533), commit `396efa7`.
The `.codex/` candidate is untracked local adaptation, discovered during #626 /
[#629](https://github.com/paulmfischer/MediaSet/pull/629); author/runtime provenance
beyond that is unverified. It has no committed source revision. It was deliberately
excluded from #629. It remains untouched and untracked here.

## Registrations and contracts

`.claude/settings.json` and candidate `.codex/hooks.json` each register six command
hooks: PreToolUse Bash → filter-test-output and block-main-commits; PreToolUse
Write|Edit → protect-files; PostToolUse Write|Edit → eslint-fix and prettier-format;
Stop → verify-tests (120 seconds). Neither defines SessionStart. Keep project
definitions tracked deliberately as adapters are replaced; do not copy the entire
candidate into Git. Codex also has an unregistered helper `file-paths.sh`.

Common legacy dependencies: Bash, `jq`, and core shell utilities. Claude scripts
depend on `CLAUDE_PROJECT_DIR`; candidate scripts derive their checkout from their
own location, except payload-only helpers. Registrations rely on native agent
discovery, workspace trust, supported event/matcher/tool contracts, and execution
permissions. Claude registration commands have unquoted project paths, so paths
with spaces can fail; replacement path correctness belongs to the adapter slices.
The new explicit setup commands handle spaces independently.

## Every tracked Claude script

| Script under `.claude/hooks/` | Purpose and dependencies beyond common tools | Observed source/direct-fixture behavior | Agreed disposition |
| --- | --- | --- | --- |
| `block-main-commits.sh` | Main commit / force-push guard; Git, grep, head | Checks literal `git commit` against project branch; first-line force regex rejects feature force pushes too. Does not cover main reset/merge/target directories or signing bypasses. | **Change**, #636: shared main/signing guard; remove blanket feature force prohibition. |
| `protect-files.sh` | Environment-file protection; basename | File-path payload blocks `.env` and every `.env.*`, including `.env.example`; native patch-only payload has no file path and passes. No scoped authorization. | **Change**, #637: safe templates, native edits, narrow temporary exceptions. |
| `eslint-fix.sh` | Per-edit fixes; Node/npm, npx, frontend ESLint | For TS/TSX/JS/JSX calls `npx eslint --fix`, discards stderr and always exits 0; can invoke package acquisition when local tools are missing. | **Remove hook**, #638; explicit scoped formatter/fix command. |
| `prettier-format.sh` | Per-edit formatting; Node/npm, npx, frontend Prettier | Same extension selection, calls `npx prettier --write`, suppresses errors and always returns 0. | **Remove hook**, #638; explicit scoped formatting. |
| `filter-test-output.sh` | Rewrite test command output; grep/head | Anchored npm/dotnet test commands get a pipeline appended. Manually interpolates JSON, breaking quoted arguments; pipeline hides test status and passing output. | **Remove hook**, #639; explicit verification, preserve ordinary commands. |
| `verify-tests.sh` | Stop-time tests; Git, dotnet, npm, grep/tail | Stops recursion via `stop_hook_active`; selects staged/unstaged + upstream triple-dot, omits untracked/pushed changes on tracking branches; extension-only selection misses config. Last 15 lines, no full logs. Can run tests for discussion turns. | **Remove hook**, #639; completion verification, #640 CI alignment. |

## Every local Codex candidate script

All scripts below are under `.codex/hooks/`, adapted from the Claude scripts
above, with the same common dependencies; no extra runtime is provisioned.

| Script | Candidate differences / observed behavior | Agreed disposition |
| --- | --- | --- |
| `block-main-commits.sh` | Resolves its own checkout, otherwise same literal commit/force restrictions and coverage gaps; Git/grep/head. | **Change** with shared main/signing guard, #636. |
| `file-paths.sh` | New helper; jq reads legacy `file_path` or parses patch Add/Update/Delete/Move header lines from `tool_input.command`. Direct patch fixture extracts `.env` and move destination; no validation of complete patch grammar. Malformed inputs may pass via process-substitution consumers. | **Change** into supported thin adapter parsing/normalization, #637/#638; not a third policy implementation. |
| `protect-files.sh` | Uses helper; permits exactly `.env.example`, blocks other `.env.*`; no scoped exceptions. Direct native patch fixture blocks `.env`. jq/helper errors can be lost through process substitution. | **Change**, #637. |
| `eslint-fix.sh` | Uses helper + `realpath`, scopes existing frontend files and uses local `node_modules/.bin/eslint`; failures visible on stderr but final exit still 0. Resolves relative paths using process working directory. | **Remove hook**, #638; retain explicit scoped fixes. |
| `prettier-format.sh` | Same helper/path scoping, local Prettier binary; errors visible but final success still masks them. | **Remove hook**, #638; explicit formatting. |
| `filter-test-output.sh` | Uses jq to emit valid JSON and captures original test status; includes passing summary lines. Still rewrites arbitrary shell text, captures output in memory and truncates it without full logs. | **Remove hook**, #639; explicit verification. |
| `verify-tests.sh` | Resolves its own checkout; same upstream/extension/untracked selection gaps, stop-time behavior, 15-line output and missing logs as Claude. | **Remove hook**, #639; explicit completion verification. |

## Evidence and additional behavior

Harmless direct stdin fixtures checked template/environment decisions, native
patch header extraction, quoted test-command JSON, and recursion no-op. Formatters
and stop-time application tests were **not** invoked: source inspection identified
their write/test behavior. Live activation/trust remains **unverified** for Codex
0.163.0 and Claude Code 2.1.309. No candidate or personal setting was changed.

Additional behavior reconciled with #630: npx acquisition, silent formatter errors,
patch-parser fail-open paths, worktree/cwd assumptions, and quote/selection bugs
are addressed by the already assigned replacement slices above, not copied into
setup. There are no other tracked Claude scripts or local candidate files in this
inspection. Exact duplicate registrations are diagnosed where detectable; native
inspection remains required for effective configuration and plugin duplicates.
