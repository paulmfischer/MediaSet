# Checks and formatting

The commands CI runs. They need no secrets, database, or running API/UI. Run the
area you changed (both for shared configuration) before reporting completion, and
rerun after later edits.

## API (repository root)

```bash
dotnet restore MediaSet.sln
dotnet build MediaSet.sln --no-restore
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj --no-restore
```

## UI (`MediaSet.Remix`)

```bash
npm ci
npm run lint
npm run lint:format
npm run typecheck
npm run build
npm test
```

`npm test` runs every server test with `remix test` (router, controller and module suites).
Browser and end-to-end tests run separately in Playwright Chromium:

```bash
npx playwright install chromium   # once, and after a Playwright version change
npm run test:e2e
```

Add `--with-deps` to the install once if Chromium fails to start on Linux.

## Whitespace (repository root)

```bash
git diff --check
git diff --cached --check
git diff --check origin/main...HEAD --
```

Line endings are LF everywhere (`.gitattributes`, `.editorconfig`). `git diff --check`
flags any added CRLF line, so do not commit CRLF. Confirm with `git ls-files --eol`
(no `i/crlf` or `i/mixed` on text files).

## CI

Every eligible PR (base `main`, `feat/*`, `feature/*`, `fix/*`, `chore/*`) runs
three independent jobs: **API**, **UI** and **UI Browser** (`npm run test:e2e`).
They use the same commands as above and never format in place.

## Focused tests

```bash
# NUnit class or method (append .MethodName to narrow)
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj --filter "FullyQualifiedName~TestClassName"
# From MediaSet.Remix: server tests whose file name contains a fragment
NODE_ENV=test npx remix test --type server router
# From MediaSet.Remix: one test file (any server test, e.g. a data module)
NODE_ENV=test npx remix test --type server app/api/entity-data.test.ts
# From MediaSet.Remix: browser or end-to-end tests by file name fragment
NODE_ENV=test npx remix test --type browser --type e2e router
```

Focused tests do not replace the full checks.

## Formatting

Formatting rewrites files. Run it only on the files you changed and review the
diff; do not format the whole repository.

```bash
# From MediaSet.Remix
npm exec -- eslint --fix app/path/to/changed-file.tsx
npm exec -- prettier --write app/path/to/changed-file.tsx
# From the repository root
dotnet format MediaSet.sln --no-restore --include MediaSet.Api/path/to/changed-file.cs
```

Use Prettier alone for application JSON. C# style comes from `.editorconfig`
(LF line endings; `dotnet format` keeps them). Do not
suppress diagnostics or skip signing to get a check or commit through.
