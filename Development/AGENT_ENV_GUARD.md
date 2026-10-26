# Environment edit guard (Linux)

The shared `scripts/agents/env_guard.py` implements #637. Thin Claude and Codex
adapters use it before native edits. Python >=3.11 and Git are required; no packages,
Git hooks, global configuration, credentials or services are installed.

## Classification and coverage

Case-sensitive basenames `.env` and `.env.*` are protected at any directory depth.
Only names ending in `.example`, `.sample` or `.template` are safe templates:
`.env.example` and `.env.production.template` pass; `.env.example.local` does not.
Templates must contain placeholders, never real credentials. Naming is a convention,
not content inspection. Other names such as `appsettings.json`, `secrets.json`,
`my.env` and `.envrc` are outside this classification.

Both the requested basename and the final symlink-resolved basename are checked.
A template symlink pointing at a real `.env` is protected; a requested `.env`
pointing at another filename is also protected. Protected edits through symlinks
(including directory symlinks) are always denied, even with an exception: use a
direct path to the intended file. A protected `.env` symlink to a differently named
file needs manual user handling; a grant cannot bless that alias. A Codex patch
mixing protected edits with **any** symlink operation is also denied; split it into
separate patches and use direct paths for the protected edits. This avoids treating
unlinking/moving an alias as deleting its referent when classifying later writes.
Standalone nonprotected symlink edits continue through normal runtime permissions.
Paths use the event's `cwd`, never
the adapter process directory. Absolute paths, relative paths, spaces, subdirectories
and independent Git worktrees are supported. A protected path outside the event's
checkout is denied and cannot receive a grant from that checkout.

Claude `PreToolUse` matches `Write|Edit`, reads `tool_input.file_path`, and validates
the native content/old-string/new-string fields without printing them. Write is
`create` when its destination does not exist, otherwise `update`; Edit is `update`.
Claude has no native delete/move operation in this adapter's coverage.

Codex `PreToolUse` matches `apply_patch` and reads `tool_input.command` (canonical
tool name remains `apply_patch`, including when matched through Write/Edit aliases).
The parser accepts native `*** Begin Patch` / `*** End Patch` framing and Add,
Update, Delete and Update-with-Move headers. An Update's first hunk can omit `@@`;
later hunks use `@@` or `@@ <skip-ahead context>`. Moves require update content.
It checks every affected file and both move endpoints. Add uses `create` only for
a missing destination; Add overwriting an existing file requires `update`, like
Claude Write. Update, Delete and move endpoints use `update`, `delete`, `move-from`
and `move-to`, respectively. Earlier operations in the same patch affect whether
a later Add creates or overwrites. It does not read target file contents or apply
the patch.

The parser accepts at most 2 MiB of UTF-8 JSON input, 256 affected endpoints, and
4096 characters per path. Wrapped shell/heredoc patches, structured Responses API
patch objects, unknown patch records, control characters in paths and malformed
native inputs are rejected. Malformed native edits fail closed, including when an
exception exists; unrelated tools abstain. A deny is native `PreToolUse`
`hookSpecificOutput.permissionDecision: "deny"` JSON. Success emits nothing and
does not override the runtime's other permissions or other hook decisions.

This is a native edit safeguard, not a shell/filesystem security boundary. Shell
redirections, scripts, other tools/MCP servers, external editors and racing filesystem
changes are outside coverage. Runtime or Python startup failure, timeout, disabled
hooks and untrusted definitions can prevent enforcement. No claim of runtime
activation follows from script fixtures passing.

