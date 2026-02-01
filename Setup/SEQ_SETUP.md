# Seq Logging Setup Guide

This guide explains how to configure Seq for centralized structured logging with MediaSet in production environments.

## What is Seq?

[Seq](https://datalust.co/seq) is a centralized structured logging server that collects, stores, and allows you to search and analyze application logs. It provides a web-based UI for viewing logs with advanced filtering, querying, and alerting capabilities.

## Why Use Seq with MediaSet?

- **Centralized Logging**: Aggregates logs from all MediaSet API instances in one place
- **Structured Logging**: Preserves the structured nature of logs (key-value pairs) for better searchability
- **Powerful Querying**: Filter and search logs using SQL-like query language
- **Real-time Monitoring**: View logs in real-time as they're generated
- **Alerts**: Set up alerts for specific log patterns or error conditions
- **Trace Correlation**: View distributed traces across HTTP requests with OpenTelemetry integration

## Prerequisites

- Docker or Podman for running Seq
- MediaSet API configured with Serilog (included by default)
- Network connectivity between MediaSet API and Seq server

## Quick Start with Docker Compose

### 1. Add Seq Service to docker-compose.prod.yml

Add the following service definition to your `docker-compose.prod.yml`:

```yaml
services:
  # ... existing services ...

  seq:
    image: datalust/seq:latest
    container_name: mediaset-seq
    restart: unless-stopped
    ports:
      - "5341:80"  # Seq web UI and ingestion endpoint
    environment:
      ACCEPT_EULA: "Y"
      SEQ_FIRSTRUN_ADMINPASSWORDHASH: "your-password-hash-here"  # Optional: Set admin password
    volumes:
      - seq-data:/data
    networks:
      - mediaset

volumes:
  # ... existing volumes ...
  seq-data:
    driver: local
```

### 2. Enable Seq Logging in MediaSet API

Uncomment and configure the Seq logging settings in `docker-compose.prod.yml` under the `mediaset-api` service:

```yaml
mediaset-api:
  # ... other configuration ...
  environment:
    # ... other environment variables ...
    
    # Seq logging configuration
    ExternalLogging__Enabled: "true"
    ExternalLogging__SeqUrl: "http://seq:80"  # Use container name 'seq' for internal Docker networking
```

### 3. Start Services

```bash
docker compose -f docker-compose.prod.yml up -d
```

### 4. Access Seq

Open your browser and navigate to: `http://localhost:5341`

Default credentials (first run):
- Username: `admin`
- Password: (set via `SEQ_FIRSTRUN_ADMINPASSWORDHASH` or prompted on first login)

## Configuration Options

### ExternalLogging Settings

These settings control the connection to Seq:

```yaml
ExternalLogging__Enabled: "true"
```
- **Purpose**: Enables or disables external logging to Seq
- **Values**: `"true"` or `"false"`
- **Default**: `false` (disabled)
- **When to use**: Set to `"true"` in production when you want centralized logging

```yaml
ExternalLogging__SeqUrl: "http://seq:80"
```
- **Purpose**: URL of the Seq server ingestion endpoint
- **Format**: `http://hostname:port` or `https://hostname:port`
- **Default**: `http://localhost:5341` (if not specified in code)
- **Examples**:
  - `http://seq:80` - For Docker Compose (using service name)
  - `http://192.168.1.100:5341` - For external Seq server
  - `https://seq.example.com` - For HTTPS-enabled Seq server

### HttpLoggingOptions: ExcludedPaths

Controls which HTTP request paths should **not** be logged to Seq (exact match).

```yaml
HttpLoggingOptions__ExcludedPaths__0: "/api/logs"
HttpLoggingOptions__ExcludedPaths__1: "/health"
HttpLoggingOptions__ExcludedPaths__2: "/health/ready"
HttpLoggingOptions__ExcludedPaths__3: "/health/live"
```

**What it does:**
- Prevents HTTP logs for **exact path matches** from being sent to Seq
- Console logs still show these requests (only Seq filtering is applied)
- Reduces noise in centralized logging by excluding high-frequency, low-value endpoints

**When to use:**
- Health check endpoints that are polled frequently (every few seconds)
- Monitoring endpoints that generate excessive log volume
- Log ingestion endpoints to prevent recursive logging
- Any endpoint that creates log noise without providing value

**How it works:**
- The application checks each HTTP request path against this list
- If the path **exactly matches** any entry, that HTTP request log is **not sent to Seq**
- Application logs (errors, warnings, info) are still sent regardless of path
- Only affects HTTP request/response logging, not other log events

**Examples:**
```yaml
# Exclude exact paths
HttpLoggingOptions__ExcludedPaths__0: "/health"          # Excludes: /health
HttpLoggingOptions__ExcludedPaths__1: "/api/logs"       # Excludes: /api/logs
HttpLoggingOptions__ExcludedPaths__2: "/metrics"        # Excludes: /metrics
```

❌ **Will NOT match:**
- `/health/ready` (not an exact match for `/health`)
- `/api/logs/search` (not an exact match for `/api/logs`)
- `/HEALTH` (comparison is case-insensitive, so this WILL match)

### HttpLoggingOptions: ExcludePathStartsWith

Controls which HTTP request paths should **not** be logged to Seq (prefix match).

```yaml
HttpLoggingOptions__ExcludePathStartsWith__0: "/api/health"
HttpLoggingOptions__ExcludePathStartsWith__1: "/swagger"
```

**What it does:**
- Prevents HTTP logs for paths **starting with** these prefixes from being sent to Seq
- More flexible than `ExcludedPaths` as it matches entire path hierarchies
- Console logs still show these requests (only Seq filtering is applied)

**When to use:**
- Exclude entire endpoint hierarchies (e.g., all `/api/health/*` endpoints)
- Development/documentation endpoints (e.g., Swagger UI at `/swagger/*`)
- Static asset paths that generate high log volume
- Well-known paths for security/discovery endpoints

**How it works:**
- The application checks if each HTTP request path **starts with** any entry in this list
- If a match is found, that HTTP request log is **not sent to Seq**
- Application logs (errors, warnings, info) are still sent regardless of path
- Only affects HTTP request/response logging, not other log events

**Examples:**
```yaml
# Exclude path hierarchies
HttpLoggingOptions__ExcludePathStartsWith__0: "/api/health"   # Excludes: /api/health, /api/health/ready, /api/health/live
HttpLoggingOptions__ExcludePathStartsWith__1: "/swagger"      # Excludes: /swagger, /swagger/index.html, /swagger/v1/swagger.json
HttpLoggingOptions__ExcludePathStartsWith__2: "/.well-known"  # Excludes: /.well-known/openid-configuration, etc.
```

✅ **Will match:**
- `/api/health` matches prefix `/api/health`
- `/api/health/ready` matches prefix `/api/health`
- `/api/health/live` matches prefix `/api/health`
- `/swagger/index.html` matches prefix `/swagger`
- `/SWAGGER/test` matches prefix `/swagger` (case-insensitive)

❌ **Will NOT match:**
- `/health` does NOT match prefix `/api/health`
- `/api/healthcheck` does NOT match prefix `/api/health` (must match from start of path segment)

## ExcludedPaths vs ExcludePathStartsWith

| Feature | ExcludedPaths | ExcludePathStartsWith |
|---------|---------------|----------------------|
| **Match Type** | Exact match | Prefix match (starts with) |
| **Use Case** | Single specific endpoint | Entire path hierarchy |
| **Example Config** | `/health` | `/api/health` |
| **Matches** | Only `/health` | `/api/health`, `/api/health/ready`, `/api/health/live` |
| **Case Sensitive** | No | No |
| **Best For** | Known exact paths | Groups of related endpoints |

**Recommendation**: Use `ExcludePathStartsWith` for broader filtering and `ExcludedPaths` for specific endpoints.

## Complete Example Configuration

Here's a complete example with Seq enabled in `docker-compose.prod.yml`:

```yaml
services:
  mediaset-api:
    image: ghcr.io/paulmfischer/mediaset-api:latest
    container_name: mediaset-api
    environment:
      # Core settings
      ASPNETCORE_URLS: "http://+:8080"
      ASPNETCORE_ENVIRONMENT: "Production"
      
      # Seq logging configuration
      ExternalLogging__Enabled: "true"
      ExternalLogging__SeqUrl: "http://seq:80"
      
      # HTTP logging exclusions
      HttpLoggingOptions__ExcludedPaths__0: "/api/logs"
      HttpLoggingOptions__ExcludedPaths__1: "/health"
      HttpLoggingOptions__ExcludedPaths__2: "/health/ready"
      HttpLoggingOptions__ExcludedPaths__3: "/health/live"
      HttpLoggingOptions__ExcludePathStartsWith__0: "/api/health"
      HttpLoggingOptions__ExcludePathStartsWith__1: "/swagger"
    depends_on:
      - mongo
      - seq
    networks:
      - mediaset

  seq:
    image: datalust/seq:latest
    container_name: mediaset-seq
    restart: unless-stopped
    ports:
      - "5341:80"
    environment:
      ACCEPT_EULA: "Y"
    volumes:
      - seq-data:/data
    networks:
      - mediaset

volumes:
  seq-data:
    driver: local

networks:
  mediaset:
    driver: bridge
```

## Using Seq

### Viewing Logs

1. Navigate to `http://localhost:5341` in your browser
2. Use the search bar to filter logs:
   - `@Level = 'Error'` - Show only errors
   - `RequestPath like '/api/books%'` - Show requests to book endpoints
   - `@Message like '%MongoDB%'` - Search log messages for "MongoDB"

### Common Queries

**Show all errors:**
```
@Level = 'Error'
```

**Show logs from the last hour:**
```
@Timestamp > Now() - 1h
```

**Show logs for a specific endpoint:**
```
RequestPath = '/api/books'
```

**Show slow requests (over 1 second):**
```
Elapsed > 1000
```

**Show logs with exceptions:**
```
@Exception is not null
```

### Setting Up Alerts

1. Navigate to **Settings** → **Alerts**
2. Click **Add Alert**
3. Configure:
   - **Signal**: The query that triggers the alert (e.g., `@Level = 'Error'`)
   - **Condition**: When to fire (e.g., "more than 5 events in 5 minutes")
   - **Action**: Email, Slack, webhook, etc.

## Security Considerations

### Production Deployment

1. **Set Admin Password**: Use `SEQ_FIRSTRUN_ADMINPASSWORDHASH` to set a strong admin password
2. **Enable HTTPS**: Configure Seq behind a reverse proxy with TLS/SSL
3. **Network Isolation**: Ensure Seq is only accessible from trusted networks
4. **Authentication**: Enable API key authentication for log ingestion in production
5. **Regular Updates**: Keep Seq updated to the latest version

### Environment Variables Security

- Store sensitive values (API keys, passwords) in `.env` files or secrets management
- Never commit `.env` files to version control
- Use Docker secrets or Kubernetes secrets in production

## Troubleshooting

### Logs Not Appearing in Seq

1. **Check Seq is running**: `docker compose -f docker-compose.prod.yml ps seq`
2. **Check connectivity**: From API container: `curl http://seq:80/api`
3. **Check API logs**: `docker compose -f docker-compose.prod.yml logs mediaset-api | grep -i seq`
4. **Verify configuration**: Ensure `ExternalLogging__Enabled` is `"true"`
5. **Check Seq logs**: `docker compose -f docker-compose.prod.yml logs seq`

### High Log Volume

- Add more paths to `ExcludedPaths` or `ExcludePathStartsWith`
- Adjust minimum log level in `appsettings.json`
- Enable Seq retention policies to auto-delete old logs
- Increase Seq storage capacity

### Permission Issues

```bash
# Fix Seq data directory permissions
docker compose -f docker-compose.prod.yml down
docker volume rm mediaset_seq-data
docker compose -f docker-compose.prod.yml up -d
```

## Additional Resources

- [Seq Documentation](https://docs.datalust.co/docs)
- [Seq Docker Image](https://hub.docker.com/r/datalust/seq)
- [Serilog Documentation](https://serilog.net/)
- [Structured Logging Best Practices](https://stackify.com/what-is-structured-logging-and-why-developers-need-it/)

## Cost and Licensing

- **Seq**: Free for development (single user), paid for production (multiple users)
- **Alternative**: Self-hosted ELK stack or other logging solutions
- **Free tier**: Available for small teams (check Seq website for current limits)

For more information about Seq licensing, visit: https://datalust.co/pricing
