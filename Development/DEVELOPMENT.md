# Local development

The API and frontend run on the host. Podman runs only MongoDB. Application
dependencies use the normal host NuGet cache and frontend node_modules directory;
startup does not rebuild images, repair labels, clear caches, or rewrite configuration.
Developers and agents use the same commands below; no agent bootstrap or Python
installation is required. Opening a project or agent session performs no setup.

## Prerequisites

- .NET 10 SDK compatible with the repository's global.json.
- Node.js 24.22.1 LTS and its bundled npm (exact version in [`.nvmrc`](../.nvmrc)).
  With nvm already installed, run `nvm install` followed by `nvm use` from the
  checkout root; otherwise use the official Node installer for that exact version.
  Verify `node --version` and `npm --version` before `npm ci`. The application
  supports serviced Node 24 patches >=24.22.1; CI and both Docker stages pin
  24.22.1.
- Podman and podman-compose to run MongoDB locally; they are not needed for the
  build, lint, type, and unit-test checks. On Fedora: `dnf install podman podman-compose`.
- Git. For VS Code API debugging, install the recommended C# extension.

The commands below run from the repository root unless specified otherwise.
Docker Compose can replace `podman-compose` in terminal commands; the checked-in
VS Code database tasks use `podman-compose`.

## First setup

1. Copy `.env.example` to `.env`. Set distinct `MONGO_ROOT_PASSWORD` and
   `MONGO_APP_PASSWORD` values. Generate URL-safe values with `openssl rand -hex 24`.
   Do not commit credentials.
2. Configure the API with the same application password:

   ```bash
   dotnet user-secrets set "MediaSetDatabaseSettings:ConnectionString" 'mongodb://mediaset:YOUR_APP_PASSWORD@127.0.0.1:27017/MediaSet?authSource=MediaSet' --project MediaSet.Api
   ```

   Replace `YOUR_APP_PASSWORD` with `MONGO_APP_PASSWORD` from `.env`. If using
   characters outside URL-safe hex, URL-encode the password in the connection string.
   The API does not read the root `.env`; Compose reads it only for database setup.
3. Install dependencies:

   ```bash
   dotnet restore MediaSet.sln
   npm ci --prefix MediaSet.Remix
   ```

4. Start MongoDB and check its authenticated health:

   ```bash
   podman-compose -f docker-compose.dev.yml up -d
   podman-compose -f docker-compose.dev.yml ps
   ```

   Wait for MongoDB to report healthy on its first initialization. The health
   check authenticates as the application user and lists database collections.
   Inspect initialization failures with
   `podman-compose -f docker-compose.dev.yml logs mongodb`.

MongoDB data lives in ignored `data/dev-mongodb`. API image storage resolves to
`data/images` in the repository. MongoDB listens only on 127.0.0.1:27017.
Keep SELinux enforcing; the two MongoDB bind mounts use private container labels.

For checks alone, install dependencies as above and skip database/secret setup.
NuGet uses its normal user cache (`~/.nuget/packages` on Linux); npm uses its
normal cache (`npm config get cache`). Each checkout/worktree owns its own
`node_modules`, `bin`, and `obj` directories; do not link those between worktrees.
Rerun restore/install after dependency or lockfile changes. Setup does not need
global npm tools or personal agent integrations.

## Daily terminal workflow

Start the database if needed:

```bash
podman-compose -f docker-compose.dev.yml up -d
```

In one terminal, start the API with watch/reload:

```bash
dotnet watch --project MediaSet.Api run
```

In a second terminal, start the UI:

```bash
cd MediaSet.Remix
npm run dev
```

- UI: http://localhost:3000 (also accessible at http://YOUR_LAN_IP:3000).
- API and Swagger: http://127.0.0.1:5000 and http://127.0.0.1:5000/swagger.
- API port and Development environment come from the API launch profile.
- `npm run dev` runs the Remix 3 server (`server.ts`) with `node --watch`,
  plus the Tailwind CLI in watch mode writing `public/app.css`. Port 3000 is
  fixed; the server fails if it is already occupied.
