# Isolated React Router replacement

Issue #674 is the application-shell checkpoint under #673. This is an independent
application and lockfile; `MediaSet.Remix`, its commands, containers and ports are
unchanged. Work integrates into `feat/react-router-7`, never directly into main.
Collection lists/details now use API-backed loaders at the existing lowercase
Books/Movies/Games/Music URLs. `/` shows server-loaded API health. Editing,
metadata and administration arrive in #676–#678. This is not the complete frontend
or a deployable cutover. See [collection contracts and evidence](../Development/evidence/react-router-675/README.md).

## Run with disposable services

Use Node 24.22.1 (`nvm use` from the repository root). Never point this verification
at a production API, existing MongoDB data or existing image directory.

From the repository root, start a fresh database without a persistent volume:

```bash
podman run -d --rm --name mediaset-674-mongo \
  -p 127.0.0.1:27028:27017 docker.io/library/mongo:8.0
dotnet restore MediaSet.sln
dotnet build MediaSet.sln --no-restore
ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://127.0.0.1:5100 \
  MediaSetDatabaseSettings__ConnectionString=mongodb://127.0.0.1:27028 \
  MediaSetDatabaseSettings__DatabaseName=MediaSet674 \
  OpenLibraryConfiguration__BaseUrl=https://openlibrary.org \
  ImageConfiguration__StoragePath=/tmp/mediaset-674-images \
  ExternalLogging__Enabled=false BackgroundImageLookupConfiguration__Enabled=false \
  dotnet run --project MediaSet.Api --no-build --no-launch-profile
```

The OpenLibrary section is needed by existing API dependency registration even
when only health/stats are used. No metadata request is made by the shell. The
Production API environment avoids loading development User Secrets. Stop only
this task's processes/container after testing; the database is discarded.

In a second terminal:

```bash
cd MediaSet.Router
npm ci
API_URL=http://127.0.0.1:5100 npm run dev
```

Visit `http://127.0.0.1:3100`. Production mode can run alongside development:

```bash
npm run build
API_URL=http://127.0.0.1:5100 PUBLIC_API_URL=http://127.0.0.1:5100 npm start
```

`npm start` delegates to the pinned framework server with defaults HOST=127.0.0.1
and PORT=3101. Explicit HOST/PORT values override these defaults. The separate
ports keep the current frontend operational throughout migration.

## Configuration boundary

`API_URL` is the private server-to-server address, read only from `.server.ts`
modules. The home loader fetches `/health` with cancellation and a five-second
timeout, serializes only `status`, and turns network/invalid/upstream errors into
a sanitized 503. The API's database name and other diagnostic fields never cross
that boundary. .NET keeps all data and domain responsibilities.

`PUBLIC_API_URL` is intentionally public browser/image configuration, independent
of `API_URL`. It is included in loader data and production image CSP. The default
`/dev-api` uses Vite's same-origin development proxy; production must supply the
browser-reachable API/image URL (there is no production Vite proxy). Future image
screens consume this public value in #677. Do not put credentials in it. The
framework server reads runtime environment variables; export them for production.

`.env.example` lists development overrides. No private setting uses a `VITE_`
prefix. `VITE_APP_VERSION` remains the build-time public footer version setting.

## Mobile HTTPS

Stop the ordinary dev server before using the same port in mobile mode. Reuse the
certificate setup in [local development](../Development/DEVELOPMENT.md#mobile-https-and-barcode-scanning).
Certificates must include the intended LAN IP/hostname and be trusted on the
device. Set an explicit intended interface, never an unrestricted bind by default:

```bash
API_URL=http://127.0.0.1:5100 DEV_HOST=YOUR_LAN_IP \
  DEV_HTTPS_CERT=../data/certs/dev.pem DEV_HTTPS_KEY=../data/certs/dev-key.pem \
  npm run dev:mobile
```

Visit `https://YOUR_LAN_IP:3100`. Only the frontend needs LAN reachability. The
`/dev-api` proxy keeps browser requests on the HTTPS origin while API/MongoDB stay
on loopback. Missing certificate settings fail startup explicitly; ordinary dev
ignores them. No trust-store or firewall changes are automated. Real-device
camera permission/scanner validation belongs to #664 and the final parity gate.

## Checks and manual smoke

```bash
npm ci
npm run lint
npm run lint:format
npm run typecheck
npm run build
npm test
npm audit --omit=dev
```

Typecheck generates framework route types before tsc. Types/build/node_modules
are excluded from Git. CI runs API, existing UI and replacement gates for PRs
into `feat/react-router-7`. Preserve these gates as routes migrate. Full existing
API/UI commands remain in [local development](../Development/DEVELOPMENT.md#checks).

Verify the following against the disposable API in both development/production:

1. Fetch `/` with curl: status 200 and `healthy` in HTML before JavaScript runs;
   no `MediaSet674` database name or private API URL in loader data/client assets.
2. In a browser, click Tools; Image Statistics appears. At 390px, open/close the
   menu. Inspect console and network for hydration errors or failed asset loads.
3. Visit `/missing`: status 404, error shell and Go Home/Try Again recovery.
4. Set a breakpoint/sentinel on `window`, change the home heading, and verify
   HMR updates text while preserving that sentinel. Restore the source afterward.
5. In HTTPS mode, verify `isSecureContext`, `navigator.mediaDevices`, and fetch
   `/dev-api/health` on the same HTTPS origin. Use an explicitly trusted device
   certificate for real-device testing.

For comparison, start the unchanged baseline after its normal build:

```bash
cd ../MediaSet.Remix
apiUrl=http://127.0.0.1:5100 clientApiUrl=http://127.0.0.1:5100 \
  HOST=127.0.0.1 PORT=3001 npm start
```

Evidence, package decisions and limits are recorded in
[the checkpoint report](../Development/evidence/react-router-674/README.md).
