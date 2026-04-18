#!/bin/bash

# MediaSet Development Environment Setup Script
set -e

COMPOSE_COMMAND="podman-compose"
COMPOSE_FILE="docker-compose.dev.yml"

# Verify Podman and podman-compose are available
check_requirements() {
    if ! command -v podman &> /dev/null; then
        echo "❌ Podman is not installed."
        echo "Install Podman: https://podman.io/getting-started/installation"
        exit 1
    fi

    if ! command -v podman-compose &> /dev/null; then
        echo "❌ podman-compose is not installed."
        echo "Install via: pip install podman-compose"
        echo "Or via your package manager: dnf install podman-compose / apt install podman-compose"
        exit 1
    fi

    echo "✅ Podman: $(podman --version)"
    echo "✅ podman-compose: $(podman-compose --version 2>&1 | head -1)"
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
    local check_ui=false

    case "$target" in
        all|ALL|*)
            # Default to checking everything if target is empty or 'all'
            check_mongo=true
            check_api=true
            check_ui=true
            ;;
    esac

    # Fine-tune based on explicit targets
    if [[ "$target" == "mongodb" || "$target" == "mongo" ]]; then
        check_mongo=true
        check_api=false
        check_ui=false
    fi
    if [[ "$target" == "api" || "$target" == "backend" ]]; then
        check_mongo=true   # API depends on Mongo
        check_api=true
        check_ui=false
    fi
    if [[ "$target" == "ui" || "$target" == "remix" ]]; then
        check_mongo=true   # UI -> API -> Mongo
        check_api=true
        check_ui=true
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

    if $check_ui; then
        echo "⏳ Waiting for UI to be ready..."
        timeout 60 bash -c 'until curl -s http://localhost:3000 > /dev/null 2>&1; do sleep 2; done' || {
            echo "❌ UI health check failed"; return 1;
        }
    fi

    echo "✅ Health checks passed"
}

# Function to clean NuGet cache to prevent restore issues
clean_nuget_cache() {
    echo "🧼 Clearing NuGet cache to prevent restore issues..."
    rm -rf ~/.nuget/http-cache 2>/dev/null || true
    echo "✅ NuGet cache cleared"
}

# Function to start development environment
start_dev() {
    local target="${1}"
    local clean_build="${2}"

    # If only --clean is provided (no explicit target), treat as default (api+ui)
    if [[ "$target" == "--clean" ]]; then
        target=""
        clean_build="--clean"
    fi

    prune_dev_images
    clean_nuget_cache

    local build_flags="--build"
    if [ "$clean_build" = "--clean" ]; then
        build_flags="--build --no-cache"
        echo "🧹 Forcing clean rebuild (skipping Podman layer cache)..."
    fi

    if [[ "$target" == "all" ]]; then
        echo "🔧 Building and starting all containers..."
        $COMPOSE_COMMAND -f $COMPOSE_FILE up $build_flags -d
        check_health "all"
    elif [[ -z "$target" ]]; then
        echo "🔧 Building and starting API and UI..."
        $COMPOSE_COMMAND -f $COMPOSE_FILE up $build_flags -d api ui
        check_health "ui"
    else
        # Normalize aliases and start a single service
        case "$target" in
            backend) target="api" ;;
            remix)   target="ui" ;;
            mongo)   target="mongodb" ;;
        esac
        echo "🔧 Building and starting $target..."
        $COMPOSE_COMMAND -f $COMPOSE_FILE up $build_flags -d "$target"
        check_health "$target"
    fi

    echo ""
    echo "🎉 Development environment is ready!"
    echo ""
    echo "📋 Available services:"
    echo "   🌐 UI (Remix):           http://localhost:3000"
    echo "   🚀 API (.NET):           http://localhost:5000"
    echo "   🗄️  MongoDB:              mongodb://localhost:27017"
    echo ""
    echo "📝 Useful commands:"
    echo "   View logs:               ./dev.sh logs [service] [-f]"
    echo "   Stop api+ui:             ./dev.sh stop"
    echo "   Stop all:                ./dev.sh stop all"
    echo "   Restart api+ui:          ./dev.sh restart"
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
    local target="${1}"
    if [[ "$target" == "all" ]]; then
        echo "🛑 Stopping all development containers..."
        $COMPOSE_COMMAND -f $COMPOSE_FILE down
    elif [[ -z "$target" ]]; then
        echo "🛑 Stopping API and UI (MongoDB keeps running)..."
        $COMPOSE_COMMAND -f $COMPOSE_FILE stop api ui
    else
        case "$target" in
            backend) target="api" ;;
            remix)   target="ui" ;;
            mongo)   target="mongodb" ;;
        esac
        echo "🛑 Stopping $target (leaving others running)..."
        $COMPOSE_COMMAND -f $COMPOSE_FILE stop "$target"
    fi
    prune_dev_images
    echo "✅ Stop complete"
}

