# Deliberate project formatting

Run the shared command when you intend to apply frontend fixes:

```bash
python3 -B scripts/agents/format.py
python3 -B scripts/agents/format.py --base main
python3 -B scripts/agents/format.py -- MediaSet.Remix/app/routes/books.tsx
python3 -B scripts/agents/format.py --all
python3 -B scripts/agents/format.py --help
```

From a subdirectory, invoke the script in the intended checkout; explicit file
arguments are relative to your current directory:

```bash
python3 -B "$(git rev-parse --show-toplevel)/scripts/agents/format.py" -- "./file with spaces.ts"
```

The default includes committed branch changes against the merge base with local
`origin/main` (or local `main` when that remote ref is unavailable), staged and
unstaged paths, and nonignored untracked files. Pushing a feature branch does not
remove its changes from scope. `--base REF` overrides the local comparison ref.
No fetch runs; refresh your local main ref deliberately when needed. Missing or
unrelated bases fail with visible diagnostics; choose a valid base, explicit
files, or `--all`. The command never falls back to broad rewrites.

Supported files are frontend `.js`, `.jsx`, `.ts`, and `.tsx` files (installed
ESLint `--fix`, then Prettier `--write`) and JSON under `MediaSet.Remix/app`
(Prettier). This retains the former hook fixes and existing app JSON formatting
scope. Backend files, other JSON, documentation, static `public` assets, and
generated/dependency directories (`node_modules`, `build`, `coverage`, `.cache`,
`.git`) are excluded. `--all` deliberately includes every supported tracked or
nonignored untracked file, including otherwise unchanged files. Use explicit
file arguments to avoid touching unrelated local edits within the default set.
Modes cannot be combined.

Deleted/nonexistent supported files are reported and skipped. Explicit outside,
unsupported, ignored, linked, or nonregular files fail before any formatter runs.
The command uses literal argument arrays and NUL-separated Git paths; spaces and
shell metacharacters are safe. It resolves its own checkout, including worktrees,
and requires independent local dependencies; linked source paths, linked
`node_modules`, or tool links escaping that directory are refused.

Linux, Python 3.11+, Git, Node.js, and installed frontend tools are required.
Install prerequisites and explicitly bootstrap dependencies using
[agent setup](AGENT_SETUP.md). No `npx`, package fetching, dependency restore,
service operation, staging, or commit runs. No selected files is a successful
no-op without requiring frontend tools. Selected JSON requires only Prettier;
selected code also requires ESLint. Tool stdout/stderr stays visible. Any failed
tool makes the command fail, while remaining selected fixes still run. Files
already fixed remain changed after a later failure; review partial changes too.

Always inspect `git diff` afterward, preserve unrelated user edits, and stage
only intended changes. Formatting a selected file can change more than its edited
lines; explicit file selection does not isolate hunks. Do not discard user work
to resolve formatter failures.

## Migration from automatic rewrites

#638 removes tracked Claude PostToolUse ESLint/Prettier registrations and their
scripts. Tracked Codex `.codex/config.toml` has no automatic formatting hooks.
The existing local, untracked `.codex/hooks.json` candidate has only its two
formatting registrations removed in this checkout; its other registrations and
untracked helper scripts are preserved. Do not stage that candidate or copy it
into a fresh clone. Personal/global/plugin hook definitions remain user-owned:
inspect `/hooks` in each runtime and remove old per-edit formatting registrations
there deliberately if present. Reopen/review effective definitions as required
by native trust handling. This slice does not claim live activation tests.

Legacy test rewriting and stop-time verification remain for #639; final migration
and live smoke tests remain #641. See [hook inventory](AGENT_HOOK_INVENTORY.md).

Behavior tests use disposable Git repositories and recording tool fixtures:

```bash
python3 -B -m unittest discover -s scripts/agents/tests -p test_format.py -v
```
