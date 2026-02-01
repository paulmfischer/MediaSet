# Seq Logging Configuration Guide for MediaSet.Api

This guide explains how to configure MediaSet.Api to send logs to a Seq server for centralized structured logging.

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

## HTTP Logging Exclusions

MediaSet.Api includes HTTP request/response logging that can generate high log volume for frequently-accessed endpoints. The following configuration options allow you to exclude specific paths from being logged to Seq while still logging them to console.

### HttpLoggingOptions__ExcludedPaths

**Purpose**: Excludes HTTP request logs for exact path matches from being sent to Seq.

**Type**: Array of strings (indexed)

**Format**: `HttpLoggingOptions__ExcludedPaths__N` where N is the array index (0, 1, 2, etc.)

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
HttpLoggingOptions__ExcludedPaths__0: "/api/logs"
HttpLoggingOptions__ExcludedPaths__1: "/health"
HttpLoggingOptions__ExcludedPaths__2: "/health/ready"
HttpLoggingOptions__ExcludedPaths__3: "/health/live"
```

**Matching behavior**:
- ✅ `/health` matches `/health`
- ❌ `/health/ready` does NOT match `/health` (not an exact match)
- ✅ `/HEALTH` matches `/health` (case-insensitive)

### HttpLoggingOptions__ExcludePathStartsWith

**Purpose**: Excludes HTTP request logs for paths starting with specified prefixes from being sent to Seq.

**Type**: Array of strings (indexed)

**Format**: `HttpLoggingOptions__ExcludePathStartsWith__N` where N is the array index (0, 1, 2, etc.)

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
HttpLoggingOptions__ExcludePathStartsWith__0: "/api/health"
HttpLoggingOptions__ExcludePathStartsWith__1: "/swagger"
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
      HttpLoggingOptions__ExcludedPaths__0: "/api/logs"
      HttpLoggingOptions__ExcludedPaths__1: "/health"
      HttpLoggingOptions__ExcludedPaths__2: "/health/ready"
      HttpLoggingOptions__ExcludedPaths__3: "/health/live"
      
      # Exclude path hierarchies from Seq
      HttpLoggingOptions__ExcludePathStartsWith__0: "/api/health"
      HttpLoggingOptions__ExcludePathStartsWith__1: "/swagger"
```

## Default Configuration

If not specified, MediaSet.Api uses the following defaults from `appsettings.json`:

```json
{
  "ExternalLogging": {
    "Enabled": false
  },
  "HttpLoggingOptions": {
    "ExcludedPaths": [
      "/api/logs",
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
