#!/bin/bash
# Helper script to deploy MonadicPipeline to cloud Kubernetes (EKS, GKE, or any registry)
# This script helps you deploy to cloud Kubernetes with custom registry
#
# Prerequisites:
# - kubectl configured for your cluster
# - Docker installed
# - Authentication configured for your registry
#
# Usage: ./deploy-cloud.sh <registry-url> [namespace]
#   registry-url: Full registry URL (e.g., 123.dkr.ecr.us-east-1.amazonaws.com, gcr.io/my-project, docker.io/username)
#   namespace: Kubernetes namespace (default: monadic-pipeline)
#
# Example: 
#   ./deploy-cloud.sh 123456789.dkr.ecr.us-east-1.amazonaws.com monadic-pipeline
#   ./deploy-cloud.sh gcr.io/my-project monadic-pipeline
#   ./deploy-cloud.sh docker.io/myusername monadic-pipeline

set -e

# Check arguments
if [ -z "$1" ]; then
    echo "Error: Registry URL is required"
    echo ""
    echo "Usage: $0 <registry-url> [namespace]"
    echo ""
    echo "Examples:"
    echo "  AWS ECR:     $0 123456789.dkr.ecr.us-east-1.amazonaws.com"
    echo "  GCP GCR:     $0 gcr.io/my-project"
    echo "  Docker Hub:  $0 docker.io/myusername"
    echo "  Azure ACR:   Use deploy-aks.sh instead"
    exit 1
fi

REGISTRY_URL="$1"
NAMESPACE="${2:-monadic-pipeline}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
K8S_DIR="$PROJECT_ROOT/k8s"

echo "================================================"
echo "MonadicPipeline Cloud Kubernetes Deployment"
echo "================================================"
echo "Registry: $REGISTRY_URL"
echo "Namespace: $NAMESPACE"
echo ""

# Check prerequisites
echo "Checking prerequisites..."

if ! command -v kubectl &> /dev/null; then
    echo "Error: kubectl is not installed"
    exit 1
fi

if ! command -v docker &> /dev/null; then
    echo "Error: Docker is not installed"
    exit 1
fi

# Check kubectl cluster connection
if ! kubectl cluster-info &> /dev/null; then
    echo "Error: Cannot connect to Kubernetes cluster"
    exit 1
fi

echo "✓ Prerequisites met"
echo ""

# Prompt for registry authentication
echo "⚠ Make sure you are authenticated to your container registry!"
echo ""
echo "Examples:"
echo "  AWS ECR:     aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin 123456789.dkr.ecr.us-east-1.amazonaws.com"
echo "  GCP GCR:     gcloud auth configure-docker"
echo "  Docker Hub:  docker login"
echo ""
read -p "Have you authenticated to your registry? (y/N): " -n 1 -r
echo ""
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Please authenticate first, then run this script again."
    exit 1
fi

# Build and tag images
echo "Step 1: Building Docker images..."
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

# Push images
echo "Step 2: Pushing images to registry..."
echo "Pushing CLI image..."
if ! docker push "${REGISTRY_URL}/monadic-pipeline:latest"; then
    echo "Error: Failed to push CLI image"
    echo "Make sure you are authenticated to the registry"
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
echo "Step 3: Preparing Kubernetes manifests..."
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

# Check if imagePullSecrets are needed
echo "⚠ Note: If using a private registry, you may need to configure imagePullSecrets"
echo ""
read -p "Do you need to create imagePullSecrets? (y/N): " -n 1 -r
echo ""
if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo ""
    echo "Creating imagePullSecret..."
    read -r -p "Docker server (e.g., gcr.io): " DOCKER_SERVER
    read -r -p "Docker username: " DOCKER_USERNAME
    read -r -sp "Docker password: " DOCKER_PASSWORD
    echo ""
    
    kubectl create namespace "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -
    
    kubectl create secret docker-registry regcred \
        --docker-server="$DOCKER_SERVER" \
        --docker-username="$DOCKER_USERNAME" \
        --docker-password="$DOCKER_PASSWORD" \
        --namespace="$NAMESPACE" \
        --dry-run=client -o yaml | kubectl apply -f -
    
    # Update manifests to include imagePullSecrets (portable sed for Linux and macOS)
    if sed --version >/dev/null 2>&1; then
        # GNU sed (Linux)
        sed -i 's|# imagePullSecrets:|imagePullSecrets:|g' "$TEMP_DIR/deployment.yaml"
        sed -i 's|# - name: regcred|- name: regcred|g' "$TEMP_DIR/deployment.yaml"
        sed -i 's|# imagePullSecrets:|imagePullSecrets:|g' "$TEMP_DIR/webapi-deployment.yaml"
        sed -i 's|# - name: regcred|- name: regcred|g' "$TEMP_DIR/webapi-deployment.yaml"
    else
        # BSD sed (macOS)
        sed -i '' 's|# imagePullSecrets:|imagePullSecrets:|g' "$TEMP_DIR/deployment.yaml"
        sed -i '' 's|# - name: regcred|- name: regcred|g' "$TEMP_DIR/deployment.yaml"
        sed -i '' 's|# imagePullSecrets:|imagePullSecrets:|g' "$TEMP_DIR/webapi-deployment.yaml"
        sed -i '' 's|# - name: regcred|- name: regcred|g' "$TEMP_DIR/webapi-deployment.yaml"
    fi
    
    echo "✓ imagePullSecret created"
fi
echo ""

# Deploy to cluster
echo "Step 4: Deploying to Kubernetes..."

# Create namespace
echo "Creating namespace..."
kubectl apply -f "$K8S_DIR/namespace.yaml"

# Apply secrets
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

# Deploy application
echo "Deploying MonadicPipeline CLI..."
kubectl apply -f "$TEMP_DIR/deployment.yaml"

echo "Deploying MonadicPipeline Web API..."
kubectl apply -f "$TEMP_DIR/webapi-deployment.yaml"

echo ""
echo "Step 5: Waiting for deployments to be ready..."
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
echo "  1. Check authentication to registry"
echo "  2. Verify imagePullSecrets are configured (if using private registry)"
echo "  3. View pod events: kubectl describe pod <pod-name> -n $NAMESPACE"
echo ""
echo "Delete deployment:"
echo "  kubectl delete namespace $NAMESPACE"
echo ""
