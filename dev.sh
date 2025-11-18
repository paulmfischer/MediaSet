#!/bin/bash

# MediaSet Development Environment Setup Script
set -e

# Function to detect and setup container runtime
setup_container_runtime() {
    echo "🚀 Setting up MediaSet Development Environment"

    # Detect container runtime (Docker or Podman)
    CONTAINER_RUNTIME=""
    COMPOSE_COMMAND=""

    if command -v podman &> /dev/null; then
        CONTAINER_RUNTIME="podman"
        echo "✅ Found Podman"
        
        # Check for podman-compose or docker-compose
        if command -v podman-compose &> /dev/null; then
            COMPOSE_COMMAND="podman-compose"
            echo "✅ Using podman-compose"
        elif command -v docker-compose &> /dev/null; then
            COMPOSE_COMMAND="docker-compose"
            echo "✅ Using docker-compose with Podman"
            # Set Docker socket to use Podman
            export DOCKER_HOST="unix:///run/user/$UID/podman/podman.sock"
        else
            echo "❌ Neither podman-compose nor docker-compose found. Please install one of them."
            exit 1
        fi
    elif command -v docker &> /dev/null; then
        CONTAINER_RUNTIME="docker"
        echo "✅ Found Docker"
        
        if command -v docker-compose &> /dev/null; then
            COMPOSE_COMMAND="docker-compose"
            echo "✅ Using docker-compose"
        elif docker compose version &> /dev/null 2>&1; then
            COMPOSE_COMMAND="docker compose"
            echo "✅ Using docker compose (plugin)"
        else
            echo "❌ Docker Compose is not installed. Please install Docker Compose."
            exit 1
        fi
    else
        echo "❌ Neither Docker nor Podman is installed."
        echo "Please install one of the following:"
        echo "  - Docker: https://docs.docker.com/get-docker/"
        echo "  - Podman: https://podman.io/getting-started/installation"
        exit 1
    fi

    echo "🔧 Using: $CONTAINER_RUNTIME with $COMPOSE_COMMAND"

    # Choose the appropriate compose file
    COMPOSE_FILE="docker-compose.dev.yml"
    if [ "$CONTAINER_RUNTIME" = "podman" ]; then
        COMPOSE_FILE="docker-compose.podman.yml"
        echo "📄 Using Podman-optimized compose file"
    else
        echo "📄 Using Docker compose file"
    fi

    # Calculate and export application version from git tags
    if [ -d ".git" ]; then
        VITE_APP_VERSION=$(git describe --tags --abbrev=7 2>/dev/null || git rev-parse --short HEAD 2>/dev/null || echo "0.0.0-dev")
        # Remove 'v' prefix if present for VITE_APP_VERSION
        VITE_APP_VERSION="${VITE_APP_VERSION#v}"
        # Append -local to indicate local development
        VITE_APP_VERSION="${VITE_APP_VERSION}-local"
        export VITE_APP_VERSION
        echo "📦 Version: $VITE_APP_VERSION"
        
        # Update .env in MediaSet.Remix for Vite to pick up version in dev server
        sed -i "s/^VITE_APP_VERSION=.*/VITE_APP_VERSION=$VITE_APP_VERSION/" .env
        sed -i "s/^VITE_APP_VERSION=.*/VITE_APP_VERSION=$VITE_APP_VERSION/" MediaSet.Remix/.env.local 2>/dev/null || echo "VITE_APP_VERSION=$VITE_APP_VERSION" > MediaSet.Remix/.env.local
    fi

    # Determine local IP for VITE_API_URL for local development
    # Try to get the local IP address (not 127.0.0.1, but the machine's IP on the network)
    LOCAL_IP=$(hostname -I 2>/dev/null | awk '{print $1}' || echo "localhost")
    VITE_API_URL="http://${LOCAL_IP}:5000"
    export VITE_API_URL
    echo "🌐 API URL: $VITE_API_URL"
    
    # Update .env.local in MediaSet.Remix for Vite to pick up the API URL
    echo "VITE_API_URL=$VITE_API_URL" >> MediaSet.Remix/.env.local 2>/dev/null || echo "VITE_API_URL=$VITE_API_URL" > MediaSet.Remix/.env.local

    # Create necessary directories if they don't exist
    mkdir -p ~/.nuget/packages
    mkdir -p ~/.dotnet/tools
    # Create persistent data dir for MongoDB
    mkdir -p ./data/mongodb
        # Workaround: some Linux systems have a ~/.docker/config.json that references
        # docker-credential-desktop (installed with Docker Desktop). On systems without
        # that helper (typical Linux servers/WSL without Desktop) docker/compose can
        # error with "docker-credential-desktop not installed or available in PATH".
        # Create a temporary DOCKER_CONFIG that strips `credsStore`/`credHelpers` so
        # docker/compose won't try to invoke the missing helper. This only affects
        # commands run by this script and is cleaned up on exit.
        if [ "$CONTAINER_RUNTIME" = "docker" ]; then
            if [ -f "$HOME/.docker/config.json" ]; then
                if (grep -q '"credsStore"' "$HOME/.docker/config.json" 2>/dev/null) || (grep -q '"credHelpers"' "$HOME/.docker/config.json" 2>/dev/null); then
                    if ! command -v docker-credential-desktop >/dev/null 2>&1; then
                        echo "⚠️  docker credential helper 'docker-credential-desktop' not found. Creating temporary DOCKER_CONFIG without credsStore to avoid errors."
                        TMP_DOCKER_CONFIG=$(mktemp -d)
                        # Try to cleanly remove the keys using python if available
                        if command -v python3 >/dev/null 2>&1; then
python3 - <<'PY' > "$TMP_DOCKER_CONFIG/config.json"
import json,sys,os
p=os.path.expanduser('~/.docker/config.json')
try:
    j=json.load(open(p))
except Exception:
    j={}
j.pop('credsStore',None)
j.pop('credHelpers',None)
json.dump(j,sys.stdout)
PY
                        else
                            # Fallback: create a minimal config so docker doesn't attempt helpers
                            echo '{"auths":{}}' > "$TMP_DOCKER_CONFIG/config.json"
                        fi
                        export DOCKER_CONFIG="$TMP_DOCKER_CONFIG"
                        # Clean up temp dir on script exit
                        trap 'rm -rf "$TMP_DOCKER_CONFIG"' EXIT
                    fi
                fi
            fi
        fi
}

