#!/bin/bash
# Deployment script for Ouroboros on Kubernetes
# Usage: ./deploy-k8s.sh [namespace]
#
# Note: This script builds Docker images locally and deploys them to Kubernetes.
# For local Kubernetes clusters (Docker Desktop, minikube, kind):
#   - Images are built locally and loaded directly into the cluster
#   - imagePullPolicy is set to "Never" to prevent pulling from registries
#
# For cloud Kubernetes (AKS, EKS, GKE):
#   - Build images locally, then push to a container registry (ACR, ECR, GCR)
#   - Update k8s/webapi-deployment.yaml to use your registry URL
#   - Set imagePullPolicy to "Always" or "IfNotPresent"
#   Example:
#     docker build -f Dockerfile.webapi -t myregistry.azurecr.io/monadic-pipeline-webapi:latest .
#     docker push myregistry.azurecr.io/monadic-pipeline-webapi:latest
#     # Update image in webapi-deployment.yaml to use the full registry path

set -e

NAMESPACE="${1:-monadic-pipeline}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
K8S_DIR="$PROJECT_ROOT/k8s"

echo "================================================"
echo "Ouroboros Kubernetes Deployment"
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

# Detect cluster type
CLUSTER_CONTEXT=$(kubectl config current-context)
echo "Current cluster context: $CLUSTER_CONTEXT"

if [[ "$CLUSTER_CONTEXT" == *"docker-desktop"* ]] || [[ "$CLUSTER_CONTEXT" == *"minikube"* ]] || [[ "$CLUSTER_CONTEXT" == *"kind"* ]]; then
    echo "✓ Detected local Kubernetes cluster"
    echo "  Images will be built locally and used directly"
else
    echo "⚠ Warning: This appears to be a remote/cloud Kubernetes cluster"
    echo "  You may need to:"
    echo "  1. Push images to a container registry (ACR, ECR, GCR, Docker Hub)"
    echo "  2. Update image references in k8s/*.yaml to use registry URLs"
    echo "  3. Configure imagePullSecrets if using a private registry"
    echo ""
    read -p "Continue with local image build? (y/N): " -n 1 -r
    echo ""
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "Deployment cancelled. Please push images to a registry first."
        exit 1
    fi
fi
echo ""

# Build Docker images (assumes local Docker registry or cluster with image pull)
echo "Step 1: Building Docker images..."
cd "$PROJECT_ROOT"

echo "Building CLI image..."
if ! docker build -t monadic-pipeline:latest .; then
    echo "Error: CLI Docker build failed"
    exit 1
fi

echo "✓ CLI Docker image built successfully"

echo "Building Web API image..."
if ! docker build -f Dockerfile.webapi -t monadic-pipeline-webapi:latest .; then
    echo "Error: Web API Docker build failed"
    exit 1
fi

echo "✓ Web API Docker image built successfully"
echo ""

# Load images into cluster if it's a local cluster
if [[ "$CLUSTER_CONTEXT" == *"minikube"* ]]; then
    echo "Loading images into minikube..."
    minikube image load monadic-pipeline:latest
    minikube image load monadic-pipeline-webapi:latest
    echo "✓ Images loaded into minikube"
    echo ""
elif [[ "$CLUSTER_CONTEXT" == *"kind"* ]]; then
    echo "Loading images into kind cluster..."
    CLUSTER_NAME="${CLUSTER_CONTEXT#kind-}"
    kind load docker-image monadic-pipeline:latest --name "$CLUSTER_NAME"
    kind load docker-image monadic-pipeline-webapi:latest --name "$CLUSTER_NAME"
    echo "✓ Images loaded into kind cluster"
    echo ""
elif [[ "$CLUSTER_CONTEXT" == *"docker-desktop"* ]]; then
    echo "✓ Images are automatically available in Docker Desktop Kubernetes"
    echo ""
fi

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

echo "Deploying Ouroboros CLI..."
kubectl apply -f "$K8S_DIR/deployment.yaml"

echo "Deploying Ouroboros Web API..."
kubectl apply -f "$K8S_DIR/webapi-deployment.yaml"

echo ""
echo "Step 3: Waiting for deployments to be ready..."
kubectl wait --for=condition=available --timeout=300s deployment/ollama -n "$NAMESPACE"
kubectl wait --for=condition=available --timeout=300s deployment/qdrant -n "$NAMESPACE"
kubectl wait --for=condition=available --timeout=300s deployment/monadic-pipeline -n "$NAMESPACE"
kubectl wait --for=condition=available --timeout=300s deployment/monadic-pipeline-webapi -n "$NAMESPACE"

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
echo "  kubectl logs -f deployment/monadic-pipeline-webapi -n $NAMESPACE"
echo ""
echo "Access Web API:"
echo "  kubectl port-forward -n $NAMESPACE service/monadic-pipeline-webapi-service 8080:80"
echo "  Then open: http://localhost:8080"
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
