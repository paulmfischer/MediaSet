# Explicit completion verification

Run completion checks deliberately from the intended checkout:

```bash
python3 -B scripts/agents/verify.py
python3 -B scripts/agents/verify.py --base origin/release
python3 -B scripts/agents/verify.py --full
# From any subdirectory:
python3 -B "$(git rev-parse --show-toplevel)/scripts/agents/verify.py"
```

Focused tests from [shared policy](../.github/instructions/shared.md#testing-commands)
support iteration. They do not replace the selected completion checks before
reporting implementation complete. Discussion-only turns require no verification.
This command is never registered as a startup, tool-rewrite, or stop hook.
Ordinary `dotnet test` and `npm test` commands remain available and unchanged.

## CI and local equivalence

The `PR Checks / Full Verification` job runs on every pull request covered by
the workflow's existing target branches. It replaces the separate path-filtered
API and UI jobs, so documentation and shared tooling changes receive the same
full gate. Other workflows are unchanged.

The runner installs Python 3.11, the .NET SDK selected by `global.json`, and
Node 24.22.1 LTS from `.nvmrc` using setup actions, then runs these shared commands from the root:

```bash
bash scripts/agents/setup.sh bootstrap
python3 -B scripts/agents/verify.py --full --show-output
```

Bootstrap explicitly restores checkout-local NuGet/npm dependencies and caches;
CI caches only `.agent-local/nuget` and `.agent-local/npm-cache`, keyed by runtime
and dependency manifests, rather than verification logs or build outputs.
diagnosis and verification never install global tools. No local secrets,
database, application services, branch upstream or fetched comparison ref is
required. Full mode selects all groups even in a shallow detached PR checkout.
The same command works locally after explicit dependency preparation.

Both environments run whitespace and agent contract checks, backend build and
tests, and frontend lint, formatting, explicit `npm run typecheck`, build and
tests. `--show-output` additionally prints complete enabled-check logs to the
console, including successful output and failures, while preserving per-check
statuses and the first nonzero exit code. Local verification keeps its concise
default output. CI does not upload `.agent-local` logs or summaries as artifacts.
Review diagnostics before using this opt-in output in a shared local transcript.

Contract fixtures validate the workflow command in a shallow detached checkout
with deterministic tools, including a failing command and subsequent checks.
They do not prove hosted runner integration: record a successful actual PR check
run and its URL before treating CI integration as verified.

## Selection and commands

Default selection uses the merge base of `HEAD` and the local `origin/main` ref
(falling back to local `main` when the remote ref is absent). It combines **all
branch commits** with staged, unstaged and nonignored untracked paths. A tracking
feature branch already pushed to its remote still includes its committed changes.
Renames consider both paths and deleted paths still select their original checks.
Staged changes remain selected even if an unstaged edit cancels them out.

`--base REF` overrides the comparison ref; quote it as one shell argument. Missing,
invalid or unrelated refs produce an explanation and full checks. The command
never fetches. `--full` runs every group regardless of base or changed files.
Inspect `selection.log` to audit the resolved merge base and path list.

| Changes | Completion checks |
| --- | --- |
| `MediaSet.Api/**`, `MediaSet.Api.Tests/**`, solutions/projects, `global.json` | `dotnet build MediaSet.sln --no-restore`; `dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj --no-restore` |
| `MediaSet.Remix/**` | From `MediaSet.Remix`: `npm run lint`, `npm run lint:format`, `npm run typecheck`, `npm run build`, `npm run test` |
| `scripts/agents/**`, `.claude/**`, `.codex/**` | `python3 -B -m unittest discover -s scripts/agents/tests -v` |
| Documentation (`.md`, `.rst`, `.txt`, `docs/**`) | Lightweight Git whitespace checks |
| Other/shared configuration, such as `Directory.Build.props`, `.editorconfig`, CI definitions or unknown paths | Both application groups and agent contracts |

Documentation classification takes precedence within application directories.
Whitespace checks always run against the branch comparison when available,
staged changes, and unstaged changes. New nonignored documentation text files
also receive a trailing-whitespace check without staging them. Review new
documentation content and links as part of the task.
With no changes, application/agent checks are explicitly skipped.

Commands run as argument arrays without shell interpolation, grep pipelines or
rewriting user commands. Quoted arguments and checkout paths containing spaces,
quotes or shell punctuation stay literal. No formatter uses `--fix`/`--write`.
The command does not edit, stage, reset or commit source. Builds/tests may create
normal local outputs and caches; source changes made by package scripts themselves
still require review. Use [explicit formatting](AGENT_FORMATTING.md) to fix reported
formatting, then rerun affected checks.

## Prerequisites, results and troubleshooting

Use [setup diagnosis and explicit bootstrap](AGENT_SETUP.md#explicit-commands)
first. Verification does not install dependencies or provision services. Backend
checks require `dotnet` and restored assets for both backend projects. Frontend
checks require Node/npm and the installed tool for each npm script. Missing tools,
missing dependencies, linked `node_modules` or tools linked outside that checkout's
dependencies produce a failed/incomplete check with remediation in its log. Each
worktree owns its dependencies, logs and outputs; invoke the script in the intended
worktree, even when the caller is elsewhere. Do not link dependency directories.
Application runtime/database configuration stays in [DEVELOPMENT.md](DEVELOPMENT.md).

Every check prints `PASSED`, `FAILED` (with exit status) or `SKIPPED`, plus its
full log path. A successful check remains visible even if it printed no failure
lines. Full stdout/stderr is streamed directly to disk, including diagnostics
from failed commands. Other checks continue after failures. The command returns
the first nonzero check status (signal termination becomes 128 + signal number);
setup/prerequisite failures return 1. A concise `summary.json` records all results.
Read the failed check's full log before changing code; do not infer success from
an empty filtered output.

Distinguish a pre-existing failure using concrete evidence, such as an earlier
failure on the unchanged base or a verified unchanged relevant file/configuration
set. Report that evidence separately from regressions introduced by the task.
An unexplained failure is unresolved; do not label it pre-existing by assumption.
Do not relax checks or expand the task to fix unrelated failures. The command
does not attempt automatic baseline execution or silently convert failures to skips.

## Local logs and reuse

Full logs live under ignored `.agent-local/verification/run-<UTC>-<unique-id>/`,
inside the script's checkout, including linked Git worktrees. They are never
uploaded automatically. Review/redact any diagnostics before explicitly sharing
them, since underlying tool output can contain local configuration details.
Completed runs retain the ten most recent command-owned run directories; failed
runs count toward the same limit. Cleanup requires an exact ownership marker,
recognized run name and only known regular log files. It preserves unrelated files,
directories, symlinks and owned-looking directories containing added user data.
Linked log roots are refused. There is no persistent verification cache.

Agents may reuse successful results only when they establish that all relevant
source, tests, dependency manifests/locks, tool configuration and runtime inputs
are unchanged since that run. Identify the reused run, checks and evidence in the
completion report. A later relevant edit invalidates affected checks, which must
rerun. A docs-only edit can reuse proven-unchanged application checks but still
needs its relevant lightweight verification. Focused tests alone are insufficient
evidence of completion. Report executed/reused/skipped checks, any failures and
their provenance, and the local log/summary paths.

## Test-hook migration

#639 removes tracked Claude Bash test-output rewriting and Stop verification
registrations and their scripts. Tracked Codex `.codex/config.toml` already has
only the Git/environment guards; no test hooks are added. This checkout's local
untracked `.codex/hooks.json` candidate has the same two obsolete test registrations
removed deliberately, preserving its other definitions and every local helper
script. The candidate remains untracked and is not installed in fresh clones.

Review native `/hooks` for other personal/global/plugin sources; remove or disable
only obsolete test rewriting and stop-time testing definitions deliberately.
Do not overwrite personal settings. Review changed definitions and reopen sessions
as required by [native trust handling](AGENT_SETUP.md#native-activation-and-opt-out).
Direct fixtures prove configuration and command behavior, not live activation.
Final migration/live smoke evidence remains #641; CI alignment remains #640.

```bash
python3 -B -m unittest discover -s scripts/agents/tests -p test_verify.py -v
```

Fixtures use disposable Git repositories and deterministic tool stand-ins without
application services, dependency downloads, or changes to real source. They cover
branch/pushed/local selection, base overrides/fallback, all check groups, literal
paths/arguments, success and true failures, missing prerequisites, full diagnostic
logs, ownership-limited retention and independent worktrees.