# Quick TCP port wait helper (faster than repeated exec into containers)
wait_for_port() {
    local host="$1"      # e.g., 127.0.0.1
    local port="$2"      # e.g., 27017
    local timeout_sec="${3:-30}"
    local interval="${4:-0.5}"

    echo "⏳ Waiting for $host:$port to accept TCP connections..."
    timeout "$timeout_sec" bash -c "until (echo > /dev/tcp/$host/$port) >/dev/null 2>&1; do sleep $interval; done"
}

# Function to check if containers are healthy (scoped by target)
check_health() {
    local target="${1:-all}"
    local check_mongo=false
    local check_api=false
    local check_frontend=false

    case "$target" in
        all|ALL|*)
            # Default to checking everything if target is empty or 'all'
            check_mongo=true
            check_api=true
            check_frontend=true
            ;;
    esac

    # Fine-tune based on explicit targets
    if [[ "$target" == "mongodb" || "$target" == "mongo" ]]; then
        check_mongo=true
        check_api=false
        check_frontend=false
    fi
    if [[ "$target" == "api" || "$target" == "backend" ]]; then
        check_mongo=true   # API depends on Mongo
        check_api=true
        check_frontend=false
    fi
    if [[ "$target" == "frontend" || "$target" == "remix" ]]; then
        check_mongo=true   # Frontend -> API -> Mongo
        check_api=true
        check_frontend=true
    fi

    echo "🔍 Checking container health (target: $target)..."

    if $check_mongo; then
        # Fast readiness: wait for port 27017 on localhost (bound by compose)
        wait_for_port 127.0.0.1 27017 30 0.5 || { echo "❌ MongoDB port not ready"; return 1; }
        # Optional: a brief grace period to allow full readiness after bind
        sleep 0.5
    fi

    if $check_api; then
        echo "⏳ Waiting for API to be ready..."
        timeout 60 bash -c 'until curl -s http://localhost:5000/health > /dev/null 2>&1; do sleep 2; done' || {
            echo "❌ API health check failed"; return 1;
        }
    fi

    if $check_frontend; then
        echo "⏳ Waiting for Frontend to be ready..."
        timeout 60 bash -c 'until curl -s http://localhost:3000 > /dev/null 2>&1; do sleep 2; done' || {
            echo "❌ Frontend health check failed"; return 1;
        }
    fi

    echo "✅ Health checks passed"
}

