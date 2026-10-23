# TV artwork for existing video entities

Issue: [#609](https://github.com/paulmfischer/MediaSet/issues/609).

## Matching and artwork rules

Videos remain `Movie` entities. The existing `IsTvSeries` flag routes known TV entries through
TMDB TV resources. Explicit numeric seasons (`Show: Season 6`, `Show: Series 6`), ordinal seasons
(`Show: The Complete Sixth Season`, through twentieth), and `The Complete Series` also select TV
without requiring that flag. Explicit season and complete-series requests never select a movie.

TV lookup uses [TV search](https://developer.themoviedb.org/reference/search-tv), series details,
and [season details](https://developer.themoviedb.org/reference/tv-season-details). Season details
provide the season's `poster_path` and episode names; a separate season-images request is unnecessary.
Both search and detail titles must match the normalized requested series name. Unknown seasons do
not silently fall back to the series. For a verified season, its poster takes precedence; if that
poster is null/blank, use the series poster. If both are absent, report missing artwork. A complete
series or unqualified series uses the series poster. Unit fixtures verify both poster branches and
the missing-season rejection.

Named specials such as `Doctor Who: The Time of the Doctor` must match an episode name in season 0.
The existing imported form `Doctor Who - Matt Smith Specials [HD] - S02E002 - The Time of the Doctor [HD]`
is recognized, but its package-specific S/E numbering is deliberately ignored: the provider episode
name must agree. Use the verified season-0 poster, falling back to the series poster. Unknown special
names remain unmatched. Duplicate episode names retain distinct episode identities and stay ambiguous
unless the stored date distinguishes them. Punctuation alone is insufficient to classify a special:
full-title TV lookup also preserves actual series names such as `Star Trek: Discovery`.

For unclassified titles, search movies and TV. Reject unrelated normalized titles before fetching
movie details and recheck the details. A bare film/series name such as `Constantine` stays ambiguous
when both identities satisfy the stored metadata. `IsTvSeries = true` rejects the same-named film;
an explicit season provides equivalent TV evidence. Missing provider responses caused by errors
cannot establish uniqueness: incomplete searches report the provider failure before any download.

Year checks remain mandatory when a stored year exists. TV entries may store the series debut year
or the requested season/episode's air year; either matching year is acceptable. Other years remain
unmatched. Remakes with identical names are distinguished by those dates where possible; otherwise
the existing ambiguity gate rejects them. Matching is discrete (normalized title, known season or
special, available date, unique provider identity), not a numeric relevance score.

Confirmed identities retain provider `Tmdb`; movies keep their existing numeric IDs. TV identities
are namespaced as `tv:<seriesId>`, `tv:<seriesId>:<seasonNumber>`, or
`tv:<seriesId>:0:<episodeNumber>`. Retry reads that exact resource and preserves the stored display
title after verifying it. No entity migration or new top-level media type is required.

## Read-only failed-video replay, October 23, 2026

The production image-stats snapshot captured at **2:32:33 PM CDT** (19:32:33 UTC) listed 177
all-media failures, including **53 unique video entities** (32 marked TV, 21 not marked TV).
Read-only entity-detail GETs supplied their titles, release dates and existing TV flags. This is a
current, non-atomic retained-failure snapshot with last attempts October 15–23; it is not an
exhaustive historical cohort of every failed attempt during the investigation window.

A local .NET harness used the actual `TmdbClient`, `MovieLookupStrategy` and
`ImageCandidateMatcher` against that frozen input. The baseline reproduced main's movie-only search
and original full-title/year confidence filtering. The updated run used this change's title/TV
context and confidence rules. Both runs issued only provider GET requests and successful candidates
were checked with unauthenticated image HEAD requests (success status and image content type).
No production image writes, downloads, entity updates, retries, container changes or deployment occurred.
The final replay, including the provider-failure gate added during review, finished at
**2:44:36 PM CDT** with the same counts and no provider errors.

| Cohort | Movie-only baseline | Updated | Additional matching posters |
| --- | ---: | ---: | ---: |
| All 53 captured failed videos | 13/53 (24.5%) | 40/53 (75.5%) | 27 |
| Last attempt October 16–23 CDT (47 videos) | 11/47 (23.4%) | 37/47 (78.7%) | 26 |

These are **read-only recovery candidates**, not claims that production failures have already been
recovered. Existing retry eligibility and backlog reprocessing remain separate in #607. A provider
poster is not verification of a physical disc's region, package edition, or box-set artwork.

### Additional TV matches inspected

The accepted provider identities below were checked against requested series/season/special names
and stored years, rather than counting any accessible image URL as a correct match.

| Failed entries | Accepted TMDB identity | Artwork |
| --- | --- | --- |
| Archer seasons 1–2 | TV 10283, seasons 1–2 | Requested season posters |
| Berserk season 1 | TV 35935, season 1 | Season poster; 1997 distinguishes remake |
| Bob's Burgers seasons 1–9 | TV 32726, corresponding seasons 1–9 | Requested season posters |
| Chappelle's Show seasons 1–2 | TV 713, seasons 1–2 | Requested season posters |
| Community seasons 1, 2, 3, 4, 6 | TV 18347, corresponding seasons | Requested season posters |
| Constantine: The Complete Series | TV 60743 | Series poster; 2014 TV series, not 2005 film |
| Doctor Who: The Snowmen (imported special title) | TV 57243, season 0, episode 66 | Specials season poster; episode air year 2012 |
| Doctor Who: The Time of the Doctor (imported special title) | TV 57243, season 0, episode 84 | Specials season poster; episode air year 2013 |
| Doctor Who seasons 3–7 | TV 57243, corresponding seasons 3–7 | Requested season posters; 2007–2012 dates reject other series |

The 13 film matches already recoverable by the baseline remained recoverable: American Psycho
(1359), Angels & Demons (13448), Bedazzled (1636), Bee Movie (5559), Beverly Hills Cop (90),
Bright Star (29963), Caddyshack (11977), Dark City (2666), The Black Hole (9570), The Book Thief
(203833), The Cable Guy (9894), The Da Vinci Code (591), and The Devil's Backbone (1433).

### Remaining 13 entries

| Entry | Reason retained as unmatched |
| --- | --- |
| Along Came Polly | Stored 2003 conflicts with provider film release 2004 |
| Aqua Teen Hunger Force: Volume 1; Volume 2 | Disc volumes are not assumed to equal TV seasons |
| Avatar: The Last Airbender: Book 1: Water; Book 3: Fire | Book/package-to-season mapping is not inferred |
| Back To The Future: The Complete Trilogy | Multi-film package; no unique single film/season identity |
| Blackadder Remastered: The Ultimate Edition | Package qualifier does not establish exact series identity |
| Boys Night Out: Dude You Need To Stop Dancing | No verified exact film/series/special match |
| Clerks | Stored 2002 conflicts with TV 2 (2000) and TV 206532 (1995); marked TV, so film is rejected |
| Community: Season 5 | Stored 2010 conflicts with series debut 2009 and season air year 2014 |
| Cowboy Bebop Perfect Collection #1-6 | Package qualifier does not establish exact series identity |
| Darkman Trilogy | Multi-film package; no unique single film/season identity |
| Dirk Gently | Stored 2010 conflicts with TMDB TV 38215 first-air year 2012 |

These limitations are intentionally conservative. Correcting stored dates or defining package-specific
volume/book/collection mappings requires separate data decisions; this change does not rewrite metadata.

## Automated verification

Targeted NUnit coverage exercises representative Community and Bob's Burgers season lookup, season
poster preference and series fallback, missing-season rejection, complete-series lookup, both special
title forms, unknown/duplicate specials, full series names containing colons, TV versus film metadata,
incorrect movie titles, film/series ambiguity, year constraints, namespaced confirmed retries, HTTP
request/JSON contracts, quota failures, cancellation, and incomplete-provider-search rejection.
The final targeted run passed **111 tests**, with no failures or skips.

```bash
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj --filter 'FullyQualifiedName~MovieLookupStrategyTests|FullyQualifiedName~TvArtworkLookupTests|FullyQualifiedName~TmdbTvClientTests|FullyQualifiedName~ImageFallbackConfidenceTests|FullyQualifiedName~ImageLookupServiceTests|FullyQualifiedName~ImageProviderFailureTests' --no-restore
```
