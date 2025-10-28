# Setting Up TMDB API Token for Docker

The TMDB (The Movie Database) API bearer token is required for movie barcode lookup functionality.

## For Docker Development

1. **Get your TMDB Bearer Token:**
   - Go to https://www.themoviedb.org/settings/api
   - Create an account if you don't have one
   - Request an API key (choose the API option, not the website option)
   - Copy your "API Read Access Token" (this is the bearer token)

2. **Create a `.env` file in the project root:**
   ```bash
   cd /home/fischerp/projects/MediaSet
   cp .env.example .env
   ```

3. **Edit the `.env` file and set your tokens:**
   ```bash
   TMDB_BEARER_TOKEN=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...your_actual_token_here
   UPCITEMDB_API_KEY=your_api_key_here  # Optional
   ```

4. **Restart the Docker containers:**
   ```bash
   ./dev.sh restart all
   # or
   docker-compose -f docker-compose.dev.yml down
   docker-compose -f docker-compose.dev.yml up -d
   ```

## For Local Development (without Docker)

If you're running the API directly with `dotnet run` (not in Docker), use .NET User Secrets:

```bash
cd MediaSet.Api
dotnet user-secrets set "TmdbConfiguration:BearerToken" "your_tmdb_bearer_token_here"
```

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
