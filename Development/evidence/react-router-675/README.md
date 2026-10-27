# Collection routing checkpoint — 2026-10-27

Issue #675, step 2 under #673. Topic PR targets `feat/react-router-7`.
The existing Remix frontend, API, MongoDB schema and production configuration
are unchanged. No package or lockfile versions changed. The versions and holds
recorded in [#674 evidence](../react-router-674/README.md) remain applicable;
the pinned framework group was revalidated with this slice's full checks.

## Reusable contracts for #676

| Surface | Contract |
| --- | --- |
| Collection URLs | `/books`, `/movies`, `/games`, `/musics` |
| Detail URLs | `/{collection}/{entityId}`, with a MongoDB 24-character hex ID |
| Route registration | One `:entity` layout, index list and `:entityId` detail; generated Router 7 route argument/data types |
| API reads | PascalCase `/{Books|Movies|Games|Musics}/pagedsearch` and `/{Entity}/{id}` |
| List query | `searchText`, `orderBy=field:asc|desc`, positive integer `page`; API page size remains 75 |
| Response | Existing `items`, `page`, `totalPages`, `totalCount` and media-specific JSON models |
| Browser images | Public API prefix plus `/static/images/{filePath}`; private `API_URL` remains server-only |
| Cancellation/errors | Request signal reaches API fetch; missing/invalid records 404, unreachable API 503, upstream/JSON failure 502; recovery UI exposes no upstream body/host |

`app/utils/collections.ts` defines the shared typed registry, media-specific list
columns, detail fields, mobile summaries, duration/array formatting and query
validation. Unsupported ordering and invalid page inputs fall back to title
ascending/page 1. `app/api/collections.server.ts` owns read contracts only.

Search and clearing search reset to page 1 and retain ordering. Sorting retains
search and resets the page; pagination retains both. Detail links and the explicit
Back link preserve the query; browser back/forward also restore loader state.
Query strings are canonicalized for links and the search form has an explicit
collection action: raw `title:desc` direct URLs otherwise caused server/client
attribute differences during hydration. The driven test checks this regression.

The collection layout owns recovery boundaries and can accept the future editing
routes. #676 should add `/{collection}/add`, `/{collection}/{id}/edit` and
`/{collection}/{id}/delete` without changing these read URLs/contracts, using
generated `Route.ActionArgs` for mutations. Add/edit/delete controls are omitted
until their working routes arrive. Image upload/lookup/reset remains #677; image
display, existing lookup failure text and track lists are retained here.

List/table/card and detail field layouts retain the existing Tailwind colors,
breakpoints and media-specific choices. Sort links are now keyboard reachable;
search preserves focus through query navigation. Path navigation focuses main
content. The image preview uses a native modal dialog for Escape, focus trapping
and trigger-focus restoration. Styling/icon modernization remains #663/#665;
the inherited `xs` author-column breakpoint behavior belongs with styling parity.

## Verification

Final application checks used Node **24.22.1**, .NET SDK **10.0.112**. Earlier
Node 22 exploratory checks were superseded by full pinned-runtime runs.

| Area | Commands/result |
| --- | --- |
| API | `dotnet restore MediaSet.sln`; `dotnet build MediaSet.sln --no-restore`; `dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj --no-restore`: 0 warnings/errors, 771 passed, 1 existing skip |
| Baseline Remix | `npm run lint`, `npm run lint:format`, `npm run typecheck`, `npm run build`, `npm test`: passed, 1053 tests |
| Router | Same npm checks including Router type generation: passed, 39 tests across 5 files |
| Browser | Chromium desktop 1280×800/mobile 390×844; all four collections pass driven list/detail/search/sort/page/direct URL/history/SSR/hydration/image/keyboard checks |
| Pending/revalidation | Delayed real loader response displays/removes status overlay; submitting unchanged search performs another loader request |
| Errors | Invalid collections and missing/malformed IDs return 404; separate production instance with unreachable API returns 503 and usable recovery UI without private address |
| Production | Built detail SSR and browser image loading succeed with a separate `PUBLIC_API_URL` origin |

Normal-page console/page errors are empty after fixing query hydration. Expected
404/503 response diagnostics are checked separately. Unit-router tests print a
test-only missing HydrateFallback warning; actual server-hydrated routes have no
hydration warning. Existing framework future-flag/Browserslist warnings remain
within the already tracked modernization work.

The disposable runtime used container `mediaset-675-mongo`, no volume, loopback
27028/database `MediaSet675`, API loopback 5100 and `/tmp/mediaset-675-images`.
The API ran in Production environment without loading development User Secrets,
with background image lookup/external logging disabled. The inert OpenLibrary
configuration works around existing #670; no metadata requests are needed.
Original MongoDB 27017, existing data and production infrastructure were untouched.

## Reproduce

Use the [isolated startup instructions](../../../MediaSet.Router/README.md), replacing
the `674` container/database/image names with `675`. Start only a **fresh empty**
disposable database. With Node 24 selected:

```bash
node Development/evidence/react-router-675/seed.mjs
# Router dev 3100; baseline Remix dev 3001 with apiUrl=http://127.0.0.1:5100
# Built Router 3101 with API_URL and PUBLIC_API_URL=http://127.0.0.1:5100
# Built error fixture 3102 with API_URL=http://127.0.0.1:5199
UI_CAPTURE_DIR="$HOME/.agents/skills/ui-capture" node Development/evidence/react-router-675/walkthrough.mjs
```

The seeder creates 78 records in each collection through the real API, with valid
PNG uploads on the first/last records; it refuses existing collections. The
walkthrough imports Playwright installed by the ui-capture skill. It verifies
loader responses, drives the rendered UI and saves viewport screenshots here.

## Screenshots

| Collection | Replacement list | Replacement detail |
| --- | --- | --- |
| Books | [list](books-list.png) | [detail](books-detail.png) |
| Movies | [list](movies-list.png) | [detail](movies-detail.png) |
| Games | [list](games-list.png) | [detail](games-detail.png) |
| Music | [list](musics-list.png) | [detail](musics-detail.png) |

Representative baseline comparisons: [desktop list](baseline-books-list.png),
[detail](baseline-books-detail.png), [mobile list](baseline-books-mobile.png).
Replacement [mobile list](books-mobile.png) and [API recovery](api-unavailable.png).
All screenshots were visually inspected. Baseline action controls intentionally
arrive with #676; the read-only slice has no inactive mutation controls.

## Follow-ups

- #676 owns complete mutations; #677 owns metadata/image mutations.
- #663/#665 own styling/icons and inherited author-column breakpoint parity;
  #664/#679 own real-device and supported-browser validation. Chromium emulation
  here does not establish Safari/Firefox/Edge or device-camera parity.
- Existing tooling advisories/support work remains #663/#666/#667/#668.
- Existing backend startup registration issue #670 remains unchanged.
- No new untracked follow-up candidates.
