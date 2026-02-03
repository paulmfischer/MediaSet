# Background Image Lookup Service Configuration Guide

This guide explains how to configure MediaSet.Api's background image lookup service, which automatically finds and downloads cover images for entities that are missing them.

## What is the Background Image Lookup Service?

The Background Image Lookup Service is a scheduled background process that runs periodically to find and download cover images for your Books, Movies, Games, and Music entities that don't already have cover images. Instead of manually adding images for each item, this service automatically:

1. Finds entities without cover images
2. Uses their identifiers (ISBN for books, UPC/EAN for other media) to look up images from external APIs
3. Downloads and saves the images
4. Updates the entities with their new cover images

This reduces manual data entry and ensures your media collection has complete visual coverage.

## How It Works

The service operates on a configurable schedule using cron expressions:

1. **Scheduled Execution**: Runs at specified intervals (e.g., daily at 2 AM)
2. **Batch Processing**: Processes a limited number of entities per run to avoid overwhelming external APIs
3. **Proportional Allocation**: Distributes processing across all enabled entity types (Books, Movies, Games, Music) proportionally
4. **Rate Limiting**: Respects external API rate limits with configurable requests per minute
5. **Runtime Management**: Stops processing after a maximum runtime to prevent long-running operations
6. **Permanent Failure Tracking**: Remembers entities that have no available images to avoid repeated lookup attempts

The service only processes entities that:
- Don't have a cover image
- Haven't been previously looked up (no `ImageLookup` metadata)
- Have the required identifiers (ISBN for books, barcode for others)

## Prerequisites

For the background image lookup service to work, you need:

- At least one external API configured for metadata lookup:
  - **Books**: OpenLibrary API (configured via `OpenLibraryConfiguration`)
  - **Movies**: TMDB API (configured via `TmdbConfiguration`)
  - **Games**: GiantBomb API (configured via `GiantBombConfiguration`)
  - **Music**: MusicBrainz API (configured via `MusicBrainzConfiguration`)
- UpcItemDb API (optional but recommended for better barcode lookup accuracy)

See the main MediaSet documentation for configuring these external APIs.

## Configuration Options

MediaSet.Api supports the following background image lookup configuration options through environment variables in `docker-compose.prod.yml`.

### BackgroundImageLookupConfiguration__Enabled

**Purpose**: Enables or disables the background image lookup service.

**Type**: Boolean string

**Values**: `"true"` or `"false"`

**Default**: `false` (disabled)

**Example**:
```yaml
BackgroundImageLookupConfiguration__Enabled: "true"
```

When set to `"true"`, the background service will start and run on the configured schedule. When `"false"` or omitted, the service is disabled and will not run.

### BackgroundImageLookupConfiguration__Schedule

**Purpose**: Defines when the background service runs using a cron expression.

**Type**: String (cron expression)

**Format**: Standard cron format (minute hour day month weekday)

**Default**: `"0 2 * * *"` (daily at 2:00 AM UTC)

**Minimum Interval**: 1 hour (enforced in production environments only)

**Examples**:
```yaml
# Daily at 2:00 AM UTC
BackgroundImageLookupConfiguration__Schedule: "0 2 * * *"

# Every 6 hours
BackgroundImageLookupConfiguration__Schedule: "0 */6 * * *"

# Weekly on Sunday at 3:00 AM UTC
BackgroundImageLookupConfiguration__Schedule: "0 3 * * 0"

# Twice daily (2 AM and 2 PM UTC)
BackgroundImageLookupConfiguration__Schedule: "0 2,14 * * *"
```

