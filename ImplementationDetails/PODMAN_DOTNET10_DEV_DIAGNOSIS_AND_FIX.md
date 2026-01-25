# Podman + .NET 10 Dev Setup: OpenAPI Restore Error Diagnosis and Fix

**Related Issue:** https://github.com/paulmfischer/MediaSet/pull/427

**Error:**
```
/usr/share/dotnet/sdk/10.0.102/Sdks/Microsoft.NET.Sdk/targets/Microsoft.PackageDependencyResolution.targets(266,5): 
error NETSDK1064: Package Microsoft.AspNetCore.OpenApi, version 10.0.2 was not found. 
It might have been deleted since NuGet restore. Otherwise, NuGet restore might have only partially completed, 
which might have been due to maximum path length restrictions.
```

## Diagnosis

### Root Causes

#### 1. **Unnecessary `Microsoft.AspNetCore.OpenApi` Package Reference**

The API project explicitly references `Microsoft.AspNetCore.OpenApi` version 10.0.2 in `MediaSet.Api.csproj`:

```xml
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.2" />
```

**Why it's unnecessary:**
- The API only uses `AddEndpointsApiExplorer()` and `AddSwaggerGen()` from the Swagger/Swashbuckle stack (configured in `Program.cs`).
- `Swashbuckle.AspNetCore` 10.1.0 is the primary dependency and fully supports OpenAPI document generation.
- The `Microsoft.AspNetCore.OpenApi` package is a framework-bundled component that has no direct usage in the codebase.
- This creates a strict, flaky dependency that can fail during container restores if the package cache is invalidated or unavailable.

#### 2. **NuGet Cache and Volume Mount Mismatch in Podman**

The current `docker-compose.podman.yml` mounts host user caches into the container:

```yaml
volumes:
  - ~/.nuget/packages:/root/.nuget/packages:Z
  - ~/.dotnet/tools:/root/.dotnet/tools:Z
```

**Problems:**
- **Path semantics:** Host `~/.nuget` (user home) is mounted to `/root/.nuget` (container root home), mixing user and root cache semantics.
- **SELinux relabeling (`:Z`):** Podman's `:Z` option re-labels directories for the container, but cache invalidation or permission issues can occur if the host cache is modified during a container operation.
- **Cache coherency:** `dotnet restore` during image build uses one cache state; `dotnet watch run` at runtime uses a potentially different cache state via the same mount, especially if the host clears or modifies `~/.nuget/packages` between build and run.
- **Restore in image build:** The `Dockerfile.dev` includes:
  ```dockerfile
  RUN dotnet nuget locals all --clear
  RUN dotnet restore
  ```
  This clears and restores during image build, but at runtime the container re-mounts a live host cache that may be stale or incomplete.

#### 3. **Fragile Restore Strategy in Development Dockerfile**

`Dockerfile.dev` clears all NuGet local caches and restores during image build:

```dockerfile
RUN dotnet nuget locals all --clear
RUN dotnet restore
```

**Issues:**
- Caches are ephemeral in a Docker/Podman layer and don't persist across container restarts.
- The mounted volume at runtime (`~/.nuget/packages`) may not contain the same packages restored during image build, especially if the host NuGet cache was cleaned or modified.
- No fallback recovery if restore fails during image build; the error surfaces only at runtime.

---

## Recommended Approach

### Strategy: Decouple Package Definition from Container Cache Management

#### 1. **Remove Unnecessary Package Reference**

Delete the `Microsoft.AspNetCore.OpenApi` package reference from `MediaSet.Api.csproj`:

**Rationale:**
- Eliminates a direct dependency flake point.
- Swashbuckle alone provides full OpenAPI/Swagger support.
- Reduces the attack surface for package restore issues.
- Aligns with SOLID: single responsibility (let Swashbuckle own the OpenAPI layer).

#### 2. **Use Named Volume for NuGet Cache (Podman-aware)**

