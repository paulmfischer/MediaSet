# Image lookup regressions

This separate NUnit project exercises real MongoDB access, provider clients, lookup
strategies, image validation, background processing and local filesystem storage.
It is deliberately outside the normal unit-test solution and CI. Product defects
must keep failing assertions; book fallback and quota scenarios cover #907,
movie/TV scenarios cover #908, game/authentication scenarios cover #909, and further
scenarios belong to #910–912.

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
sequentially. The database scenario fixture clears all media collections and removes
images before each test. Setup failures and normal teardown both clean suite-owned
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
