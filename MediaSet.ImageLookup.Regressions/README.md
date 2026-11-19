# Image lookup regressions

This separate NUnit project exercises real MongoDB access, provider clients, lookup
strategies, image validation, background processing and local filesystem storage.
It is deliberately outside the normal unit-test solution and CI. Product defects
must keep failing assertions; book fallback and quota scenarios cover #907; other product scenarios belong to #908–912.

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
