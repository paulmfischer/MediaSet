#!/bin/bash

# MediaSet Development Environment Setup Script
set -e

echo "🚀 Setting up MediaSet Development Environment"

# Check if Docker and Docker Compose are installed
if ! command -v docker &> /dev/null; then
    echo "❌ Docker is not installed. Please install Docker first."
    exit 1
fi

if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null; then
    echo "❌ Docker Compose is not installed. Please install Docker Compose first."
    exit 1
fi

# Create necessary directories if they don't exist
mkdir -p ~/.nuget/packages
mkdir -p ~/.dotnet/tools

# Function to check if containers are healthy
check_health() {
    echo "🔍 Checking container health..."
    
    # Wait for MongoDB to be ready
    echo "⏳ Waiting for MongoDB to be ready..."
    timeout 60 bash -c 'until docker-compose -f docker-compose.dev.yml exec -T mongodb mongosh --eval "db.admin.hello()" > /dev/null 2>&1; do sleep 2; done'
    
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
    docker-compose -f docker-compose.dev.yml up --build -d
    
    check_health
    
    echo ""
    echo "🎉 Development environment is ready!"
    echo ""
    echo "📋 Available services:"
    echo "   🌐 Frontend (Remix):     http://localhost:3000"
    echo "   🚀 API (.NET):           http://localhost:5000"
    echo "   🔒 API (HTTPS):          https://localhost:5001"
    echo "   📊 MongoDB:              mongodb://localhost:27017"
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
        docker-compose -f docker-compose.dev.yml logs -f "$2"
    else
        docker-compose -f docker-compose.dev.yml logs -f
    fi
}

# Function to stop services
stop_dev() {
    echo "🛑 Stopping development environment..."
    docker-compose -f docker-compose.dev.yml down
    echo "✅ Services stopped"
}

# Function to restart services
restart_dev() {
    echo "🔄 Restarting development environment..."
    docker-compose -f docker-compose.dev.yml restart
    check_health
}

# Function to clean everything
clean_dev() {
    echo "🧹 Cleaning development environment..."
    docker-compose -f docker-compose.dev.yml down -v --remove-orphans
    docker system prune -f
    echo "✅ Environment cleaned"
}

# Function to show status
status_dev() {
    echo "📊 Development environment status:"
    docker-compose -f docker-compose.dev.yml ps
}

# Function to enter a container shell
shell_dev() {
    case "$2" in
        api|backend)
            docker-compose -f docker-compose.dev.yml exec api bash
            ;;
        frontend|remix)
            docker-compose -f docker-compose.dev.yml exec frontend sh
            ;;
        mongo|mongodb)
            docker-compose -f docker-compose.dev.yml exec mongodb mongosh MediaSet
            ;;
        *)
            echo "Available shells: api, frontend, mongo"
            echo "Usage: ./dev.sh shell [api|frontend|mongo]"
            ;;
    esac
}

# Main command handler
case "$1" in
    start|up)
        start_dev
        ;;
    stop|down)
        stop_dev
        ;;
    restart)
        restart_dev
        ;;
    logs)
        show_logs "$@"
        ;;
    status|ps)
        status_dev
        ;;
    shell|exec)
        shell_dev "$@"
        ;;
    clean)
        clean_dev
        ;;
    *)
        echo "MediaSet Development Environment Manager"
        echo ""
        echo "Usage: $0 {start|stop|restart|logs|status|shell|clean}"
        echo ""
        echo "Commands:"
        echo "  start     - Start the development environment"
        echo "  stop      - Stop the development environment"
        echo "  restart   - Restart all services"
        echo "  logs      - Show logs (add service name for specific service)"
        echo "  status    - Show container status"
        echo "  shell     - Enter container shell (api|frontend|mongo)"
        echo "  clean     - Stop and remove all containers, volumes, and images"
        echo ""
        echo "Examples:"
        echo "  $0 start"
        echo "  $0 logs api"
        echo "  $0 shell frontend"
        exit 1
        ;;
esac