# Issue #611: book artwork recovery benchmark

The frozen cohort contains all **43 books** among 177 image lookup failures captured from the production `/image-stats` page on **2026-10-23 at 20:13:27 UTC**. Book details were read again before validation to include stored subtitle, publisher, authors, and publication date. The user explicitly approved read-only Open Library queries for this cohort. No production entities, images, or retry state were changed.

## Results

| Result | Books |
|---|---:|
| Verified downloadable artwork | 17 / 43 (39.5%) |
| Exact-edition identifier artwork | 15 |
| Work or alternate-edition artwork; exact edition unverified | 2 |
| No eligible downloadable artwork found | 24 |
| Ambiguous provider identities; no selection | 1 |
| Unresolved provider errors | 1 |

The baseline is the **43 recorded production failures**, not a simultaneous rerun of the old client. These results demonstrate current recovery opportunities; they do not prove that each image became available solely because of this code change. The cohort is a failure backlog, not a representative sample of the whole collection.

## Method and limits

A scratch .NET 10 harness referenced the working API project and invoked the actual `OpenLibraryClient.GetReadableBookByIsbnAsync`, `SearchByEntityPropertiesAsync`, and `ImageCandidateMatcher.Select`. It expanded `BookResponse.Artwork` candidates with their provenance. All identifier and search candidates passed the actual matcher using stored title, authors, date, subtitle, and publisher. After matching failed or eligible candidates were exhausted, the harness used the same stored-ISBN cover fallback as `ImageLookupService`, provided the provider scope reported no error. Provider requests were serialized with a 1.1-second delay. HTTP timeout was 40 seconds.

A recovery required an actual successful image GET, an `image/*` response MIME type, and successful ImageSharp identification with width and height greater than one. Candidate URL presence alone was never counted as recovery. Generated identifier URLs use `default=false`; returned 404s were rejected. Twelve recoveries used the authoritative stored ISBN directly; five used matched provider metadata (three ISBN-stage and two search-stage). ISBN-only recovery establishes the requested edition identifier, rather than independent title/author agreement. Images were decoded but were not visually inspected, so publisher catalog mistakes cannot be excluded.

The work-search scope-only review fix was applied while the benchmark was running. Both search-stage recoveries are reported as work/alternate artwork regardless of their pre-fix recorded scope; URLs and matching behavior were unchanged.

One provider error remains unresolved: `XIII Vol. 17` exceeded the 40-second HTTP timeout. `Tundra: In Full Color` had equally matching provider identities and was deliberately rejected as ambiguous. Neither is counted as an ordinary artwork miss or as a recovery. `Small Favor (The Dresden Files, Book 10)` has no ISBN; its initial harness attempt incorrectly called the empty-ISBN endpoint. A corrected search-only rerun, matching production behavior, completed without provider errors and found no eligible image. The evidence below uses the corrected observation.

### Coverless readable metadata with a verified alternative

