# GiantBomb API Setup

This project uses the GiantBomb API to enrich game metadata after identifying a product via UPC/EAN.

## 1) Get an API key
- Request an API key at: https://www.giantbomb.com/api/
- Keys are free for non-commercial use; review their terms and rate limits.

## 2) Configure for Development

### Containerized Development (Docker/Podman)

1) Create a `.env` file in the project root (if you haven't already):

```bash
cp .env.example .env
```

2) Edit `.env` and set your GiantBomb key (and optionally override the base URL):

```bash
GIANTBOMB_API_KEY=your_giantbomb_api_key_here
# Optional; default is https://www.giantbomb.com/api/
GIANTBOMB_BASE_URL=https://www.giantbomb.com/api/
```

3) Restart the dev environment so the container picks up changes:

```bash
./dev.sh restart
```

The API container maps these variables to .NET configuration:
- GiantBombConfiguration__ApiKey ← GIANTBOMB_API_KEY
- GiantBombConfiguration__BaseUrl ← GIANTBOMB_BASE_URL (defaults to https://www.giantbomb.com/api/)

### Local Development (no containers)

Add a `GiantBombConfiguration` section to `MediaSet.Api/appsettings.Development.json`:

```json
"GiantBombConfiguration": {
  "BaseUrl": "https://www.giantbomb.com/api/",
  "ApiKey": "your-giantbomb-api-key-here",
  "Timeout": 10
}
```

Or set environment variables:

- GiantBombConfiguration__BaseUrl=https://www.giantbomb.com/api/
- GiantBombConfiguration__ApiKey=your-giantbomb-api-key-here
- GiantBombConfiguration__Timeout=10

## 3) How it’s used
- Games barcode lookups (UPC/EAN) follow a two-step approach:
  1. UPCitemdb identifies the product and provides a noisy title/description
  2. GiantBomb Search refines by cleaned title; then details are fetched for a selected match
- The final response maps to the Game form: title (+edition), platform, genres, developers, publishers, release date, rating, description, and format

## 4) Rate limits and reliability
- Respect GiantBomb API limits and fair-use policies
- Consider adding application-level caching (in-memory or distributed) to reduce repeated lookups
- Transient failures will be logged; ensure logs are visible in your environment

## 5) Troubleshooting
- 401/403: Verify `ApiKey` is correct and active
- 404/Empty results: The cleaned title may not match; try manual edits in the form and re-run lookup
- Timeouts: Increase `Timeout` or check network connectivity

## 6) Security note
Never commit real API keys. Use `appsettings.Development.json` for local dev and environment variables or secret stores for other environments.
