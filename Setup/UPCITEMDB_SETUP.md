# UPCItemDb integration — end-user how-to

This document explains how to enable UPCItemDb configuration to allow for UPC lookups. This is needed if you want to lookup Books/Games/Movies by barcode. These entities will first call out to UPCItemDb and get the Title from UPCItemDb and then make a call to their respective metadata service for more information. This will only explain the configuration to enable for UPCItemDb. To enable Book/Game/Movie integrations, see their respective setup documents.

[Book OpenLibrary Setup](OPENLIBRARY_SETUP.md)

[Game IGDB Setup](IGDB_SETUP.md)

[Movie TMDB Setup](TMDB_SETUP.md)

## Rate Limiting

UPCItemDb enforces strict rate limits to prevent abuse:

- **Free Plan**: 100 requests per day, 6 requests per minute burst limit
- **DEV Plan**: 20,000 requests per day, higher burst limits

MediaSet includes intelligent rate limit handling to prevent errors and optimize API usage. The client automatically:
- Throttles proactively to stay below limits
- Pauses and retries when burst limits are hit (up to 65 seconds)
- Fails gracefully when daily limits are exceeded
- Logs detailed rate limit information for monitoring

For more details on UPCItemDb rate limits, see: https://www.upcitemdb.com/wp/docs/main/rate-limiting/

## Configuration

1) Configure MediaSet (recommended: edit `docker-compose.prod.yml`)

    - Open `docker-compose.prod.yml` and locate the `mediaset-api` service environment section.
    - Find the commented UpcItemDb entries and uncomment them
    - The default configuration is optimized for the free tier (100 req/day, 6 req/min):

    ```yaml
    # UpcItemDb configuration (for barcode lookup)
    # Free tier: 100 requests/day, 6 requests/minute burst limit
    UpcItemDbConfiguration__BaseUrl: "https://api.upcitemdb.com/"
    UpcItemDbConfiguration__Timeout: "10"
    UpcItemDbConfiguration__MaxRequestsPerMinute: "5"     # Buffer below 6/min limit
    UpcItemDbConfiguration__MaxRequestsPerDay: "90"       # Buffer below 100/day limit
    UpcItemDbConfiguration__MinDelayBetweenRequestsMs: "1000"
    UpcItemDbConfiguration__MaxRetryPauseSeconds: "65"
    ```

    - If you have a paid plan (DEV tier or higher), adjust the rate limits accordingly:
      - DEV tier: 20,000 requests/day, higher burst limit
      - Increase `MaxRequestsPerDay` and `MaxRequestsPerMinute` to match your plan
      - Reduce `MinDelayBetweenRequestsMs` for faster lookups (e.g., 100ms)

    - **Important**: If enabling `BackgroundImageLookupConfiguration`, ensure `RequestsPerMinute` is set to 5 or lower to respect UpcItemDb limits:

    ```yaml
    # Background image lookup service configuration
    BackgroundImageLookupConfiguration__Enabled: "true"
    BackgroundImageLookupConfiguration__Schedule: "0 2 * * *"
    BackgroundImageLookupConfiguration__MaxRuntimeMinutes: "60"
    BackgroundImageLookupConfiguration__BatchSize: "25"
    BackgroundImageLookupConfiguration__RequestsPerMinute: "5"  # Must be ≤5 for free tier
    ```

    - After updating `docker-compose.prod.yml` (or your `.env`), restart the stack:

    ```bash
    docker compose -f docker-compose.prod.yml up -d --build
    ```

## Rate Limit Behavior

UPCItemDb enforces two types of rate limits:

### Burst Limit (Per-Minute)
- **Free Plan**: 6 requests per minute
- **Behavior**: When exceeded, the API returns a 429 status with an `X-RateLimit-Reset` header
- **MediaSet Response**: Automatically pauses for up to 65 seconds and retries the request once
- **Logs**: Warning messages like "UpcItemDb burst rate limit hit for {code}, pausing for 45.2 seconds..."

