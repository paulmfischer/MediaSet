# MediaSet agent guidelines

MediaSet manages personal media collections: a .NET 10 minimal API with MongoDB and a
Remix 3 / TypeScript / Tailwind frontend. Tests are NUnit (API) and `remix test` (UI,
plus a few Vitest module suites until #686). For the directory map, route/data patterns,
integrations and image storage read [architecture](../../Development/ARCHITECTURE.md).
Before editing TypeScript or TSX read [frontend conventions](frontend.instructions.md).
C# style is enforced by `.editorconfig`.

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

1. Read the instructions above and inspect repository state before editing.
2. Branch, then make the change within the authorized scope, following existing feature,
   service and route patterns. Update tests for behavior changes; skip tests that only
   mirror low-impact documentation or configuration edits.
3. Run the relevant full checks in [AGENT_CHECKS.md](../../AGENT_CHECKS.md) and inspect
   the final diff.
4. Commit, push the branch and open a PR. An implementation request authorizes
   branching, editing, formatting, testing, commits, pushes and the PR; do not ask
   again. Ask only about genuinely unresolved scope. Planning-only requests authorize
   no implementation.
5. Report what changed, the commands run and their results, checks not run, and any
   failures.

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
