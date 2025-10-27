# Movie Barcode Lookup Setup Guide

This guide walks you through setting up the movie barcode lookup feature for MediaSet.

## Prerequisites

You need API keys from two services:
1. **Barcode Lookup API** - for UPC/EAN product information
2. **TMDb (The Movie Database)** - for movie metadata

## Getting API Keys

### 1. Barcode Lookup API

1. Visit https://www.barcodelookup.com/api
2. Sign up for a free account
3. Choose a plan (free tier includes 100 requests/day)
4. Copy your API key from the dashboard

### 2. TMDb API

1. Visit https://www.themoviedb.org/signup
2. Create a free account
3. Go to https://www.themoviedb.org/settings/api
4. Request an API key (choose "Developer" option)
5. Fill in the application details (can be simple/personal use)
6. Copy your API key (v3 auth)

## Local Development Setup

### Option 1: Using appsettings.Development.json (Recommended for VS Code/local dev)

1. Open `MediaSet.Api/appsettings.Development.json`
2. Replace the placeholder values:
   ```json
   "BarcodeLookupConfiguration": {
     "ApiKey": "YOUR_ACTUAL_BARCODE_LOOKUP_API_KEY"
   },
   "TmdbConfiguration": {
     "ApiKey": "YOUR_ACTUAL_TMDB_API_KEY"
   }
   ```
3. Save the file
4. Run the API locally: `dotnet watch run --project MediaSet.Api/MediaSet.Api.csproj`

**Note:** Do NOT commit your actual API keys to git! The `.gitignore` should exclude sensitive files.

### Option 2: Using Environment Variables (Recommended for Docker)

1. Create a `.env` file in the project root:
   ```bash
   cp .env.example .env
   ```

2. Edit `.env` and add your API keys:
   ```bash
   BARCODE_LOOKUP_API_KEY=your_actual_barcode_lookup_api_key
   TMDB_API_KEY=your_actual_tmdb_api_key
   ```

3. The `.env` file is git-ignored by default

4. Start the services with Docker:
   ```bash
   ./dev.sh start all
   # or
   docker-compose -f docker-compose.dev.yml up
   ```

## Production Deployment

### Docker Compose (Production)

Update `MediaSet.Api/docker-compose-api.yml` and uncomment the API configuration lines:

```yaml
environment:
  "BarcodeLookupConfiguration:BaseUrl": "https://api.barcodelookup.com/v3/"
  "BarcodeLookupConfiguration:ApiKey": "YOUR_BARCODE_LOOKUP_API_KEY"
  "BarcodeLookupConfiguration:Timeout": "30"
  "TmdbConfiguration:BaseUrl": "https://api.themoviedb.org/3/"
  "TmdbConfiguration:ApiKey": "YOUR_TMDB_API_KEY"
  "TmdbConfiguration:Timeout": "30"
```

**Security Best Practice:** Use Docker secrets or environment variables instead of hardcoding keys in docker-compose files.

### Using Docker Secrets

For production deployments, consider using Docker secrets:

```bash
echo "your_barcode_key" | docker secret create barcode_lookup_api_key -
echo "your_tmdb_key" | docker secret create tmdb_api_key -
```

Then reference them in your docker-compose:

```yaml
secrets:
  - barcode_lookup_api_key
  - tmdb_api_key

services:
  mediaset-api:
    secrets:
      - barcode_lookup_api_key
      - tmdb_api_key
```

## Verifying the Setup

### 1. Check API Health

Start the API and visit: http://localhost:7130/health

You should see a response indicating the API is healthy.

### 2. Test Movie Lookup

Try looking up a movie by UPC/EAN barcode:

```bash
# Example UPC for a movie (replace with actual barcode)
curl http://localhost:7130/lookup/movies/upc/043396578234
```

### 3. Check Logs

If lookup fails, check the API logs for configuration errors:
- "BarcodeLookupConfiguration is not configured" - missing Barcode Lookup API key
- "TmdbConfiguration is not configured" - missing TMDb API key
- "Movie lookup service is not available" - both configs required

## Troubleshooting

### "Movie lookup is not available"

**Cause:** One or both API keys are missing or invalid.

**Solution:** 
1. Verify both API keys are set in your configuration
2. Check for typos in the keys
3. Ensure the keys are valid (test them directly with the respective APIs)

### "No results found for barcode"

**Cause:** The barcode may not be in the Barcode Lookup database, or it's not a movie product.

**Solution:**
1. Verify the barcode is correct (12-13 digit numeric)
2. Try a different barcode from a known movie
3. Check the Barcode Lookup API dashboard for quota limits

### "No movie found matching title"

**Cause:** The product title from Barcode Lookup couldn't be matched to a movie in TMDb.

**Solution:**
1. The product may not be a movie (could be book, game, etc.)
2. TMDb may not have that specific release
3. Title normalization may need adjustment (check logs for extracted title)

## Rate Limits

### Barcode Lookup API
- Free tier: 100 requests/day
- Paid tiers: 3,000 - 10,000 requests/day
- Caching is enabled (5 minutes default) to minimize API calls

### TMDb API
- Free tier: 50,000 requests/day
- Rate limit: 50 requests per second
- Caching is enabled (5 minutes default) to minimize API calls

## Feature Availability

The movie barcode lookup feature is:
- ✅ Available on "Add Movie" form
- ✅ Supports UPC (12-digit) and EAN (13-digit) barcodes
- ✅ Auto-populates: Title, Year, Runtime, Rating, Plot, Genres, Studio, Format
- ❌ Not yet available on "Edit Movie" form (future enhancement)

## Cost Considerations

Both APIs offer free tiers that should be sufficient for personal use:
- **Barcode Lookup**: Free tier includes 100 lookups/day
- **TMDb**: Free tier includes 50,000 lookups/day

For production use with many users, consider:
- Upgrading to paid tiers
- Implementing more aggressive caching
- Storing barcode → TMDb ID mappings in MongoDB

## Next Steps

1. Get your API keys from both services
2. Configure them using one of the methods above
3. Test the lookup feature on the "Add Movie" form
4. Adjust caching settings if needed in `appsettings.json`

## Questions or Issues?

Check the implementation details in `MOVIE_BARCODE_IMPLEMENTATION.md` for architecture and technical information.
