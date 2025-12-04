#!/bin/bash
# Helper script to deploy Ouroboros to Azure Kubernetes Service (AKS)
# This script automates the process of building, pushing, and deploying to AKS
#
# Prerequisites:
# - Azure CLI installed and configured (az login)
# - kubectl configured for your AKS cluster
# - Docker installed
#
# Usage: ./deploy-aks.sh <registry-name> [namespace]
#   registry-name: Your ACR name (without .azurecr.io)
#   namespace: Kubernetes namespace (default: monadic-pipeline)
#
# Example: ./deploy-aks.sh myregistry monadic-pipeline

set -e

# Check arguments
if [ -z "$1" ]; then
    echo "Error: Registry name is required"
    echo ""
    echo "Usage: $0 <registry-name> [namespace]"
    echo ""
    echo "Example: $0 myregistry monadic-pipeline"
    echo ""
    echo "The registry-name should be your ACR name without .azurecr.io suffix"
    exit 1
fi

REGISTRY_NAME="$1"
NAMESPACE="${2:-monadic-pipeline}"
REGISTRY_URL="${REGISTRY_NAME}.azurecr.io"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
K8S_DIR="$PROJECT_ROOT/k8s"

echo "================================================"
echo "Ouroboros AKS Deployment"
echo "================================================"
echo "Registry: $REGISTRY_URL"
echo "Namespace: $NAMESPACE"
echo ""

# Check prerequisites
echo "Checking prerequisites..."

if ! command -v az &> /dev/null; then
    echo "Error: Azure CLI (az) is not installed"
    echo "Install from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

if ! command -v kubectl &> /dev/null; then
    echo "Error: kubectl is not installed"
    exit 1
fi

if ! command -v docker &> /dev/null; then
    echo "Error: Docker is not installed"
    exit 1
fi

# Check Azure login
if ! az account show &> /dev/null; then
    echo "Error: Not logged in to Azure. Run 'az login' first"
    exit 1
fi

# Check kubectl cluster connection
if ! kubectl cluster-info &> /dev/null; then
    echo "Error: Cannot connect to Kubernetes cluster"
    echo "Configure kubectl with: az aks get-credentials --resource-group <rg> --name <cluster>"
    exit 1
fi

echo "✓ All prerequisites met"
echo ""

# Login to ACR
echo "Step 1: Logging in to Azure Container Registry..."
if ! az acr login --name "$REGISTRY_NAME"; then
    echo "Error: Failed to login to ACR '$REGISTRY_NAME'"
    echo "Make sure the registry exists and you have permissions"
    exit 1
fi
echo "✓ Logged in to ACR"
echo ""

# Build and tag images
echo "Step 2: Building Docker images..."
cd "$PROJECT_ROOT"

echo "Building CLI image..."
if ! docker build -t "${REGISTRY_URL}/monadic-pipeline:latest" .; then
    echo "Error: CLI Docker build failed"
    exit 1
fi
echo "✓ CLI image built"

echo "Building Web API image..."
if ! docker build -f Dockerfile.webapi -t "${REGISTRY_URL}/monadic-pipeline-webapi:latest" .; then
    echo "Error: Web API Docker build failed"
    exit 1
fi
echo "✓ Web API image built"
echo ""

# Push images to ACR
echo "Step 3: Pushing images to ACR..."
echo "Pushing CLI image..."
if ! docker push "${REGISTRY_URL}/monadic-pipeline:latest"; then
    echo "Error: Failed to push CLI image"
    exit 1
fi
echo "✓ CLI image pushed"

echo "Pushing Web API image..."
if ! docker push "${REGISTRY_URL}/monadic-pipeline-webapi:latest"; then
    echo "Error: Failed to push Web API image"
    exit 1
fi
echo "✓ Web API image pushed"
echo ""

# Create temporary deployment files with updated image references
echo "Step 4: Preparing Kubernetes manifests..."
TEMP_DIR=$(mktemp -d)
trap 'rm -rf "$TEMP_DIR"' EXIT