### Daily Limit
- **Free Plan**: 100 requests per day
- **Behavior**: When exceeded, the API returns a 429 status with a long reset time
- **MediaSet Response**: Returns null immediately without retry (no point waiting hours)
- **Logs**: Error messages like "UpcItemDb daily rate limit exceeded for {code}, cannot retry (reset in 8.5 hours)"

### Proactive Throttling
MediaSet tracks requests client-side and throttles proactively to avoid hitting limits:
- Enforces minimum delay between requests (default 1000ms)
- Pauses when approaching per-minute limit
- Throws exception when daily limit reached
- Logs detailed rate limit information on every request

## Rate Limit Monitoring

MediaSet logs comprehensive rate limit information on every UPCItemDb request:

```
[Information] UpcItemDb rate limit status for 012345678901:
              Limit=100, Remaining=85, Reset=1644505200,
              LocalMinute=5, LocalDay=42
```

- **Limit**: Your rate limit ceiling (from API headers)
- **Remaining**: Requests remaining in current window (from API headers)
- **Reset**: When the limit resets, Unix timestamp (from API headers)
- **LocalMinute**: Requests made in current minute (client-side tracking)
- **LocalDay**: Requests made today (client-side tracking)

Monitor these logs to understand your API usage and adjust configuration if needed.

## Verification

2) Verification for barcode lookup

    - After starting MediaSet, perform a book/movie/game lookup by barcode from the Add/Edit screen.
      - The API logs should show UpcItemDb calls with rate limit headers
      - The Add/Edit form will be populated with metadata if a match is found

    - Check logs for rate limit information:
      ```
      [Information] UpcItemDb rate limit status for 012345678901:
                    Limit=100, Remaining=85, Reset=1644505200,
                    LocalMinute=5, LocalDay=42
      ```

    - If you see "burst rate limit hit" warnings: This is normal and automatic - the client will pause and retry
    - If you see "daily rate limit exceeded" errors: You've used your daily quota and need to wait for reset

## Troubleshooting

### Rate Limit Errors

**Burst limit warnings:**
- These are automatically handled - the client pauses and retries
- If you see frequent burst limit warnings, consider:
  - Increasing `MinDelayBetweenRequestsMs` (e.g., 2000ms)
  - Reducing `MaxRequestsPerMinute` (e.g., 3-4)
  - Upgrading to a paid plan for higher limits

**Daily limit errors:**
- You've exhausted your daily quota (100 requests for free tier)
- Wait until the reset time shown in logs
- Consider:
  - Upgrading to a paid plan (DEV tier: 20,000/day)
  - Reducing background service usage
  - Adjusting `MaxRequestsPerDay` to leave buffer for manual lookups

### Background Image Lookup Service Conflicts

The BackgroundImageLookupService can consume significant API quota if not configured carefully:

**Recommended settings for free tier:**
- `BatchSize`: 10-25 entities per run (default: 25)
- `RequestsPerMinute`: 5 or lower (default: 5)
- `Schedule`: Run less frequently, e.g., once per day at off-peak hours
  - Example: `"0 2 * * *"` (2 AM daily)
  - Avoid running every 15 minutes on free tier
- `MaxRuntimeMinutes`: Limit total runtime to prevent quota exhaustion

**Monitoring usage:**
- Check logs for daily request counts: `LocalDay` property in rate limit logs
- Calculate expected usage: `BatchSize × runs per day = daily requests`
- Example: 25 entities × 4 runs = 100 requests (entire free quota!)
- Leave buffer for manual lookups: Set `MaxRequestsPerDay` to 70-90 instead of 100

### No Results Found

If lookups return no results but rate limits are fine:
- UPCItemDb has limited coverage, especially for non-US products
- Try looking up the product manually at https://www.upcitemdb.com/
- Consider alternative lookup methods (manual metadata entry, ISBN for books, etc.)

### Configuration Validation Errors

If the API fails to start with configuration errors:
- Check that all rate limit values are positive integers
- Ensure `MaxRequestsPerMinute > 0` and `MaxRequestsPerDay > 0`
- Verify `MinDelayBetweenRequestsMs >= 0` (can be 0 for paid plans)
- Confirm `MaxRetryPauseSeconds > 0` (default: 65)