# Function to start development environment
start_dev() {
    local target="${1:-all}"
    echo "🔧 Building and starting development containers (target: $target)..."

    if [[ -z "$target" || "$target" == "all" ]]; then
        $COMPOSE_COMMAND -f $COMPOSE_FILE up --build -d
    else
        # Normalize common aliases
        case "$target" in
            backend) target="api" ;;
            remix) target="frontend" ;;
            mongo) target="mongodb" ;;
            api+frontend|frontend+api|app)
                echo "🚀 Starting API and Frontend..."
                $COMPOSE_COMMAND -f $COMPOSE_FILE up --build -d api frontend
                check_health "frontend"  # ensures api+frontend+mongo
                echo "✅ Started API and Frontend"
                return
                ;;
        esac
        $COMPOSE_COMMAND -f $COMPOSE_FILE up --build -d "$target"
    fi

    check_health "$target"

    echo ""
    echo "🎉 Development environment is ready!"
    echo ""
    echo "📋 Available services:"
    echo "   🌐 Frontend (Remix):     http://localhost:3000"
    echo "   🚀 API (.NET):           http://localhost:5000"
    echo "   🗄️  MongoDB:              mongodb://localhost:27017"
    echo ""
    echo "📝 Useful commands:"
    echo "   View logs:               ./dev.sh logs [service] [-f]"
    echo "   Stop services:           ./dev.sh stop [api|frontend|mongo]"
    echo "   Restart services:        ./dev.sh restart [api|frontend|mongo]"
    echo "   Clean (keep data):       ./dev.sh clean"
    echo "   Clean & purge data:      ./dev.sh clean --purge"
    echo ""
}

# Function to show logs
show_logs() {
    if [ -n "$2" ]; then
        if [ "$3" = "-f" ] || [ "$3" = "--follow" ]; then
            $COMPOSE_COMMAND -f $COMPOSE_FILE logs -f "$2"
        else
            $COMPOSE_COMMAND -f $COMPOSE_FILE logs --tail=50 "$2"
        fi
    else
        if [ "$2" = "-f" ] || [ "$2" = "--follow" ]; then
            $COMPOSE_COMMAND -f $COMPOSE_FILE logs -f
        else
            $COMPOSE_COMMAND -f $COMPOSE_FILE logs --tail=50
        fi
    fi
}

# Function to stop services
stop_dev() {
    local target="${1:-all}"
    if [[ -z "$target" || "$target" == "all" ]]; then
        echo "🛑 Stopping all development containers..."
        $COMPOSE_COMMAND -f $COMPOSE_FILE down
    else
        case "$target" in
            backend) target="api" ;;
            remix) target="frontend" ;;
            mongo) target="mongodb" ;;
            api+frontend|frontend+api|app)
                echo "🛑 Stopping API and Frontend (leaving MongoDB running)..."
                $COMPOSE_COMMAND -f $COMPOSE_FILE stop api frontend
                echo "✅ Stopped API and Frontend"
                return
                ;;
        esac
        echo "🛑 Stopping container: $target (leaving others running) ..."
        $COMPOSE_COMMAND -f $COMPOSE_FILE stop "$target"
    fi
    echo "✅ Stop complete"
}

# Function to restart services
restart_dev() {
    local target="${1:-all}"
    if [[ -z "$target" || "$target" == "all" ]]; then
        echo "🔄 Restarting all services..."
        $COMPOSE_COMMAND -f $COMPOSE_FILE restart
        check_health "all"
    else
        case "$target" in
            backend) target="api" ;;
            remix) target="frontend" ;;
            mongo) target="mongodb" ;;
            api+frontend|frontend+api|app)
                echo "🔄 Restarting API and Frontend..."
                $COMPOSE_COMMAND -f $COMPOSE_FILE restart api frontend
                check_health "frontend"  # ensures api+frontend+mongo
                return
                ;;
        esac
        echo "🔄 Restarting service: $target ..."
        $COMPOSE_COMMAND -f $COMPOSE_FILE restart "$target"
        check_health "$target"
    fi
}