# Copy cloud deployment templates
cp "$K8S_DIR/deployment.cloud.yaml" "$TEMP_DIR/deployment.yaml"
cp "$K8S_DIR/webapi-deployment.cloud.yaml" "$TEMP_DIR/webapi-deployment.yaml"

# Update image references (portable sed for Linux and macOS)
if sed --version >/dev/null 2>&1; then
    # GNU sed (Linux)
    sed -i "s|REGISTRY_URL|${REGISTRY_URL}|g" "$TEMP_DIR/deployment.yaml"
    sed -i "s|REGISTRY_URL|${REGISTRY_URL}|g" "$TEMP_DIR/webapi-deployment.yaml"
else
    # BSD sed (macOS)
    sed -i '' "s|REGISTRY_URL|${REGISTRY_URL}|g" "$TEMP_DIR/deployment.yaml"
    sed -i '' "s|REGISTRY_URL|${REGISTRY_URL}|g" "$TEMP_DIR/webapi-deployment.yaml"
fi

echo "✓ Manifests prepared with registry: $REGISTRY_URL"
echo ""

# Deploy to AKS
echo "Step 5: Deploying to AKS..."

# Create namespace
echo "Creating namespace..."
kubectl apply -f "$K8S_DIR/namespace.yaml"

# Apply secrets (warning about customization)
echo "Creating secrets..."
echo "⚠ Warning: Make sure to update secrets in k8s/secrets.yaml before production deployment!"
kubectl apply -f "$K8S_DIR/secrets.yaml"

# Apply configmap
echo "Creating configmap..."
kubectl apply -f "$K8S_DIR/configmap.yaml"

# Deploy services
echo "Deploying dependencies..."
kubectl apply -f "$K8S_DIR/ollama.yaml"
kubectl apply -f "$K8S_DIR/qdrant.yaml"
kubectl apply -f "$K8S_DIR/jaeger.yaml"

# Deploy application with updated image references
echo "Deploying Ouroboros CLI..."
kubectl apply -f "$TEMP_DIR/deployment.yaml"

echo "Deploying Ouroboros Web API..."
kubectl apply -f "$TEMP_DIR/webapi-deployment.yaml"

echo ""
echo "Step 6: Waiting for deployments to be ready..."
echo "This may take a few minutes..."
kubectl wait --for=condition=available --timeout=300s deployment/ollama -n "$NAMESPACE" || true
kubectl wait --for=condition=available --timeout=300s deployment/qdrant -n "$NAMESPACE" || true
kubectl wait --for=condition=available --timeout=300s deployment/monadic-pipeline -n "$NAMESPACE" || true
kubectl wait --for=condition=available --timeout=300s deployment/monadic-pipeline-webapi -n "$NAMESPACE" || true

echo ""
echo "================================================"
echo "Deployment Complete!"
echo "================================================"
echo ""
echo "Images deployed:"
echo "  - ${REGISTRY_URL}/monadic-pipeline:latest"
echo "  - ${REGISTRY_URL}/monadic-pipeline-webapi:latest"
echo ""
echo "Check deployment status:"
echo "  kubectl get all -n $NAMESPACE"
echo ""
echo "View pod status and events:"
echo "  kubectl get pods -n $NAMESPACE"
echo "  kubectl describe pod <pod-name> -n $NAMESPACE"
echo ""
echo "View logs:"
echo "  kubectl logs -f deployment/monadic-pipeline -n $NAMESPACE"
echo "  kubectl logs -f deployment/monadic-pipeline-webapi -n $NAMESPACE"
echo ""
echo "Access Web API:"
echo "  kubectl port-forward -n $NAMESPACE service/monadic-pipeline-webapi-service 8080:80"
echo "  Then open: http://localhost:8080"
echo ""
echo "If you encounter ImagePullBackOff errors:"
echo "  1. Verify images are in ACR: az acr repository list --name $REGISTRY_NAME"
echo "  2. Check AKS has permissions: az aks check-acr --resource-group <rg> --name <cluster> --acr $REGISTRY_NAME"
echo "  3. View pod events: kubectl describe pod <pod-name> -n $NAMESPACE"
echo ""
echo "Delete deployment:"
echo "  kubectl delete namespace $NAMESPACE"
echo ""
