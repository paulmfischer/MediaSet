# Image lookup retries and historical recovery

The scheduled worker attempts new items and due retries only when `CoverImage` is absent.
Successful attempts and terminal outcomes are excluded. Quota, network, timeout and provider
server errors use exponential backoff: `RetryInitialDelayMinutes * 2^(AttemptCount - 1)`,
capped at `RetryMaxDelayMinutes`. Defaults are 60 minutes and seven days. Eligibility does
not wake the worker: processing happens on the next configured schedule after that time.
Each media-type batch reserves at least half its slots for never-attempted items; retries
use oldest eligible time first. Empty slots are filled from the other cohort.

State retains attempt count, last outcome, next eligible time, searchable metadata fingerprint,
strategy version and requeue operation ID. No match, missing artwork (including artwork HTTP
404), missing metadata and invalid artwork are terminal for the attempted inputs. Cancellation
does not complete an attempt or write a no-match. Provider clients retain their foreground
behavior; a scoped failure collector prevents swallowed provider errors from becoming a
background no-match.

## Selective historical recovery

This is an explicit operator action, not an automatic database migration. Deploying this code
does not requeue historical failures. Obtain production authorization before using this endpoint
against production. No production recovery or deployment was performed as part of issue #607.

Send `POST /images/lookup/{entityType}/requeue` (`books`, `movies`, `games`, `musics`) with:

```json
{
  "operationId": "music-title-fallback-2026-10",
  "attemptedBefore": "2026-10-23T00:00:00Z",
  "batchSize": 100,
  "afterId": null,
  "includeChangedTerminal": true,
  "entityIds": ["000000000000000000000001"]
}
```

`entityIds` is optional; omit it to scan failures of that media type before the cutoff.
Use a fixed cutoff and operation ID for the entire operation. Batch size is the number of
records scanned, from 1 to 500. The response contains `scanned`, `queued`, `nextCursor` and
`complete`. Repeat with `afterId` set to `nextCursor` until `complete` is true, including when
`queued` is zero. The cursor advances past skipped unchanged terminal failures as well.
Save the cursor after each successful response. If interrupted before receiving a response,
repeat the last request: already queued/completed records with that operation ID are excluded.
If interrupted while retry processing runs, the operation ID remains on the attempt state.

Recoverable historical failures may be queued directly. Terminal failures require explicit
`includeChangedTerminal: true` and a changed searchable metadata fingerprint or strategy version.
Historical records lacking the new fingerprint/version qualify for this reassessment. Increment
`BackgroundImageLookupConfiguration:StrategyVersion` when deploying lookup strategy improvements
that should permit another assessment. Edits to the fields marked as lookup identifiers or to
`ImageUrl` change the fingerprint. This endpoint does not erase failure history or attempt counts.
Successful/manual images remain excluded, including images added between scanning and updating.
Retries are queued for scheduled processing and still obey rate and runtime limits.

## Recovery reporting

Structured worker logs report `Cohort=NewItem` or `Cohort=BacklogRecovery` with each outcome.
Per-media-type summaries contain `NewProcessed`, `NewSucceeded`, `BacklogProcessed`, and
`BacklogRecovered`. A first attempt is new-item work; subsequent or explicitly requeued work
is backlog recovery. Compare successes within each cohort rather than reporting a combined
new-item success rate. Stored attempts also retain `IsBacklogRecovery` and `RequeueOperationId`.

## Acceptance verification

Run the relevant regression tests using the repository's standard backend command:

```sh
dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj --filter "FullyQualifiedName~Features.Images"
```

`ImageRetryPolicyTests` verifies configured/capped backoff, terminal scheduling, due selection,
metadata/version reassessment and separate backlog provenance. `ImageRetryProcessingTests`
demonstrates selective cursor batches, resumability, unchanged terminal/manual/success exclusion,
new-item reservation and cancellation without persistence. `ImageProviderFailureTests` exercises
the HTTP client/strategy/service path for 429, 500, 503 and timeout plus concurrent scope isolation.
`ImageLookupServiceTests` covers manual/success exclusion, missing artwork versus no match and
cancellation during download. These use mocks or in-memory HTTP handlers and no production data.
