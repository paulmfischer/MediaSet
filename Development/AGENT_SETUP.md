# Linux agent onboarding

This is the foundation in [#635](https://github.com/paulmfischer/MediaSet/issues/635)
for the approved [#630 specification](https://github.com/paulmfischer/MediaSet/issues/630).
The maintained policy is [.github/instructions/shared.md](../.github/instructions/shared.md).
Claude, Codex, and Copilot entry points refer to that policy. Personal skills,
context-mode, credentials, and integrations are optional and are not installed.

## Explicit commands

From the checkout root:

```bash
bash scripts/agents/setup.sh diagnose
# Separate opt-in operation: downloads/restores project dependencies.
bash scripts/agents/setup.sh bootstrap
# Behavioral checks for onboarding (no application runtimes/services required):
python3 -B -m unittest discover -s scripts/agents/tests -v
# Deliberate frontend fixes (review the resulting diff):
python3 -B scripts/agents/format.py
# Explicit completion checks for the branch and local changes:
python3 -B scripts/agents/verify.py
```

From any subdirectory, use an absolute script path or:

```bash
bash "$(git rev-parse --show-toplevel)/scripts/agents/setup.sh" diagnose
```

Diagnosis is read-only: it probes installed tool versions, resolves the SDK with
`global.json`, checks dependency presence, and summarizes known hook registration
locations. Exit 1 means prerequisites/dependencies are missing; exit 0 means those
checks passed, **not** that hooks are trusted or dependencies are current. It
does not run package restore, formatters, tests, service commands, or migrations.
It never reads environment-file values, user secrets, or credential stores.
Settings values and arbitrary version-command output are not printed. Invalid
settings get a content-free warning. No files, trust records, or settings change.

Bootstrap first checks prerequisites, then runs `dotnet restore MediaSet.sln`
and `npm ci --no-audit --no-fund` in `MediaSet.Remix`, in that order. Failures stop
the operation with a nonzero status and visible package-manager output. Review
manifests/lock files and package lifecycle scripts before running: `npm ci` can
execute dependency install scripts and replaces that checkout's `node_modules`.
Rerun after fixing a failure or changing manifests; it never stages/commits files.
It refuses linked project/dependency/cache roots before running package managers.
NuGet packages and npm's cache go in ignored `.agent-local/nuget` and
`.agent-local/npm-cache`; build outputs remain in that checkout's `obj`/`bin`.
No global installs or personal package-manager settings are written. Standard
package managers can still use their normal runtime metadata directories.

The script resolves the checkout containing itself, independent of the caller's
directory. Paths with spaces and Git worktrees are supported. Invoke the script
inside the intended worktree; even when called from another checkout, that script
owns its dependencies. Do not link/share `node_modules`, `obj`, `bin`, or
`.agent-local` between worktrees. Each worktree owns its future results/logs too.
Nothing copies secrets, creates worktrees, allocates ports, or provisions runtimes.
Use [DEVELOPMENT.md](DEVELOPMENT.md) for application configuration, restore/manual
development alternatives, database setup, and application startup.

## Prerequisites

Linux, Bash, Git >=2, Python >=3.11 (standard library only), Node >=20 with npm,
and the .NET SDK selected by [global.json](../global.json) are required. The launcher
reports missing/old Python before importing the diagnostic. Existing legacy hooks
add `jq` and shell/core utilities; they are not prerequisites for the shared setup.
Claude Code and Codex are optional installed runtimes, not bootstrap dependencies.

Remediation is explicit and user-run. On Debian/Ubuntu with Python >=3.11 in the
distribution, install missing system tools with:

```bash
sudo apt-get update
sudo apt-get install bash git python3 jq
```

On other distributions use the distribution's package manager for those names;
if its Python is older, install Python >=3.11 following that distribution's guide.
These commands are documentation, never executed by setup. For Node/npm use the
[Node download/install instructions](https://nodejs.org/en/download) for Linux,
choose a supported version >=20, then verify `node --version` and `npm --version`.
For .NET use the [.NET 10 Linux installation guide](https://learn.microsoft.com/en-us/dotnet/core/install/linux),
select the distribution's SDK instructions (not just the runtime), then run
`dotnet --version` **from this checkout** to verify `global.json` resolves. The
bootstrap never provisions SDKs. After prerequisites are fixed, rerun diagnosis,
run the separate bootstrap command above for missing dependencies, and diagnose
again. Missing `jq` only affects legacy hooks; inspect the inventory below.

## Native activation and opt-out

Evidence recorded 2026-10-25: installed `codex-cli 0.163.0`, Claude Code `2.1.309`.
Version probes and direct script fixtures do not demonstrate live activation.
No live guard/edit smoke test was run for this slice: **unverified** in both agents.
The Git guard is registered for both runtimes by #636; see
[Git guard activation, migration, limits and smoke procedure](AGENT_GIT_GUARD.md).
The environment edit guard is registered by #637; see
[classification, explicit exceptions and native smoke procedure](AGENT_ENV_GUARD.md).
Other legacy definitions remain pending later tickets; these are not the completed
#630 safeguards. See [hook inventory](AGENT_HOOK_INVENTORY.md).

Claude reads project `.claude/settings.json`; personal and local settings can add
hooks. Review definitions/scripts and workspace trust before use, then inspect
`/hooks` in the intended checkout. Review changes there after updates; reopen the
session when checking effective configuration. For one-run opt-out, launch
`claude --settings '{"disableAllHooks":true}'`; remove that override to reactivate.
Managed policies can limit opt-out. Command hooks read JSON stdin: Bash uses
`tool_input.command`, Write/Edit use `tool_input.file_path`; exit 2 can block at
supported events. Shell path references need quotes. `CLAUDE_PROJECT_DIR` can
remain the original checkout after entering a worktree; event `cwd` tracks the
working directory. Legacy scripts do not handle that distinction correctly.
Source: [official Claude hooks reference](https://code.claude.com/docs/en/hooks).

Codex discovers `.codex/hooks.json` or inline hooks in an active `config.toml`
layer, including user and plugin sources. Trust the project layer and review
the exact hook definitions through `/hooks`; changed definition hashes require
renewed review. `/hooks` can disable/reactivate individual non-managed hooks.
For a one-run whole-feature opt-out use `codex -c features.hooks=false`; omit the
override to reactivate, then check `/hooks`. Managed settings may restrict this.
No bypass-trust flag is part of setup. JSON stdin uses `tool_name`, `cwd`, and
`tool_input`; Bash and native `apply_patch` use `tool_input.command`. Native patches
also match Edit/Write aliases. Denial uses supported PreToolUse JSON or exit 2.
Multiple matching sources accumulate. The local candidate uses these payloads but
is not live-tested. Source: [official OpenAI hooks documentation](https://learn.chatgpt.com/docs/hooks).

Diagnosis labels file registrations **configured**, an explicit file-level off
switch **disabled in this file**, and effective activation/trust **unverified**.
It does not parse undocumented trust databases or infer trust from file presence.
Native `/hooks` and a live smoke test are the evidence for active/trusted status.
Only exact duplicate runtime/event/matcher/command tuples in inspected JSON/TOML
files are detected. Semantic duplicates, plugins, managed policy, session hooks,
and other configuration roots require native inspection. Configured sources may
be inactive due to trust, precedence, or policy. Inspect all effective sources;
never overwrite personal configuration to resolve duplicates.

## Migration boundaries

The tracked Claude Git and environment registrations are replaced; per-edit
formatting registrations/scripts are removed by #638. Other definitions are preserved.
Codex has tracked Git and environment guards in inline `.codex/config.toml` registrations. The local untracked Codex
candidate has its two formatting registrations removed locally by #638 and its
test-output rewrite and stop-time verification registrations removed by #639;
other settings and its scripts are preserved. It is **not** installed, staged, trusted, or
converted into public configuration. Fresh clones do not contain it. Diagnostics
warn when that candidate is present but untracked. Personal settings are read only
at known locations, never merged or edited. Backup/review any personal definitions
yourself before changing them. Keep any backup outside discoverable hook paths.

The shared main/signing guard is implemented in #636 and environment guard in #637.
Explicit scoped formatting is implemented in #638; see
[usage, scope, and per-edit hook migration](AGENT_FORMATTING.md).
[Explicit completion verification and local logs](AGENT_VERIFICATION.md) replace
test rewriting and stop-time testing in #639. CI alignment (#640) and final
migration/live smoke tests (#641) remain. Inspect effective personal sources for
obsolete registrations. Startup itself remains passive: no SessionStart or
automatic bootstrap registrations are installed. There are no Git hooks.
