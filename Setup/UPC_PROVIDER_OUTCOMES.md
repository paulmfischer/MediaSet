# Provider outcomes and UPC quota investigation (#612)

## Evidence and attribution limits

The issue's October 23, 2026 production Seq review reported five UPC failures from
02:00:12–02:01:00 America/Chicago, each logging daily quota exhaustion with about
11 hours until reset. Example UPC: `883929293513`; trace:
`32d46cf8566d87e1d053d0a8e1c20790`. Those failures previously became empty UPC
results and then generic missing-image messages. The broader October 16–23 review
covered eight nightly runs (87 entities, two successes) and 177 recorded image
failures. These are the issue's supplied observations, not a new production audit;
the deployed revision was not independently verified.

The reviewed source used `/prod/trial/lookup`, including the short burst retry.
UPC documents 100 **combined lookup/search requests per day** for Explorer, a
provider-defined reset, and a distinct burst window. Local successful-request
counters cannot attribute the provider's whole quota: they omit unsuccessful
requests and other processes, users, deployments or clients sharing the trial
allocation. A 02:00 local cron boundary is not evidence that the provider's quota
resets at local midnight. Eleven hours remaining implies approximately 13:00 CDT
for those responses, but does not establish the reset policy or who consumed it.

Before recommending a schedule or subscription change, collect one complete
provider quota window across application instances, including lookup/search
request timestamps, endpoint/plan, status, remaining/reset headers and caller
category. Correlate other consumers with the provider's usage accounting where
available. Do not log keys or full metadata payloads. Remaining gaps: prior quota
consumers, trial allocation identity, all caller volumes, deployment revision,
and whether errors/retries were counted by the provider. No schedule, subscription,
production configuration or deployment change is part of this issue.

## Behavior

Background lookup preserves quota-limited, unauthorized, provider-unavailable,
no-match, missing-artwork, ambiguous-match, and download-failure outcomes. Valid
artwork from an independent path can succeed even when another provider failed.
Matching/ranking and new identifier/metadata fallbacks remain in #608–613.

Quota, authentication, unavailable and download failures remain retryable.
`Retry-After` seconds/date and `X-RateLimit-Reset` Unix seconds are retained; if
both exist the later time is used. UPC short burst pauses still retry once;
successful retries do not poison the lookup or defer the provider. Long/exhausted
quota responses defer subsequent UPC clients sharing an endpoint/plan/key until
reset without blocking unrelated providers. Other background providers retain
HTTP failure/reset information at their HTTP boundary and defer requests to an
exhausted host while allowing independent hosts. Missing/invalid reset information uses
a five-minute provider cooldown. This cooldown is process-local; provider headers
remain authoritative after process restart. Entity eligibility is persisted.

Image stats now include outcome, provider(s), attempted media/identifier strategy,
attempt count, last-attempt timestamp and next eligibility. Legacy records display
unknown diagnostics. Background runs log bounded entity attempt diagnostics and
a structured run summary without full metadata. Existing terminal records retain
their #607 selective requeue rules; this change does not automatically requeue
historical failures.

## Explicit UPC plans

`UpcItemDbConfiguration__Plan` defaults to `Trial` and selects
`/prod/trial/lookup` without paid headers, even when a key is present. `Paid`
requires `UpcItemDbConfiguration__ApiKey` and selects `/prod/v1/lookup` with
`user_key` and `key_type: 3scale` headers on both initial and burst retry requests.
Paid configuration is opt-in; no paid requests were made during verification.

Primary sources checked October 23, 2026:
[UPC API](https://www.upcitemdb.com/api/) (plans, paths, `user_key`, burst and
reset headers), [Explorer](https://www.upcitemdb.com/api/explorer),
[provider getting started](https://www.upcitemdb.com/wp/docs/main/development/getting-started/)
(paid `key_type: 3scale`).
