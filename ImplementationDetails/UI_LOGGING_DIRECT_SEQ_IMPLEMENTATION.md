# UI Logging: Direct Seq Integration

## Background

`UI_LOGGING_REFACTOR_APPROACH.md` documents the original decision to route Remix server-side logs through the `/api/logs` endpoint (Approach 1). That approach was chosen for low implementation effort, but it required three workarounds to avoid circular logging:

1. `HttpLoggingFilterOptions` + `ExcludedPaths` to suppress HTTP logging for `/api/logs`
2. `HttpLoggingFilterMiddleware` + `ExcludePathHttpLoggingInterceptor` to suppress the actual log entries
3. A dedicated `client-logs` rate limiter policy

The Approach 2 con "Requires Seq API key in the browser" no longer applies — `serverLogger` is exclusively server-side Node.js code (Remix loaders and actions). The Seq key is never exposed to a browser. The simplest fix is to cut out the middleman.

## Target Architecture

```
Before:
  Remix (Node.js) ──POST /api/logs──▶ MediaSet.Api ──ILogger──▶ Seq
                         (workarounds needed to suppress HTTP logging of this call)

After:
  Remix (Node.js) ──POST /api/events/raw──▶ Seq  (direct CLEF ingestion)
  MediaSet.Api ──ILogger──▶ Seq                  (unchanged)
```

---

## What Changes

### Remix (`MediaSet.Remix`)

**`app/utils/serverLogger.ts`** — replace `sendLogToApi()` with direct Seq CLEF ingestion.

- Read `ExternalLogging__SeqUrl` from `process.env` (same variable as the API — no new env vars needed)
- If not set, fall back to console-only (same as current behavior when API is unreachable)
- Post to `${SEQ_URL}/api/events/raw` with `Content-Type: application/vnd.serilog.clef`
- Enrich each event with `Application: "MediaSet.Remix"` and `Environment` from `process.env.NODE_ENV`
- Include `TraceId` from `getRequestContext()` for cross-service log correlation

**CLEF event shape:**
```json
{"@t":"2024-01-01T00:00:00.000Z","@mt":"{Message}","@l":"Warning","Application":"MediaSet.Remix","Environment":"production","TraceId":"abc123","Message":"Entity lookup failed","entityType":"books"}
```

Level mapping (`@l` field):
| `serverLogger` level | CLEF `@l` |
|----------------------|-----------|
| `debug`              | `Debug`   |
| `info`               | omit (Information is CLEF default) |
| `warn`               | `Warning` |
| `error`              | `Error`   |

**No changes needed to callers** — the public `serverLogger` API (`info`, `warn`, `error`, `debug`) is unchanged. All existing tests mock the whole module and are unaffected.

---

### API (`MediaSet.Api`)

**Files to delete:**
- `MediaSet.Api/Features/Logs/Endpoints/LogsApi.cs`
- `MediaSet.Api/Features/Logs/Models/ClientLogEvent.cs`

**`Program.cs`** — three removals:
1. Remove `using MediaSet.Api.Features.Logs.Endpoints;`
2. Remove `app.MapLogs();`
3. Remove the `"client-logs"` policy from `builder.Services.AddRateLimiter()`

**`appsettings.json`** — remove `/api/logs` from `HttpLoggingFilterOptions.ExcludedPaths`  
**`appsettings.Development.json`** — same removal

> `HttpLoggingFilterMiddleware`, `HttpLoggingFilterOptions`, and `ExcludePathHttpLoggingInterceptor` are **kept** — they still suppress noise from `/health`, `/swagger`, and `/config/integrations`.

---

### Environment / Docker Compose

**`docker-compose.dev.yml`** — add to the `ui` service `environment` block (same variables as the API service):

```yaml
ExternalLogging__SeqUrl: "${ExternalLogging__SeqUrl:-}"
```

No new env vars — setting `ExternalLogging__SeqUrl` in `.env` already configures both services.

**`docker-compose.prod.yml`** — two changes:

1. Add commented Seq env vars to the `mediaset-ui` service `environment` block (same variable names as the API):
```yaml
# Seq logging configuration (for structured logging from Remix server)
# Uses the same variable as the API — set once to enable both services
# ExternalLogging__SeqUrl: "http://seq:5341"
```

2. Remove the `/api/logs` entry from the commented `HttpLoggingFilterOptions` block in the `mediaset-api` service and renumber the remaining entries:
```yaml
# HttpLoggingFilterOptions__ExcludedPaths__0: "/health"
# HttpLoggingFilterOptions__ExcludedPaths__1: "/health/ready"
# HttpLoggingFilterOptions__ExcludedPaths__2: "/health/live"
# HttpLoggingFilterOptions__ExcludePathStartsWith__0: "/api/health"
# HttpLoggingFilterOptions__ExcludePathStartsWith__1: "/swagger"
```

---

### `.env.example`

Add a note below `ExternalLogging__SeqUrl` clarifying it now configures both services, and add `ExternalLogging__ApiKey`:

