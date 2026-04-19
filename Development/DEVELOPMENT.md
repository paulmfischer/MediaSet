# MediaSet - Containerized Local Development Setup

This guide helps new developers set up a complete development environment without needing to install .NET, MongoDB, or Node.js locally. Everything runs in Podman containers with full hot-reload support.

## Prerequisites

- **Podman**: Install from [podman.io](https://podman.io/getting-started/installation)
  - Fedora/RHEL: `sudo dnf install podman podman-compose`
  - Ubuntu/Debian: `sudo apt install podman` + `pip install podman-compose`
  - macOS: `brew install podman podman-compose`
- **Git**: For cloning the repository
- **VS Code** (recommended): For the best development experience

That's it! No need for .NET SDK, MongoDB, or Node.js on your host machine.

## Quick Start

1. **Clone and enter the repository:**
   ```bash
   git clone https://github.com/paulmfischer/MediaSet.git
   cd MediaSet
   ```

2. **Start the development environment:**
   ```bash
   ./dev.sh start
   ```

3. **Wait for all services to be ready** (the script will check automatically)

4. **Access the applications:**
   - 🌐 **Frontend**: http://localhost:3000
   - 🚀 **API**: http://localhost:5000
   - 📊 **MongoDB**: mongodb://localhost:27017

## Development Workflow

### Hot Reloading

Both frontend and backend support hot reloading:

- **Frontend (Remix)**: Changes to files in `MediaSet.Remix/app/` will automatically reload the browser
- **Backend (.NET)**: Changes to `.cs` files in `MediaSet.Api/` will automatically restart the API server
- **Database**: MongoDB data persists between container restarts and clean operations (data stored under `./data/mongodb`). Use `./dev.sh clean --purge` to remove it.

### Making Code Changes

1. **Edit files normally** in your IDE (VS Code recommended)
2. **Save changes** - hot reload will handle the rest
3. **View logs** if needed: `./dev.sh logs [service]`

### Useful Commands

```bash
# Start development environment
./dev.sh start

# View logs from all services
./dev.sh logs

# View logs from specific service
./dev.sh logs api
./dev.sh logs frontend
./dev.sh logs mongodb

# Check container status
./dev.sh status

# Restart all services
./dev.sh restart

# Stop development environment
./dev.sh stop

# Get shell access to containers
./dev.sh shell api      # .NET API container
./dev.sh shell frontend # Remix frontend container
./dev.sh shell mongo    # MongoDB container

# Clean everything (removes all data)
./dev.sh clean
```

## Debugging

### VS Code Debugging Setup

1. **Install the "Dev Containers" extension** in VS Code
2. **Create `.vscode/launch.json`** for debugging the .NET API:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Debug API in Container",
      "type": "coreclr",
      "request": "attach",
      "processId": "${command:pickRemoteProcess}",
      "pipeTransport": {
        "pipeCwd": "${workspaceRoot}",
        "pipeProgram": "podman",
        "pipeArgs": ["exec", "-i", "mediaset-dev-api"],
        "debuggerPath": "/vsdbg/vsdbg",
        "quoteArgs": false
      }
    }
  ]
}
```

Ensure the Podman socket is running before attaching the debugger:
```bash
systemctl --user start podman.socket
```

### Browser Debugging

- **Frontend**: Use browser dev tools as normal at http://localhost:3000
- **API**: Use tools like Postman or curl to test endpoints at http://localhost:5000

### Database Access

Access MongoDB directly:
```bash
# Open MongoDB shell
./dev.sh shell mongo

# Or connect with any MongoDB client to:
# mongodb://localhost:27017
# Database: MediaSet
```

## Project Structure in Containers

```
/app (in containers)
├── MediaSet.Api/     # .NET API source (mounted from host)
├── MediaSet.Remix/   # Remix frontend source (mounted from host)
```

## Environment Variables

### API Container
- `ASPNETCORE_ENVIRONMENT=Development`
- `MediaSetDatabase__ConnectionString=mongodb://mongodb:27017`
- `MediaSetDatabase__DatabaseName=MediaSet`
- `OpenLibraryConfiguration__BaseUrl=https://openlibrary.org/`

### Frontend Container
- `NODE_ENV=development`
- `apiUrl=http://api:5000`
- `REMIX_DEV_HTTP_ORIGIN=http://localhost:3000`



## Troubleshooting

### Port Conflicts
If you get port conflicts, you can modify `docker-compose.dev.yml` to use different ports:
- Change `"3000:5173"` to `"3001:5173"` for frontend
- Change `"5000:5000"` to `"5001:5000"` for API

### Container Build Issues
```bash
# Clean and rebuild (preserve data)
./dev.sh clean
./dev.sh start

# Purge everything including MongoDB data
./dev.sh clean --purge
./dev.sh start
```

### Permission Issues (Linux/macOS)
```bash
# Fix file permissions
sudo chown -R $USER:$USER .
```

### Logs Not Showing Changes
```bash
# Check if file watching is working
./dev.sh logs api
# Look for "dotnet watch" messages

./dev.sh logs frontend
# Look for Vite/Remix reload messages
```

### Podman Socket Issues

```bash
# Start Podman socket service
systemctl --user start podman.socket
systemctl --user enable podman.socket

# Verify socket is running
systemctl --user status podman.socket
```

