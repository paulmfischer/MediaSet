# Image fallback and confidence evaluation (#608)

Evaluated October 23, 2026. Read-only IGDB title searches were performed with the
existing local provider configuration. No production entity, cover, retry state,
or service was changed. Production records' stored platform/year values were not
verified, so these results demonstrate matching behavior for the explicit contexts
below rather than completed backlog recovery.

| Title and explicit context | Live provider evidence | Matching result and rationale |
| --- | --- | --- |
| Super Mario 64 DS; Nintendo DS (alias DS); 2004 | IGDB 22301, exact title, 2004, Nintendo DS and Wii U, cover present. Other results included mods such as New Super Mario 64 DS, Online, and Sub Zero. | Eligible game artwork: complete title, year, and DS platform agree. DS is retained as part of the title. A first-listed Wii U platform cannot exclude the DS release because all provider platforms are retained. |
| Red Dead Redemption 2; PlayStation 4 (alias PS4); 2018 | IGDB 25076, exact title, 2018, Stadia/PS4/PC/Xbox One, cover present. Alternatives included Ultimate Edition 103206, Special Edition 103205, Collector's Box 103207, bundle 262052 (2023), and original Red Dead Redemption 434. | Eligible game artwork: full title, year, and PS4 agree. Different titles, special editions, and bundles cannot win because they have artwork. |
| The Last of Us; PlayStation 3 (alias PS3); 2013 | First search result was Part II 26192 (2020, PS4/PS5). Original 1009 is exact title, 2013, PS3, cover present. Other results included Part I 204350 (2022, PC/PS5), Remastered 6036 (2014, PS4), Complete 340213 (2025, PS5), and Left Behind 11790. | Eligible game artwork from 1009: complete title, year, and PS3 agree. First-result order cannot substitute Part II. |
| The Last of Us; PlayStation 4; 2013 | Same live result set. Original 1009 is PS3; Remastered 6036 has a different title/year. | Unresolved. No candidate satisfies the stored title, platform, and year together. A PS4 cover is not accepted merely because it is available. |

All three eligible covers are **game-level provider artwork**. Region, packaging,
budget reprint, and exact physical edition are unverified. The provenance persisted
by the lookup records this limitation explicitly.

## Deterministic validation

`ImageFallbackConfidenceTests` uses synthetic provider responses whose identity
fields reflect the live observations above. It drives the real game strategy and
image orchestration with a missed UPC, checks metadata fallback and provider IDs,
and includes the unresolved PS4 case. Fixture artwork URLs and downloads are mocked;
these tests do not prove a live image download or recovery of a production record.

Additional tests cover a coverless wrong-year first result, equally matching
identities with only one image, creator conflicts, bundles/edition qualifiers,
rejected downloads, and persistence/reuse of provider IDs after download failure.

Existing image retry policy/processing tests protect #607's backoff, requeue,
cancellation, and successful/manual image behavior. The strategy default is now
version 2; historical terminal failures require the existing explicit selective
requeue operation. No requeue has been run as part of this work.

## Existing test assertions corrected during validation

`ImageProviderFailureTests` contained stale assertions against the unchanged
provider failure code: HTTP 429/500/503 are expected as `TransientFailure`, but the
current classifier returns `QuotaLimited`/`ProviderUnavailable`; its timeout-message
assertion expects the swallowed inner message; its concurrent scope test expects a
raw `TimeoutException`, but `LookupFailureScope.Record` wraps it in
`ProviderLookupException`. After the full-suite verification hook exposed these
failures, the assertions were updated to check the explicit outcome and retry
eligibility, and the scope test now checks the wrapped outcome and original message.
Each HTTP fixture also uses a unique mock origin so a 429's process-wide quota
cooldown cannot leak into a later 500, 503, timeout, or recovery test.
Production provider behavior was not changed. The focused `ProviderOutcomeTests`
verify the explicit current classifications separately.
