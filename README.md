## MediaSet Library Management

### Screenshots of application
Home screen:

![Home screen with library information](assets/screenshots/Home-page.png)

Books home screen:

![Books index list page](assets/screenshots/Books-index.png)

Books home screen filtered:

![Books index list page filtered](assets/screenshots/Books-index-filter.png)

Books home screen mobile friendly:

![Books index list mobile view](assets/screenshots/Books-index-mobile.png)

Books detail page:

![Books detail page](assets/screenshots/Books-detail.png)

## Features

### 📚 Metadata Lookup

MediaSet includes built-in metadata lookup functionality to quickly populate item details:

**Books:**
- **ISBN Lookup**: Search by ISBN to automatically retrieve book metadata from OpenLibrary
- Supports multiple identifier types: ISBN, LCCN, OCLC, OLID, UPC, EAN
- Auto-populates title, authors, publisher, publication date, genres, page count, and more
- Inline lookup button in add/edit forms - results populate the form for review before saving

**Movies:**
- **Barcode Lookup**: Scan or enter UPC/EAN barcodes to retrieve movie metadata
- Two-stage lookup: UPCitemdb for product identification → TMDB for comprehensive movie data
- Auto-populates title, genres, studios, release date, rating, runtime, and plot
- Inline lookup button in add/edit forms

**Games:**
- **Barcode Lookup**: Scan or enter UPC/EAN barcodes to retrieve game metadata
- Two-stage lookup: UPCitemdb for product identification → GiantBomb for comprehensive game data
- Auto-populates title, platform, genres, developers, publishers, release date, rating, description, and format
- Inline lookup button in add/edit forms

**Configuration:**
- **UPCitemdb**: Free tier (100 requests/day) - no API key required
- **TMDB**: Free API key required - [sign up here](https://www.themoviedb.org/signup)
  - Add your TMDB Bearer Token to `appsettings.Development.json`:
    ```json
    "TmdbConfiguration": {
      "BaseUrl": "https://api.themoviedb.org/3/",
      "BearerToken": "your-tmdb-bearer-token-here",
      "Timeout": 10
    }
    ```
- **OpenLibrary**: No API key required (existing feature)
- **GiantBomb**: Free API key required - request one at https://www.giantbomb.com/api/
  - Add your GiantBomb settings to `appsettings.Development.json`:
    ```json
    "GiantBombConfiguration": {
      "BaseUrl": "https://www.giantbomb.com/api/",
      "ApiKey": "your-giantbomb-api-key-here",
      "Timeout": 10
    }
    ```
  - See detailed setup in [GIANTBOMB_SETUP.md](GIANTBOMB_SETUP.md)

## Development

### 🐳 Containerized Development (Recommended)

**New developers don't need to install .NET, Node.js, or MongoDB!** Everything runs in containers with full hot-reload support. 

**Supports both Docker and Podman** - the setup script automatically detects your container runtime.

#### Quick Start
```bash
# Clone the repository
git clone https://github.com/paulmfischer/MediaSet.git
cd MediaSet

# Start the development environment
./dev.sh start

# Access the applications:
# Frontend: http://localhost:3000
# API: http://localhost:5000 
# MongoDB: mongodb://localhost:27017
```

For complete setup instructions, debugging, and troubleshooting, see **[DEVELOPMENT.md](DEVELOPMENT.md)**.

### � Performance & Caching

MediaSet implements in-memory caching for metadata queries and statistics to significantly improve performance. For details on caching strategy, configuration, and monitoring, see **[CACHING.md](CACHING.md)**.

### �📖 Traditional Development

If you prefer to install dependencies locally:

**Prerequisites:**
- .NET 8.0 SDK
- Node.js 20+
- MongoDB

**Setup:**
1. Start MongoDB locally
2. Start the API: Press `F5` in VS Code or `dotnet run` in `MediaSet.Api/`
3. Start the frontend: `npm run dev` in `MediaSet.Remix/`
4. Access: Frontend at http://localhost:3000, API at http://localhost:5000