# Function to restart services
restart_dev() {
    local target="${1}"
    if [[ "$target" == "all" ]]; then
        echo "🔄 Restarting all services..."
        $COMPOSE_COMMAND -f $COMPOSE_FILE restart
        check_health "all"
    elif [[ -z "$target" ]]; then
        echo "🔄 Restarting API and UI..."
        $COMPOSE_COMMAND -f $COMPOSE_FILE restart api ui
        check_health "ui"
    else
        case "$target" in
            backend) target="api" ;;
            remix)   target="ui" ;;
            mongo)   target="mongodb" ;;
        esac
        echo "🔄 Restarting $target..."
        $COMPOSE_COMMAND -f $COMPOSE_FILE restart "$target"
        check_health "$target"
    fi
}

# Function to prune dangling Podman images
prune_dev_images() {
    # Skip if PODMAN_AUTO_PRUNE is explicitly set to 0
    if [ "${PODMAN_AUTO_PRUNE}" = "0" ]; then
        return
    fi

    podman image prune -f > /dev/null 2>&1 || true
}

# Function to clean everything
clean_dev() {
    local mode="$1" # empty or --purge
    echo "🧹 Cleaning development environment..."
    if [ "$mode" = "--purge" ] || [ "$mode" = "purge" ] || [ "$mode" = "all" ]; then
        echo "⚠️  Purge mode: removing containers, networks, and volumes (this deletes MongoDB data)"
        $COMPOSE_COMMAND -f $COMPOSE_FILE down -v --remove-orphans
        rm -rf ./data/mongodb || true
    else
        echo "🧼 Default clean: removing containers and networks (keeping volumes and ./data)"
        $COMPOSE_COMMAND -f $COMPOSE_FILE down --remove-orphans
    fi
    podman system prune -f
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
        ui|remix)
            $COMPOSE_COMMAND -f $COMPOSE_FILE exec ui sh
            ;;
        mongo|mongodb)
            $COMPOSE_COMMAND -f $COMPOSE_FILE exec mongodb mongosh MediaSet
            ;;
        *)
            echo "Available shells: api, ui, mongo"
            echo "Usage: ./dev.sh shell [api|ui|mongo]"
            ;;
    esac
}

# Calculate and export application version from git tags
setup_version() {
    if [ -d ".git" ]; then
        VITE_APP_VERSION=$(git describe --tags --abbrev=7 2>/dev/null || git rev-parse --short HEAD 2>/dev/null || echo "0.0.0-dev")
        # Remove 'v' prefix if present
        VITE_APP_VERSION="${VITE_APP_VERSION#v}"
        # Append -local to indicate local development
        VITE_APP_VERSION="${VITE_APP_VERSION}-local"
        export VITE_APP_VERSION
        echo "📦 Version: $VITE_APP_VERSION"

        # Update .env in MediaSet.Remix for Vite to pick up version in dev server
        sed -i "s/^VITE_APP_VERSION=.*/VITE_APP_VERSION=$VITE_APP_VERSION/" .env
        sed -i "s/^VITE_APP_VERSION=.*/VITE_APP_VERSION=$VITE_APP_VERSION/" MediaSet.Remix/.env.local 2>/dev/null || echo "VITE_APP_VERSION=$VITE_APP_VERSION" > MediaSet.Remix/.env.local
    fi
}

# Determine local IP for VITE_API_URL
setup_api_url() {
    LOCAL_IP=$(hostname -I 2>/dev/null | awk '{print $1}' || echo "localhost")
    VITE_API_URL="http://${LOCAL_IP}:5000"
    export VITE_API_URL
    echo "🌐 API URL: $VITE_API_URL"

    echo "VITE_API_URL=$VITE_API_URL" >> MediaSet.Remix/.env.local 2>/dev/null || echo "VITE_API_URL=$VITE_API_URL" > MediaSet.Remix/.env.local
}

