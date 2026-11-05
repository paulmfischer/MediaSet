# Remix 3 cutover readiness (TEMPORARY)

> **DELETE THIS FILE FROM `feat/remix-3` BEFORE THE CUTOVER MERGE.**
> It is migration evidence for issue #704, not project documentation. It must never reach `main`.
> The cutover PR (`feat/remix-3` into `main`) must not contain `Development/REMIX_CUTOVER.md`;
> remove it in a commit on `feat/remix-3` first and check with
> `git diff --stat origin/main...feat/remix-3 -- Development/REMIX_CUTOVER.md` (expect no output).

Run date: 2026-11-04. Compared `origin/main` (legacy Remix 2 UI, dev server on port 3100) with
`feat/remix-3` at `e408e5f` plus the fixes in this PR (Remix 3 UI, dev server on port 3000), both against
the same API (the `feat/remix-3` API build, port 5000) and one throwaway MongoDB 8 container
(`mediaset-cutover-mongo`, 127.0.0.1 only, database `MediaSetCutover`). Seed data: 14 books, 2 movies,
1 game, 1 album. Headless Chromium drove both apps; the browser console and page errors were collected
for every visit.

Legend: PASS = same behavior as `main`; DIFF = behavior differs (kept, accepted or follow-up, see the
table); FIXED = defect found in this review and fixed in this PR; N/A = not applicable to that app.

## 1. Route and workflow matrix

Routes come from `main`'s `MediaSet.Remix/app/routes/` (flat routes) and `MediaSet.Remix/app/routes.ts`.

