# Seq Logging Configuration Guide

This guide explains how to configure MediaSet to send logs to a Seq server for centralized structured logging. Both `MediaSet.Api` (.NET) and `MediaSet.Remix` (Node.js) log directly to Seq and are distinguished in Seq by the `Application` property (`MediaSet.Api` vs `MediaSet.Remix`).

## Prerequisites

- A running Seq server (see [Seq documentation](https://docs.datalust.co/docs) for setup)
- MediaSet.Api configured with the production docker-compose file

## Configuration Options

MediaSet.Api supports the following Seq logging configuration options through environment variables in `docker-compose.prod.yml`.

### ExternalLogging__Enabled

**Purpose**: Enables or disables external logging to Seq.

**Type**: Boolean string

**Values**: `"true"` or `"false"`

**Default**: `false` (disabled)

**Example**:
```yaml
ExternalLogging__Enabled: "true"
```

When set to `"true"`, MediaSet.Api will send structured logs to the configured Seq server. When `"false"` or omitted, logs are only written to console output.

### ExternalLogging__SeqUrl

**Purpose**: Specifies the URL of the Seq server ingestion endpoint.

**Type**: String (URL)

**Format**: `http://hostname:port` or `https://hostname:port`

**Default**: `http://localhost:5341` (if not specified)

**Example**:
```yaml
ExternalLogging__SeqUrl: "http://seq.example.com:5341"
```

**Configuration patterns**:
- Docker Compose (same network): Use the Seq service name, e.g., `http://seq:80`
- External Seq server: Use the full hostname and port, e.g., `http://192.168.1.100:5341`
- HTTPS-enabled server: Use `https://` protocol, e.g., `https://seq.example.com`

### ExternalLogging__SeqApiKey

**Purpose**: Optional Seq ingestion API key. When set, the API sends it with every event and MediaSet.Remix sends it
as the `X-Seq-ApiKey` header. When missing or blank no key is sent and behaviour is unchanged.

**Type**: String (secret)

**Example** (placeholder, never commit a real key):
```yaml
ExternalLogging__SeqApiKey: "${SEQ_API_KEY}"
```

Docker Compose substitutes `${SEQ_API_KEY}` from the compose environment, so the variable must be defined in an `.env`
file next to the compose file or passed with `--env-file`; otherwise it expands to an empty string and no key is sent.
A key containing control characters (such as a stray newline) is treated as absent.

The API reads `ExternalLogging:SeqApiKey` (user-secrets locally, `ExternalLogging__SeqApiKey` in containers) and
Remix reads the environment variable `ExternalLogging__SeqApiKey`. The key is never logged.

## Ingestion API keys per environment

Development and Production share one Seq instance. Seq workspaces only group signals, queries and dashboards;
they do not partition events, so separation is by ingestion API key instead: one key per environment, each applying an
`Environment` property.

1. In Seq, open **Settings > API Keys > Add API Key**.
2. Give it a title (`MediaSet Development`, `MediaSet Production`) and grant the **Ingest** permission only.
3. Under **Applied properties**, add `Environment` = `Development` (or `Production`).
4. Copy the token and store it as a secret: `dotnet user-secrets set "ExternalLogging:SeqApiKey" "YOUR_DEV_INGEST_KEY" --project MediaSet.Api`
   locally, and in the Production secret/env file (`SEQ_API_KEY`) for both the API and Remix services.
5. Scope Production signals, queries and dashboards with `Environment = 'Production'` (the property the Production key
   applies), so they no longer depend on remembering a filter for Development events.

Notes:
- Both the API and Remix already set their own `Environment` property on each event (the ASP.NET environment name, and
  `Development`, `Production` or `Testing` mapped from `NODE_ENV`). A key-applied property is added by Seq on ingestion;
  the values should agree, but how Seq resolves a same-named property already on an event is not verified here, so
  check a few ingested events after adding the keys.
- Events that arrive without a key (or with the wrong one) are not tagged by the key, so use the right key per environment.

## HTTP Logging Exclusions

MediaSet.Api includes HTTP request/response logging that can generate high log volume for frequently-accessed endpoints. The following configuration options allow you to exclude specific paths from being logged to Seq while still logging them to console.

### HttpLoggingFilterOptions__ExcludedPaths

**Purpose**: Excludes HTTP request logs for exact path matches from being sent to Seq.

**Type**: Array of strings (indexed)

**Format**: `HttpLoggingFilterOptions__ExcludedPaths__N` where N is the array index (0, 1, 2, etc.)

**Behavior**:
- Performs **exact** case-insensitive path matching
- Only affects HTTP request/response logs
- Application logs (errors, warnings, info) are still sent to Seq
- Console logs still show all requests

**When to use**:
- Health check endpoints polled frequently by load balancers
- Monitoring endpoints that generate excessive log volume
- Specific endpoints that create noise without providing value

**Example**:
```yaml
HttpLoggingFilterOptions__ExcludedPaths__0: "/api/logs"
HttpLoggingFilterOptions__ExcludedPaths__1: "/health"
HttpLoggingFilterOptions__ExcludedPaths__2: "/health/ready"
HttpLoggingFilterOptions__ExcludedPaths__3: "/health/live"
```

**Matching behavior**:
- ✅ `/health` matches `/health`
- ❌ `/health/ready` does NOT match `/health` (not an exact match)
- ✅ `/HEALTH` matches `/health` (case-insensitive)

### HttpLoggingFilterOptions__ExcludePathStartsWith

**Purpose**: Excludes HTTP request logs for paths starting with specified prefixes from being sent to Seq.

**Type**: Array of strings (indexed)

**Format**: `HttpLoggingFilterOptions__ExcludePathStartsWith__N` where N is the array index (0, 1, 2, etc.)

**Behavior**:
- Performs **prefix** case-insensitive path matching
- Only affects HTTP request/response logs
- Application logs (errors, warnings, info) are still sent to Seq
- Console logs still show all requests

**When to use**:
- Exclude entire endpoint hierarchies (e.g., all health check endpoints under `/api/health/`)
- Development/documentation endpoints (e.g., all Swagger UI routes under `/swagger/`)
- Well-known paths or static asset hierarchies

**Example**:
```yaml
HttpLoggingFilterOptions__ExcludePathStartsWith__0: "/api/health"
HttpLoggingFilterOptions__ExcludePathStartsWith__1: "/swagger"
```

**Matching behavior**:
- ✅ `/api/health` matches prefix `/api/health`
- ✅ `/api/health/ready` matches prefix `/api/health`
- ✅ `/api/health/live` matches prefix `/api/health`
- ✅ `/swagger/index.html` matches prefix `/swagger`
- ❌ `/health` does NOT match prefix `/api/health`
- ✅ `/SWAGGER/test` matches prefix `/swagger` (case-insensitive)

## ExcludedPaths vs ExcludePathStartsWith

| Feature | ExcludedPaths | ExcludePathStartsWith |
|---------|---------------|----------------------|
| **Match Type** | Exact match only | Prefix match (starts with) |
| **Use Case** | Single specific endpoint | Entire path hierarchy |
| **Example Config** | `/health` | `/api/health` |
| **Matches `/api/health`** | Only `/api/health` | `/api/health`, `/api/health/ready`, `/api/health/live` |
| **Case Sensitive** | No | No |

**Recommendation**: Use `ExcludePathStartsWith` for broader filtering of endpoint hierarchies, and `ExcludedPaths` for specific individual endpoints.

## Complete Configuration Example

Here's a complete example of Seq configuration in `docker-compose.prod.yml`:

```yaml
services:
  mediaset-api:
    image: ghcr.io/paulmfischer/mediaset-api:latest
    environment:
      # Enable Seq logging
      ExternalLogging__Enabled: "true"
      ExternalLogging__SeqUrl: "http://seq:80"
      
      # Exclude exact paths from Seq (high-frequency endpoints)
      HttpLoggingFilterOptions__ExcludedPaths__0: "/health"
      HttpLoggingFilterOptions__ExcludedPaths__1: "/health/ready"
      HttpLoggingFilterOptions__ExcludedPaths__2: "/health/live"
      
      # Exclude path hierarchies from Seq
      HttpLoggingFilterOptions__ExcludePathStartsWith__0: "/api/health"
      HttpLoggingFilterOptions__ExcludePathStartsWith__1: "/swagger"
```

## Default Configuration

If not specified, MediaSet.Api uses the following defaults from `appsettings.json`:

```json
{
  "ExternalLogging": {
    "Enabled": false
  },
  "HttpLoggingFilterOptions": {
    "ExcludedPaths": [
      "/health",
      "/health/ready",
      "/health/live"
    ],
    "ExcludePathStartsWith": [
      "/api/health",
      "/swagger"
    ]
  }
}
```

These defaults provide sensible exclusions for common high-frequency endpoints. You can override them in your docker-compose configuration as needed.

## Remix Logging Configuration

`MediaSet.Remix` logs directly to Seq using the CLEF ingestion endpoint. For native
development, configure API logging through .NET User Secrets and frontend logging
through `MediaSet.Remix/.env.local`; the two processes do not share configuration
files. See the [development guide](../Development/DEVELOPMENT.md#configuration).
Production containers use the same environment variable names for both services:

| Variable | Description |
|----------|-------------|
| `ExternalLogging__SeqUrl` | Seq server URL, e.g. `http://seq:5341`. If not set, Remix logs to console only. |
| `ExternalLogging__SeqApiKey` | Optional ingestion API key, sent as the `X-Seq-ApiKey` header. See [per-environment keys](#ingestion-api-keys-per-environment). |

Remix logs appear in Seq with `Application = "MediaSet.Remix"` and include a `TraceId` property that correlates with API logs from the same request.
Their `Environment` is always `Development`, `Production` or `Testing` (whatever the casing of `NODE_ENV`), `SourceContext`
is the logging module (such as `app/api/entity-data`) and each message is a template (`Fetching {entityType} entity {id}`),
so events group by template and their values are properties.

## Filtering properties

| Property | Values | Set on |
|----------|--------|--------|
| `Application` | `MediaSet.Api`, `MediaSet.Api.Processor.BackgroundImage`, `MediaSet.Api.Processor.PlexSync`, `MediaSet.Remix` | Every event. The processor values are the scheduled image lookup and scheduled Plex syncs (a manual Plex sync stays `MediaSet.Api`). |
| `Component` | `Plex`, `Lookup`, `Images`, `Stats` | API events of those features (endpoint handlers, services and clients) and the outgoing HTTP spans they make. The incoming request span is not tagged: the endpoint filter runs inside the endpoint. |
| `Provider` | `TMDB`, `IGDB`, `OpenLibrary`, `MusicBrainz`, `UpcItemDb` | Lookup client events, their outgoing HTTP spans, provider-specific lookup warnings and cover downloads. |
| `MediaType` | `Books`, `Movies`, `Games`, `Musics` | Lookup requests and image lookups. |
| `RunId`, `Trigger` | run id, `manual` or `scheduled` | Every event of a Plex sync run. |

For example `Component = 'Images' and Provider = 'OpenLibrary' and @Level = 'Error'`.

## Troubleshooting

### Logs not appearing in Seq

1. Verify `ExternalLogging__Enabled` is set to `"true"` (string, not boolean)
2. Check the `ExternalLogging__SeqUrl` is correct and accessible from the container
3. Check MediaSet.Api logs for connection errors: `docker compose logs mediaset-api | grep -i seq`
4. Verify network connectivity between containers: `docker compose exec mediaset-api curl http://seq:80/api`

### Too many logs in Seq

- Add more paths to `ExcludedPaths` for specific endpoints generating noise
- Add path prefixes to `ExcludePathStartsWith` for entire endpoint hierarchies
- Remember: These settings only filter HTTP request/response logs, not application logs

### Path exclusions not working

- Verify the path matches exactly (for `ExcludedPaths`) or starts with the prefix (for `ExcludePathStartsWith`)
- Check for typos in the path strings
- Remember: Matching is case-insensitive
- Array indices must be sequential starting from 0 (e.g., `__0`, `__1`, `__2`)

## Additional Resources

- [Seq Documentation](https://docs.datalust.co/docs)
- [Serilog Documentation](https://serilog.net/)
- [ASP.NET Core Logging](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/)
