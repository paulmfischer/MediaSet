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

    # Create necessary directories if they don't exist
    mkdir -p ~/.nuget/packages
    mkdir -p ~/.dotnet/tools
}

# Function to check if containers are healthy
check_health() {
    echo "🔍 Checking container health..."
    
    # Wait for MongoDB to be ready
    echo "⏳ Waiting for MongoDB to be ready..."
    timeout 60 bash -c "until $COMPOSE_COMMAND -f $COMPOSE_FILE exec -T mongodb mongosh --eval \"db.admin.hello()\" > /dev/null 2>&1; do sleep 2; done"
    
    # Wait for API to be ready
    echo "⏳ Waiting for API to be ready..."
    timeout 60 bash -c 'until curl -s http://localhost:5000/health > /dev/null 2>&1; do sleep 2; done'
    
    # Wait for frontend to be ready
    echo "⏳ Waiting for Frontend to be ready..."
    timeout 60 bash -c 'until curl -s http://localhost:3000 > /dev/null 2>&1; do sleep 2; done'
    
    echo "✅ All services are healthy!"
}

# Function to start development environment
start_dev() {
    echo "🔧 Building and starting development containers..."
    $COMPOSE_COMMAND -f $COMPOSE_FILE up --build -d
    
    check_health
    
    echo ""
    echo "🎉 Development environment is ready!"
    echo ""
    echo "📋 Available services:"
    echo "   🌐 Frontend (Remix):     http://localhost:3000"
    echo "   🚀 API (.NET):           http://localhost:5000"
    echo "    MongoDB:              mongodb://localhost:27017"
    echo ""
    echo "📝 Useful commands:"
    echo "   View logs:               ./dev.sh logs"
    echo "   Stop services:           ./dev.sh stop"
    echo "   Restart services:        ./dev.sh restart"
    echo "   Clean everything:        ./dev.sh clean"
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
    echo "🛑 Stopping development environment..."
    $COMPOSE_COMMAND -f $COMPOSE_FILE down
    echo "✅ Services stopped"
}

# Function to restart services
restart_dev() {
    echo "🔄 Restarting development environment..."
    $COMPOSE_COMMAND -f $COMPOSE_FILE restart
    check_health
}

# Function to clean everything
clean_dev() {
    echo "🧹 Cleaning development environment..."
    $COMPOSE_COMMAND -f $COMPOSE_FILE down -v --remove-orphans
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
        echo "Usage: $0 {start|stop|restart|logs|status|shell|clean}"
        echo ""
        echo "Commands:"
        echo "  start     - Start the development environment"
        echo "  stop      - Stop the development environment" 
        echo "  restart   - Restart all services"
        echo "  logs      - Show recent logs (add service name for specific service)"
        echo "  status    - Show container status"
        echo "  shell     - Enter container shell (api|frontend|mongo)"
        echo "  clean     - Stop and remove all containers, volumes, and images"
        echo ""
        echo "Examples:"
        echo "  $0 start"
        echo "  $0 logs api          # Show recent API logs"
        echo "  $0 logs api -f       # Follow API logs (Ctrl+C to exit)"
        echo "  $0 shell frontend"
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
        start_dev
        ;;
    stop|down)
        setup_container_runtime
        stop_dev
        ;;
    restart)
        setup_container_runtime
        restart_dev
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
        clean_dev
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