`Homicidal Psycho Jungle Cat` (ISBN `9780590222105`) returned readable metadata for `OL42851674M`, but its first record had neither `details.details.covers` nor `data.cover`. The generated edition and ISBN image URLs both returned 404 with `default=false`. The search then selected matching title and author `Bill Watterson` and verified [cover ID 636099](https://covers.openlibrary.org/b/id/636099-L.jpg?default=false): HTTP 200, `image/jpeg`, **500 × 389**. This is work or alternate-edition artwork; the user's exact edition is unverified.

The same read-only spot check confirmed absent nested cover IDs for `Cosmic Connection` (`OL7437662M`) and `It's A Magical World` (`OL20941208M`), whose matching ISBN-stage alternative candidates were verified successfully. This directly exercises coverless metadata rather than inferring absence from the old failure state.

The [machine-readable observations](issue-611-book-artwork-observations.json) retain selected title/author evidence, candidate URLs, response status, MIME, and decoded dimensions for every cohort row. Search-stage scope labels reflect the final scope-only fix.

## Per-book observations

ISBN distinguishes rows with the same stored title. Dimensions refer to the first verified eligible image. “No eligible image” means the current pipeline found no acceptable match/image, not that artwork cannot exist elsewhere.

| Stored title | ISBN | Result | Verified dimensions |
|---|---|---|---|
| A Study In Emerald | 9781506703930 | No eligible image | — |
| Batman | 9781401204259 | Exact edition via stored ISBN | 338 × 500 |
| Black Orchid | 9781401240356 | No eligible image | — |
| Cosmic Connection | 9780385173650 | Exact edition via matching metadata | 292 × 500 |
| Digital Apollo | 9780262516105 | No eligible image | — |
| Equal Rites | 9780061020698 | Exact edition via stored ISBN | 294 × 475 |
| Hacking: The Art Of Exploitation | 9781593270070 | Exact edition via stored ISBN | 405 × 500 |
| Hacking: The Art Of Exploitation, 2nd Edition | 9781593271442 | Exact edition via stored ISBN | 384 × 500 |
| Homicidal Psycho Jungle Cat | 9780590222105 | Work/alternate (edition unverified) | 500 × 389 |
| It's A Magical World | 9780836221367 | Exact edition via matching metadata | 500 × 359 |
| Locke & Key Vol. 5: Clockworks | 9781613776995 | No eligible image | — |
| Moving Pictures | 9780061020636 | Exact edition via matching metadata | 302 × 500 |
| Myth Directions (Myth 03) | 9780441555291 | Exact edition via stored ISBN | 280 × 475 |
| Neverwhere | 9780380789016 | Exact edition via stored ISBN | 304 × 500 |
| Preacher | 9781401230944 | No eligible image | — |
| Preacher | 9781401245016 | No eligible image | — |
| Preacher | 9781401252793 | Work/alternate (edition unverified) | 305 × 475 |
| Really Big Tundra Treasury | 9781578331499 | No eligible image | — |
| Saga Of The Swamp Thing | 9781401220839 | No eligible image | — |
| Saga Of The Swamp Thing | 9781401230968 | No eligible image | — |
| Saga Of The Swamp Thing | 9781401240462 | No eligible image | — |
| Serenity Vol. 1 | 9781593074494 | Exact edition via stored ISBN | 333 × 500 |
| Serenity Vol. 2 | 9781595821621 | No eligible image | — |
| Sex On The Moon | 9780307741349 | No eligible image | — |
| Small Favor (The Dresden Files, Book 10) |  | No eligible image | — |
| The Black Dahlia | 9781608868681 | No eligible image | — |
| The Books Of Magic | 9781401246860 | No eligible image | — |
| The Manhattan Projects: Vol. 6 | 9781632156280 | No eligible image | — |
| The Sandman Vol. 10 | 9781401237547 | No eligible image | — |
| The Sandman Vol. 8 | 9781401234027 | No eligible image | — |
| The Unwritten Vol. 10 | 9781401250553 | No eligible image | — |
| The Unwritten Vol. 9 | 9781401246945 | No eligible image | — |
| Tundra: In Full Color | 9781578331932 | Ambiguous match | — |
| XIII Vol. 14 | 9781849181280 | No eligible image | — |
| XIII Vol. 17 | 9781849181457 | Provider error (unresolved) | — |
| XIII Vol. 2 | 9781849180405 | No eligible image | — |
| XIII Vol. 6 | 9781849180733 | No eligible image | — |
| XIII Vol. 9 | 9781849180931 | No eligible image | — |
| Y: The Last Man Vol. 1 | 9781563899805 | Exact edition via stored ISBN | 312 × 500 |
| Y: The Last Man Vol. 10 | 9781401218133 | Exact edition via stored ISBN | 325 × 500 |
| Y: The Last Man Vol. 3 | 9781401202019 | Exact edition via stored ISBN | 315 × 500 |
| Y: The Last Man Vol. 4 | 9781401202323 | Exact edition via stored ISBN | 319 × 500 |
| Y: The Last Man Vol. 6 | 9781401205010 | Exact edition via stored ISBN | 324 × 500 |

