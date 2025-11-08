# Setting Up TMDB API Token

The TMDB (The Movie Database) API bearer token is required for movie barcode lookup functionality. This guide covers setup for both containerized (Docker/Podman) and local development environments.

## Setup Instructions

### 1. Get Your TMDB Bearer Token

1. **Create an account** at [themoviedb.org](https://www.themoviedb.org/signup)
2. **Navigate to Settings → API** (https://www.themoviedb.org/settings/api)
3. **Request an API key** (choose the "Developer" option, not "Website")
4. **Copy your "API Read Access Token"** - this is the bearer token you need

### 2. Configure for Development

**For Containerized Development (Docker/Podman):**

1. **Create a `.env` file in the project root:**
   ```bash
   cp .env.example .env
   ```

2. **Edit the `.env` file and set your tokens:**
   ```bash
   TMDB_BEARER_TOKEN=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...your_actual_token_here
   UPCITEMDB_API_KEY=your_api_key_here  # Optional - for better rate limits
   ```

3. **Restart the development environment:**
   ```bash
   ./dev.sh restart
   ```

> ⚠️ **IMPORTANT**: The `.env` file is git-ignored and should never be committed to source control!

## Verification

After setting up, you should see successful TMDB API calls in the logs when looking up a movie barcode:

```
info: MediaSet.Api.Services.MovieLookupStrategy[0]
      Looking up movie with Upc: 013023153899
info: MediaSet.Api.Clients.TmdbClient[0]
      Searching TMDB for movie: Akira
info: MediaSet.Api.Clients.TmdbClient[0]
      TMDB search found 1 results for movie: Akira
```

If you see errors about authentication or the bearer token being null, double-check:
1. The `.env` file exists in the project root
2. The `TMDB_BEARER_TOKEN` is set correctly
3. The Docker containers have been restarted after creating/updating `.env`
