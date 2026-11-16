# Artwork lookup misses (issue #613)

Findings and changes for the 87 recorded image failures reported on 2026-11-16
(37 movies, 28 books, 16 games, 6 music). Work is limited to code; production was not touched, so
the per-title outcome of each change is verified by unit tests built from the issue's titles and by read-only
public API checks, not by a production re-run (see "Verification still to do").

## Movies (TMDB)

Cause: titles written as DVD listings never reached a TMDB series. `Futurama, Vol. 5` was searched as a film;
`Gilmore Girls S4 Comp` was not recognised as a season; `Pre-Owned` and retailer words stayed in the query.

Changes (`RetailVideoTitle`, `VideoTitle`):
- Leading or trailing `Pre-Owned` / `Pre-Played`, and a leading retailer name followed by a separator
  (`Amazon.com:`, `Walmart -`, `Best Buy |`, `Target:`, `eBay -`, `GameStop:`), are removed. `Used` and `New` are deliberately
  kept (`Used Cars`, `New Girl`).
- `Series, Vol. N` and `Series, Volume N` (comma form only) are a series-level release: the verified TMDB series poster is used;
  when no series matches, the full title is searched as a film (`Kill Bill, Vol. 1`).
  A volume number does not map reliably to a TMDB season, so none is guessed. `Kill Bill: Vol. 1` (colon) stays a film.
- `Series S4`, `Series S04 Complete Series`, `Series: Season 4 Comp` resolve to that season's poster.
- `Series: Seasons 1-3`, `Seasons 1 & 2` resolve to the series poster.

Limits: the title must still equal the TMDB series name after normalisation, and a stored year must agree with the
series, season or episode air year, so a DVD whose release year differs from every air year is still rejected.
Titles with a retailer or marketing prefix that is not listed above cannot be handled without a risk of false matches.
This is the representative artwork (series or season poster) the issue accepts, not the retail package.

## Books (Open Library, Google Books)

Findings from code:
- The #872 Google Books fallback ran only inside the ISBN attempt, after the direct Open Library cover failed. It did run when
  the Open Library ISBN search found no record (the "no match" case), but only for a book with a valid ISBN.
- A book without an ISBN, or whose ISBN Google Books does not list, had no Google Books lookup at all.

Change: after the title attempt finds nothing usable, `IGoogleBooksClient.GetCoverUrlByTitleAsync` searches Google Books
with `intitle:"..." inauthor:"..."`. A volume is accepted only when its title (with or without its subtitle) equals the stored
title and it lists a stored author. A stored title that ends in a volume number (`Preacher Vol. 1`) also accepts the same
volume with its own subtitle (`Preacher Vol. 1: Gone to Texas`). The result is saved as a `GoogleBooks` confirmed match with
"Provider artwork" scope. A 403 is treated as no cover; a 429 or 5xx is a retryable outcome, as for the ISBN lookup.

A Google Books 429 or 5xx on this title lookup makes a book with no Open Library match retryable (`QuotaLimited` or
`ProviderUnavailable`) instead of terminal `NoMatch`, as for the ISBN lookup, so an outage delays the terminal classification
until a retry succeeds. Double quotes are removed from the title and author in the query.

Limits: the keyless Google Books API shares a per-IP daily quota; one extra request is made per unresolved book per run. Series
volumes whose stored title differs from Google's title by more than a subtitle (`Sandman Vol 1` vs `The Sandman: Preludes & Nocturnes`)
are still not matched; accepting them would need fuzzy matching and risks the wrong volume.

## Music (MusicBrainz, Cover Art Archive)

Read-only checks on 2026-11-16 (public MusicBrainz and Cover Art Archive APIs):

| Stored item | Result |
|---|---|
| Gaslighter (The Chicks) | Three releases share release group `feeac938-9cb1-4e36-9b98-4ec2ad9a65c3`; each release front and the release-group front redirect to an image, so artwork exists. Equal-looking releases of one album were treated as different identities, which makes the match ambiguous. |
| My Fair Lady: British Cast | MusicBrainz credits the releases to "Lerner"; none is credited to "British Cast". Stored artist and credit differ, so this is a no-match, not a missing cover. |
| Walt Disney's Snow White | No release titled Snow White is credited to Disney in the search results. No match. |

Changes: the release lookup now includes `release-groups`; a matched release without a front cover falls back to
`https://coverartarchive.org/release-group/{id}/front` (saved as "Release-group artwork"), and editions of one release group
count as one identity when title, artist, year and format agree. Releases from different release groups stay ambiguous.

