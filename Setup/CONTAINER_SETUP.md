# Running MediaSet (user guide)

This guide explains how to run the MediaSet application using Docker Compose and the provided `docker-compose.prod.yml`. It is written for someone who wants to run the application locally (tester / end-user), not for development work.

Prerequisites
- Docker Engine (20.10+) or Podman with Docker-compatibility
- Docker Compose v2 (the `docker compose` CLI) or `podman compose`
- Sufficient disk space for images and MongoDB data

Overview
- The production compose file `docker-compose.prod.yml` defines three services: `mediaset-api`, `mediaset-ui`, and `mongo`.

Configuration approach

- Edit `docker-compose.prod.yml` directly: open the file and update the `environment:` entries under each service (for example `mediaset-api` and `mediaset-ui`). This is the recommended approach for end users — the compose file is self-contained and explicit.

Integration placeholders and enabling

By default integration settings (OpenLibrary, TMDB, IGDB, etc.) are commented out or disabled in `docker-compose.prod.yml`. To enable an integration:

1. Edit `docker-compose.prod.yml` and uncomment the integration block you want to enable (look for the commented `# OpenLibrary configuration`, `# TMDB configuration`, etc.). You need to uncomment every line of that integration if you plan to use it.
2. Replace any `[ReplaceThis]` placeholders in the compose file with the real values required for that integration (for example an email for OpenLibrary, API keys or bearer tokens). You can also place these values in a `.env` file next to `docker-compose.prod.yml` instead of editing the compose file directly, if you prefer.
3. Ensure `clientApiUrl` points to the API URL your browser will use (e.g., `http://localhost:8080`). This is the URL you would directly access the API from, not the docker URL.

Placeholders you must replace (examples)

- `OpenLibraryConfiguration__ContactEmail` — replace `[ReplaceThis]` with your contact email for OpenLibrary.
- `TmdbConfiguration__BearerToken` — replace `[ReplaceThis]` with your TMDB bearer token.
- `IgdbConfiguration__ClientId` — replace `[ReplaceThis]` with your Twitch Client ID.
- `IgdbConfiguration__ClientSecret` — replace `[ReplaceThis]` with your Twitch Client Secret.
- `clientApiUrl` — replace `[ReplaceThis]` with the API URL browsers should use (e.g., `http://localhost:8080`).

Starting the application

From the repository root (where `docker-compose.prod.yml` lives) run:

```bash
# Pull latest images (optional)
docker compose -f docker-compose.prod.yml pull

# Start services in background
docker compose -f docker-compose.prod.yml up -d
```

If you use Podman with compose, replace `docker compose` with `podman compose`.

Confirm services are running

```bash
docker compose -f docker-compose.prod.yml ps

# or view containers directly
docker ps --filter "name=mediaset"
```

Access the application
- Frontend (UI): http://localhost:3000
- API: http://localhost:8080

Logs and health checks

Stream logs:

```bash
docker compose -f docker-compose.prod.yml logs -f mediaset-ui
docker compose -f docker-compose.prod.yml logs -f mediaset-api
```

Health endpoint (API):

```bash
curl -sS http://localhost:8080/health
```

Updating to newer images

```bash
# Stop and remove containers (retain volumes)
docker compose -f docker-compose.prod.yml down

# Pull latest images
docker compose -f docker-compose.prod.yml pull

# Start again
docker compose -f docker-compose.prod.yml up -d
```

Stopping and removing everything (including volumes)

```bash
docker compose -f docker-compose.prod.yml down -v
```

Troubleshooting

- Port conflicts: ensure ports `3000` (UI) and `8080` (API) are free on the host. Use `ss -ltn` or `lsof -i :3000` to check.
- Failed to pull images: confirm network access and Docker Hub/GitHub Container Registry reachability.
- Credential helper errors (Linux): if you see messages about `docker-credential-desktop`, edit `~/.docker/config.json` after backing it up and remove `credsStore`/`credHelpers`, or run Docker commands with a temporary `DOCKER_CONFIG`:

```bash
# create a temporary Docker config dir and start compose using it
export DOCKER_CONFIG=$(mktemp -d)
docker compose -f docker-compose.prod.yml up -d
```

- MongoDB data: data is persisted in the `mediaset-db` named volume. To reset data, stop containers and run `docker compose -f docker-compose.prod.yml down -v` (this removes volumes).
- Healthcheck failures: inspect API logs (`docker compose -f docker-compose.prod.yml logs mediaset-api`) for stack traces or environment errors.

Notes about configuration

- `CLIENT_API_URL` must be set to an address your browser can reach for the API (e.g., `http://localhost:8080` when running locally).
- Secrets and API keys (TMDB, IGDB, UPCitemdb) are optional; if unset, the associated lookup features will be disabled or limited.

Useful commands summary

```bash
# Start
docker compose -f docker-compose.prod.yml up -d

# Tail logs
docker compose -f docker-compose.prod.yml logs -f

# Show running services
docker compose -f docker-compose.prod.yml ps

# Stop and remove (keep volumes)
docker compose -f docker-compose.prod.yml down

# Stop and remove including volumes (data reset)
docker compose -f docker-compose.prod.yml down -v

# Pull updates
docker compose -f docker-compose.prod.yml pull
```

Where to get help

- Check container logs (`docker compose -f docker-compose.prod.yml logs`) for errors.
- Inspect service-specific logs with `docker logs <container-name>` (container names appear in `docker ps`).
- Open an issue in the repository if a reproducible problem remains.