**Notes**:
- Uses UTC timezone
- In production, intervals less than 1 hour are rejected
- In development environments, any valid cron expression is allowed for testing
- See [crontab.guru](https://crontab.guru) for help creating cron expressions

### BackgroundImageLookupConfiguration__MaxRuntimeMinutes

**Purpose**: Maximum runtime in minutes per scheduled run. The service stops processing when this limit is reached.

**Type**: Integer

**Default**: `60` (1 hour)

**Range**: 1 to unlimited (practical maximum: 1440 minutes / 24 hours)

**Example**:
```yaml
BackgroundImageLookupConfiguration__MaxRuntimeMinutes: "120"
```

**When to adjust**:
- **Increase** if you have a large backlog of entities and want each run to process more
- **Decrease** if you want to limit resource usage per run
- Consider your API rate limits and the number of entities you need to process

**How it works**:
The service checks the elapsed time before processing each entity. When the maximum runtime is reached, it completes the current entity and stops, even if the batch size hasn't been reached.

### BackgroundImageLookupConfiguration__BatchSize

**Purpose**: Total number of entities to process per run, distributed proportionally across enabled entity types.

**Type**: Integer

**Default**: `25`

**Range**: 1 to unlimited (practical maximum: consider API rate limits)

**Example**:
```yaml
BackgroundImageLookupConfiguration__BatchSize: "50"
```

**How batch allocation works**:
The batch size is divided proportionally across all entity types that have lookup strategies available. For example, with a batch size of 25:
- If Books, Movies, and Games are configured (3 types): ~8 books, ~8 movies, ~9 games
- If only Books and Movies are configured (2 types): ~12 books, ~13 movies

**When to adjust**:
- **Increase** if you have many entities and want faster processing
- **Decrease** if you're hitting API rate limits or want lighter processing
- Consider your `RequestsPerMinute` setting and external API quotas

### BackgroundImageLookupConfiguration__RequestsPerMinute

**Purpose**: Maximum number of API requests per minute to external services (rate limiting).

**Type**: Integer

**Default**: `30`

**Range**: 1 to 60 (respect external API rate limits)

**Example**:
```yaml
BackgroundImageLookupConfiguration__RequestsPerMinute: "20"
```

**How rate limiting works**:
The service calculates the delay between requests to stay within the configured rate. For example:
- `RequestsPerMinute: 30` → ~2 second delay between requests
- `RequestsPerMinute: 60` → ~1 second delay between requests
- `RequestsPerMinute: 10` → ~6 second delay between requests

**When to adjust**:
- **Decrease** if external APIs are rate-limiting you
- **Increase** if you want faster processing and APIs allow it
- Check the rate limit documentation for each external API you use:
  - OpenLibrary: Generally permissive (use appropriate contact email)
  - TMDB: 40 requests per 10 seconds (~240/minute)
  - GiantBomb: 200 requests per hour (~3/minute)
  - MusicBrainz: 1 request per second (60/minute)

**Recommendation**: Set to the lowest rate limit of the APIs you're using (e.g., if using GiantBomb, set to `3`).

## Complete Configuration Example

Here's a complete example of background image lookup configuration in `docker-compose.prod.yml`:

```yaml
services:
  mediaset-api:
    image: ghcr.io/paulmfischer/mediaset-api:latest
    environment:
      # Enable background image lookup service
      BackgroundImageLookupConfiguration__Enabled: "true"

      # Run daily at 2 AM UTC
      BackgroundImageLookupConfiguration__Schedule: "0 2 * * *"

      # Maximum runtime of 2 hours per run
      BackgroundImageLookupConfiguration__MaxRuntimeMinutes: "120"

      # Process 50 entities per run (distributed across media types)
      BackgroundImageLookupConfiguration__BatchSize: "50"

      # Maximum 20 API requests per minute (conservative for GiantBomb)
      BackgroundImageLookupConfiguration__RequestsPerMinute: "20"
```

## Default Configuration

If not specified, MediaSet.Api uses the following defaults from `appsettings.json`:

```json
{
  "BackgroundImageLookupConfiguration": {
    "Enabled": false,
    "Schedule": "0 2 * * *",
    "MaxRuntimeMinutes": 60,
    "BatchSize": 25,
    "RequestsPerMinute": 30
  }
}
```

## Monitoring and Logs

The background image lookup service logs all its activities with the application identifier `MediaSet.Api.Processor.BackgroundImage`. You can filter logs by this identifier to monitor the service.

**Key log events**:
- Service start/stop
- Next scheduled run time
- Number of entities found for processing
- Successful image downloads
- Failed lookups with reasons
- Run completion summary (processed, succeeded, failed counts)

**Example log filtering** (if using Seq):
```
Application = "MediaSet.Api.Processor.BackgroundImage"
```

## Understanding Image Lookup Results

After processing, each entity will have an `ImageLookup` metadata object that tracks:

- `LookupAttemptedAt`: When the lookup was last attempted
- `FailureReason`: Why the lookup failed (if it did)
- `PermanentFailure`: Whether this is a permanent failure (won't retry)

**Permanent failures** occur when:
- Entity lacks the required identifier (ISBN/barcode)
- No lookup strategy is available for this media type
- Multiple retries have failed (future enhancement)

**Temporary failures** occur when:
- External API is unavailable (will retry on next run)
- Network errors (will retry on next run)
- API rate limiting (will retry on next run)

Entities with permanent failures will not be processed again unless you manually clear the `ImageLookup` metadata.

## Troubleshooting

### Service not starting

1. Verify `BackgroundImageLookupConfiguration__Enabled` is set to `"true"` (string, not boolean)
2. Check the cron expression is valid (use [crontab.guru](https://crontab.guru))
3. In production, ensure the schedule interval is at least 1 hour
4. Check MediaSet.Api logs for validation errors on startup

### No images being downloaded

1. Verify at least one external API is configured (OpenLibrary, TMDB, GiantBomb, or MusicBrainz)
2. Check that entities have the required identifiers (ISBN for books, barcodes for others)
3. Review logs for "No lookup strategy available" messages
4. Verify external API credentials are correct and not expired
5. Check logs for API errors or rate limiting

### Images downloaded but not showing in UI

1. Verify the `CoverImage` property is being set (check the entity in MongoDB)
2. Ensure the image storage path is correct and accessible
3. Check that the static file middleware is configured correctly
4. Review logs for image download errors

### Too many/too few entities processed per run

- Adjust `BatchSize` to change the total number of entities processed
- Adjust `MaxRuntimeMinutes` if the service is stopping before completing the batch
- Check logs to see how many entities are being found for each media type

### API rate limiting errors

1. Reduce `RequestsPerMinute` to respect API limits
2. Reduce `BatchSize` to process fewer entities per run
3. Increase schedule interval to run less frequently
4. Check external API documentation for rate limit specifics

### Service runs too frequently or not frequently enough

- Adjust the `Schedule` cron expression
- Verify the cron expression matches your intended schedule (use [crontab.guru](https://crontab.guru))
- Remember the schedule uses UTC timezone

## Performance Considerations

**Batch Size vs. Rate Limiting**:
- Larger batch sizes mean more entities processed per run, but require more time
- Rate limiting determines how fast you can process entities
- Balance both based on your external API quotas

**Example calculation**:
- BatchSize: 50
- RequestsPerMinute: 20
- Expected time: ~2.5 minutes (50 requests ÷ 20 requests/min)

**Recommended starting configuration**:
- Small collections (< 1000 items): BatchSize=25, RequestsPerMinute=30, Schedule=daily
- Medium collections (1000-10000 items): BatchSize=50, RequestsPerMinute=20, Schedule=daily
- Large collections (> 10000 items): BatchSize=100, RequestsPerMinute=20, Schedule=twice daily

Adjust based on your external API rate limits and how quickly you want to backfill images.

## Additional Resources

- [Cron Expression Generator](https://crontab.guru)
- [OpenLibrary API Documentation](https://openlibrary.org/developers/api)
- [TMDB API Documentation](https://developers.themoviedb.org/3)
- [GiantBomb API Documentation](https://www.giantbomb.com/api/)
- [MusicBrainz API Documentation](https://musicbrainz.org/doc/MusicBrainz_API)