Replace host-to-root path mounts with a named Podman volume for NuGet packages:

**In `docker-compose.podman.yml` and `docker-compose.dev.yml`:**

```yaml
volumes:
  api:
    volumes:
      - nuget_cache:/nuget:Z  # Named volume for cache coherency
      - ./MediaSet.Api:/app:Z
      - ./.git:/app/.git:Z
      - ./data/images:/app/data/images:Z
    environment:
      - NUGET_PACKAGES=/nuget  # Point dotnet to the mounted cache

# At the top level:
volumes:
  nuget_cache:
```

**Benefits:**
- Named volumes are Podman-managed and handle SELinux re-labeling transparently.
- Cache persists across container restarts and image rebuilds.
- No path confusion between host user space and container root.
- Simpler, more portable: works the same on Docker Compose and Podman Compose.

#### 3. **Move Restore to Runtime for Development**

Simplify `Dockerfile.dev` to defer restore until container startup:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /app

# Install debugger and tools
RUN curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install --global dotnet-ef
RUN dotnet tool install --global dotnet-watch

# Copy project files (no restore here)
COPY *.csproj ./
COPY . ./

# Set environment variables
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
ENV ASPNETCORE_ENVIRONMENT=Development
ENV DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1
ENV NUGET_PACKAGES=/nuget

EXPOSE 5000

# dotnet watch will restore on first run
CMD ["dotnet", "watch", "run", "--urls", "http://0.0.0.0:5000"]
```

**Benefits:**
- Image build is faster (no network call).
- Restore happens once per container startup, using the mounted NuGet cache volume.
- Aligns with local dev flow: `dotnet restore` and `dotnet watch` run in the same context.
- If restore fails, the error surfaces immediately and is easier to debug.

#### 4. **Align Docker and Podman Compose Configurations**

Apply the same cache strategy to both `docker-compose.dev.yml` and `docker-compose.podman.yml`:

- Both should define the `nuget_cache` named volume.
- Both should use `NUGET_PACKAGES=/nuget` environment variable.
- Both should mount the same volume to `/nuget:Z`.
- Remove host `~/.nuget/packages` and `~/.dotnet/tools` mounts; tools can be installed in the image or accessed via a separate named volume if sharing is needed.

#### 5. **Optional: Pin SDK Image Tag**

For reproducibility, pin the .NET SDK image in `Dockerfile.dev`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0.102  # Match global.json exactly
```

Verify in `global.json`:
```json
{
  "sdk": {
    "version": "10.0.102"
  }
}
```

---

## Implementation Steps

### Step 1: Remove `Microsoft.AspNetCore.OpenApi` Package

**File:** `MediaSet.Api/MediaSet.Api.csproj`

Remove:
```xml
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.2" />
```

### Step 2: Update `Dockerfile.dev`

**File:** `MediaSet.Api/Dockerfile.dev`

Replace:
```dockerfile
# Development Dockerfile for .NET API with hot reload support
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS development

WORKDIR /app

# Clear NuGet cache to prevent restore issues in local development
RUN dotnet nuget locals all --clear

# Install debugger
RUN curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg

# Install dotnet tools globally
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install --global dotnet-ef
RUN dotnet tool install --global dotnet-watch

# Copy project files
COPY *.csproj ./
RUN dotnet restore

# Copy source code
COPY . ./

# Set environment variables for development
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
ENV ASPNETCORE_ENVIRONMENT=Development
ENV DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1

EXPOSE 5000

# Default command - can be overridden in docker-compose
CMD ["dotnet", "watch", "run", "--urls", "http://0.0.0.0:5000"]
```

