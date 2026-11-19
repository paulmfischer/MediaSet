# Production-derived regressions (#912)

These are behavioral reconstructions, not recorded provider responses. All titles,
identifiers, artwork, authentication values and provider payloads are synthetic.
No production database or live provider is used. The real clients, background
processor, isolated MongoDB and local image storage are exercised unchanged.

## Evidence source and bounds

Read-only Seq searches on November 19, 2026 used application filter
`Application = 'MediaSet.Api.Processor.BackgroundImage'` and the exact interval
`2026-11-18T08:00:00Z` through `2026-11-18T08:05:00Z`, equivalent to November 18
02:00–02:05 America/Chicago (UTC−06:00). The run began at 02:00:00 and completed
by 02:04:34. Access: [Seq events UI](https://seq.fcortext.net/#/events?range=1d)
(select the historical interval above; no event-specific link was verified).

Queries comprised a 30-event application sample; a warning/error/fatal sample
capped at 120 (45 returned); a 120-event correlation query (84 returned) using
the six trace IDs below; and a 500-event query (101 returned) limited to the
background processor, game strategy, image service, and TMDB 404 events. These
are bounded samples, not server-side aggregate incident totals. Only sanitized
findings are retained here; raw events, request queries, titles and entity IDs
were not saved in the repository. No credentials are included in fixtures.

## Observed evidence versus fixture assumptions

Times below are America/Chicago on November 18, 2026. Trace IDs identify evidence,
not test fixture data. Public test method names beginning `Production912_` are
stable historical scenario IDs; the table gives the part after that prefix.

| Stable ID suffix | Observed evidence | Reconstructed assumptions and assertions |
| --- | --- | --- |
| `BookQuota_DefersFreshAndIneligibleBooksAsync` | Google Books HTTP 429 at 02:00:01.420600; trace `416923b2c6570cc000a07efde70826e4`. Subsequent book failure events through 02:01:25 contain quota classification. | Empty Open Library title results followed by Google 429; a synthetic HTTP-date `Retry-After` makes exact persistence testable. Real reset/header/body and prior quota cache state are unknown. Assert quota outcome, provider, retry time/due boundary, no early repeat, fresh-book cache deferral and no image. |
| `SeasonZero404_RejectsUnverifiedSeriesArtworkAsync` | TMDB season **0** HTTP 404 at 02:02:03.599550 and 02:02:16.191096; traces `6a5f0375af50173258f6bd03c3bb09a4` and `391d65be9c2718b644c80f0e6c932aac`. Correlated series/search requests succeeded; both ended without an unambiguous match. | One verified synthetic series and special title, then `{}`/404 for specials. Other production candidates and season bodies are unknown. Assert terminal no-match, no series-poster substitution, no files and no retry. |
| `CollectionTitleMiss_NeverSubstitutesIndividualFilmAsync` | The two traces above contain collection-shaped titles and failed matching after movie/TV searches and season-zero probes. | A sanitized trilogy and empty collection search model exhausted collection identity. This intentionally tests today's collection route, not the historical movie/TV request sequence. Assert no-match without falling back to any individual film poster. Empty collection JSON is inferred, not observed. |
| `GameMatchMiss_PersistsTerminalNoMatchAsync` | IGDB 200 followed by no-results warning at 02:03:55.282836; trace `143dfb50ca1ae100544546838a9131b1`. The same trace has successful UPC lookup first. Other game miss warnings occur earlier in the run. | Seed the sanitized title directly rather than reconstruct unknown UPC payload/cleaning. Synthetic token exchange and empty IGDB array model the observed miss. Assert terminal no-match, no identity, no files and retry suppression. |
| `GameArtworkMiss_PreservesConfirmedIdentityAsync` | The 02:03:55.392584 game failure mentions match/artwork while its correlated IGDB lookup reports no results. | Evidence does **not** establish a confirmed game without a cover. This adjacent hypothesis is modeled separately with a verified synthetic game and absent cover; assert terminal missing-artwork and retained identity. Neither cover-field absence nor artwork 404 is claimed observed. |
| `OversizedMusicArtwork_PersistsInvalidArtworkAsync` | At 02:04:10.203348 the image service rejected Cover Art Archive Content-Length **23,579,815** against **5,242,880** bytes; download/save error followed immediately. These image events have no TraceId. | Scale to 1 MiB limit plus one byte, preserving the real Content-Length guard without storing production bytes. Synthetic MusicBrainz match and PNG-type zero payload exercise direct `TooLarge` rejection, then background processing. Assert no files, confirmed release and intended terminal `InvalidArtwork`; the active assertion currently fails under #918. |
| `MusicBrainz503_RetriesAndRecoversWhenDueAsync` | Entity-property release search at 02:04:10.209434, HTTP 503 at 02:04:10.679101 and search warning at 02:04:10.680413; trace `824d852cac04f8b244ff40ff88fe4ed1`. | Inject search 503, then successful synthetic search/detail/artwork at retry eligibility. Real error body, headers, duration and later recovery are unknown. Assert provider-unavailable, MusicBrainz attribution, no early retry, exact due transition, attempt history, backlog recovery and readable saved image. |

## Gaps and interpretation

Logs identify symptoms and dependency statuses, not exact response JSON, artwork
bytes or complete candidate lists. Oversize rejection precedes the MusicBrainz
503 by milliseconds; without an image-event correlation ID this is temporal
association, not proof of a shared entity. The two symptoms therefore have
independent fixtures. A reconstructed future recovery is a contract assertion,
not a claim that production recovered. Persisted production outcomes were not
queried. No timing, quota reset or response schema beyond what the real client
needs is claimed to match the provider's original response.

The seven `Production912_` cases reuse shared scenario routines where behavior
already exists, preserving the original IDs and assertions from #907–#911.
The historical runner retains these IDs, actual outcomes, consumed fixture IDs
and this evidence-ledger reference in `summary.json` and `report.md`; progress
also includes Mongo snapshots and HTTP order. Expected product failures stay
active: #916's two Google Books attribution cases and #918's existing five
invalid-artwork cases, plus this production-derived oversized-artwork case.
Unexpected HTTP, unused fixtures or `harness-failure` records require separate
investigation and must not be counted as known product defects.