| # | Route / workflow | Result | Evidence |
| --- | --- | --- | --- |
| 1 | `/` home dashboard (per-type tiles, stats, attribution) | PASS | Rendered main text identical on both; side-by-side screenshots match. Header has a light grey background in light mode on Remix 3 (`bg-zinc-200`, from #709) where `main` had none: DIFF, accepted (an opaque sticky header). e2e: `dashboard.test.e2e.ts` (tabs by click and keyboard) |
| 2 | `/books`, `/movies`, `/games`, `/musics` lists | PASS | Rows, columns and counts identical for movies, games, music; screenshots at 1280 and 390 px. Books list on Remix 3 shows an extra Authors column at desktop width (DIFF, accepted: `main`'s dev render showed Title, Format and Pages only; the mobile card layout matches). e2e: `lists.test.e2e.ts` |
| 3 | List search, sort, paging (`searchText`, `orderBy`, `page`) | PASS | Same query parameter names. Page past the last (`?page=2` with 14 items): `main` shows the empty-collection message, Remix 3 shows the last page (DIFF, improvement). Search with no match: Remix 3 says `No Books match "zzzz".`, `main` shows the empty-collection message (DIFF, improvement) |
| 4 | List row delete icon (opens a confirm dialog) | DIFF | `main` has a trash icon on every list row and mobile card; Remix 3 rows show Edit only, delete is on the detail page (and `/:entity/:id/delete`). Deliberate per #710 ("delete dialog left to #695"), but a user-visible change. See follow-ups |
| 5 | Detail pages for all four types | PASS | Field labels and values identical for the four seeded items. DIFF: `main` shows a "No image available" placeholder when there is no cover; Remix 3 shows nothing (accepted, see follow-ups). Remix 3 repeats the TMDB/IGDB attribution captions in the footer on detail pages. e2e: `detail.test.e2e.ts` |
| 6 | Add book / movie / game / music | PASS | Created each through the form in both apps, redirected to the item, item found in the list. e2e: `add-*.test.e2e.ts` (validation errors keep input, no full reload, pending state, track list for music) |
| 7 | Edit book / movie / game / music | PASS after fix | Edited the title in both apps, saved, detail shows the new title. FIXED: the edit form rendered `value="null"` for a number the API returns as `null` (book pages, movie runtime, music tracks and discs), logging "The specified value "null" cannot be parsed" in the browser. Fixed in the `to*Values` mappers with a test. FIXED: the multi-select rendered duplicate sibling keys for each chosen value ("Duplicate keys detected in siblings" on every edit page with genres, authors and so on); hidden input keys are now prefixed. e2e: `edit-delete-book.test.e2e.ts` |
| 8 | Delete book / movie / game / music | PASS | Deleted each in both apps through the confirm dialog (`main`: list row; Remix 3: detail page) and confirmed with the API that the item is gone. Remix 3 also serves `GET /:entity/:id/delete` as a no-JavaScript confirmation page; `main` answers 400 to that GET. e2e: `edit-delete-book.test.e2e.ts` (cancel, then confirm) |
| 9 | Metadata lookup, by title and by identifier | PASS after fix | Live OpenLibrary: title "The Hobbit" lists results in a dialog and "Use this result" fills the form; ISBN 9780261103344 returns a result (both apps). FIXED: saving, or running another lookup, after applying a real result failed with 413 "Upload too large": OpenLibrary returned 93 genres, the form then had 100+ fields and `boundedFormData()` capped parts at 100 (urlencoded bodies are counted too). The cap is now 1000 with a test. Verified live: the book saved with 93 genres. Movie, game and music lookups were not run live (TMDB and IGDB credentials are not configured here); they are covered by server tests and `add-book` / `edit-delete-book` e2e lookup flows |
| 10 | Cover image upload (file) | PASS | Uploaded a PNG on add in both apps; the list shows the stored cover. e2e: `book-image.test.e2e.ts` (preview, URL preview, failed URL, replace and remove on edit) |
| 11 | Cover image by URL | PASS | e2e only (`book-image.test.e2e.ts`, `csp.test.e2e.ts` for the production CSP and the client API origin). Lookup apply sets `imageUrl` (seen live) |
| 12 | Barcode scan | PASS (automated); real device PENDING | e2e `barcode-scanner.test.e2e.ts` (42 e2e tests total pass): native detector, zxing fallback, cancel and Escape release the camera, permission denied, no camera, no camera API, production CSP. The real-device check is in section 5 and is for the maintainer |
| 13 | `/image-stats` | PASS | Rendered main text identical. e2e: `image-stats.test.e2e.ts` |
| 14 | `/config.json` | PASS | Remix 3 returns the runtime config in dev and in the container (200). `main`'s Vite dev server answers 404 for it (it works under `remix-serve`; not re-run here) |
| 15 | Errors: unknown path, unknown collection, missing item | DIFF (improvement) | Remix 3: "Page not found" / "Book not found" pages with status 404 and no console noise. `main`: bare 404 with `Root Error` logged to the console |
| 16 | Mobile layout and menu (390 px) | PASS | Screenshots of every route at 390 px; menu hydrates (e2e `app.test.e2e.ts`: open, outside click, Escape, focus return, pending indicator) |
| 17 | Edit/add form fields per type | DIFF (known, intended) | Lookup is inline "Look up" buttons beside Title and ISBN/Barcode (a separate lookup panel on `main`); music artist and label are selects, duration is minutes plus seconds. Games have no lookup because IGDB is not configured |

Browser console: with the fixes, no errors or warnings on any route in either app other than the expected
404 resource logs. One benign network entry on every Remix 3 page: a `blob:` script request reported as
`net::ERR_ABORTED` (the import-map feature probe, revoked after use), not a console error.

### Playwright e2e coverage (Remix 3, `npm run test:e2e`, 13 files, 42 tests)

| Area | File |
| --- | --- |
| App shell, menus, pending indicator | `app.test.e2e.ts` |
| Home dashboard tabs | `dashboard.test.e2e.ts` |
| Lists (sort, page, search) | `lists.test.e2e.ts` |
| Detail and not-found | `detail.test.e2e.ts` |
| Add book, movie, game, music (+ book lookup, track list) | `add-book`, `add-movie`, `add-game`, `add-music` |
| Edit and delete book (+ edit lookup) | `edit-delete-book.test.e2e.ts` |
| Cover image | `book-image.test.e2e.ts` |
| Barcode scanner (stubbed camera, production CSP) | `barcode-scanner.test.e2e.ts` |
| Production CSP and hydration | `csp.test.e2e.ts` |
| Image statistics | `image-stats.test.e2e.ts` |

Not covered by e2e: edit and delete for movie, game and music (covered by server tests and by the manual
run above), live provider lookups, and anything against the real API (the e2e suite uses fakes; this review
did the real-API pass).

## 2. Real API, disposable database and Seq

- API: `dotnet run --project MediaSet.Api` (Development), connection string and database name supplied
  through environment variables for this process only (the user's User Secrets were not changed; the throwaway
  database password was random and never printed or stored in the repository). `GET /health` returned
  `healthy` with `database.status: up` on `MediaSetCutover`.
- Database: `podman run` of a new `mediaset-cutover-mongo` container (mongo 8.0, port bound to 127.0.0.1:27018).
  No existing container, volume or the OMV server was used for data.
- Remix 3 ran both as `npm run dev` and as the production image (below) against that API.
- Seq (read-only queries with the `seq-logs` helper, 25-minute window on 2026-11-04):
  - `MediaSet.Api` events arrived (8,216 Information, 4 Warning) tagged `Environment = Development`.
  - `MediaSet.Remix` events arrived with `@tr` set.
  - End to end trace: the Remix event "Paged searching for Books" (trace `c62beb2c6f0e8f40d9a644fee7ccb289`)
    shares that trace with the API span `HTTP {RequestMethod} {RequestPath}` for `/Books/pagedsearch`, its child
    spans (`PagedSearch`, `find MediaSetCutover.Books`, `countDocuments`, `aggregate`) and the HTTP log. One
    trace spans the UI log, the API request and the database calls.
  - Observation: the Remix `Environment` property is lowercase (`development`) while the API's is
    `Development`, so one Seq filter on `Environment` misses one of them. See follow-ups.
- Note: events from this review reached the maintainer's Seq (the `ExternalLogging` User Secrets point there);
  they are identifiable by `Environment = Development` and the database name `MediaSetCutover`.

## 3. Accessibility, browsers, dependencies, bundle size

### Accessibility (axe-core 4.10.2, WCAG 2.0/2.1 A and AA plus best-practice, headless Chromium 1280 px)

Run on 18 Remix 3 routes (home, four lists, four details, four add, two edit, delete, image-stats, 404) and
the same routes of `main`.

- Remix 3: one rule class only, `color-contrast` (serious), on every page (for example 25 nodes on the home
  dashboard, 13 to 21 on forms, 4 on details, 2 on lists), plus `empty-table-header` (minor, 1 node) on the
  four lists (the cover and actions columns).
- `main`: the same `color-contrast` and `empty-table-header`, plus `region`, `scrollable-region-focusable`
  (dev-overlay noise) and, on the 404, `document-title`. Remix 3 has no violations that `main` did not have,
  and fewer (the extra `region` and `scrollable-region-focusable` hits on `main` come from its dev overlay, and its 404 page has no title). Detail pages have 4 contrast nodes against
  2 on `main` (the footer attribution captions).
- Keyboard and focus behavior is covered by e2e (menus, dropdown, dialogs, scanner, tabs).
- Not done: screen reader pass, forced-colors, zoom to 400 percent.
- Pre-existing and inherited from `main`; it needs a design decision (grey text on the dark tiles and form
  placeholders). See follow-ups.

### Supported browsers

| Browser | Result | How |
| --- | --- | --- |
| Chromium (Playwright 1.64, desktop and 390 px) | PASS | Full workflows above, 42 e2e tests, axe |
| Firefox (Playwright) | PASS (smoke) | Add Book renders and hydrates, input works, no page errors, Scan button shown (no `BarcodeDetector`, zxing path) |
| WebKit / Safari | NOT RUN | Playwright WebKit cannot launch on this Fedora host (missing system libraries); needs a Mac or iPhone, see section 5 |
| iOS Safari, Android Chrome | PENDING (maintainer) | Section 5 |

The app needs ES modules and import maps; the import-map polyfill path is covered by `csp.test.e2e.ts`.

### `npm audit --omit=dev` (MediaSet.Remix)

- Before: `source-map-js` high (GHSA-68fv-2mgg-jv7q), `esbuild` low under `@remix-run/test`.
- `npm audit fix` updated `package-lock.json` only (no `package.json` change): `source-map-js` is fixed.
  After the fix, checks, build, server tests and e2e all pass.
- After: 1 low, `esbuild` 0.27.3 to 0.28.0 under `@remix-run/test` (GHSA-g7r4-m6w7-qqqr, arbitrary file read in
  the esbuild dev server **on Windows**). `npm audit fix` cannot raise the nested copy further. It is a
  dev-only test-runner dependency, the trim step removes it from the image, and it does not apply on Linux.
  Accepted. Full `npm audit` also lists `minimatch` (moderate, dev tooling through
  `@typescript-eslint`); its fix is a breaking downgrade, so it is left alone.

### Client bundle and `icons.ts`

The concern (all 33 icons re-exported from `app/ui/icons.ts` loaded by any client island) does not hold: the
browser fetches the lucide icon modules individually, and a page loads only the ones its islands import. On
the production image the network log shows exactly four icon modules (`menu`, `x`, `settings`, `shell`, about
0.1 KB each). Client JavaScript per page, decoded, served from the app: 134 KB on `/`, 134 KB on `/books`,
152 KB on `/books/add`, 136 KB on a detail page; the largest files are the `@remix-run/component` runtime
(reconcile 23.6 KB, frame 22.3 KB). The zxing decoder loads only when scanning (covered by e2e). No split is
needed.

## 4. Checks, image and CI

Run from the repository with Node 24.22.1 and the .NET SDK from `global.json`, after the changes in this PR.

| Check | Result |
| --- | --- |
| `dotnet restore`, `dotnet build MediaSet.sln --no-restore` | 0 warnings, 0 errors |
| `dotnet test` | 771 passed, 1 skipped, 0 failed |
| `npm run lint`, `lint:format`, `typecheck`, `build` | pass |
| `npm test` | 606 tests pass |
| `npm run test:e2e` | 13 files, 42 tests pass |
| `git diff --check` (worktree, cached, `origin/main...HEAD`) | clean |
| `podman-compose -f docker-compose.prod.yml config` | valid; no diff to `docker-compose.prod.yml` or `docker-compose.dev.yml` against `main` |

Container image (`MediaSet.Remix/Dockerfile`, built locally with podman, about 70 s):

| Image | Size |
| --- | --- |
| Remix 3 image (`feat/remix-3` + this PR) | 268 MB |
| `main` legacy UI image built locally | 401 MB |
| Published `ghcr.io/paulmfischer/mediaset-ui:latest` (locally pulled earlier) | 363 MB |
| Reported on #737 before the trim | 418 MB |

The trim work in #743 brought the image 26 percent under the published legacy image. The container was run on
the host network against the real API: `/`, `/books`, `/image-stats` and `/config.json` returned 200, it runs as
the non-root `mediaset` user, the production CSP and security headers are present, and the page loads with no
console errors. The environment contract (`apiUrl`, `clientApiUrl`, `NODE_ENV`, `ExternalLogging__*`,
`APP_VERSION`) matches `docker-compose.prod.yml`.

CI: PR #744 (the head of `feat/remix-3`, `e408e5f`) had API, UI and UI Browser all passing. CI runs on pull
requests only, so this PR's own run is the check for the final state (see the PR).

## 5. Real-device mobile scan test (maintainer to fill in)

Not run: needs a phone, a camera and a physical product. Steps (full detail in
[DEVELOPMENT.md](DEVELOPMENT.md#mobile-https-and-barcode-scanning)):

1. Prerequisites on the dev machine: API and MongoDB running, a lookup provider configured so Scan is offered
   (OpenLibrary needs nothing for books; ISBN-13 on the back of a book is the easiest barcode).
2. Install [mkcert](https://github.com/FiloSottile/mkcert) and run `mkcert -install` once.
3. From the repository root:
   ```bash
   mkdir -p data/certs
   mkcert -cert-file data/certs/dev.pem -key-file data/certs/dev-key.pem localhost 127.0.0.1 ::1 YOUR_LAN_IP
   ```
4. In `MediaSet.Remix/.env.local` set `DEV_HTTPS_CERT=../data/certs/dev.pem` and
   `DEV_HTTPS_KEY=../data/certs/dev-key.pem`.
5. Copy only the public `rootCA.pem` (`mkcert -CAROOT` shows where) to the phone and trust it: Android under
   Settings, Security, Install certificate, CA certificate; iOS install the profile, then Settings, General,
   About, Certificate Trust Settings, enable full trust. Never copy `rootCA-key.pem`.
6. Allow port 3000 on the LAN firewall for your interface; the API and MongoDB stay local.
7. From `MediaSet.Remix` run `npm run dev:mobile` (stop the plain `npm run dev` first; same port).
8. On the phone open `https://YOUR_LAN_IP:3000` and confirm there is no certificate warning.

Checklist (mark each, with device, OS and browser version):

| Check | Android Chrome | iOS Safari |
| --- | --- | --- |
| Device, OS and browser version | | |
| No certificate warning over HTTPS | | |
| Scan button shown on add book, movie, music (absent on plain HTTP) | | |
| Tap Scan: permission prompt, then live rear-camera preview in the dialog | | |
| Barcode held in view fills ISBN/Barcode, dialog closes, lookup results open | | |
| Apply a result and Save: item is created | | |
| Same on an edit form | | |
| Cancel, Escape or Back turns the camera indicator off | | |
| Deny permission: clear message; allow in site settings, Scan works again | | |
| Reasonable speed and tolerance of blur or glare (iOS uses the zxing decoder) | | |

Result and date: _to be filled in by the maintainer_.

## 6. Open items from this review

| Item | Kind | Status |
| --- | --- | --- |
| List rows have no delete icon (row 4) | Behavior change vs `main` | Restored in PR #752 (closes #746) |
| Detail page placeholder differs from `main` (row 5; the Remix 3 page already had an icon-only placeholder, not nothing as first reported) | Visual difference | Matched to `main`'s box and caption in PR #751 (closes #747) |
| Color contrast on dark tiles, placeholders and detail footer captions (axe) | Accessibility, inherited | Fixed in PR #753 (closes #748): always-dark theme (the light OS theme gave light text on white), slightly darker primary buttons, lighter footer and label text. axe color-contrast is 0 in both schemes |
| Remix `Environment` property is lowercase `development`, the API's is `Development` | Observability consistency | Fixed in PR #750 (closes #749) |
| Issue #698 (Barcode scanner) is still open although the scanner and its e2e tests are merged | Tracker hygiene | Close or update #698 |
| Real-device scan, WebKit and iOS checks | Manual | Maintainer, section 5 |
| Movie, game and music lookups against live providers | Not verified live | Verify with credentials before release if wanted |

Rows 1 to 5 and the accessibility section above describe the state before PRs #750 to #753; those PRs are
independent of this one and target `feat/remix-3` too. The 2 `Open` statuses above are the only ones left
for the maintainer apart from the manual device checks.