Contracts checked 2026-10-25: [Claude hooks reference](https://code.claude.com/docs/en/hooks)
and [official OpenAI hooks documentation](https://learn.chatgpt.com/docs/hooks).
Use the Codex native command patch contract, not the separate Responses API
structured apply-patch contract.

## Explicit temporary exception

Only the user, or an agent acting on the user's **explicit conversational instruction
authorizing the particular file and operation**, may invoke the helper. Assigned
implementation work, a blocked edit, passage of time, and the `--authorized` flag
alone do not grant that permission. Obtain the instruction first. This helper records
that decision; it cannot authenticate a conversation or prevent a process with local
write access from changing the safeguard or its state. Never supply secret values,
diffs, or authorization transcripts as arguments or records.

From the intended checkout (paths are relative to the invocation directory):

```bash
# ONLY after explicit user instruction permitting this file's update:
python3 scripts/agents/env_guard.py --file '.env.local' --operation update --ttl 300 --authorized
```

Each grant permits one matching **native tool invocation**, for the canonical file
and exact operation. Grant requests through symlink paths are rejected; specify the
direct path. Default lifetime is 300 seconds, allowed TTL is 1–600 seconds;
time only expires permission. There is no session-wide bypass. A protected move
needs `move-from` for its source and `move-to` for its destination; grant each
protected endpoint explicitly. An update grant cannot authorize a move, create or
delete. A multi-file patch needs all required grants or the entire invocation is
denied without consuming any. Duplicate updates to the same file in one patch are
one invocation; a grant is not bound to particular content. Inspect the intended
patch before granting it. A repeated grant replaces that file/operation's deadline.

Grants live in ignored `.agent-local/env-guard/exceptions.json` in the actual
checkout, independent in each worktree. Records contain only canonical path,
operation and expiration. State files are created mode 0600 inside directories
created mode 0700. Linux `flock` and atomic replacement serialize grant creation
and consumption, so parallel calls cannot reuse one grant. Symlinked state
directories/files and malformed records are rejected. Expired grants are ignored
and removed on a successful grant/consumption; no background cleanup runs.

All matching grants are consumed **before** the hook abstains, even if another
hook, native permission prompt or the actual tool subsequently rejects/fails the
edit. Request fresh explicit authorization when retrying needs another grant.
The next invocation may consume a grant, including another agent using that
checkout. Grants are not tied to session/tool-use IDs. Revoke outstanding grants
by deleting only this checkout's `exceptions.json`; do not edit its JSON by hand.
Target content is never loaded by the guard, and neither values, patch lines nor
exception paths are printed in diagnostics. Native tool transcripts have their
own disclosure rules; use harmless placeholders in smoke tests.

## Activation, migration and opt-out

Review the tracked adapters, shared implementation and registrations first:
`.claude/settings.json` replaces the old Write/Edit `protect-files.sh` entry and
the obsolete tracked script is removed. `.codex/config.toml` adds the native
`apply_patch` entry alongside the existing Git guard. Per-edit and Stop hooks are removed by #638/#639. No permission allow-list is
added or widened.

Follow [native activation and opt-out](AGENT_SETUP.md#native-activation-and-opt-out)
in each intended checkout. Inspect `/hooks`, review/trust the project definitions
through the runtime's native UI, and reopen the session when necessary. Changed
Codex definitions require renewed review. Do not edit global trust databases or
automatically accept trust. Disable/reactivate the project guard through native
`/hooks` controls or the documented whole-feature opt-out, then repeat a smoke test.

The inspected obsolete local candidate is archived outside discoverable hook
paths by #641. See [migration boundaries](AGENT_SETUP.md#migration-boundaries)
for older checkouts: preserve unrelated personal entries and remove only obsolete
project registrations after inspection. Run `python3 scripts/agents/setup.py diagnose`
and inspect `/hooks` for duplicates; diagnosis cannot prove effective activation.
Never leave both old and new environment guards active and assume exceptions
override a deny. [Validation evidence](AGENT_ACTIVATION_VALIDATION.md) distinguishes
script fixtures from live native smoke tests.

## Automated checks and live smoke procedure

```bash
python3 -B -m unittest discover -s scripts/agents/tests -p 'test_env_guard.py' -v
```

Fixtures invoke both adapters in disposable repositories, check native denial and
abstention, and exercise templates, actual files, symlinks, all Codex operations,
multiple files, malformed input, one-use grants, expiration, concurrency, spaces,
subdirectories, alternate checkouts and worktrees. They do not activate native hooks.

For **each** agent, use a disposable clone with no actual credentials, and record
runtime version, checkout/path, effective registrations/trust and observed outcomes
in #641. Leave live evidence **unverified** until performed:

1. Inspect `/hooks` and confirm exactly one active environment guard. Ask for a
   native edit creating `.env.example` with `SMOKE_PLACEHOLDER=example`; it should
   succeed without an exception. Create/update `.env.smoke` with the same harmless
   placeholder using native Write/Edit (Claude) or apply_patch (Codex): it should deny.
2. The user explicitly authorizes creating `.env.smoke` once. Record a `create`
   grant with the helper and retry that native operation: it should proceed through
   normal runtime permissions. A subsequent update should deny. Explicitly authorize
   an `update` grant to exercise Claude Edit/Write or Codex Update; verify the next
   update denies again. Attempt a different file/operation before using the grant:
   it should deny without consuming the matching grant.
3. For Codex, try a mixed safe/protected multi-file patch, Delete, and a move into
   and out of `.env.smoke`; each should deny until all protected endpoints have
   their exact authorized grants. Then verify consumption. Claude deletion/move
   via Bash is outside this guard's coverage.
4. Authorize a harmless fixture grant with `--ttl 1`, wait past expiry, and confirm
   denial. Repeat native-path checks from a subdirectory and an independent worktree
   with a space in its path; verify a grant in one checkout cannot authorize another.
5. Confirm malformed protected adapter fixtures deny, and diagnostics contain no
   sentinel payload text. Disable/reactivate via native controls, review any changed
   trust definition, and repeat the template/default-deny smoke. Clean up only the
   disposable clone/fixtures through normal user-approved operations.
