# Image lookup regressions

This separate NUnit project exercises real MongoDB access, provider clients, lookup
strategies, image validation, background processing and local filesystem storage.
It is deliberately outside the normal unit-test solution and CI. Product defects
must keep failing assertions; book fallback and quota scenarios cover #907,
movie/TV scenarios cover #908, game/authentication scenarios cover #909, music and
validation scenarios cover #910, processing/recovery scenarios cover #911, and
further scenarios belong to #912.

Production-derived coverage is now in `ProductionRegressionScenarios.cs`.
[The evidence ledger](PRODUCTION_EVIDENCE.md) maps seven stable `Production912_`
scenario IDs to bounded, correlated November 18 Seq evidence, documents synthetic
response assumptions and gaps, and distinguishes active product assertions from
harness failures. No production behavior or provider access is added.

Prerequisites: .NET 10 SDK, Python 3, and a working local rootless Podman (default)
or Docker runtime. The runtime must be able to obtain `docker.io/library/mongo:8.0.17`.
Podman is forced into local mode; Docker is restricted to a local Unix socket
(`DOCKER_HOST` may select a local rootless socket). Remote runtimes are rejected.
No provider keys, production configuration or existing database are used. The suite
always creates a unique container with an ephemeral loopback-only port, an isolated
database, and temporary filesystem storage. Do not point a runtime at a remote host.

From the repository root, run:

```sh
python3 scripts/image-lookup-regressions.py
# Docker instead of Podman:
IMAGE_REGRESSION_RUNTIME=docker python3 scripts/image-lookup-regressions.py
```

The command preserves historical reports under the ignored directory
`artifacts/image-lookup-regressions/<UTC-timestamp>-<run-id>/`. Each run records the
tested commit and dirty-tree flag, `results.trx`, `test-output.log`, a readable
`report.md`, `summary.json`, and structured `progress.jsonl` events flushed during
execution. History remains until explicitly removed. `complete: false` is written
before starting and retained if execution is interrupted before results arrive.
A nonzero exit can indicate an assertion failure or a harness/infrastructure failure;
consult scenario failures, `harness-failure` events and test output to distinguish them.
Report generation never deletes raw test results or progress logs. On an uncatchable
process/host termination, the last checkpoint identifies the incomplete run and its
unique container name in progress; remove only that container after investigation.

The NUnit project can also run directly with `dotnet test
MediaSet.ImageLookup.Regressions/MediaSet.ImageLookup.Regressions.csproj`; use the
Python entry point to produce the full historical artifact set. Scenarios run
sequentially. Each database scenario gets a unique database and image subdirectory;
no scenario clears another scenario's data. Setup failures and normal teardown both clean suite-owned
infrastructure; the runner provides a second exact-name cleanup attempt.

## Fixtures and scenarios

`Fixtures/book-success.json` is a deterministic Open Library readable-book response
and binary PNG cover. Each request entry specifies a stable `Id`, exact `Method`,
`Host` (authority, including port), `Path`, `Query` (including `?`, empty by default),
and UTF-8 `Body` (empty by default). All five must match. Optional `RequestHeaders`
require exact values for the specified request headers. The `Responses` list is
consumed in order; add another response to model a retry. Responses support `Status`,
`ContentType`, `Text` or binary `Base64`, and `Headers` (for example `Retry-After`).
`Failure` supports `network`, `timeout`, and `wait-for-cancellation`; the last waits
for the caller's cancellation token rather than sleeping for a real retry interval.
Never record real credentials in fixtures. Authentication uses synthetic values.

Use `StrictHttpFixtures.Load` for JSON files or `Add` for small contract fixtures.
Pass this transport to **every** HTTP client (including token acquisition and
artwork downloads). There is no socket handler or network fallback. Call
`VerifyComplete` after processing: it fails for unused responses and unexpected
requests even if application code catches the initial exception. Artwork DNS uses
the existing test DNS seam, with a fixed public address and no real DNS lookup;
the production SSRF checks and image decode/validation still run.