# Create necessary local directories
setup_directories() {
    mkdir -p ~/.nuget/packages
    mkdir -p ~/.dotnet/tools
    mkdir -p ./data/mongodb
}

# Main command handler
case "$1" in
    help|--help|-h|"")
        echo "MediaSet Development Environment Manager"
        echo "🦭 Powered by Podman"
        echo ""
        echo "Usage: $0 {start|stop|restart|logs|status|shell|clean|rebuild} [service]"
        echo ""
        echo "Commands:"
        echo "  start [service|all]      - Start api+ui by default, or a specific service, or 'all' (includes mongo)"
        echo "  start [service] --clean  - Start with clean rebuild (no Podman layer cache)"
        echo "  stop [service|all]       - Stop api+ui by default, or a specific service, or 'all' (includes mongo)"
        echo "  restart [service|all]    - Restart api+ui by default, or a specific service, or 'all'"
        echo "  logs [service]           - Show recent logs (add -f to follow)"
        echo "  status                   - Show container status"
        echo "  shell                    - Enter container shell (api|ui|mongo)"
        echo "  clean                    - Stop and remove containers and networks (keeps data)"
        echo "  clean --purge            - Also remove volumes and local ./data (deletes data)"
        echo "  rebuild                  - Rebuild everything from scratch (removes old images, clears caches, fresh build)"
        echo "  cache-clean              - Clear NuGet cache (useful if builds fail with package not found errors)"
        echo ""
        echo "Examples:"
        echo "  $0 start                # Start api+ui (mongo starts automatically if needed)"
        echo "  $0 start all            # Start everything including mongo"
        echo "  $0 start api            # Start just the API"
        echo "  $0 start --clean        # Clean rebuild of api+ui (no Podman layer cache)"
        echo "  $0 stop                 # Stop api+ui (mongo keeps running)"
        echo "  $0 stop all             # Stop everything"
        echo "  $0 stop ui              # Stop only the UI"
        echo "  $0 restart              # Restart api+ui"
        echo "  $0 restart api          # Restart only the API"
        echo "  $0 logs api             # Show recent API logs"
        echo "  $0 logs api -f          # Follow API logs (Ctrl+C to exit)"
        echo "  $0 shell ui"
        echo "  $0 clean                # Remove containers, keep data"
        echo "  $0 clean --purge        # Remove everything including data"
        echo "  $0 rebuild              # Full rebuild from scratch (recommended if persistent build failures)"
        echo ""
        echo "Environment Variables:"
        echo "  PODMAN_AUTO_PRUNE=0  - Disable auto-pruning of dangling Podman images (default: enabled)"
        echo ""
        echo "Note: Podman automatically prunes dangling images on 'start' and 'stop' to prevent"
        echo "accumulation from repeated rebuilds. Set PODMAN_AUTO_PRUNE=0 to disable this behavior."
        echo ""
        echo "Need help installing Podman? See Development/DEVELOPMENT.md"
        exit 0
        ;;
    start|up)
        check_requirements
        setup_version
        setup_api_url
        setup_directories
        start_dev "$2" "$3"
        ;;
    stop|down)
        check_requirements
        stop_dev "$2"
        ;;
    restart)
        check_requirements
        restart_dev "$2"
        ;;
    logs)
        check_requirements
        show_logs "$@"
        ;;
    status|ps)
        check_requirements
        status_dev
        ;;
    shell|exec)
        check_requirements
        shell_dev "$@"
        ;;
    clean)
        check_requirements
        clean_dev "$2"
        ;;
    cache-clean)
        clean_nuget_cache
        ;;
    rebuild)
        check_requirements
        echo "🔨 Performing full rebuild from scratch..."
        echo "  1️⃣  Cleaning environment and removing old images..."
        clean_dev "purge"
        echo ""
        echo "  2️⃣  Clearing NuGet cache..."
        clean_nuget_cache
        echo ""
        echo "  3️⃣  Rebuilding from scratch..."
        setup_version
        setup_api_url
        setup_directories
        start_dev "all" "--clean"
        ;;
    *)
        echo "❌ Unknown command: $1"
        echo ""
        echo "MediaSet Development Environment Manager"
        echo "🦭 Powered by Podman"
        echo ""
        echo "Usage: $0 {start|stop|restart|logs|status|shell|clean}"
        echo ""
        echo "Run '$0 help' for detailed information"
        exit 1
        ;;
esac
