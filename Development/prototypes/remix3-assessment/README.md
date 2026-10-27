# Isolated Remix3.1.0 assessment demonstration

This is a disposable feasibility probe for [#654](https://github.com/paulmfischer/MediaSet/issues/654).
The [framework assessment](https://github.com/paulmfischer/MediaSet/issues/654#issuecomment-6442679743)
and [dependency inventory](https://github.com/paulmfischer/MediaSet/issues/654#issuecomment-6442676787)
are recorded as issue comments; the original issue body retains the investigation scope.
It does not change `MediaSet.Remix`, CI, the .NET API, or production configuration.
Use only a disposable API/database: the reduced book form omits fields and the
existing API replaces whole entities on edit. Never point this at real records.

## Reproduce

Use Node24.22.1 from the repository runtime policy; `node --version` must resolve
that baseline rather than a default Node22 shell. Install the existing frontend
dependencies by the project development guide first (needed only for its current
Tailwind3 compiler). From this directory:

```bash
npm ci --ignore-scripts --no-fund
npm run style
npm run typecheck
```

Start a **new** isolated local MongoDB service, using an available unused port.
The commands below use27154,51654 and31654. A name conflict is a stop condition;
do not repurpose an existing container. This container has no host data volume.

```bash
podman run --detach --name mediaset-654-mongo \
  --publish 127.0.0.1:27154:27017 docker.io/library/mongo:8.0
```

From the repository root, start the existing API against it:

```bash
dotnet run --no-launch-profile --project MediaSet.Api -- \
  --urls=http://127.0.0.1:51654 --environment=Assessment \
  --MediaSetDatabaseSettings:ConnectionString=mongodb://127.0.0.1:27154 \
  --MediaSetDatabaseSettings:DatabaseName=MediaSet654 \
  --ImageConfiguration:StoragePath=/tmp/mediaset-654-images \
  --ExternalLogging:Enabled=false \
  --BackgroundImageLookupConfiguration:Enabled=false \
  --IgdbConfiguration:ClientId= --IgdbConfiguration:ClientSecret= \
  --OpenLibraryConfiguration:BaseUrl=http://127.0.0.1:9/ \
  --OpenLibraryConfiguration:Timeout=1
```

The custom environment avoids development user-secrets. Explicit flags isolate
DB/image storage and disable background/external logging. The inert lookup config
registers services required by the existing API routing without contacting any
external service. Do not invoke metadata endpoints in this probe.

From this directory, start the candidate then run the HTTP probe in another shell:

```bash
npm start
# Separate terminal:
npm test
```

The candidate binds only `127.0.0.1:31654`, defaults to the assessment API port,
and rejects API hostnames other than127.0.0.1. Loopback alone does not certify a
disposable database: verify the API command above before submitting forms.

`npm test` checks a real API roundtrip, native URL-encoded validation, multipart
PNG generation/upload persistence,5MiB rejection, add/edit303 redirects, server
HTML with book content and hydration markers, search and404 error HTML. It deletes
only the book it creates in `finally`. Browser/manual probe records remain solely
in the disposable DB, which can be discarded with this new container.

For browser evidence use any Playwright installation. `browser-steps.json` is
compatible with the optional ui-capture skill's `drive.mjs`; that personal tool
is not a repository prerequisite. It asserts hydration, clicks add/edit/search,
captures1280x800 and390x844 layouts and checks module/hints/reset at `/scan-probe`.
Each run adds one `Browser assessment654` book to the disposable DB. Equivalent
manual checks: open `/books`, click hydration counter, add/edit a book, search it,
open `/scan-probe` and inspect browser errors. No physical camera check is claimed.

## Measured results (2026-10-27)

- Runtime Node24.22.1; API .NET SDK10.0.112; Mongo8 local image; existing API
  endpoints, with external lookups/background work disabled.
- Native HTTP test passed: SSR list/detail/search, validation400, missing404,
  add/edit303, actual PNG `coverImage.filePath` persistence. Generated valid PNG
  uses proper chunk CRCs; initial corrupt fixture was corrected before acceptance.
- Bounded multipart test passed:5MiB+1 file rejected413 before API mutation.
  Remix parser throws limit errors; its default adapter returned500, so this
  demonstration explicitly maps those typed errors to413.
- Chromium153.0.8010.12 (headless Linux): hydrated counter0→1; browser add/edit and
  search passed;1280/390px screenshots, no horizontal overflow, no console/page
  errors in the recorded flow. This is not the full required browser matrix.
- ZXing0.23.0 loaded through Remix's asset server in Chromium; creating a reader,
  configuring EAN13/UPCA hints and reset succeeded. It no longer exports the
  current browser reader, so #664 must assess `@zxing/browser@0.2.1` or an explicit
  camera wrapper. No decoding/camera/torch compatibility is inferred from loading.
- TypeScript5.9.3 with Node24.19.2 types passes the isolated `typecheck`.
- Candidate `npm audit --json --package-lock-only --ignore-scripts`: exit1,
  one low esbuild0.28.0 advisory (Windows development-server read); see the
  [inventory comment](https://github.com/paulmfischer/MediaSet/issues/654#issuecomment-6442676787). No overrides applied.
- Tailwind3 source CSS compiles; it warns that existing Browserslist data is old.
  Source reuse and basic layout were demonstrated; full visual parity is pending.

No-JS evidence is raw server HTML/form processing, not a recorded disabled-JS
browser matrix. Navigation API fallback and import-map CSP were assessed from
upstream documentation; current/previous Safari/Firefox/Edge/mobile, physical
camera grant/denial/release/repeated scans, real ISBN/UPC/EAN fixtures and torch
remain explicit holds. See the
[assessment comment](https://github.com/paulmfischer/MediaSet/issues/654#issuecomment-6442679743)
for the numbered implementation plan and the isolated replacement approach.

## Cleanup

Stop only the assessment API/candidate terminal processes with Ctrl+C. Remove
only the newly created `mediaset-654-mongo` after verifying its identity and
following the user's current container-operation approval policy. Never stop the
existing development database. Generated CSS/node_modules are ignored and can be
regenerated; `/tmp/mediaset-654-images` contains only this disposable probe's images.