Give each NUnit test a stable descriptive method name; the report uses that name
(including parameter values) as its scenario ID. Log scenario start/end and fixture
IDs through `RegressionSuite.Progress`. Reuse the real services from `RegressionSuite`
when adding scenarios. `ProcessAllEntityTypesAsync` executes one bounded production
batch without cron scheduling; `ScenarioTimeProvider.Now` controls eligibility and
persisted attempt time. The scenario's high request rate makes rate delay zero,
without changing production defaults. Keep cancellation deadlines on all scenarios.
The fixture contract tests verify request matching, binary/headers/sequences,
failures, timeout, cancellation, and injected IGDB authentication.

## Book regression coverage (#907)

`BookRegressionScenarios.cs` adds these inline fixture scenarios through real clients,
lookup/background services, MongoDB and filesystem storage:

| Scenario | Verified behavior |
| --- | --- |
| `IsbnArtworkUnavailable` | Open Library identifier miss and cover 404, exact-ISBN Google cover, HTTPS/page-curl normalization, saved image and second-run suppression |
| `TitleAndAuthorFallback` | No ISBN, Open Library miss, exact Google title/author selection rejects other titles/authors |
| `RepeatedUnavailableCover` | Stored URL plus duplicate Open Library candidate URLs download once; the next artwork succeeds |
| `IdentifierAndTitleCandidatesExhausted` | Wrong Google ISBN rejected, identifier and title misses, terminal Mongo state and no retry/files |
| `MatchingTitleWithoutArtwork` | Confirmed Open Library identity with missing artwork, terminal state and no retry/files |
| `ProviderQuota` (both providers) | HTTP 429 with exact HTTP-date reset, persisted retry time, ineligible book untouched, next fresh book deferred without another provider request |
| `TemporaryProviderFailure` (both providers × HTTP 503/network/timeout) | Retry state, Mongo due filter before/at eligibility, recovery, attempt count 1→2, backlog flag, saved image and exact request sequence |

Every scenario asserts the complete consumed fixture order. Unexpected or repeated
requests fail the strict transport even if product code catches the exception.
Lookup hosts use unique `.invalid` authorities so the real host-keyed quota cache
cannot contaminate other scenarios. Artwork still uses real provider URL shapes,
with all responses terminating in the fixture handler. The deterministic clock
advances retry eligibility without sleeping. The Mongo model retains the latest
attempt state and cumulative count; assertions check both before and after recovery.

Known product regression: Google Books transport network/timeout failures currently
persist `ImageLookup.Provider = null`, although the failing provider is GoogleBooks.
Both cases retain active provider assertions and run all recovery/order assertions
inside a multiple-assertion scope. They are expected to fail until a separate product
fix preserves provider attribution. HTTP 503 classification, retry eligibility,
recovery and storage are asserted separately and remain covered. This suite does
not change production behavior to repair the defect. A failing scenario with this
provider assertion is a product failure; `http-unexpected`, unused fixtures or
`harness-failure` events indicate fixture/infrastructure problems to investigate.

## Movie and TV scenarios

`MovieRegressionScenarios.cs` adds 28 cases through the real TMDB client,
movie/TV lookup strategy, background processor, MongoDB and filesystem image
validation/storage. Search, detail, season, collection and artwork responses are
inline fixtures. TMDB requests require the synthetic `Bearer fixture-token`
header; no key, provider configuration, live authentication or sockets are used.
Each scenario has a unique `.invalid` TMDB host to isolate quota caches.

| Scenario | Verified behavior |
| --- | --- |
| `RemakesAndUnrelatedCandidates` | Search/detail title agreement, release year disambiguation, readable selected poster, persisted identity and second-run suppression |
| `EquallyMatchingRemakesWithOnlyOnePoster` / `EquallyMatchingSeries` | Distinct matching identities remain ambiguous even when only one has artwork; no artwork download or file |
| `UnverifiedDetailTitle` | Matching search title with conflicting detail title is rejected |
| `CollectionTitle` | A trilogy requires matching collection identity and three parts; never searches individual films or uses their posters |
| `VerifiedSeason` | Verified season poster takes precedence; absent season poster falls back to verified series artwork |
| `MissingSeasonResource` | Season 404 is terminal for an ordinary season; split-season release may use verified series artwork |
| `ExistingArtworkMissing` | Stored URL 404 falls back to TMDB search/details and saves the verified film poster |
| `VerifiedIdentityWithoutUsableArtwork` | Film poster 404 or TV season/series without posters persist confirmed identity and terminal missing-artwork state |
| `InvalidPosterBytes` | Real image validation rejects invalid bytes, no file is saved, intended invalid-artwork outcome stays actively asserted |
| `TemporaryProviderFailure` (movie/TV × 503/network/timeout) | Correct provider, retry time and due filter, suppressed early retry, recovery at eligibility, cumulative attempts, backlog flag and readable saved image |
| `RejectedSyntheticAuthentication` (movie/TV × 401/403) | Unauthorized state remains retryable with TMDB attribution and no saved files |
| `ProviderQuota` (movie/TV) | Exact HTTP-date reset, no early retry, fresh entity deferred by provider quota cache without another HTTP request |
| `PartialDetailFailure` (movie/TV) | Unavailable competing details prevent accepting an apparently unique surviving candidate |