Limits: a release group's front is a representative cover and may be another edition's artwork. The two no-match titles are
caused by artist credit differences and are left for manual artwork; loosening the artist comparison would accept wrong albums.

## Games (IGDB)

Findings: stored platforms carry the manufacturer (`Nintendo Wii`, `Sony PlayStation 3`, `Microsoft Xbox 360`) while IGDB uses
`Wii`, `PlayStation 3`; PC is `PC (Microsoft Windows)`. A bracketed year in the title (`Need for Speed Most Wanted (2012)`) made the
title differ from IGDB's name.

Changes: platform comparison drops the manufacturer and maps common aliases (PS/PSX/PSP/Vita, N64, GameCube, PC, Xbox Series);
`FIFA 10` and `FIFA Soccer 10` count as one title; a trailing `(YYYY)` or `[YYYY]` is removed from the search title and is used as the release year when no release date is stored;
IGDB `alternative_names` are requested and an alternative name equal to the stored title is accepted (regional names such as
`FIFA Soccer 10`).

Read-only IGDB search results (2026-11-16, same search the lookup uses):

| Stored title | IGDB lists | Result |
|---|---|---|
| FIFA 10 | `FIFA Soccer 10` (id 3133, PS3 and Xbox 360, alternative `FIFA 10: World Class Soccer`); the Wii, DS, PSP, PS2, PC and mobile releases are separate records named `FIFA Soccer 10` with alternative name `FIFA 10` | Real cause: the name differs. Alternative names fix the Wii/DS/PSP/PS2/PC records; the PS3 and Xbox 360 record needed a narrow rule that treats `FIFA 10` and `FIFA Soccer 10` as one title. Both now match. |
| FIFA 11 | `FIFA Soccer 11` (id 503, PS3, PC, Xbox 360); other platforms are separate records with alternative `FIFA 11` | Same cause and fix. |
| Need for Speed Most Wanted (2012) | `Need for Speed: Most Wanted` 2012 (id 3193, PS3, PC, Xbox 360; Vita id 248206; mobile id 248205) and the 2005 records | Matches once the bracketed year is dropped; the year (or the stored platform) picks the 2012 record. With no platform stored, the 2012 PS3/PC/Xbox 360, Vita and mobile records are equally good and the lookup stays ambiguous. |
| Wii Fit | `Wii Fit` (id 2186, Wii, 2007); `Wii Fit Plus` and `Wii Fit U` are different titles | Failed only on the platform text `Nintendo Wii` versus IGDB `Wii`; now matches. |

Secondary source: not needed. All four causes were title or platform text handling, and IGDB has the records.

## Operational: requeue after the fix ships

`POST /images/lookup/{entityType}/requeue` (`ImageRequeueService`, `ImageRetryPolicy.CanRequeue`) requeues a permanently
failed item only with `includeChangedTerminal: true` and a changed fingerprint or `StrategyVersion`. The 87 failures were
recorded under strategy version 2, so this change raises the default and `appsettings.json` to 3
(`ImageRetryPolicyTests.CanRequeue_PermanentFailureFromPreviousStrategyVersion_QueuesOnlyWithOptIn`). If production overrides
`BackgroundImageLookupConfiguration__StrategyVersion` in its environment, raise that value as well.

## Verification still to do (needs production access)

1. Deploy, then confirm `StrategyVersion` is 3 in the running configuration.
2. Take the `/stats/images` counts per media type as the "before" figures.
3. Requeue each media type with a fixed cutoff and operation ID, `includeChangedTerminal: true`, and page with `afterId`
   until `complete` (see `Setup/IMAGE_LOOKUP_RECOVERY.md`).
4. After the next worker run, re-read `/stats/images` and record per media type the items recovered, those still unresolved, and any
   false matches (spot-check each recovered cover).

## Proposed new providers

None is proposed. Remaining misses are title or credit differences that a second artwork source would not resolve either, and the
issue excludes replacing providers or adding an LLM lookup (#601). If a provider is later wanted for each gap:

| Gap | Candidate | Constraints and cost | Attribution | Maintenance |
|---|---|---|---|---|
| Books, series volumes | Google Books (already used) by title | Keyless shared quota, free; API key raises the limit | Cover use is governed by Google's terms; already in use | None added |
| Music | Cover Art Archive (already used) | Free, no key, redirects to archive.org | Attribute per CAA terms for each image's source | None added |
| Games, DVD box art | TheGamesDB, SteamGridDB or RAWG | Free tiers need an API key and have rate limits; terms restrict caching/redistribution | Required by each provider | New client, key and quota handling |
| Movies, packaging | None free with retail packaging | UPC databases lack images; TMDB has posters only | n/a | n/a |
