# MediaSet - Containerized Local Development Setup

This guide helps new developers set up a complete development environment without needing to install .NET, MongoDB, or Node.js locally. Everything runs in Docker containers with full hot-reload support.

## Prerequisites

Choose **one** of the following container runtimes:

### Option 1: Docker (Recommended for most users)
- **Docker**: Install from [docker.com](https://docs.docker.com/get-docker/)
- **Docker Compose**: Usually included with Docker Desktop

### Option 2: Podman (Recommended for Linux users who prefer rootless containers)
- **Podman**: Install from [podman.io](https://podman.io/getting-started/installation)
- **Podman Compose**: Install via `pip install podman-compose` or your package manager
- Alternatively, use `docker-compose` with Podman (the script will configure this automatically)

### Additional Requirements
- **Git**: For cloning the repository
- **VS Code** (recommended): For the best development experience

That's it! No need for .NET SDK, MongoDB, or Node.js on your host machine.

**Need help installing?** See **[CONTAINER_SETUP.md](CONTAINER_SETUP.md)** for detailed installation instructions.

### Container Runtime Detection

The development script automatically detects which container runtime you have installed:
- 🐳 **Docker**: Uses `docker-compose.dev.yml` 
- 🦭 **Podman**: Uses `docker-compose.podman.yml` (optimized for Podman with rootless containers)

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
        "pipeProgram": "docker",
        "pipeArgs": ["exec", "-i", "mediaset-dev-api"],
        "debuggerPath": "/vsdbg/vsdbg",
        "quoteArgs": false
      }
    }
  ]
}
```

### Container Runtime Debugging

**Docker Users:**
- Use the "Debug API in Container (Docker)" configuration in VS Code

**Podman Users:**
- Use the "Debug API in Container (Podman)" configuration in VS Code
- Ensure Podman socket is running: `systemctl --user start podman.socket`

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

## Adding New Dependencies

### .NET Packages
```bash
# Enter the API container
./dev.sh shell api

# Add packages as normal
dotnet add package PackageName

# The changes will persist in your local files
```

### NPM Packages
```bash
# Enter the frontend container
./dev.sh shell frontend

# Add packages as normal
npm install package-name

# The changes will persist in your local files
```

## Troubleshooting

### Port Conflicts
If you get port conflicts, you can modify `docker-compose.dev.yml` to use different ports:
- Change `"3000:3000"` to `"3001:3000"` for frontend
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

### Podman-Specific Issues

**Socket Permission Issues:**
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

# If issues persist, try running with sudo:
sudo ./dev.sh start
```

## Performance Tips

1. **Use .dockerignore files** to exclude unnecessary files
2. **Keep node_modules in containers** using volumes (already configured)
3. **Use Docker Desktop's WSL2 backend** on Windows for better performance

## Production Deployment

This setup is for development only. For production:

1. Use the existing `Dockerfile` files (not `Dockerfile.dev`)
2. Use the production `docker-compose-api.yml` and `docker-compose-app.yml` files
3. Set appropriate environment variables for production

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
4. Check Docker Desktop for resource usage