Assertions read the saved Mongo documents and re-open the stored image through
`IImageService`, checking dimensions, file existence and that exactly one image
was stored. Confirmed-match URLs record fetched artwork; the entity's input
`ImageUrl` is not rewritten by background processing. Terminal outcomes are checked
for persisted reason, fingerprint, attempt count and permanent/no-retry state;
retryable failures verify due-filter behavior with an advanced scenario clock.
Each scenario verifies the exact consumed fixture order and unused/unexpected
requests. All cases run against suite-owned local Mongo and temporary images.

Known product regression: `InvalidPosterBytes` currently persists `MissingArtwork`
after `ImageService` rejects the payload with `ArgumentException`; the later
metadata miss overwrites `InvalidArtwork` in `ImageLookupService`. The intended
`InvalidArtwork` assertion remains active in a multiple-assertion scope, so the
same case also checks no files, terminal state, retry suppression and fixture
consumption. This is a product assertion failure, not a harness failure, and is
outside this coverage change's production-fix scope.

## Game and authentication scenarios (#909)

`GameRegressionScenarios.cs` adds 15 cases through the real `IgdbTokenService`,
`IgdbClient`, game strategy, background processor, MongoDB and local image storage.
The token endpoint requires the exact form body containing dummy client ID/secret
and client-credentials grant. Every IGDB search requires the exact POST query,
`Client-ID`, and scenario-specific Bearer token. Authentication, lookup and artwork
all terminate in `StrictHttpFixtures`; no provider configuration or API keys load.

| Scenario | Verified behavior |
| --- | --- |
| `SuccessfulLookups` | Protocol-relative thumbnail becomes HTTPS large-cover URL; readable image, confirmed provider identity, fingerprint and persisted success; a second entity reuses one authentication request across scopes; completed entities are suppressed |
| `FreshScenario` | A fresh service provider/cache must authenticate again and use its own token after the previous scenario is disposed |
| `NoMatchOrMatchedWithoutArtwork` | Empty results or a confirmed game without artwork produce distinct terminal outcomes, no files, and no repeated lookup |
| `UnavailableArtwork` | Artwork 404 retains confirmed game identity, terminal missing-artwork state, and no files |
| `AuthenticationOrProviderFailure` (auth/lookup × 401/403/503/network/timeout) | Unauthorized versus unavailable outcomes, IGDB attribution, exact retry time, no early HTTP/retry, due-filter transition, recovery, cumulative attempt count and readable saved image |

Every case verifies the complete fixture request order and consumption. Failed
authentication is requested again on recovery; failed lookup reuses its successfully
cached token. Each scenario owns its memory cache and unique `.invalid` lookup/auth
host, avoiding token and provider-quota contamination. Persisted attempt timestamps
and retry eligibility use the scenario clock; stored images are decoded through the
real image service and checked for dimensions, existence and expected file count.
The suite continues to retain the unrelated active product assertions tracked by
#916 (Google Books provider attribution) and #918 (invalid TMDB artwork outcome).

## Music and image validation scenarios (#910)

`MusicRegressionScenarios.cs` adds 14 cases through the real `MusicBrainzClient`,
music lookup strategy, background processor, MongoDB and local filesystem image
storage. Exact entity-search queries, release-detail queries and Cover Art Archive
release/release-group artwork all terminate in strict fixtures. Each scenario owns
its service provider, a unique `.invalid` lookup host, a cancellation deadline and
an explicit request-order/consumption assertion. The production MusicBrainz rate
limiter remains active; no live HTTP, DNS, API keys or production database is used.

