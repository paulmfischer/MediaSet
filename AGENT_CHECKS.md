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

`npm test` runs the router (server) tests plus the retained Vitest module suites.
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

## CI

Every eligible PR (base `main`, `feat/*`, `feature/*`, `fix/*`, `chore/*`) runs
three independent jobs: **API**, **UI** and **UI Browser** (`npm run test:e2e`).
They use the same commands as above and never format in place.

## Focused tests

```bash
# NUnit class or method (append .MethodName to narrow)
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj --filter "FullyQualifiedName~TestClassName"
# From MediaSet.Remix: router tests by partial file name
NODE_ENV=test npx remix test router
# From MediaSet.Remix: one retained Vitest file
npm run test:unit -- app/path/to/test-file.test.ts
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

Use Prettier alone for application JSON. C# style comes from `.editorconfig`. Do not
suppress diagnostics or skip signing to get a check or commit through.
