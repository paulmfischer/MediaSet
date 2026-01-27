# External Logging Integration

This document describes the current logging setup for MediaSet (API + UI), evaluates self-hosted external logging systems, and provides concrete implementation steps to integrate an external logger while keeping the current console logging as a fallback.

**Scope:** API (`MediaSet.Api`) and UI (`MediaSet.Remix`).

**Goals:**
- Provide searchable, queryable logs in a self-hosted system (Seq, Loki, ELK, Graylog).
- Make integration configurable and toggleable at runtime/environment level.
- Minimize changes required to the codebase; prefer configuration + small plumbing.

**Current logging (summary)**
- **API (`MediaSet.Api`)**: Uses `Microsoft.Extensions.Logging` with `AddSimpleConsole` and built-in HTTP logging (`AddHttpLogging`). The app configures a bootstrap logger for some early startup messages in `Program.cs`. Logs are created via injected `ILogger<T>` throughout services and endpoints. Correlation IDs are added via a middleware scope (`BeginScope`) and log messages are structured (message templates). No structured external sink is currently configured (no Serilog/Seq/Loki/Elasticsearch sinks).
- **UI (`MediaSet.Remix`)**: Uses ad-hoc `console.log` calls in several places (client-side) and `console.log` in server-side loaders/routes. There is no client-side log aggregation or forwarding to a centralized collector.

**Requirements for chosen external logger**
- Prefer self-hosted solutions.
- Simple to configure from .NET (Serilog sinks or HTTP endpoints).
- Ability to preserve structured logging (property templates) and correlate request IDs.
- Reasonable operational burden (installation, storage, retention policies, backups).

Recommended provider