| Scenario | Verified behavior |
| --- | --- |
| `ReleaseArtworkOrGroupFallback` | Release artwork success; release 404 falls back to release-group artwork with the wider artwork scope persisted; saved image reopens as a 2×2 bitmap; completed lookup does not repeat |
| `NoMatchOrUnavailableArtwork` | Empty lookup or both release/group artwork 404 yield distinct terminal Mongo outcomes, confirmed identity where applicable, no files and no retry |
| `TemporaryProviderFailure` (search/detail × HTTP 502/network/timeout) | MusicBrainz attribution, retry time/due filter, suppressed early lookup, recovery at eligibility, cumulative attempts, backlog flag and readable stored image |
| `InvalidArtwork` (oversized/corrupt/non-image/pixels) | Real download/validation rejection with exact `ImageFailureReason`, no files or Mongo records from the direct validation probe, then real background processing and active intended `InvalidArtwork` persisted-state assertions |

Validation uses a 1 MiB configured limit and a 1 MiB + 1 byte response, a truncated
PNG, an HTML response, and a valid 2×2 PNG exceeding a configured three-pixel limit.
Each payload first traverses the real image service to assert `TooLarge`,
`UnsupportedFormat` or `PixelLimit`, then traverses the same pipeline through music
background processing. Rejected artwork must leave no saved image, preserve the
confirmed MusicBrainz release, and be terminal with no second attempt.

The four invalid-artwork cases currently expose the same shared outcome overwrite
tracked in #918: `ImageLookupService` replaces the validation `InvalidArtwork` result
with `MissingArtwork` after exhausting matching metadata candidates. Those intended
assertions remain active inside multiple-assertion scopes, so no-file, Mongo state,
retry suppression and fixture-consumption checks still run. This is a known product
assertion failure, not a harness failure; production behavior is unchanged.

## Multi-pass processing and recovery (#911)

`RecoveryRegressionScenarios.cs` exercises successive production batches using real
Mongo collections, a controlled clock and strict book/artwork fixtures:

| Scenario | Verified behavior |
| --- | --- |
| `SuccessiveFailuresAndDueBoundaries` | Three failed attempts, exponential retry capped at 15 minutes, no work one millisecond before eligibility, retry exactly at eligibility, fourth-attempt readable-image recovery and completed suppression |
| `IneligibleState` (eight outcomes) | Terminal/success outcomes remain excluded even with a past retry date; unavailable state without an eligibility date remains untouched |
| `ActiveLookupCancellation` (fresh/retry × lookup/artwork) | Wait for a fixture request to become active, cancel it, assert cancellation propagates and the whole persisted document remains unchanged with no file |
| `BoundedBatch` | A two-item batch reserves a fresh slot and a due backlog slot, preserves requeue identity and attempt history, processes the remaining item next pass, then reports zero work |
| `RuntimeBoundary` | Advancing controlled time during artwork stops processing before the next entity once the configured runtime is exceeded |
| `IsolatedDatabaseAndImages` | An empty scenario cannot read or mutate a separate database with the same entity ID, or another scenario's filesystem sentinel |

`pass-start`/`pass-finished` events retain clock, batch settings, request history and
persisted documents after each pass. Structured application logs retain typed totals
and per-run attempt diagnostics; tests assert bounded counts and that diagnostics
reset between runs. `scenario-snapshot` captures all four collections and image paths
before teardown, including failed assertions. Each progress record has a run ID and
scenario ID, and scenario-start records identify its unique database and image path.
These logs survive disposal of temporary databases/images.

Run just this coverage with `dotnet test MediaSet.ImageLookup.Regressions --filter
FullyQualifiedName~ProcessRecovery`. The Python runner contract checks run without
containers using `python3 -m unittest discover -s scripts -p
test_image_lookup_regressions.py`. They simulate assertion failures, interruption,
invalid/incomplete results and report-finalization failure, then start another run
in the same history directory and verify every previous artifact remains byte-for-byte
unchanged while container, image directory, artifact directory and run ID are distinct.