- Server source changes restart the server; reload the browser to see them.
  See [Hot reload decision](#hot-reload-decision).
- Ordinary development requires no certificates or frontend environment file.

Ctrl+C stops each application. MongoDB remains running until explicitly stopped:

```bash
podman-compose -f docker-compose.dev.yml stop mongodb
```

Neither starting nor stopping the database removes its data.

## VS Code

Complete first setup before using Run and Debug (F5):

| Configuration | Behavior |
| --- | --- |
| Launch API | Starts MongoDB, builds the API, and attaches the .NET debugger on port 5000. |
| Launch UI | Starts the UI dev server in a JavaScript Debug Terminal on port 3000. |
| Launch API + UI | Starts both applications; MongoDB startup runs only through the API prerequisite. |
| Launch UI — Mobile HTTPS | Starts the HTTPS frontend after the optional certificate setup below. |
| Launch API + UI — Mobile HTTPS | Starts the API debugger and HTTPS frontend together. |

UI debug launches open the default browser when the UI server starts, including combined
and mobile HTTPS launches. UI-only
launches expect an already-running API for database-backed pages.
Stopping a compound debug session stops both application sessions and leaves MongoDB running.

Terminal → Run Task also offers **Start MongoDB**, **Stop MongoDB**, **Build API**,
**Watch API**, **Start UI**, and **Start API + UI**. Use the watch tasks for the
normal API hot-reload loop; use Launch API for breakpoint debugging. Stop watch
tasks with Terminal → Terminate Task (terminate both application tasks for the
combined task). Avoid running debug launches and watch tasks on the same ports
at the same time.

## Configuration

The native API uses appsettings.Development.json plus .NET User Secrets.
Optional metadata services and Seq settings use their normal configuration keys:

```bash
dotnet user-secrets set "TmdbConfiguration:BearerToken" "YOUR_TOKEN" --project MediaSet.Api
dotnet user-secrets set "IgdbConfiguration:ClientId" "YOUR_CLIENT_ID" --project MediaSet.Api
dotnet user-secrets set "IgdbConfiguration:ClientSecret" "YOUR_CLIENT_SECRET" --project MediaSet.Api
dotnet user-secrets set "ExternalLogging:Enabled" "true" --project MediaSet.Api
dotnet user-secrets set "ExternalLogging:SeqUrl" "http://YOUR_SEQ_HOST:5341" --project MediaSet.Api
```

See the integration guides in [Setup](../Setup/) for other providers.
Do not copy container hostnames such as `mongodb` or `api` into host configuration.

The frontend defaults to server-side API calls at http://127.0.0.1:5000.
Browser API/image requests use the relative `/dev-api` prefix. The development
server (never production) strips that prefix and forwards requests to the local API, so phones never call their own
localhost or load HTTP images from an HTTPS page.

Optional frontend overrides belong in `MediaSet.Remix/.env.local`; see
`MediaSet.Remix/.env.example`; `npm run dev` and `npm run dev:mobile` load it.
Restart the UI dev server after changing environment variables.
`apiUrl` controls the server-side API/proxy target; `clientApiUrl` can override
the browser URL (served by `/config.json`) when deliberately testing another API.
Leave browser overrides unset for normal local and mobile testing.
Production continues to use its existing explicit API URL configuration.

## Mobile HTTPS and barcode scanning

HTTP LAN access supports browsing/editing. Live camera scanning needs a trusted
HTTPS origin. Mobile HTTPS is an optional runtime mode using the same UI port;
stop the ordinary frontend before starting it.

1. Install [mkcert](https://github.com/FiloSottile/mkcert) on the host and create
   its local CA with `mkcert -install`. This is an explicit one-time trust setup,
   not an application startup action.
2. From the repository root, generate a certificate for localhost and the actual
   LAN IP or hostname you will use on the phone:

   ```bash
   mkdir -p data/certs
   mkcert -cert-file data/certs/dev.pem -key-file data/certs/dev-key.pem localhost 127.0.0.1 ::1 YOUR_LAN_IP
   ```

3. Add these entries to `MediaSet.Remix/.env.local`:

   ```dotenv
   DEV_HTTPS_CERT=../data/certs/dev.pem
   DEV_HTTPS_KEY=../data/certs/dev-key.pem
   ```

4. Locate the public `rootCA.pem` using `mkcert -CAROOT` and install/trust that
   certificate on your phone. Follow mkcert's mobile device instructions; on iOS,
   enable full trust under Certificate Trust Settings after installing the profile.
   Never transfer `rootCA-key.pem` to the phone or repository.
5. Start the API normally, then run `npm run dev:mobile` from MediaSet.Remix
   (or use the mobile VS Code launch). Visit `https://YOUR_LAN_IP:3000`, allow
   camera access, and open Scan Barcode on an add/edit form.

Only UI port 3000 needs LAN reachability; the API and MongoDB remain local.
If the host firewall blocks it, allow port 3000 on your intended LAN interface.
Certificate trust and firewall changes are manual host/device setup steps.

If the LAN IP changes or the certificate expires, rerun certificate generation
with the new address and restart mobile mode. Reinstall the public CA only if
you create a new CA. Missing certificate settings produce an explicit startup
error; ordinary `npm run dev` ignores them and stays on HTTP.

## Verification and troubleshooting

Check actual database connectivity, not just a listening port:

```bash
node --input-type=module -e 'const r = await fetch("http://127.0.0.1:5000/health"); const h = await r.json(); if (!r.ok || h.status !== "healthy" || h.database?.status !== "up") throw new Error("API or database unhealthy"); console.log("API and database healthy");'
curl --fail http://localhost:3000/
```

The API's health endpoint can return HTTP 200 with degraded database status.
After startup, load the dashboard, create/edit/reload an entity, and upload/view
its image. Restart applications and recreate only the MongoDB container to
verify newly created data survives:

```bash
podman-compose -f docker-compose.dev.yml up -d --force-recreate mongodb
```

- **Authentication failed:** compare the app password in .env and User Secrets.
  Initialization only runs on empty database storage; changing .env does not
  change existing users. For disposable data, stop MongoDB and rename
  `data/dev-mongodb` to an unused sibling path before starting it again.
  Use `podman unshare mv` if rootless MongoDB ownership prevents the rename.
  Keep `data/images` intact.
- **Connection refused:** check MongoDB health and API listening messages; retry
  after first initialization. Verify no stale environment variable overrides
  your launch profile or User Secrets.
- **Port occupied:** stop the old local stack or the other watch/debug session.
  The UI dev server deliberately does not select a different port.
- **Dependency errors:** rerun `dotnet restore` or `npm ci --prefix MediaSet.Remix`.
  Run dependency installation after lockfile changes. Do not clear all caches
  as a normal startup step.
- **Unstyled pages:** `public/app.css` is generated, not committed. `npm run dev`
  rebuilds it on start; otherwise run `npm run css` (or `npm run build`).
- **Mobile camera unavailable:** confirm trusted HTTPS, certificate address,
  browser camera permission, and LAN reachability. Certificate-warning bypasses
  are not a replacement for installing the CA.
- **SELinux:** only MongoDB/init-script mounts need container labels now.
  Investigate their labels and scoped permissions; do not disable enforcement.

## Checks

These are the same commands used by the PR workflow. They need no secrets,
database, running API/UI, or agent tools. Restore dependencies explicitly first.

From the repository root, check the API:

```bash
dotnet restore MediaSet.sln
dotnet build MediaSet.sln --no-restore
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj --no-restore
```

From `MediaSet.Remix`, check the UI:

```bash
cd MediaSet.Remix
npm ci
npm run lint
npm run lint:format
npm run typecheck
npm run build
npm test
```

`npm run build` compiles the Tailwind stylesheet to `public/app.css`; there is no
other build output because Node runs the TypeScript sources directly.
`npm test` runs the retained module unit tests (Vitest) and the router (server)
tests (`remix test --type server`). Browser and end-to-end tests run separately
in Playwright Chromium; install the browser build matching the locked Playwright
version once, then run them:

```bash
npx playwright install chromium
npm run test:e2e
```

`npm run test:e2e` rebuilds `public/app.css` first, then runs every
`*.test.browser.tsx` and `*.test.e2e.ts` file. Rerun the install step after a
Playwright version change in the lockfile. On Linux, add `--with-deps` once if
Chromium fails to start because of missing system libraries.

Check whitespace from the repository root:

```bash
git diff --check
git diff --cached --check
# Check all committed branch changes after fetching origin/main:
git diff --check origin/main...HEAD --
```

The PR workflow runs independent **API**, **UI** and **UI Browser** jobs for
every eligible PR (base branch `main`, `feat/*`, `feature/*`, `fix/*` or `chore/*`),
including documentation-only changes. All three run concurrently. The UI job runs
the UI checks above and never installs a browser. The UI Browser
job installs the Chromium headless shell for the locked Playwright version,
cached by that version, and runs `npm run test:e2e`. CI compares the
PR base/head commits for whitespace with full Git history, then uses the same
application commands above. Each check has its own step and failures fail the
job; other checks still run after dependency installation succeeds. No check
formats source in place. All job results must be reviewed before approving a PR.

For focused tests during development:

```bash
# Repository root: NUnit class or method (append .MethodName to narrow further)
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj --filter "FullyQualifiedName~TestClassName"
# From MediaSet.Remix: one retained module test file (Vitest)
npm run test:unit -- app/path/to/test-file.test.ts
# From MediaSet.Remix: remix test files by partial name, e.g. app/router.test.ts
NODE_ENV=test npx remix test router
```

Router tests use `*.test.ts`, browser tests `*.test.browser.tsx`, and end-to-end
tests `*.test.e2e.ts` (see `remix.json`). The retained module tests listed in
`vitest.config.ts` are excluded from `remix test`.

Use focused tests while iterating, then the full checks for the affected area
before completion. Shared configuration changes require both API and UI checks.
Report commands and results, including anything not run or any pre-existing
failure. Inspect new files as well as staged/unstaged diffs before committing.

## Formatting

Formatting commands modify files. Run them only on intended changes and review
the diff afterward; avoid unrelated repository-wide formatting.

For changed frontend TypeScript/TSX files, after `npm ci`, from `MediaSet.Remix`:

```bash
npm exec -- eslint --fix app/path/to/changed-file.tsx
npm exec -- prettier --write app/path/to/changed-file.tsx
```

Replace example paths with the actual files. For application JSON, use Prettier
alone. The existing `npm run lint:fix` and `npm run lint:format:fix` commands apply
broader fixes when that scope is intentional. Check mode is `npm run lint` and
`npm run lint:format`.

For changed C# files, from the repository root after restore:

```bash
dotnet format MediaSet.sln --no-restore --include MediaSet.Api/path/to/changed-file.cs
```

Replace the example path; this invokes .NET formatting/analyzers and is not a new
CI formatting gate. It follows `.editorconfig`, including C# line endings, so
review the selected file's diff. Do not suppress diagnostics or disable signing
to get checks or commits to pass.

## Hot reload decision

Decision: the UI dev loop uses `node --watch` server restarts plus the Tailwind
CLI watch, with a manual browser reload. Remix hot module replacement is not used.

Evaluated on `remix@3.1.0`: `remix/node-hmr` with `remix/component-hmr` (the
`remix new` starter's `hmr.ts`). On a desktop browser at localhost it swapped
server-rendered markup in about 0.1 s while keeping the document. It was rejected
for now because:

- The browser event stream URL is hard-coded to `http://127.0.0.1:<port>`, so a
  phone on the LAN cannot reach it, and an HTTPS page (mobile mode) blocks it as
  mixed content. It would only work on the development machine.
- The Tailwind stylesheet is a static file outside the asset server, so new
  utility classes still need a full reload, which covers most UI edits.
- It needs a supervising proxy process and two extra ports in front of the server
  that already owns the `/dev-api` proxy and HTTPS.

Revisit when node-hmr supports a configurable public event URL or HTTPS, or if
the styling follow-up moves CSS into the asset pipeline.

## Manual application checks

Manually verify the UI server restart and API watch reload. For mobile, check
actual-phone camera permission, barcode detection, images, and that pages load
without mixed-content errors.