With:
```dockerfile
# Development Dockerfile for .NET API with hot reload support
FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /app

# Install debugger
RUN curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg

# Install dotnet tools globally
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install --global dotnet-ef
RUN dotnet tool install --global dotnet-watch

# Copy project files (restore deferred to runtime)
COPY *.csproj ./
COPY . ./

# Set environment variables for development
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
ENV ASPNETCORE_ENVIRONMENT=Development
ENV DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1
ENV NUGET_PACKAGES=/nuget

EXPOSE 5000

# dotnet watch will restore on first run using the mounted cache volume
CMD ["dotnet", "watch", "run", "--urls", "http://0.0.0.0:5000"]
```

### Step 3: Update `docker-compose.dev.yml`

**File:** `docker-compose.dev.yml`

Replace the `api` service `volumes` and `environment`:

```yaml
  api:
    build:
      context: ./MediaSet.Api
      dockerfile: Dockerfile.dev
    container_name: mediaset-dev-api
    restart: unless-stopped
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:5000
      - ASPNETCORE_HTTP_PORTS=5000
      - ASPNETCORE_HTTPS_PORTS=
      - NUGET_PACKAGES=/nuget
      - MediaSetDatabaseSettings__ConnectionString=mongodb://mongodb:27017
      - MediaSetDatabaseSettings__DatabaseName=MediaSet
      - OpenLibraryConfiguration__BaseUrl=https://openlibrary.org/
      - TmdbConfiguration__BearerToken=${TMDB_BEARER_TOKEN}
      - UpcItemDbConfiguration__ApiKey=${UPCITEMDB_API_KEY}
      - GiantBombConfiguration__BaseUrl=${GIANTBOMB_BASE_URL:-https://www.giantbomb.com/api/}
      - GiantBombConfiguration__ApiKey=${GIANTBOMB_API_KEY}
    volumes:
      - ./MediaSet.Api:/app
      - ./.git:/app/.git
      - ./data/images:/app/data/images
      - nuget_cache:/nuget
    depends_on:
      - mongodb
    networks:
      - mediaset-dev
    command: ["dotnet", "watch", "run", "--urls", "http://0.0.0.0:5000"]

# Add at the top level (after networks):
volumes:
  nuget_cache:
```

### Step 4: Update `docker-compose.podman.yml`

**File:** `docker-compose.podman.yml`

Replace the `api` service `volumes` and `environment`:

```yaml
  api:
    build:
      context: ./MediaSet.Api
      dockerfile: Dockerfile.dev
    container_name: mediaset-dev-api
    restart: unless-stopped
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:5000
      - ASPNETCORE_HTTP_PORTS=5000
      - ASPNETCORE_HTTPS_PORTS=
      - NUGET_PACKAGES=/nuget
      - MediaSetDatabaseSettings__ConnectionString=mongodb://mongodb:27017
      - MediaSetDatabaseSettings__DatabaseName=MediaSet
      - OpenLibraryConfiguration__BaseUrl=https://openlibrary.org/
      - TmdbConfiguration__BearerToken=${TMDB_BEARER_TOKEN}
      - UpcItemDbConfiguration__ApiKey=${UPCITEMDB_API_KEY}
      - GiantBombConfiguration__BaseUrl=${GIANTBOMB_BASE_URL:-https://www.giantbomb.com/api/}
      - GiantBombConfiguration__ApiKey=${GIANTBOMB_API_KEY}
    volumes:
      - ./MediaSet.Api:/app:Z
      - ./.git:/app/.git:Z
      - ./data/images:/app/data/images:Z
      - nuget_cache:/nuget:Z
    depends_on:
      - mongodb
    networks:
      - mediaset-dev
    command: ["dotnet", "watch", "run", "--urls", "http://0.0.0.0:5000"]

# Add at the top level (after networks):
volumes:
  nuget_cache:
```

---

## Validation Steps

### 1. Clean Up Existing State

```bash
# Stop containers
./dev.sh stop api frontend

# Remove the dev compose containers and images
podman compose -f docker-compose.podman.yml down --rmi local

# (Optional) Clean the local NuGet cache if you want a fresh start
rm -rf ~/.nuget/packages/microsoft.aspnetcore.openapi
```