```bash
# External Logging (Seq) - optional, disabled by default
# ExternalLogging__Enabled=true
# ExternalLogging__SeqUrl=http://your-seq-host:5341  # configures both API and Remix
```

---

### `Setup/SEQ_SETUP.md`

Three locations reference `/api/logs` that must be updated:

1. **"Complete Configuration Example"** — remove the `HttpLoggingFilterOptions__ExcludedPaths__0: "/api/logs"` line and renumber remaining entries (`__0` through `__2`, `__0` through `__1`)

2. **"Default Configuration"** JSON block — remove `"/api/logs"` from the `ExcludedPaths` array

3. **Scope of the document** — the document is currently titled "Seq Logging Configuration Guide for MediaSet.Api". Expand the intro sentence and add a short section explaining that `MediaSet.Remix` also sends logs directly to Seq via `SEQ_URL` / `SEQ_API_KEY` env vars, and that both services appear in Seq distinguished by their `Application` property (`MediaSet.Api` vs `MediaSet.Remix`).

---

### `Development/DEVELOPMENT.md`

In the **"Environment Variables → Frontend Container"** section, add the new optional vars:

```
### Frontend Container
- `NODE_ENV=development`
- `apiUrl=http://api:5000`
- `REMIX_DEV_HTTP_ORIGIN=http://localhost:3000`
- `SEQ_URL` (optional) — set to match `ExternalLogging__SeqUrl` to enable Remix structured logging to Seq
```

---

## Implementation Steps

1. **Rewrite `serverLogger.ts`**
   - Replace `sendLogToApi()` with `sendLogToSeq()` using native `fetch`
   - Format payload as CLEF (one JSON object, `Content-Type: application/vnd.serilog.clef`)
   - Pull `TraceId` from `getRequestContext()` to preserve cross-service correlation
   - Guard: if `SEQ_URL` is not set, skip the fetch entirely (console output remains)

2. **Delete API logs feature**
   - Delete `Features/Logs/Endpoints/LogsApi.cs`
   - Delete `Features/Logs/Models/ClientLogEvent.cs`
   - Remove the empty `Features/Logs/` directory

3. **Update `Program.cs`**
   - Remove `using` import, `app.MapLogs()` call, and `"client-logs"` rate limiter policy

4. **Update appsettings**
   - Remove `/api/logs` from `ExcludedPaths` in both `appsettings.json` and `appsettings.Development.json`

5. **Update Docker Compose**
   - `docker-compose.dev.yml`: add `SEQ_URL` (and optional `SEQ_API_KEY`) to the `ui` service environment
   - `docker-compose.prod.yml`: add commented `SEQ_URL`/`SEQ_API_KEY` vars to the `mediaset-ui` service environment
   - `docker-compose.prod.yml`: remove the `HttpLoggingFilterOptions__ExcludedPaths__0: "/api/logs"` line from the `mediaset-api` service and renumber the remaining entries (0–2 and 0–1)

6. **Update `.env.example`**
   - Add a note that `ExternalLogging__SeqUrl` now configures both API and Remix

7. **Update `Setup/SEQ_SETUP.md`**
   - Remove `/api/logs` from the "Complete Configuration Example" and renumber
   - Remove `/api/logs` from the "Default Configuration" JSON block
   - Expand the document scope to cover Remix direct Seq logging (`SEQ_URL` / `SEQ_API_KEY`) and note that both services are distinguished in Seq by the `Application` property

8. **Update `Development/DEVELOPMENT.md`**
   - Add `SEQ_URL` (optional) to the "Environment Variables → Frontend Container" section

6. **Verify**
   - Run `dotnet test` — no backend test changes expected
   - Run `npm test` in `MediaSet.Remix` — serverLogger is mocked everywhere, no changes expected
   - Start the stack and confirm Remix logs appear in Seq under `Application = "MediaSet.Remix"`
   - Confirm `/api/logs` returns 404 (endpoint removed)
   - Confirm no duplicate or missing logs in Seq

---

## What This Removes

| Item | File/Location |
|------|--------------|
| `/api/logs` endpoint | `Features/Logs/Endpoints/LogsApi.cs` |
| `ClientLogEvent` model | `Features/Logs/Models/ClientLogEvent.cs` |
| `client-logs` rate limiter | `Program.cs` |
| `/api/logs` path exclusion config | `appsettings.json`, `appsettings.Development.json`, `docker-compose.prod.yml`, `Setup/SEQ_SETUP.md` |
| `sendLogToApi()` HTTP round-trip | `serverLogger.ts` |

## What This Keeps

| Item | Reason |
|------|--------|
| `HttpLoggingFilterMiddleware` | Still suppresses `/health`, `/swagger`, `/config/integrations` |
| `HttpLoggingFilterOptions` | Still used by above middleware |
| `ExcludePathHttpLoggingInterceptor` | Still used by above middleware |
| `serverLogger` public API | Interface unchanged; no caller updates needed |
