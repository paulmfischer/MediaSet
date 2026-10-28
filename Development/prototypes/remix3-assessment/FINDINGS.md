# Remix 3 Phase 0 findings

Throwaway spike on `remix@3.1.0` (Node 24.22.1) that ports the risky parts of `MediaSet.Remix` (Remix 2.17 on `main`).
Run it: `npm ci && npm run css && npm run dev` (port 44100), `npm test` (needs `npx playwright install chromium`).
Nothing here is production code; it exists to answer the go/no-go questions below.

## Verdict: GO, with the conditions listed at the end

All five spike questions from the plan were answered with passing tests (14 tests: 7 server, 1 browser, 6 e2e; `tsc` clean) and a working production image.

| # | Question | Result |
|---|---|---|
| 1 | Tailwind without a bundler | **Works.** `main`'s Tailwind 3 config and `tailwind.css` run unmodified through the Tailwind CLI into `public/app.css`, served by `staticFiles()`. The asset server is not involved. |
| 2 | `@zxing/library` camera scanner and charts | **Works.** Scanner is a `clientEntry` whose `<video ref>` signal releases the camera; verified the tracks end when the video unmounts. `@zxing/library` loads through the asset server via `allowPackages`. Charts are server-rendered SVG with no client JS. |
| 3 | Navigation and pending UI | **Works.** GET search form and POST add form both navigate without a full reload (document marker survives). A hydrated `PendingBar` and `SaveButton` driven by frame `reloadStart`/`reloadComplete` replace `useNavigation`. |
| 4 | Server and Docker | **Works.** Image builds, runs non-root, stops in 0.3 s on SIGTERM, serves prod CSP. `/dev-api` proxy (`remix/fetch-proxy`), mobile HTTPS dev (`https.createServer`), request-scoped traceparent (`AsyncLocalStorage`) all ported in a few lines. |
| 5 | Test runner | **Works with caveats** (see findings 3, 4). `router.fetch` server tests are fast and pleasant; browser and e2e run in Playwright. |

Also verified: literal routes (`/stats`) beat `/:entity`; form validation with `remix/data-schema` including repeated fields (multiselect), 400 re-render with preserved values, 303 redirect; `apiFetch.server.ts` copied from `main` unchanged.

## Findings that change the Phase 1+ work

1. **Production CSP from `main` breaks hydration.** `main` uses `script-src 'self' 'unsafe-inline'`. Remix 3's multiple-import-maps polyfill needs `blob:` scripts and WebAssembly (`es-module-lexer`), so hydration silently fails in production. Fix: `script-src 'self' 'unsafe-inline' blob: 'wasm-unsafe-eval'`. This only shows up with the CSP on (`NODE_ENV=production`), so keep a prod-CSP e2e test.
2. **Tailwind needs a build step in dev, CI and Docker.** `npm run css` (and `css:watch` in dev). `tailwind.config.ts` must import `tailwindcss/plugin.js` and `tailwindcss/colors.js` (with extension) to typecheck under `NodeNext`.
3. **`remix test` ignores Playwright `launchOptions`** (only `headless` and `channel` are read, plus page options such as `permissions`). Chromium's fake-camera flags cannot be passed, so scanner e2e stubs `navigator.mediaDevices.getUserMedia` with a canvas stream. Real-device scanning stays a manual check.
4. **Playwright browser version coupling.** `playwright@1.64.1` wanted Chromium build 1248; the host cache had 1243. CI needs `npx playwright install chromium` (plus caching) for browser and e2e tests, which `pr-checks.yml` does not have today.
5. **`coerce.number()` rejects `''`.** A blank optional numeric input fails validation. Optional numeric fields (pages, runtime, year, ...) need blank-to-`undefined` handling; see `app/actions/add/schema.ts`. Worth a shared helper.
6. **Action-level `middleware: [formData()]` did not type `context.formData`** in this setup (the guide shows it working); controller-level `middleware` did. Minor, but pick one convention early.
7. **Cold dev compile is slow for big packages.** First request for `@zxing/library` through the on-demand asset server took longer than Playwright's 5 s default. Production (minified, test env) was under 1 s. Not a blocker; tune e2e timeouts.
8. **Image size is 364 MB** (full `remix` package plus `@zxing/library` as prod deps; TS runs directly, no build output). Compare with the current UI image before cutover; may be acceptable, may want pruning.
9. **No built-in NavLink / active state**: header computes `active` from the request path server-side (done in `app/ui/header.tsx`). Same for any `useLocation`-driven UI.

## Not covered by this spike (carry into tickets)

- Multipart image upload (`remix/form-data-parser`, file storage), edit and delete flows (same shape as add), title/barcode lookup dialog flow.
- Error boundary equivalent (guide chapter 12), 404 and 500 pages, `showErrorDetails` behavior.
- Icons: `lucide-react` is React-only; the spike used text. Needs an SVG icon approach.
- HMR/dev loop: spike uses plain `node` without `--watch`; the starter's `remix/node-hmr` setup (`hmr.ts`) was removed and not evaluated.
- Real .NET API integration and Seq logging end to end (only a mocked fetch and the unchanged `apiFetch.server.ts` were exercised).
- Browser support matrix for the import-map polyfill, accessibility review of custom components.

## Conditions / recommendations for the tickets

- Treat findings 1-5 as explicit acceptance criteria in the Foundation tickets (CSP, Tailwind pipeline, Playwright in CI, blank-numeric helper, test conventions).
- Phase 3 (mutations) is lower risk than planned: the add flow works end to end; remaining risk is uploads and lookup.
- Phase 4 risk is mostly volume (rewriting ~35 components without hooks), not feasibility. The hooks-to-closure and `useEffect`-to-`ref`/`signal` patterns are proven by `mobile-menu.tsx` and `barcode-scanner.tsx`.