**Seq** (https://datalust.co/seq) is the chosen logger-of-choice for MediaSet's initial external logging integration.

- Pros: Excellent developer UX for structured logs, native Serilog sink (`Serilog.Sinks.Seq`), simple to self-host (Linux/Windows), minimal setup, good query language, ingestion API key support, low operational overhead for small deployments.
- Cons: Not open-source (free tier available), less suitable for extremely large volumes without sizing/maintenance.
- Integration effort: Very easy for .NET/Serilog. UI client logs can be forwarded to the API which logs them server-side.

Rationale: Seq provides the best balance of developer ergonomics, simple self-hosting, and tight Serilog integration. It allows rapid iteration and powerful queries for structured logs with minimal operational burden for an initial deployment.

Recommendation summary
- Use Seq as the primary external logging backend for MediaSet.
- Configure Serilog in the API with a Seq sink and preserve Console output for `docker logs`.
- Forward client-side logs to `POST /api/logs` so the API can enrich and emit them into Seq rather than embedding ingestion keys in browser code.


API integration approach (high level)

1. Add Serilog as the application logging provider and configure it from `appsettings.json`. Keep console logging as a fallback.
   - Add packages: `Serilog.AspNetCore`, `Serilog.Settings.Configuration`, and a sink package for the chosen backend (e.g., `Serilog.Sinks.Seq`, `Serilog.Sinks.Elasticsearch`, or `Serilog.Sinks.Grafana.Loki`).
2. Create a bootstrap logger during host startup (so early messages can be emitted to the chosen sink if desired).
3. Read Serilog configuration from `appsettings.json` and environment variables and conditionally enable the external sink based on `ExternalLogging:Enabled` flag.
4. Preserve existing use of `ILogger<T>` — Serilog will bridge to the existing abstractions when added via `UseSerilog()`.
5. Add a small `POST /api/logs` endpoint to accept client-side logs (optional): client posts structured logs to this endpoint; the endpoint logs them using an `ILogger` with `LogInformation/LogWarning/LogError` + properties. This keeps sensitive credentials out of the browser and centralizes log enrichment/correlation.

Minimal Program.cs changes (concept)

```csharp
// Before building the WebApplication
var bootstrapLogger = new LoggerConfiguration()
    .WriteTo.Console()
    // conditional: .WriteTo.Seq(configuration["ExternalLogging:SeqUrl"]) if enabled
    .CreateLogger();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();
```

appsettings.json snippet (example)

```json
"ExternalLogging": {
  "Enabled": true,
  "Provider": "seq",
  "SeqUrl": "http://seq:5341",
  "ApiKey": "",
  "MinimumLevel": "Debug"
}

"Serilog": {
  "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.Seq" ],
  "MinimumLevel": "Debug",
  "WriteTo": [
    { "Name": "Console" },
    {
      "Name": "Seq",
      "Args": { "serverUrl": "%ExternalLogging:SeqUrl%", "apiKey": "%ExternalLogging:ApiKey%" }
    }
  ],
  "Enrich": [ "FromLogContext" ]
}
```

Notes:
- Use `Serilog.Settings.Configuration` to control sinks via config and environment variables.
- Keep `AddSimpleConsole` removed once Serilog is the primary logger; or keep Console sink so logs still appear in container stdout for Docker.

Enriching logs with `Application` and `Environment` at runtime

To make `Application` and `Environment` available as structured properties (so Seq/Grafana/Loki can filter on them) prefer deriving their values from the running application instead of hard-coding them in configuration files.

Bootstrap logger (very early startup)

```csharp
using System.Reflection;

var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? "MediaSet.Api";
var envName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "NotSet";

Log.Logger = new LoggerConfiguration()
  .Enrich.WithProperty("Application", assemblyName)
  .Enrich.WithProperty("Environment", envName)
  .WriteTo.Console()
  .CreateBootstrapLogger();
```

Notes:
- The bootstrap logger uses `Assembly.GetEntryAssembly()` for the application name and `ASPNETCORE_ENVIRONMENT` for the environment. This ensures even very early startup logs contain these properties.

Primary logger (Program.cs / Host.UseSerilog)

When configuring Serilog as the host logger, use the hosting environment and the assembly name from the runtime context so the values are accurate and not read from a config file:

```csharp
using System.Reflection;

builder.Host.UseSerilog((context, services, cfg) =>
{
  var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? "MediaSet.Api";
  var envName = context.HostingEnvironment.EnvironmentName;

  cfg.ReadFrom.Configuration(context.Configuration)
     .Enrich.FromLogContext()
     .Enrich.WithProperty("Application", assemblyName)
     .Enrich.WithProperty("Environment", envName)
     .WriteTo.Console();
});
```

This approach ensures both bootstrap and host loggers emit `Application` and `Environment` properties derived from the running application, not from a static config file. Seq (or other sinks) will receive these properties and you can filter on them in the UI.

UI integration approach (high level)

Option A (recommended): Implement a small client-side logger utility that:
  - Wraps `console.*` calls.
  - ALWAYS POSTs logs to `POST /api/logs` on the API. Do not require UI-side configuration to enable/disable this behavior; the API (and host logger) controls where logs are emitted (Console/Seq/etc.).
  - Adds minimal browser metadata (userAgent, url, timestamp) and correlation IDs when available. Keep payloads small and scrub sensitive values.

Option B: Integrate directly with a hosted ingestion endpoint (some sinks offer HTTP ingest). This requires shipping API keys to the browser — avoid this for security reasons.

Client-side design considerations
- Batch logs and send on intervals or on error levels (e.g., only send warnings/errors immediately).
- Throttle/sampling to avoid large volumes.
- Scrub PII and sensitive headers before sending (do not forward auth tokens or passwords).
- Protect the `/api/logs` endpoint with rate limiting and optional authentication.

Example client-side flow (Remix)

- Add a small helper `app/utils/clientLogger.ts` with methods `info`, `warn`, `error` that always POST the structured payload to `/api/logs`.
- Keep the client simple: the API is responsible for enrichment, routing, and retention. The UI should not toggle external logging or know about Seq credentials.

Server-side minimal-API handler (Program.cs)

Ensure Serilog (or the configured logger) is created before the app maps endpoints and before the handler is invoked (use a bootstrap logger and `builder.Host.UseSerilog()` before `builder.Build()`). The API should set `Application` and `Environment` for incoming UI logs so Seq can filter UI vs API logs. Example minimal API route that merges client properties with server-set `Application`/`Environment`:

```csharp
public record ClientLogEvent(string Level, string Message, DateTimeOffset Timestamp, Dictionary<string, object?>? Properties);

app.MapPost("/api/logs", (ClientLogEvent ev, ILogger<Program> logger, IHostEnvironment env) =>
{
  // Merge client-sent properties with server-provided metadata
  var clientProps = ev.Properties ?? new Dictionary<string, object?>();
  var scopeProps = new Dictionary<string, object?>(clientProps)
  {
    ["Application"] = "MediaSet.Remix",
    ["Environment"] = env.EnvironmentName
  };

  using (logger.BeginScope(scopeProps))
  {
    if (Enum.TryParse<LogLevel>(ev.Level, true, out var level))
    {
      logger.Log(level, "{Message}", ev.Message);
    }
    else
    {
      logger.LogInformation("{Message}", ev.Message);
    }
  }

  return Results.Accepted();
});
```

Notes:
- The minimal API handler relies on the host logger being already configured (Console/Seq sinks). That configuration should happen during bootstrap and host setup so any logs emitted by this endpoint are routed appropriately.
- The handler explicitly sets `Application` = `MediaSet.Remix` and `Environment` = `IHostEnvironment.EnvironmentName` for UI-originated logs. API-originated logs will keep the assembly-derived `Application` value (e.g., `MediaSet.Api`).
- Keep the handler minimal: validate payload size and types, rate-limit upstream callers, and avoid logging sensitive tokens or PII sent accidentally from the client.

Security and operational considerations
- Ensure CORS and auth are configured for the log endpoint.
- Implement size limits, rate limits, and request validation.
- Consider retention policies, disk sizing, and backups for the chosen backend.
- Add access controls for the log viewing UI and redact or mask sensitive values.

Runtime toggle strategy

- Use environment variables and configuration keys to toggle external logging without code changes. Examples:
  - `EXTERNAL_LOGGING__ENABLED=true` (matches `ExternalLogging:Enabled`)
  - `EXTERNAL_LOGGING__PROVIDER=seq`
  - `EXTERNAL_LOGGING__SEQURL=http://seq:5341`

- The application startup should read `ExternalLogging:Enabled` and only configure the external sink if `true`. Keep console sink enabled regardless so `docker logs` still works if external logging is disabled.

Implementation plan (concrete steps)

1. Document and decide on provider (Seq recommended).
2. Add NuGet packages to `MediaSet.Api`:
   - `Serilog.AspNetCore`
   - `Serilog.Settings.Configuration`
   - `Serilog.Sinks.Seq` (or other sink as chosen)
3. Update `Program.cs` to bootstrap Serilog, call `builder.Host.UseSerilog()`, and wire configuration.
4. Add config keys to `appsettings.json` and `appsettings.Development.json` for easy local testing.
5. (Optional) Create `POST /api/logs` endpoint to accept client logs from the Remix UI.
6. Add client-side helper in `MediaSet.Remix` to forward logs to the API when runtime config enables it.
7. Add docs for running Seq/Loki locally (docker-compose snippets) and sample `docker-compose` services.
8. Add tests and manual verification steps (smoke test that logs appear in the external UI and correlation IDs are present).

Sample `POST /api/logs` contract (simple)

```json
{
  "level": "Information",
  "message": "UI: failed to load entity",
  "timestamp": "2026-01-26T12:34:56Z",
  "properties": { "entityId": "abc", "path": "/books/abc" }
}
```

Server-side handler (concept)

```csharp
[ApiController]
[Route("api/logs")]
public class LogsController : ControllerBase
{
  private readonly ILogger<LogsController> _logger;
  public LogsController(ILogger<LogsController> logger) => _logger = logger;

  [HttpPost]
  public IActionResult Post([FromBody] ClientLogEvent ev)
  {
    using (_logger.BeginScope(ev.Properties ?? new Dictionary<string, object?>()))
    {
      _logger.Log(LogLevel.Parse(ev.Level), "{Message}", ev.Message);
    }
    return Accepted();
  }
}
```

Next steps / choices for you
- I can implement the API wiring for Serilog + Seq and add a small `POST /api/logs` endpoint and a minimal client forwarder in the Remix app. This is a small, testable change. (I will create a feature branch and propose changes for review.)
- Or I can stop here and wait for you to pick a provider and approve the plan.

Files touched (proposed)
- `MediaSet.Api/Program.cs` (add Serilog bootstrap + UseSerilog)
- `MediaSet.Api/Controllers/LogsController.cs` (new, optional)
- `MediaSet.Remix/app/utils/clientLogger.ts` (new client helper)
- `appsettings.json` / `appsettings.Development.json` (config keys)

Other options (historical)

- **Grafana Loki**
  - Pros: Designed to be inexpensive and scalable, integrates tightly with Grafana for querying, has HTTP intake (Promtail/Fluentd) and plugins (Serilog sinks exist), good for log aggregation and multi-tenant setups.
  - Cons: Querying is label-oriented (not full-text like Elasticsearch), requires Grafana to view logs, slightly more operational components (Loki + Grafana + promoter/agent).
  - Integration effort: Medium. Use Serilog.Sinks.Grafana.Loki or send log lines via promtail. Client logs can POST to an API endpoint or directly to an agent.

- **ELK / Elastic Stack (Elasticsearch + Logstash + Kibana)**
  - Pros: Powerful full-text search, mature ecosystem, many integrations, advanced analytics.
  - Cons: Resource heavy, more complex to operate, licensing for recent versions, requires sizing and maintenance.
  - Integration effort: Medium. Use `Serilog.Sinks.Elasticsearch` or Logstash/Fluentd forwarding.

- **Graylog**
  - Pros: Open-source, good for centralized log management, has GELF protocol, supports alerting and retention policies.
  - Cons: Operational overhead, Java-based stack, smaller ecosystem than ELK.
  - Integration effort: Medium. Use GELF sinks or forward from Fluent Bit/Fluentd.

- **Fluent Bit / Fluentd (collector) + Object Store**
  - Pros: Lightweight collectors, flexible routing, can forward to many backends (Elasticsearch, Loki, S3, etc.).
  - Cons: More moving parts; collectors run in infra tier (containers/hosts) rather than application.
  - Integration effort: More infra-focused; app stays mostly unchanged (write to console and collectors pick it up).

References
- `MediaSet.Api/Program.cs` — current logging bootstrapping and correlation middleware
- Serilog docs: https://github.com/serilog/serilog-aspnetcore
- Seq docs: https://docs.datalust.co/docs
- Grafana Loki: https://grafana.com/oss/loki

---

Document created by contributor for issue: Integrate with an external logger (#440).