### 2. Rebuild and Run

```bash
# Using dev.sh (if it supports podman)
./dev.sh start api

# Or using podman compose directly
cd /home/fischerp/projects/MediaSet
podman compose -f docker-compose.podman.yml up --build api mongodb
```

### 3. Verify Swagger Endpoint

Once the container is running, navigate to:
```
http://localhost:5000/swagger
```

The Swagger UI should load without NETSDK1064 errors.

### 4. Check NuGet Cache Volume

```bash
# List Podman volumes
podman volume ls

# Inspect the nuget_cache volume
podman volume inspect mediaset_nuget_cache

# View contents (if needed)
podman run --rm -v mediaset_nuget_cache:/nuget alpine ls -la /nuget/microsoft.aspnetcore.openapi
```

---

## Troubleshooting

### "Package not found" still occurs

1. **Verify the volume is mounted:**
   ```bash
   podman exec mediaset-dev-api env | grep NUGET_PACKAGES
   podman exec mediaset-dev-api ls -la /nuget
   ```

2. **Check for restore errors:**
   ```bash
   podman logs mediaset-dev-api | grep -A 5 "restore\|error\|NETSDK"
   ```

3. **Manually restore inside the container:**
   ```bash
   podman exec -it mediaset-dev-api dotnet restore --verbose
   ```

4. **Clean and retry:**
   ```bash
   podman volume rm mediaset_nuget_cache
   podman compose -f docker-compose.podman.yml down
   podman compose -f docker-compose.podman.yml up --build api
   ```

### Build succeeds but runtime fails

- Ensure `NUGET_PACKAGES=/nuget` is set in the container.
- Verify the volume mount in `docker-compose.podman.yml` includes the `:Z` flag for SELinux.
- Check if the host NuGet cache was cleared between image build and container startup.

### `.git` directory errors with MinVer

If the API uses MinVer for versioning:
- Ensure `.git` is mounted to `/app/.git:Z` in the compose file.
- If running purely in dev without versioning, you can remove the `.git` mount and set `MINVER_VERSION` in the environment.

---

## Design Rationale

### Why Remove `Microsoft.AspNetCore.OpenApi`?

- **No direct usage:** The package is auto-referenced by the SDK but not explicitly called in code.
- **Swashbuckle sufficiency:** `AddSwaggerGen()` and `AddEndpointsApiExplorer()` work independently.
- **Reduces flakiness:** Fewer explicit dependencies = fewer restore failure points.
- **Mirrors .NET best practices:** Only declare dependencies you directly use.

### Why Named Volume Over Host Mount?

- **Container-native:** Podman and Docker manage named volumes; no host path confusion.
- **SELinux-friendly:** Named volumes handle `:Z` re-labeling transparently.
- **Cache persistence:** Volume survives container restarts and image rebuilds.
- **Portability:** Works unchanged on Docker Compose, Podman Compose, Kubernetes, etc.

### Why Defer Restore to Runtime?

- **Faster image builds:** No network call during `docker build`.
- **Aligned semantics:** Restore happens in the same context as `dotnet watch`, matching local dev flow.
- **Better error feedback:** Restore errors surface at container startup, not hidden in image layers.
- **Cache coherency:** All restore operations use the same NuGet cache directory (`/nuget`), eliminating sync issues.

---

## Related References

- [PR #427](https://github.com/paulmfischer/MediaSet/pull/427) – Initial attempt to resolve NETSDK1064
- [.NET 10 Upgrade Implementation Details](DOTNET_10_UPGRADE_IMPLEMENTATION.md)
- [Podman Compose Documentation](https://docs.podman.io/en/latest/markdown/podman-compose.1.html)
- [NuGet Configuration (NUGET_PACKAGES)](https://docs.microsoft.com/en-us/nuget/reference/cli-reference/cli-ref-config)