**SELinux Issues (RHEL/Fedora/CentOS):**
```bash
# If you see permission denied errors, try:
sudo setsebool -P container_manage_cgroup true
```

**Rootless Container Issues:**
```bash
# Check user namespace setup
podman unshare cat /proc/self/uid_map
```

## API Configuration

### External API Integration

MediaSet uses external APIs for metadata lookup functionality. Configure these in `MediaSet.Api/appsettings.Development.json`:

#### OpenLibrary (Books)
```json
"OpenLibraryConfiguration": {
  "BaseUrl": "https://openlibrary.org/",
  "Timeout": "30",
  "ContactEmail": "your-email@example.com"
}
```
- **No API key required**
- Free and unlimited
- Used for ISBN, LCCN, OCLC, OLID lookups

#### UPCitemdb (Barcode Lookups)
```json
"UpcItemDbConfiguration": {
  "BaseUrl": "https://api.upcitemdb.com/",
  "Timeout": 10
}
```
- **No API key required** for free tier
- **100 requests/day limit** on free plan
- Used to identify products from UPC/EAN barcodes
- For unlimited requests, [upgrade to paid plan](https://www.upcitemdb.com/api/)

#### TMDB - The Movie Database (Movies)
```json
"TmdbConfiguration": {
  "BaseUrl": "https://api.themoviedb.org/3/",
  "BearerToken": "",
  "Timeout": 10
}
```
- **API key required** (free)
- **Sign up**: [https://www.themoviedb.org/signup](https://www.themoviedb.org/signup)
- **Get API key**: Account → Settings → API → Request API Key → Developer
- **Rate limits**: 40 requests per 10 seconds, 1M requests per month
- Used for comprehensive movie metadata

**To configure TMDB API credentials (keep secret!):**
1. Create a free account at [themoviedb.org](https://www.themoviedb.org/signup)
2. Navigate to Settings → API
3. Request an API key (choose "Developer" option)
4. Copy your **Bearer Token** (not the API Key)
5. **Create a `.env` file** in the project root (see `.env.example` for template):
   ```bash
   cp .env.example .env
   ```
6. **Add your tokens to `.env`**:
   ```bash
   TMDB_BEARER_TOKEN=your_tmdb_bearer_token_here
   UPCITEMDB_API_KEY=your_api_key_here  # Optional
   ```
7. **Restart the development environment** to apply changes:
   ```bash
   ./dev.sh restart
   ```

> ⚠️ **IMPORTANT**: 
> - Never commit your `.env` file to git! It's already in `.gitignore`.
> - The `.env` file is automatically loaded by `docker-compose.dev.yml`.
> - For detailed setup instructions, see **[TMDB_SETUP.md](../Setup/TMDB_SETUP.md)**

### Testing Lookup Functionality

Once configured, test the lookup features:

**Books (ISBN):**
```bash
curl http://localhost:5000/lookup/Books/isbn/9780385504201
```

**Movies (UPC):**
```bash
curl http://localhost:5000/lookup/Movies/upc/883929248842
```

The UI also provides inline lookup buttons in the add/edit forms for books and movies.

## Image Storage Configuration

MediaSet stores cover images locally on the filesystem, providing fast access and efficient storage management.

### Image Storage Setup

Images are automatically set up when you start the development environment:

```bash
# The ./dev.sh script creates the image directory
./dev.sh start

# Images are stored locally in:
./data/images/
```

### Image Configuration

Image storage is configured in `appsettings.json` (already configured for local development):

```json
{
  "ImageConfiguration": {
    "StoragePath": "/app/data/images",
    "MaxFileSizeMb": 5,
    "AllowedImageExtensions": "jpg,jpeg,png",
    "HttpTimeoutSeconds": 30,
    "StripExifData": true
  }
}
```

**Configuration Options:**
- **StoragePath**: Directory where images are stored (default: `/app/data/images` in containers)
- **MaxFileSizeMb**: Maximum file size in megabytes (5MB by default)
- **AllowedImageExtensions**: Comma-separated list of allowed file extensions (JPEG and PNG by default)
- **HttpTimeoutSeconds**: Timeout for downloading images from URLs in seconds
- **StripExifData**: Whether to remove EXIF metadata (enabled by default for privacy)

### Image Directory Persistence

- Images stored during development persist in `./data/images/`
- Images remain available when containers are restarted
- To clear all images: `./dev.sh clean --purge`

### Testing Image Upload

**Upload an image when creating a book:**

```bash
# Create a test image (JPEG, PNG, etc.)
# Then POST to the API with multipart form data

curl -X POST http://localhost:5000/api/books \
  -F "entity={\"title\":\"Test Book\",\"authors\":\"[\\\"Author Name\\\"]\"}" \
  -F "coverImage=@/path/to/image.jpg"
```

**Download an image via URL:**

```bash
curl -X POST http://localhost:5000/api/books \
  -F "entity={\"title\":\"Test Book\",\"authors\":\"[\\\"Author Name\\\"]\"}" \
  -F "imageUrl=https://example.com/cover.jpg"
```

## Contributing

When contributing:

1. **Test locally** using this development setup
2. **Ensure hot reload works** for your changes
3. **Check both frontend and backend** work together
4. **Verify database changes persist** through container restarts

## Support

If you encounter issues:

1. Check the logs: `./dev.sh logs`
2. Verify all containers are running: `./dev.sh status`
3. Try cleaning and restarting: `./dev.sh clean && ./dev.sh start`

