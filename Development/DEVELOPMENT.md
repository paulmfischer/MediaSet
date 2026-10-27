# Local development

The API and frontend run on the host. Podman runs only MongoDB. Application
dependencies use the normal host NuGet cache and frontend node_modules directory;
startup does not rebuild images, repair labels, clear caches, or rewrite configuration.
For passive agent diagnosis and an explicit bootstrap with checkout-local package
caches (including independent worktrees), see [Linux agent onboarding](AGENT_SETUP.md).

## Prerequisites

- .NET 10 SDK compatible with the repository's global.json.
- Node.js 24.22.1 LTS and its bundled npm (exact version in [`.nvmrc`](../.nvmrc)).
  With nvm already installed, run `nvm install` followed by `nvm use` from the
  checkout root; otherwise use the official Node installer for that exact version.
  Verify `node --version` and `npm --version` before `npm ci`. The application
  supports serviced Node 24 patches >=24.22.1; CI and both Docker stages pin
  24.22.1. See [selection and validation](NODE_24_VALIDATION.md).
- Podman and podman-compose. On Fedora: `dnf install podman podman-compose`.
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
   dotnet restore MediaSet.Api/MediaSet.Api.csproj
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
- Vite uses port 3000 and fails if it is already occupied.
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
| Launch UI | Starts Vite in a JavaScript Debug Terminal on port 3000. |
| Launch API + UI | Starts both applications; MongoDB startup runs only through the API prerequisite. |
| Launch UI — Mobile HTTPS | Starts the HTTPS frontend after the optional certificate setup below. |
| Launch API + UI — Mobile HTTPS | Starts the API debugger and HTTPS frontend together. |

UI debug launches open the default browser when Vite starts, including combined
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
Browser API/image requests use the relative `/dev-api` prefix. Vite strips that
prefix and forwards requests to the local API, so phones never call their own
localhost or load HTTP images from an HTTPS page.

Optional frontend overrides belong in `MediaSet.Remix/.env.local`; see
`MediaSet.Remix/.env.example`. Restart Vite after changing environment variables.
`apiUrl` controls the server-side API/proxy target; `clientApiUrl` and
`VITE_API_URL` can override browser URLs when deliberately testing another API.
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

## Moving from the old container stack

Stop only this checkout's old development containers before starting native
applications, so ports 3000, 5000, and 27017 are free:

```bash
podman stop mediaset-dev-ui mediaset-dev-api mediaset-dev-mongo
```

Run that once only if those old containers exist. Their data is not needed by
the new setup. The new database uses `data/dev-mongodb`, leaving the old
`data/mongodb` directory untouched; no database migration is required.

- Complete first setup with matching database credentials.
- Inspect old root `.env` integration settings and move any needed API keys to
  User Secrets; old Compose-only names such as TMDB_BEARER_TOKEN are not loaded
  by the native API.
- Remove obsolete `apiUrl`, `clientApiUrl`, `VITE_API_URL`, and
  `REMIX_DEV_HTTP_ORIGIN` entries from frontend local env files to use native
  defaults. The old helper appended duplicate VITE_API_URL entries.
- Preserve `data/images`. If old container-created files cannot be written,
  inspect ownership and repair only that project's image files, not home-directory
  permissions or global container storage.
- Retired application cache volumes are no longer used. Leaving them in place is
  harmless; do not run broad container/image/volume pruning.

## Verification and troubleshooting

Check actual database connectivity, not just a listening port:

```bash
curl --fail --silent http://127.0.0.1:5000/health | python3 -c 'import json,sys; h=json.load(sys.stdin); assert h["status"] == "healthy" and h["database"]["status"] == "up", h; print("API and database healthy")'
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
  Vite deliberately does not select a different port.
- **Dependency errors:** rerun `dotnet restore` or `npm ci --prefix MediaSet.Remix`.
  Run dependency installation after lockfile changes. Do not clear all caches
  as a normal startup step.
- **Frontend hydration/devtools errors:** retain the existing Vite prebundling
  configuration; stop Vite and run `npm run dev -- --force` if its optimized
  dependency cache needs regeneration.
- **Mobile camera unavailable:** confirm trusted HTTPS, certificate address,
  browser camera permission, and LAN reachability. Certificate-warning bypasses
  are not a replacement for installing the CA.
- **SELinux:** only MongoDB/init-script mounts need container labels now.
  Investigate their labels and scoped permissions; do not disable enforcement.

## Checks

```bash
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj
cd MediaSet.Remix
npm test
npm run typecheck
npm run lint
npm run lint:format
npm run build
```

Manually verify frontend HMR and API watch reload. For mobile, check actual-phone
camera permission, barcode detection, images, and HMR without mixed-content errors.

