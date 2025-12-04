#!/bin/bash
# Deployment script for Ouroboros using Docker Compose
# Usage: ./deploy-docker.sh [environment]

set -e

ENVIRONMENT="${1:-production}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

echo "================================================"
echo "Ouroboros Docker Deployment"
echo "================================================"
echo "Environment: $ENVIRONMENT"
echo "Project Root: $PROJECT_ROOT"
echo ""

cd "$PROJECT_ROOT"

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo "Error: Docker is not installed"
    exit 1
fi

if ! command -v docker-compose &> /dev/null; then
    echo "Error: Docker Compose is not installed"
    exit 1
fi

# Build the Docker image
echo "Step 1: Building Docker image..."
docker build -t monadic-pipeline:latest .

if [ $? -ne 0 ]; then
    echo "Error: Docker build failed"
    exit 1
fi

echo "✓ Docker image built successfully"
echo ""

# Select appropriate compose file
if [ "$ENVIRONMENT" = "development" ]; then
    COMPOSE_FILE="docker-compose.dev.yml"
else
    COMPOSE_FILE="docker-compose.yml"
fi

# Start services
echo "Step 2: Starting services with $COMPOSE_FILE..."
docker-compose -f "$COMPOSE_FILE" up -d

if [ $? -ne 0 ]; then
    echo "Error: Failed to start services"
    exit 1
fi

echo "✓ Services started successfully"
echo ""

# Wait for Ollama to be ready
echo "Step 3: Waiting for Ollama service to be ready..."
for i in {1..30}; do
    if curl -s http://localhost:11434/api/tags > /dev/null 2>&1; then
        echo "✓ Ollama service is ready"
        break
    fi
    if [ $i -eq 30 ]; then
        echo "Warning: Ollama service may not be ready yet"
    fi
    echo -n "."
    sleep 2
done
echo ""

# Pull default Ollama models
echo "Step 4: Pulling default Ollama models..."
docker exec ollama ollama pull llama3
docker exec ollama ollama pull nomic-embed-text

echo ""
echo "================================================"
echo "Deployment Complete!"
echo "================================================"
echo ""
echo "Services running:"
echo "  - Ouroboros CLI: docker exec -it monadic-pipeline dotnet LangChainPipeline.dll --help"
echo "  - Ollama: http://localhost:11434"
echo "  - Qdrant: http://localhost:6333"
echo "  - Jaeger UI: http://localhost:16686"
echo ""
echo "View logs:"
echo "  docker-compose -f $COMPOSE_FILE logs -f monadic-pipeline"
echo ""
echo "Stop services:"
echo "  docker-compose -f $COMPOSE_FILE down"
echo ""
