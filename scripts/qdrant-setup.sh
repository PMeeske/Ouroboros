#!/bin/bash
# qdrant-setup.sh - Convenience script for setting up and managing Qdrant vector store locally
# Usage: ./scripts/qdrant-setup.sh [command]

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
QDRANT_HTTP_PORT=6333
QDRANT_GRPC_PORT=6334
QDRANT_CONNECTION_STRING="http://localhost:${QDRANT_HTTP_PORT}"
DEFAULT_COLLECTION="pipeline_vectors"

# Helper functions
print_header() {
    echo -e "${BLUE}========================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}========================================${NC}"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_info() {
    echo -e "${BLUE}ℹ $1${NC}"
}

# Check if docker is installed
check_docker() {
    if ! command -v docker &> /dev/null; then
        print_error "Docker is not installed. Please install Docker first."
        exit 1
    fi
    print_success "Docker is installed"
}

# Check if docker-compose is installed
check_docker_compose() {
    if ! command -v docker-compose &> /dev/null; then
        print_error "Docker Compose is not installed. Please install Docker Compose first."
        exit 1
    fi
    print_success "Docker Compose is installed"
}

# Check if Qdrant is running
is_qdrant_running() {
    if docker ps | grep -q qdrant; then
        return 0
    else
        return 1
    fi
}

# Check if Qdrant is healthy
is_qdrant_healthy() {
    if curl -sf "${QDRANT_CONNECTION_STRING}/health" > /dev/null 2>&1; then
        return 0
    else
        return 1
    fi
}

# Start Qdrant
start_qdrant() {
    print_header "Starting Qdrant Vector Database"
    
    check_docker
    check_docker_compose
    
    if is_qdrant_running; then
        print_warning "Qdrant is already running"
    else
        print_info "Starting Qdrant container..."
        docker-compose up -d qdrant
        
        print_info "Waiting for Qdrant to become healthy..."
        MAX_RETRIES=30
        RETRY_COUNT=0
        
        while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
            if is_qdrant_healthy; then
                print_success "Qdrant is running and healthy!"
                break
            fi
            sleep 1
            RETRY_COUNT=$((RETRY_COUNT + 1))
            echo -n "."
        done
        echo ""
        
        if [ $RETRY_COUNT -eq $MAX_RETRIES ]; then
            print_error "Qdrant failed to start within 30 seconds"
            print_info "Check logs with: docker logs qdrant"
            exit 1
        fi
    fi
    
    print_success "Qdrant HTTP API: ${QDRANT_CONNECTION_STRING}"
    print_success "Qdrant gRPC API: localhost:${QDRANT_GRPC_PORT}"
    print_success "Qdrant Dashboard: ${QDRANT_CONNECTION_STRING}/dashboard"
}

# Stop Qdrant
stop_qdrant() {
    print_header "Stopping Qdrant Vector Database"
    
    if is_qdrant_running; then
        print_info "Stopping Qdrant container..."
        docker-compose stop qdrant
        print_success "Qdrant stopped"
    else
        print_warning "Qdrant is not running"
    fi
}

# Restart Qdrant
restart_qdrant() {
    print_header "Restarting Qdrant Vector Database"
    stop_qdrant
    sleep 2
    start_qdrant
}

# Check Qdrant status
status_qdrant() {
    print_header "Qdrant Status"
    
    if is_qdrant_running; then
        print_success "Qdrant container is running"
        
        if is_qdrant_healthy; then
            print_success "Qdrant is healthy and responding"
            
            # Get version
            VERSION=$(curl -s "${QDRANT_CONNECTION_STRING}/" | grep -o '"version":"[^"]*"' | cut -d'"' -f4 || echo "unknown")
            print_info "Qdrant version: ${VERSION}"
            
            # Get collections
            print_info "Fetching collections..."
            COLLECTIONS=$(curl -s "${QDRANT_CONNECTION_STRING}/collections" | jq -r '.result.collections[].name' 2>/dev/null || echo "")
            
            if [ -z "$COLLECTIONS" ]; then
                print_info "No collections found"
            else
                print_info "Collections:"
                echo "$COLLECTIONS" | while read -r collection; do
                    echo "  - $collection"
                done
            fi
        else
            print_error "Qdrant is not responding to health checks"
        fi
    else
        print_error "Qdrant container is not running"
        print_info "Start Qdrant with: ./scripts/qdrant-setup.sh start"
    fi
}

# View Qdrant logs
logs_qdrant() {
    print_header "Qdrant Logs"
    
    if is_qdrant_running; then
        docker logs qdrant --tail 50 --follow
    else
        print_error "Qdrant is not running"
        exit 1
    fi
}

# Configure Ouroboros to use Qdrant
configure_pipeline() {
    print_header "Configuring Ouroboros for Qdrant"
    
    # Check if .env exists
    if [ ! -f .env ]; then
        print_info "Creating .env file from .env.example..."
        cp .env.example .env
    fi
    
    # Update .env file
    print_info "Updating .env configuration..."
    
    # Use sed to update or add configuration
    if grep -q "PIPELINE__VectorStore__Type" .env; then
        sed -i.bak 's/^PIPELINE__VectorStore__Type=.*/PIPELINE__VectorStore__Type=Qdrant/' .env
    else
        echo "PIPELINE__VectorStore__Type=Qdrant" >> .env
    fi
    
    if grep -q "PIPELINE__VectorStore__ConnectionString" .env; then
        sed -i.bak "s|^.*PIPELINE__VectorStore__ConnectionString=.*|PIPELINE__VectorStore__ConnectionString=${QDRANT_CONNECTION_STRING}|" .env
    else
        echo "PIPELINE__VectorStore__ConnectionString=${QDRANT_CONNECTION_STRING}" >> .env
    fi
    
    if grep -q "PIPELINE__VectorStore__DefaultCollection" .env; then
        sed -i.bak "s/^.*PIPELINE__VectorStore__DefaultCollection=.*/PIPELINE__VectorStore__DefaultCollection=${DEFAULT_COLLECTION}/" .env
    else
        echo "PIPELINE__VectorStore__DefaultCollection=${DEFAULT_COLLECTION}" >> .env
    fi
    
    # Remove backup files
    rm -f .env.bak
    
    print_success "Configuration updated!"
    print_info "Current Qdrant settings in .env:"
    echo "  Type: Qdrant"
    echo "  Connection: ${QDRANT_CONNECTION_STRING}"
    echo "  Collection: ${DEFAULT_COLLECTION}"
}

# List collections
list_collections() {
    print_header "Qdrant Collections"
    
    if ! is_qdrant_healthy; then
        print_error "Qdrant is not running or not healthy"
        print_info "Start Qdrant with: ./scripts/qdrant-setup.sh start"
        exit 1
    fi
    
    RESPONSE=$(curl -s "${QDRANT_CONNECTION_STRING}/collections")
    
    if command -v jq &> /dev/null; then
        COLLECTIONS=$(echo "$RESPONSE" | jq -r '.result.collections[]')
        
        if [ -z "$COLLECTIONS" ]; then
            print_info "No collections found"
        else
            echo "$RESPONSE" | jq -r '.result.collections[] | "  \(.name): \(.vectors_count // 0) vectors, \(.points_count // 0) points"'
        fi
    else
        echo "$RESPONSE"
        print_warning "Install 'jq' for better JSON formatting"
    fi
}

# Delete a collection
delete_collection() {
    local COLLECTION_NAME=$1
    
    if [ -z "$COLLECTION_NAME" ]; then
        print_error "Collection name required"
        echo "Usage: ./scripts/qdrant-setup.sh delete-collection <collection-name>"
        exit 1
    fi
    
    print_header "Deleting Collection: $COLLECTION_NAME"
    
    if ! is_qdrant_healthy; then
        print_error "Qdrant is not running or not healthy"
        exit 1
    fi
    
    print_warning "Are you sure you want to delete collection '$COLLECTION_NAME'? (y/N)"
    read -r CONFIRM
    
    if [ "$CONFIRM" != "y" ] && [ "$CONFIRM" != "Y" ]; then
        print_info "Deletion cancelled"
        exit 0
    fi
    
    RESPONSE=$(curl -s -X DELETE "${QDRANT_CONNECTION_STRING}/collections/${COLLECTION_NAME}")
    
    if echo "$RESPONSE" | grep -q '"status":"ok"'; then
        print_success "Collection '$COLLECTION_NAME' deleted successfully"
    else
        print_error "Failed to delete collection"
        echo "$RESPONSE"
        exit 1
    fi
}

# Full setup (start + configure)
full_setup() {
    print_header "Full Qdrant Setup for Ouroboros"
    
    start_qdrant
    echo ""
    configure_pipeline
    echo ""
    
    print_success "Setup complete!"
    print_info ""
    print_info "Next steps:"
    print_info "  1. Run the application: dotnet run --project src/Ouroboros.CLI/Ouroboros.CLI.csproj"
    print_info "  2. Access Qdrant dashboard: ${QDRANT_CONNECTION_STRING}/dashboard"
    print_info "  3. Check status: ./scripts/qdrant-setup.sh status"
}

# Clean all data (dangerous!)
clean_data() {
    print_header "Clean Qdrant Data"
    
    print_error "⚠️  WARNING: This will delete ALL Qdrant data including collections and vectors!"
    print_warning "Are you absolutely sure? Type 'DELETE' to confirm:"
    read -r CONFIRM
    
    if [ "$CONFIRM" != "DELETE" ]; then
        print_info "Cleanup cancelled"
        exit 0
    fi
    
    print_info "Stopping Qdrant..."
    docker-compose stop qdrant
    
    print_info "Removing Qdrant volume..."
    docker-compose down -v qdrant
    
    print_success "All Qdrant data has been deleted"
    print_info "Start fresh with: ./scripts/qdrant-setup.sh setup"
}

# Show help
show_help() {
    cat << EOF
Qdrant Vector Store Setup & Management Script

Usage: ./scripts/qdrant-setup.sh [command]

Commands:
  setup                    Full setup: start Qdrant and configure Ouroboros
  start                    Start Qdrant container
  stop                     Stop Qdrant container
  restart                  Restart Qdrant container
  status                   Check Qdrant status and show info
  logs                     View Qdrant logs (follow mode)
  configure                Configure Ouroboros to use Qdrant
  list                     List all collections
  delete-collection <name> Delete a specific collection
  clean                    Delete all Qdrant data (WARNING: destructive!)
  help                     Show this help message

Examples:
  ./scripts/qdrant-setup.sh setup          # Complete setup
  ./scripts/qdrant-setup.sh start          # Start Qdrant only
  ./scripts/qdrant-setup.sh status         # Check status
  ./scripts/qdrant-setup.sh logs           # View logs
  ./scripts/qdrant-setup.sh list           # List collections
  ./scripts/qdrant-setup.sh delete-collection my_vectors  # Delete collection

Qdrant URLs (when running):
  - HTTP API:  ${QDRANT_CONNECTION_STRING}
  - gRPC API:  localhost:${QDRANT_GRPC_PORT}
  - Dashboard: ${QDRANT_CONNECTION_STRING}/dashboard

Documentation:
  - Quick Start: docs/VECTOR_STORES_QUICKSTART.md
  - Full Guide:  docs/VECTOR_STORES.md
EOF
}

# Main script logic
main() {
    local COMMAND=${1:-help}
    
    case $COMMAND in
        setup)
            full_setup
            ;;
        start)
            start_qdrant
            ;;
        stop)
            stop_qdrant
            ;;
        restart)
            restart_qdrant
            ;;
        status)
            status_qdrant
            ;;
        logs)
            logs_qdrant
            ;;
        configure)
            configure_pipeline
            ;;
        list)
            list_collections
            ;;
        delete-collection)
            delete_collection "$2"
            ;;
        clean)
            clean_data
            ;;
        help|--help|-h)
            show_help
            ;;
        *)
            print_error "Unknown command: $COMMAND"
            echo ""
            show_help
            exit 1
            ;;
    esac
}

# Run main function
main "$@"
