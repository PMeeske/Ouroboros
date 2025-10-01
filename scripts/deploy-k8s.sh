#!/bin/bash
# Deployment script for MonadicPipeline on Kubernetes
# Usage: ./deploy-k8s.sh [namespace]

set -e

NAMESPACE="${1:-monadic-pipeline}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
K8S_DIR="$PROJECT_ROOT/k8s"

echo "================================================"
echo "MonadicPipeline Kubernetes Deployment"
echo "================================================"
echo "Namespace: $NAMESPACE"
echo "K8s manifests: $K8S_DIR"
echo ""

# Check if kubectl is installed
if ! command -v kubectl &> /dev/null; then
    echo "Error: kubectl is not installed"
    exit 1
fi

# Check if cluster is accessible
if ! kubectl cluster-info &> /dev/null; then
    echo "Error: Cannot connect to Kubernetes cluster"
    exit 1
fi

echo "✓ kubectl is configured and cluster is accessible"
echo ""

# Build Docker image (assumes local Docker registry or cluster with image pull)
echo "Step 1: Building Docker image..."
cd "$PROJECT_ROOT"
docker build -t monadic-pipeline:latest .

if [ $? -ne 0 ]; then
    echo "Error: Docker build failed"
    exit 1
fi

echo "✓ Docker image built successfully"
echo ""

# Apply Kubernetes manifests
echo "Step 2: Applying Kubernetes manifests..."

# Create namespace
echo "Creating namespace..."
kubectl apply -f "$K8S_DIR/namespace.yaml"

# Apply secrets (you should customize this before deploying)
echo "Creating secrets..."
echo "⚠ Warning: Update secrets in k8s/secrets.yaml before production deployment!"
kubectl apply -f "$K8S_DIR/secrets.yaml"

# Apply configmap
echo "Creating configmap..."
kubectl apply -f "$K8S_DIR/configmap.yaml"

# Deploy services
echo "Deploying Ollama..."
kubectl apply -f "$K8S_DIR/ollama.yaml"

echo "Deploying Qdrant..."
kubectl apply -f "$K8S_DIR/qdrant.yaml"

echo "Deploying Jaeger..."
kubectl apply -f "$K8S_DIR/jaeger.yaml"

echo "Deploying MonadicPipeline..."
kubectl apply -f "$K8S_DIR/deployment.yaml"

echo ""
echo "Step 3: Waiting for deployments to be ready..."
kubectl wait --for=condition=available --timeout=300s deployment/ollama -n "$NAMESPACE"
kubectl wait --for=condition=available --timeout=300s deployment/qdrant -n "$NAMESPACE"
kubectl wait --for=condition=available --timeout=300s deployment/monadic-pipeline -n "$NAMESPACE"

echo ""
echo "================================================"
echo "Deployment Complete!"
echo "================================================"
echo ""
echo "Check deployment status:"
echo "  kubectl get all -n $NAMESPACE"
echo ""
echo "View logs:"
echo "  kubectl logs -f deployment/monadic-pipeline -n $NAMESPACE"
echo ""
echo "Access Jaeger UI:"
echo "  kubectl port-forward -n $NAMESPACE service/jaeger-ui 16686:16686"
echo "  Then open: http://localhost:16686"
echo ""
echo "Execute CLI commands:"
echo "  kubectl exec -it deployment/monadic-pipeline -n $NAMESPACE -- dotnet LangChainPipeline.dll --help"
echo ""
echo "Delete deployment:"
echo "  kubectl delete namespace $NAMESPACE"
echo ""
