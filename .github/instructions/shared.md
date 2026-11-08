# MediaSet agent guidelines

MediaSet manages personal media collections: a .NET 10 minimal API with MongoDB and a
Remix 3 / TypeScript / Tailwind CSS 4 frontend. Tests are NUnit (API) and `remix test` (UI). For the directory map,
route/data patterns, integrations and image storage read [architecture](../../Development/ARCHITECTURE.md).
Before editing TypeScript or TSX read [frontend conventions](frontend.instructions.md).
C# style is enforced by `.editorconfig`.

**Line endings are LF everywhere** (`.gitattributes`: `* text=auto eol=lf`; `.editorconfig`:
`end_of_line = lf`). Never commit CRLF; set your editor to LF. `dotnet format` keeps LF.

## Git

- Never commit to `main`. Branch from the latest `main` (or the branch you are told to
  base on) with a `feat/`, `fix/`, `docs/`, `chore/` or `refactor/` prefix. Changes
  reach `main` through a pull request.
- Update `main` with fast-forward-only; fetch into remote-tracking refs. Never rewrite
  published history without explicit authorization (then use `--force-with-lease`).
- Preserve unrelated changes. Destructive resets need explicit authorization.

## Commits

- Conventional Commits: `type(scope): description`, subject at most 72 characters.
  Types: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `style`, `perf`, `ci`, `build`.
- Issue references: `closes #N` when one commit resolves the issue, `refs #N` on each
  commit when the work spans several.
- No AI or agent attribution and no session trailers in commits or PR descriptions.
- Commit as the user's configured Git identity with their signing key. Never bypass
  signing (`--no-gpg-sign`, `--no-verify`); if signing fails, fix it or ask.

## Workflow

For work on a tracked issue, the main session is the coordinator: it does not implement or
review itself. Subagents never spawn subagents and follow Git and Commits. Small untracked
changes (a doc fix, a typo) are done directly, still on a branch with a PR.

1. **Read** the issue in full: body, acceptance criteria (AC), comments, linked items. Ask
   only about genuinely unresolved scope. For a feature or a change across several areas,
   show the user a short plan and wait for approval before step 2.
2. **Implementer subagent** (fresh and self-contained: repo path, issue, base branch, these
   guidelines). It branches, implements within scope, updates tests, runs the full checks for
   the affected area ([AGENT_CHECKS.md](../../AGENT_CHECKS.md)) and inspects its diff. It
   verifies each AC with evidence (a test, a run, a screenshot for UI), then commits, pushes
   and opens the PR (`closes #N`, or `refs #N` if any AC can only be verified after release).
   Once the PR is open it ticks the verified AC in the issue and comments on any left
   unticked. It waits for CI, fixes failures its change caused, and stops and reports
   unrelated ones. It reports the PR link, AC status, CI status and a `Follow-ups` list (or
   `none`).
3. **Reviewer subagent** (fresh and blind, given only the PR link). It reviews from the diff
   and posts a comment review starting "🤖 Automated review (Claude):", marking each finding
   blocking or nit and ending with `NO BLOCKING FINDINGS` or `BLOCKING FINDINGS` for the head
   commit it reviewed. It reports only; it never edits, commits or pushes.
4. **Fix round.** Resume the implementer to fix every blocking finding and every nit it can,
   in as few commits as possible, and reply on the review. If blocking findings were fixed,
   review again with a fresh reviewer pointed at the previous review and the new commits;
   repeat up to two rounds in total. Nit-only fixes need no re-review.
5. **Hand back.** Report the PR link, AC status, CI status, the reviewed head commit, what was
   fixed, and every unfixed nit and follow-up, each with a recommendation (fix, file an issue,
   or reject). Ask the user what to do with each. Nothing is left only in chat. The user
   merges.

An implementation request authorizes branching, editing, testing, commits, pushes and the PR;
do not ask again. Planning-only requests authorize no implementation.

PR titles use `type(scope): description`. The body describes the resulting behavior and
how it was validated, and carries `closes #N` or `refs #N`.

## Safety

- Use only infrastructure within the task's scope. Shared containers, volumes, images,
  compose files, proxies and host configuration need explicit authorization. Do not
  restart or update existing services, run broad cleanup, or expose new public
  interfaces.
- Never create, edit, delete or move real `.env` files or other secret files without
  explicit authorization; example and template files may be updated with placeholders.
  Never print or commit secrets. Set local secrets with
  `dotnet user-secrets set "Section:Key" "value" --project MediaSet.Api`.
- Running the app locally is covered in
  [Development/DEVELOPMENT.md](../../Development/DEVELOPMENT.md); starting it is not part
  of setup and happens only when the task needs it.

## Tests

- Arrange / Act / Assert, one behavior per test, cover edge cases and errors.
- API tests are named `MethodName_Scenario_ExpectedResult`.