# Function to clean everything
clean_dev() {
    local mode="$1" # empty or --purge
    echo "🧹 Cleaning development environment..."
    if [ "$mode" = "--purge" ] || [ "$mode" = "purge" ] || [ "$mode" = "all" ]; then
        echo "⚠️  Purge mode: removing containers, networks, and volumes (this deletes MongoDB data)"
        $COMPOSE_COMMAND -f $COMPOSE_FILE down -v --remove-orphans
        # Optionally remove local data directory too, to truly reset
        rm -rf ./data/mongodb || true
    else
        echo "🧼 Default clean: removing containers and networks (keeping volumes and ./data)"
        $COMPOSE_COMMAND -f $COMPOSE_FILE down --remove-orphans
    fi
    if [ "$CONTAINER_RUNTIME" = "docker" ]; then
        docker system prune -f
    else
        podman system prune -f
    fi
    echo "✅ Environment cleaned"
}

# Function to show status
status_dev() {
    echo "📊 Development environment status:"
    $COMPOSE_COMMAND -f $COMPOSE_FILE ps
}

# Function to enter a container shell
shell_dev() {
    case "$2" in
        api|backend)
            $COMPOSE_COMMAND -f $COMPOSE_FILE exec api bash
            ;;
        frontend|remix)
            $COMPOSE_COMMAND -f $COMPOSE_FILE exec frontend sh
            ;;
        mongo|mongodb)
            $COMPOSE_COMMAND -f $COMPOSE_FILE exec mongodb mongosh MediaSet
            ;;
        *)
            echo "Available shells: api, frontend, mongo"
            echo "Usage: ./dev.sh shell [api|frontend|mongo]"
            ;;
    esac
}

# Main command handler
case "$1" in
    help|--help|-h|"")
        # Show help and exit before container detection
        echo "MediaSet Development Environment Manager"
        echo "🐳 Supports both Docker and Podman (auto-detected)"
        echo ""
    echo "Usage: $0 {start|stop|restart|logs|status|shell|clean} [service]"
        echo ""
        echo "Commands:"
    echo "  start [service]   - Start environment or a single service (use 'api+frontend' or 'app' for both)"
    echo "  stop [service]    - Stop environment or a single service (use 'api+frontend' or 'app' for both)"
    echo "  restart [service] - Restart all or a single service (use 'api+frontend' or 'app' to restart both)"
    echo "  logs [service]    - Show recent logs (add -f to follow)"
        echo "  status    - Show container status"
    echo "  shell     - Enter container shell (api|frontend|mongo)"
    echo "  clean     - Stop and remove containers and networks (keeps data)"
    echo "  clean --purge - Also remove volumes and local ./data (deletes data)"
        echo ""
        echo "Examples:"
    echo "  $0 start                # Start everything"
    echo "  $0 start api            # Start just the API (MongoDB will start if needed)"
    echo "  $0 start app            # Start API and Frontend (MongoDB if needed)"
    echo "  $0 stop frontend        # Stop only the frontend (keep API/Mongo running)"
    echo "  $0 stop app             # Stop API and Frontend (keep MongoDB running)"
    echo "  $0 restart api          # Restart only the API"
    echo "  $0 restart api+frontend # Restart API and Frontend (Mongo stays up)"
        echo "  $0 logs api          # Show recent API logs"
        echo "  $0 logs api -f       # Follow API logs (Ctrl+C to exit)"
        echo "  $0 shell frontend"
        echo "  $0 clean             # Remove containers, keep data"
        echo "  $0 clean --purge     # Remove everything including data"
        echo ""
        echo "Container Runtime Support:"
        echo "  🐳 Docker     - Uses docker-compose.dev.yml"
        echo "  🦭 Podman     - Uses docker-compose.podman.yml"
        echo ""
        echo "Need help installing? See CONTAINER_SETUP.md"
        exit 0
        ;;
    start|up)
        setup_container_runtime
        start_dev "$2"
        ;;
    stop|down)
        setup_container_runtime
        stop_dev "$2"
        ;;
    restart)
        setup_container_runtime
        restart_dev "$2"
        ;;
    logs)
        setup_container_runtime
        show_logs "$@"
        ;;
    status|ps)
        setup_container_runtime
        status_dev
        ;;
    shell|exec)
        setup_container_runtime
        shell_dev "$@"
        ;;
    clean)
        setup_container_runtime
        clean_dev "$2"
        ;;
    *)
        echo "❌ Unknown command: $1"
        echo ""
        echo "MediaSet Development Environment Manager"
        echo "🐳 Supports both Docker and Podman (auto-detected)"
        echo ""
        echo "Usage: $0 {start|stop|restart|logs|status|shell|clean}"
        echo ""
        echo "Run '$0 help' for detailed information"
        exit 1
        ;;
esac