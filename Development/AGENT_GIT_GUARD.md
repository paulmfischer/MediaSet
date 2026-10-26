# Agent Git guard

Issue [#636](https://github.com/paulmfischer/MediaSet/issues/636) adds one Python
policy in `scripts/agents/git_guard.py` and thin Claude/Codex entry points. It
requires Python >=3.11 and Git, with no packages or Git hooks. It reads Git state
without executing the proposed command, configuring signing, or changing identity.

The guard blocks direct main commits, merges/rebases/resets and related writes,
main ref updates and pushes, and overrides of configured signing. Existing
Git's typed boolean value for `commit.gpgsign` (including a bare true key) or a
signing key activates signing preservation; invalid boolean values fail closed. Feature
commits and pushes, including force pushes, receive no approval decision. Workflow
policy still requires explicit authorization before rewriting published feature
history and requires `--force-with-lease`; the hook cannot infer conversation
approval. `gh pr merge` remains available for an approved PR. On main, fetch remote
refs then synchronize using `git merge --ff-only origin/main` or
`git pull --ff-only`. These operations delegate the fast-forward check to Git.
The exception requires main's existing, single configured upstream to name
`refs/heads/main` on a remote, with a standard remote-main tracking mapping.
Merge accepts only that existing remote-main ref (or the default upstream); pull
accepts the default or that remote's main source. Local/remote feature branches,
multiple sources, absent/ambiguous upstreams and transient branch/remote config
overrides are blocked even with `--ff-only`. This permits synchronization without
using the exception to integrate an unreviewed feature into main.
Command-local `GIT_CONFIG_*` environment changes are rejected for protected
operations independently of whether signing is configured, so main/upstream and
push/fetch target checks cannot inspect a different configuration from Git.

## Registration and migration

Tracked `.claude/settings.json` and `.codex/config.toml` each register the shared
Git and native environment guards. Legacy project guards, per-edit rewrites and
Stop tests have been removed. A fresh clone needs no copied candidate or personal
settings. Codex merges inline hooks with hooks.json; multiple sources accumulate.
See [migration boundaries](AGENT_SETUP.md#migration-boundaries) for deliberate
archival of the local candidate and preservation of unrelated personal entries.
Diagnosis warns about recognizable obsolete/competing registrations; it cannot
determine effective trust or all semantic duplicates. Trust is never automatic.

Review the new scripts/registration in the intended checkout, trust that project
layer, inspect `/hooks`, and reopen the session to check the effective definition.
Changed definition hashes may need renewed review. Existing project, personal,
plugin and managed hooks can accumulate. The native runtime is the authority for
active/trusted status. Opt-out/reactivation instructions are in
[onboarding](AGENT_SETUP.md#native-activation-and-opt-out).

The supported contract is PreToolUse `Bash`, JSON stdin with `tool_input.command`
and event `cwd` (an explicit tool `cwd` takes precedence if supplied). Blocks use
JSON `hookSpecificOutput.permissionDecision=deny` with an actionable reason.
Passing commands emit nothing and do not grant permission or rewrite input.
Sources checked 2026-10-25: [Claude reference](https://code.claude.com/docs/en/hooks)
and [OpenAI hooks](https://learn.chatgpt.com/docs/hooks). Fixture execution is
**not evidence of live native activation**; assembled runtime validation is #641.

## Supported commands and limits

Literal Git commands support `-C` (including repeated/attached forms), `--git-dir`,
`--work-tree`, `-c`/`--config-env`, environment assignments, paths with spaces,
subdirectories, and worktrees. Push inspection includes explicit refspecs,
deletions, main destinations, and effective default/upstream configuration
(including transient overrides and push-remote selection). Matching refspecs `:`
and `+:` are rejected because they may update main. Config section removal/rename
and persistent includes are blocked when signing is configured because they can
indirectly remove or override signing. Commands may be
separated by newlines, semicolons or `&&`; escaped line continuations are normalized
as shell syntax, and literal `cd` changes are tracked. Conflicting fast-forward
flags require a single unambiguous `--ff-only` invocation.
Fetch and pull also inspect every effective configured fetch refspec, including
the selected default remote and transient config overrides. Destinations that
name main or wildcard local heads are blocked; ordinary `refs/remotes/...`
tracking mappings pass. Fetch `--all`, `--multiple`, `--refmap` and
`--update-head-ok` require a simpler supported invocation.
Commit message arguments and quoted literal heredoc bodies are treated as data.
No command text or arbitrary config value is echoed in a denial.

This is a bounded guardrail, **not a shell parser or security boundary**. Recognized
Git calls with unresolved syntax fail closed: split them into literal tool calls.
Expansions, substitutions (including `$(cat <<'EOF' ...)` commit messages), pipes,
conditional `||`, shell wrappers containing Git, uncertain aliases and malformed
input are not evaluated. Use a literal quoted message or `git commit -F -` with a
quoted heredoc instead. A branch switch followed by a write in the same tool call
is conservatively blocked because the switch has not executed and could fail.
Run the switch separately, then let the next call inspect its actual result.
Some ambiguous but harmless forms may require a simpler equivalent command.
File-descriptor/compound redirections such as `2>log` and `2>&1` are rejected
because descriptor adjacency cannot be safely recovered by the bounded lexer.
Ordinary literal redirections without descriptors remain supported. Root rebase
resolves its single explicit branch operand. Rebase with effective
`rebase.updateRefs=true` or `--update-refs` is blocked because secondary refs can
include main; use `--no-update-refs` for a feature-only rebase.
Pull is also blocked whenever effective `rebase.updateRefs` is true, including
when rebase would be selected implicitly by pull/branch config. For a feature-only
pull use the scoped `git -c rebase.updateRefs=false pull ...`; pull does not accept
the direct rebase `--no-update-refs` flag. This conservative rule also applies to
an explicitly non-rebasing pull while update-refs remains enabled.
Protected Git verbs accept a bounded list of full long-option spellings.
Abbreviations accepted by Git (such as `--no-gpg-s` or `--remove-sect`) and unknown
options are rejected instead of guessing which option Git will select. Use the
full supported spelling or split the operation for review; this may also reject
new Git options until the policy and its fixtures are deliberately extended.
Supported short flags, clusters and operands are parsed before resolving refs:
for example `fetch -j 1 origin` and `fetch -j1 origin` resolve the same remote,
as do separate/attached push-option operands. Required operands cannot become
remote names; `--` ends option parsing. Unknown short flags and missing operands
are rejected. Commit message operands are data even when they resemble flags.
Signing checks apply to pull as well as direct commit/merge/rebase operations,
because a pull may create a merge commit.

Arbitrary scripts/programs, dynamically generated commands, Git invoked inside
uninspected tools, direct `.git` writes and interactive `write_stdin` input are
outside coverage. Repository state can change after inspection; native hooks can
be disabled, untrusted or unsupported in some tool paths. Keep repository workflow
and remote protection as independent controls. No remote protection is modified.

## Tests and live smoke procedure

Run `python3 -B -m unittest discover -s scripts/agents/tests -v` from the root.
Fixtures create isolated repositories with isolated HOME/config under temporary
directories. Both adapters are invoked as external processes. No production repo
commit, user signing/identity edit, service or network write is needed.

For each installed agent, use a disposable repository and an isolated user config
directory. Register the reviewed adapter with an **absolute path** in that test
project, inspect `/hooks`, and record version, source, trust and effective status.
Configure a fixture identity there, make an initial unsigned fixture commit, then
enable fixture `commit.gpgsign=true`. Ask the agent to attempt an empty main
commit; confirm the native denial and unchanged HEAD. Switch to a fixture feature
branch in a separate call; `git status` and an ordinary commit command must pass
the guard (signing may subsequently fail without a fixture signing key). Confirm
`--no-gpg-sign`, direct `HEAD:main`, and a main reset are denied. Use a disposable
bare remote if testing successful pushes. Never push this smoke test to production.
Repeat with a subdirectory/worktree and a quoted message. Verify opt-out then
reactivation through native `/hooks`, and remove only the disposable test data.
Record guard decisions separately from Git/signing success. No live smoke result
is claimed by this change.
