# Agent activation and migration validation

This is the evidence ledger for [#641](https://github.com/paulmfischer/MediaSet/issues/641)
and the final reconciliation of [#630](https://github.com/paulmfischer/MediaSet/issues/630).
Required native observations are recorded below, alongside the discovery limits
and failed runs they uncovered. Script fixtures, version probes and file
registrations alone do not establish native activation.

## Local migration

On 2026-10-26, the inspected local candidate contained two obsolete PreToolUse
registrations (`block-main-commits.sh` and `protect-files.sh`), no unrelated JSON
fields, and the seven historical scripts in [the inventory](AGENT_HOOK_INVENTORY.md).
The previous slices had already removed formatting/test registrations. All eight
remaining files were copied to ignored `.agent-local/migration/641-legacy-codex/`
with a SHA-256 manifest, checked against that archive, then removed from native
discovery paths. The archive is local evidence, never a tracked installation.
Personal settings were not edited. Both tracked definitions contain exactly two
PreToolUse guards; no PostToolUse, Stop or SessionStart behavior remains there.

Diagnosis detects exact cross-file duplicates, recognizable competing old/new
Git/environment guards, and retired project-style registrations even without a
replacement. Behavioral fixtures preserve personal settings byte for byte and
suppress sentinel values. Semantic duplicates and managed/plugin/session sources
still require native `/hooks` inspection. Removing files does not prove that an
existing session reloaded its configuration.

## Reproduction and evidence boundaries

Start from the reviewed commit in a new clone. Substitute your own empty temporary
parent directory and reviewed source; never copy secrets or dependency directories:

```bash
git clone --no-local --branch main <reviewed-source> '/tmp/agent validation/fresh checkout'
git -C '/tmp/agent validation/fresh checkout' switch -c chore/setup-validation
git -C '/tmp/agent validation/fresh checkout' worktree add -b chore/setup-independent '/tmp/agent validation/independent worktree'
cd '/tmp/agent validation/fresh checkout/MediaSet.Api'
bash '../scripts/agents/setup.sh' diagnose
bash '../scripts/agents/setup.sh' bootstrap
bash '../scripts/agents/setup.sh' bootstrap
bash '../scripts/agents/setup.sh' diagnose
# Repeat from the independent worktree, using its own script and dependencies.
```

The tested baseline was main `b05f1bc` (PR #648), Linux, Codex CLI 0.163.0 and
Claude Code 2.1.309. Real setup used a fresh clone whose path contains spaces,
invoked from `MediaSet.Api`, and an independent Git worktree. The initial
restricted-runner attempts returned 1 at `dotnet restore` without SDK diagnostics;
an approved unrestricted retry completed .NET restore and npm ci. This runner
restriction is distinct from setup behavior. No service commands, credential
configuration, secret copying or dependency links were used. Package download
caches and installed dependencies belong to each checkout.

Real explicit-command demonstrations use placeholder files only. A malformed
untracked TypeScript file makes the installed formatters fail visibly; correcting
it and selecting that file explicitly succeeds while an unselected file is
preserved. An untracked Markdown file with trailing whitespace makes verification
fail with a full diagnostic log; correcting it succeeds. The same failure/success
sequence in the independent worktree produces independent log directories.
These lightweight verification demonstrations are separate from the automated
fixtures for application check selection and tool failures.

Recorded local results (2026-10-26) are in ignored
`.agent-local/activation-validation/641/`: `native-setup-results.json` records two
successful bootstraps and diagnosis per checkout; `explicit-results.json` records
formatting failure/success and both verification failure/success pairs;
`ownership.json` records distinct physical dependency directories, checkout-local
NuGet package paths and independent verification archives. Only the tracked
`.env.example` template exists at either validation root. `completion-verify.log` records the earlier 110-test baseline;
`completion-with-launcher.log` records the final selected agent/whitespace checks: 112 contract tests passed,
with backend/frontend checks skipped because this branch changes agent setup and
documentation. Full command output is preserved in the corresponding local logs.
These ignored files are local evidence and are not uploaded or shipped in Git.

Run the assembled automated contract suite and required completion selector:

```bash
python3 -B -m unittest discover -s scripts/agents/tests -v
python3 -B scripts/agents/verify.py
```

Coverage includes branch/committed/pushed, staged, unstaged and untracked scope;
explicit base/full and unavailable-base fallback; formatter selection and visible
failures; verification exit status, full logs and retention isolation; real Git
worktrees and native-payload adapter fixtures; narrow exceptions, expiry and
malformed inputs; repeated bootstrap, missing tools and linked dependency refusal.
Fake package-manager/formatter fixtures are labeled in test module docstrings;
they do not demonstrate real installation or native activation.

## User-controlled native smoke tests

On 2026-10-26, the user reported interactive project trust and `/hooks` review for
both disposable clones and both independent worktrees. That report is recorded
separately from observed enforcement below. Exact one-use exception operations
were explicitly authorized for all four roots, at most 600 seconds, placeholders
only. No hook trust bypass, permission-mode override or persistent allow-list was
used. Claude print mode's ordinary permission denials were preserved; interactive
Claude used its already configured auto mode and exercised actual native tools.

Prepared clones are `/tmp/mediaset-agent-smoke-641/claude` and
`/tmp/mediaset-agent-smoke-641/codex`, with independent worktrees named
`claude worktree` and `codex worktree` beside them. These local temporary paths
are examples, not installation requirements. Observed runtime versions were
Codex CLI 0.163.0 for base guard/environment tests, 0.164.0 for later worktree
and trust lifecycle tests, and Claude Code 2.1.310 (the earlier baseline probe was
2.1.309). Codex session metadata records its actual version; no runtime update
command was performed by this validation.
JSONL native tool events, captured interactive terminal output, prompts and
filesystem observations are in ignored `.agent-local/activation-validation/641/native/`.

Observed base-checkout results:

- Both runtimes denied native main commit/reset/direct-push attempts and signing
  bypass. Independent HEAD observations remained b05f1bc. Ordinary feature switch
  and status succeeded; commit dry-run reached Git and reported no staged changes;
  push dry-run against a disposable bare origin succeeded. No actual commit or
  push was created, and no signing success is claimed.
- Both runtimes passed the ten-step template/default-deny/explicit-create/update/
  wrong-file/one-use/one-second-expiry native matrix. Actual `.env.smoke` was
  `SMOKE_PLACEHOLDER=two`, `.env.other` absent before opt-out. Claude's initial
  print-mode attempts were incomplete because normal permissions denied allowed
  tools; interactive retesting succeeded without changing its configured mode.
- Codex additionally passed native mixed-patch atomic denial then granted success;
  source-only protected move denial preserved the grant, adding the destination
  grant allowed the move; move to a safe path and protected delete passed with
  their exact grants. Ungranted recreation denied. Native subdirectory tests passed.
- Codex one-run `features.hooks=false` allowed native `.env.other` creation. A fresh
  invocation without the override denied native `.env.smoke` creation and left
  it absent, demonstrating reactivation.

Claude direct launch from the `smoke` subdirectory **failed** enforcement: no-grant
Write/Edit succeeded. Native `/hooks` showed only one user SessionStart hook and
zero project guards. This matches the [official primary-working-directory settings
contract](https://code.claude.com/docs/en/settings#where-claude-code-keeps-the-local-file-in-a-git-repository).
The repository now provides `bash scripts/agents/claude.sh`, callable from any
directory, to launch from its own checkout root without changing settings/trust.
Retesting the launcher **from the subdirectory**, then native Bash `cd smoke`,
passed: default native Edit denied, exact granted update succeeded, reuse denied;
actual contents remained `SMOKE_PLACEHOLDER=launcher`. The failed direct-launch
transcript remains evidence; the wrapper does not change how direct CLI invocation
or native `/cd` discovers project settings. See onboarding for the required usage.

Claude independently trusted worktree passed the ten-step native environment
matrix with actual Write/Edit and checkout-local grants. Its documented whole-feature
opt-out allowed native `.env.other` creation; a fresh normal session then denied
`.env.smoke` update and preserved `two`.

The initial Codex worktree run did not enforce environment guards. Read-only native
`/hooks` inspection in that worktree showed its project source was the **primary
checkout** `.codex/config.toml`, including the changed timeout 11, rather than the
worktree's unchanged timeout-10 file. Thus the primary definition change affected
the concurrent worktree test. Preserve this failed run as discovery/trust evidence;
it is not a passing guard test. After user review of that effective source, a fresh
worktree native retest passed default denial, authorized update, one-use denial,
one-second expiry denial and safe template editing.

For both runtimes, a primary `.env.smoke` UPDATE grant was recorded before the
worktree retest. Each worktree denied the same-basename edit until its own grant
was issued. SHA-256 observations confirmed both primary grant files remained
byte-for-byte unchanged through all worktree activity. Final native primary
consumption then succeeded in both runtimes; repeated native edits denied. Both
primary files ended as `SMOKE_PLACEHOLDER=isolation`, both worktree files as
`SMOKE_PLACEHOLDER=worktree`. The local evidence index is
`.agent-local/activation-validation/641/native/index.json`; it maps 12 Claude
transcripts and 12 Codex sessions to their IDs and runtime versions. All four
fixture exception-state files were revoked afterward; disposable checkouts,
reviewed definitions and logs remain available for inspection.

Changed-definition exercise: after active base Codex tests completed, only its
disposable environment-hook timeout changed from 10 to 11. A subsequent native
create applied before renewed review, with no startup refusal or denial. This
shows changed-definition enforcement was absent; the runtime output alone did
not prove the specific trust state. The user then reported the definition was **marked for review**, reviewed, trusted
and enabled through `/hooks`. A fresh native session using unchanged timeout 11
denied the no-grant update and independently verified the value stayed
`changed-definition`. Human-observed review status and runtime-observed denial
are separate evidence. The disposable timeout is left at 11;
no production or tracked definition changed.

Before dependent tests, record the runtime version, checkout, branch, effective
sources, trust decisions and two active guards. Preserve native transcript/log
references without secret values. A trust action in one checkout or runtime does
not establish activation in another. For each runtime record:

| Required observation | Current evidence |
| --- | --- |
| Main commit/reset/push and signing bypass denied; HEAD unchanged | Both base runtimes observed native denial; HEAD unchanged |
| Ordinary feature operations allowed by guard, distinct from Git/signing success | Both native feature switch/status and dry-run commit/push reached Git |
| Safe template native edit succeeds; real environment native edit denies | Both base runtimes passed native tools/filesystem checks |
| Exact one-use exception permits only its file/operation; mismatch/reuse/expiry deny | Both base native ten-step matrices passed |
| Codex multi-file patch, delete and both move endpoints | Native mixed/move/delete matrix passed |
| Subdirectory/worktree paths, grant isolation | Codex subdir, Claude launcher+native cd, both stable reviewed worktree retests and isolated grant-state observations passed |
| Opt-out, reactivation and changed-definition review where applicable | Both whole-feature opt-out/reactivation passed; Codex changed-definition review and native recovery recorded |
| Effective sources have no obsolete or duplicate project behavior | Project files reconciled; user reports `/hooks` review; native discovery limits above apply |

Follow [Git smoke steps](AGENT_GIT_GUARD.md#tests-and-live-smoke-procedure),
[environment smoke steps](AGENT_ENV_GUARD.md#automated-checks-and-live-smoke-procedure)
and [native opt-out/reactivation](AGENT_SETUP.md#native-activation-and-opt-out).
Use disposable repositories/bare remotes only. A successful guard abstention
must be reported separately from an actual signed commit or successful push.
Never bypass configured signing merely to produce smoke evidence.

Exception helpers require explicit conversational authorization for each intended
file and operation. Harmless fixed smoke targets are `.env.smoke`,
`.env.smoke-moved`, `.env.other`, and `smoke/.env.smoke` inside each disposable
checkout. Creating a workspace or giving implementation authorization is not
permission to grant an exception. Record exact operations approved, use placeholder
content, and create one-use grants only after approval (TTL at most 600 seconds).
Changed definitions must undergo native review again; do not mark that step tested
merely because an unchanged definition remains trusted. Opt-out runs must use
harmless disposable files and restore/review activation before enforcement tests.

## Parent specification reconciliation

| #630 acceptance area | Final implementation and evidence |
| --- | --- |
| Shared policy, entry points and workflow | #635; shared policy is maintained in one file |
| Complete original inventory and disposition | Inventory plus #636–#639 replacements and local archival above |
| Fresh Linux paths, subdirectories and independent worktrees | Real setup evidence plus contract fixtures; see local logs |
| Main/signing and native environment protection | #636/#637 fixtures and both native base matrices; discovery limits recorded above |
| Ordinary feature operations and authorized history rewrites | No conversational hook gate; policy requires authorization and force-with-lease |
| Remove per-edit rewrites, command rewriting and Stop tests | Final tracked registration contract; candidate archived; user/native source inspection and both active native matrices |
| Explicit scoped formatting with visible failures | #638 fixtures and real installed-tool demonstration |
| Branch/working-tree verification, logs, exit and reuse | #639 fixtures and real documentation failure/success demonstrations; rerun after relevant edits |
| Aligned full CI including types | #640 PR #648, commit b05f1bc, run 40317044790: all nine checks passed |
| Repeatable setup and truthful activation diagnostics | Real bootstrap plus fixtures; no trust inferred from files |
| Preserve personal settings and avoid obsolete duplicates | Local archive and preservation fixtures; native effective-source inspection recorded, including primary/worktree source sharing |
| Automated and both live runtime tests | Automated evidence recorded separately; both base native matrices and stable reviewed worktree retests passed; lifecycle/discovery limits recorded above |
| Requirements versus optional tooling/private operations | Onboarding, shared policy, development guide and guard/format/verification guides |

CI evidence: [PR #648](https://github.com/paulmfischer/MediaSet/pull/648) and
[run 40317044790](https://github.com/paulmfischer/MediaSet/actions/runs/40317044790).
This is baseline CI evidence for #640, not a claim that #641's future commit has
passed CI. Read-only signing-config query behavior is separately tracked in
[#647](https://github.com/paulmfischer/MediaSet/issues/647) and is not changed here.

The required live observations above are complete for the recorded runtimes and
supported launch procedure. Maintainers can reconcile #641 and #630 checkboxes
against this ledger; there is no claim that a future commit has passed CI.
Unexercised extras remain **unverified**: a successful signed commit or actual
non-dry-run push (guard abstention was tested separately), individual-hook toggle
UI (whole-feature opt-out was tested), native malformed/symlink payloads (fixtures
cover these), and other runtime versions/platforms. Direct Claude subdirectory
launch omits project guards; use the documented launcher. Codex linked worktrees
can use their primary checkout's project definition, so review the effective
source and changes through native `/hooks`. These limits do not invalidate the
observed active native cases or imply universal enforcement.
