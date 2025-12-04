#!/bin/bash
# Helper script to deploy Ouroboros to IONOS Cloud Kubernetes
# This script automates deployment to IONOS Cloud with their container registry
#
# Prerequisites:
# - kubectl configured for your IONOS Kubernetes cluster
# - Docker installed
# - IONOS Container Registry credentials configured
#
# Usage: ./deploy-ionos.sh [namespace]
#   namespace: Kubernetes namespace (default: monadic-pipeline)
#
# Environment variables (optional):
#   IONOS_REGISTRY: Registry URL (default: adaptive-systems.cr.de-fra.ionos.com)
#   IONOS_USERNAME: Registry username
#   IONOS_PASSWORD: Registry password
#
# Recommended: Run validation script first to check prerequisites
#   ./scripts/validate-ionos-prerequisites.sh [namespace]
#
# Example: 
#   ./deploy-ionos.sh monadic-pipeline

set -e

# Configuration
IONOS_REGISTRY="${IONOS_REGISTRY:-adaptive-systems.cr.de-fra.ionos.com}"
REGISTRY_URL="${IONOS_REGISTRY}"
NAMESPACE="${1:-monadic-pipeline}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
K8S_DIR="$PROJECT_ROOT/k8s"

echo "================================================"
echo "Ouroboros IONOS Cloud Deployment"
echo "================================================"
echo ""
echo "Registry: $REGISTRY_URL"
echo "Namespace: $NAMESPACE"
echo ""

# Verify prerequisites
if ! command -v kubectl &> /dev/null; then
    echo "Error: kubectl is not installed"
    exit 1
fi

if ! command -v docker &> /dev/null; then
    echo "Error: docker is not installed"
    exit 1
fi

# Check if kubectl is configured
if ! kubectl cluster-info &> /dev/null; then
    echo "Error: kubectl is not configured. Please configure kubectl for your IONOS cluster"
    echo ""
    echo "To configure kubectl for IONOS Cloud:"
    echo "  1. Download your kubeconfig from IONOS Cloud Console"
    echo "  2. Set KUBECONFIG environment variable or merge with ~/.kube/config"
    echo "  3. Run: kubectl cluster-info"
    exit 1
fi

# Validate IONOS-specific prerequisites
echo "Validating IONOS Cloud prerequisites..."

# Check for IONOS storage class
if kubectl get storageclass ionos-enterprise-ssd &> /dev/null; then
    echo "✓ IONOS storage class 'ionos-enterprise-ssd' found"
else
    echo "⚠️  Warning: IONOS storage class 'ionos-enterprise-ssd' not found"
    echo ""
    echo "Available storage classes:"
    kubectl get storageclass 2>&1 || echo "  Unable to list storage classes"
    echo ""
    echo "This may cause PVC provisioning to fail. Options:"
    echo "  1. Ensure you're connected to an IONOS Managed Kubernetes cluster"
    echo "  2. Use a different storage class by modifying the manifests"
    echo "  3. Continue anyway (PVCs will remain in Pending state)"
    echo ""
    read -p "Continue deployment without IONOS storage class? (y/N): " -n 1 -r
    echo ""
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

# Check cluster resources
echo "Checking cluster resources..."
if command -v kubectl &> /dev/null; then
    NODE_COUNT=$(kubectl get nodes --no-headers 2>/dev/null | wc -l)
    if [ "$NODE_COUNT" -gt 0 ]; then
        echo "✓ Cluster has $NODE_COUNT node(s)"
    else
        echo "⚠️  Warning: Unable to detect cluster nodes"
    fi
fi
echo ""

echo "Step 1: Authenticating with IONOS Container Registry..."

# Check if credentials are provided via environment
if [ -n "$IONOS_USERNAME" ] && [ -n "$IONOS_PASSWORD" ]; then
    echo "Using credentials from environment variables"
    if ! echo "$IONOS_PASSWORD" | docker login "$IONOS_REGISTRY" -u "$IONOS_USERNAME" --password-stdin; then
        echo "Error: Failed to authenticate with IONOS registry"
        exit 1
    fi
elif docker login "$IONOS_REGISTRY" --help 2>&1 | grep -q "credential-helper"; then
    echo "Using Docker credential helper"
    if ! docker login "$IONOS_REGISTRY"; then
        echo "Error: Failed to authenticate with IONOS registry"
        exit 1
    fi
else
    echo "Please enter your IONOS Container Registry credentials:"
    if ! docker login "$IONOS_REGISTRY"; then
        echo "Error: Failed to authenticate with IONOS registry"
        exit 1
    fi
fi
echo "✓ Authenticated with IONOS registry"
echo ""

# Build and push images
echo "Step 2: Building and pushing Docker images..."

# Build CLI image
echo "Building Ouroboros CLI image..."
if ! docker build -t "${REGISTRY_URL}/monadic-pipeline:latest" -f "$PROJECT_ROOT/Dockerfile" "$PROJECT_ROOT"; then
    echo "Error: Failed to build CLI image"
    exit 1
fi
echo "✓ CLI image built"

echo "Pushing CLI image to IONOS registry..."
if ! docker push "${REGISTRY_URL}/monadic-pipeline:latest"; then
    echo "Error: Failed to push CLI image"
    exit 1
fi
echo "✓ CLI image pushed"

# Build WebAPI image
echo "Building Ouroboros Web API image..."
if ! docker build -t "${REGISTRY_URL}/monadic-pipeline-webapi:latest" -f "$PROJECT_ROOT/Dockerfile.webapi" "$PROJECT_ROOT"; then
    echo "Error: Failed to build Web API image"
    exit 1
fi
echo "✓ Web API image built"

echo "Pushing Web API image to IONOS registry..."
if ! docker push "${REGISTRY_URL}/monadic-pipeline-webapi:latest"; then
    echo "Error: Failed to push Web API image"
    exit 1
fi
echo "✓ Web API image pushed"
echo ""

# Prepare Kubernetes manifests
echo "Step 3: Preparing Kubernetes manifests for IONOS Cloud..."
TEMP_DIR=$(mktemp -d)
trap 'rm -rf "$TEMP_DIR"' EXIT

# Copy cloud deployment templates
cp "$K8S_DIR/deployment.cloud.yaml" "$TEMP_DIR/deployment.yaml"
cp "$K8S_DIR/webapi-deployment.cloud.yaml" "$TEMP_DIR/webapi-deployment.yaml"
cp "$K8S_DIR/qdrant.yaml" "$TEMP_DIR/qdrant.yaml"
cp "$K8S_DIR/ollama.yaml" "$TEMP_DIR/ollama.yaml"

# Update image references (portable sed for Linux and macOS)
if sed --version >/dev/null 2>&1; then
    # GNU sed (Linux)
    sed -i "s|REGISTRY_URL|${REGISTRY_URL}|g" "$TEMP_DIR/deployment.yaml"
    sed -i "s|REGISTRY_URL|${REGISTRY_URL}|g" "$TEMP_DIR/webapi-deployment.yaml"
    # Update storage class for IONOS
    sed -i "s|storageClassName: managed-csi|storageClassName: ionos-enterprise-ssd|g" "$TEMP_DIR/qdrant.yaml"
    sed -i "s|storageClassName: managed-csi|storageClassName: ionos-enterprise-ssd|g" "$TEMP_DIR/ollama.yaml"
    # Enable imagePullSecrets
    sed -i 's|# imagePullSecrets:|imagePullSecrets:|g' "$TEMP_DIR/deployment.yaml"
    sed -i 's|#   - name: ionos-registry-secret|  - name: ionos-registry-secret|g' "$TEMP_DIR/deployment.yaml"
    sed -i 's|# imagePullSecrets:|imagePullSecrets:|g' "$TEMP_DIR/webapi-deployment.yaml"
    sed -i 's|#   - name: ionos-registry-secret|  - name: ionos-registry-secret|g' "$TEMP_DIR/webapi-deployment.yaml"
else
    # BSD sed (macOS)
    sed -i '' "s|REGISTRY_URL|${REGISTRY_URL}|g" "$TEMP_DIR/deployment.yaml"
    sed -i '' "s|REGISTRY_URL|${REGISTRY_URL}|g" "$TEMP_DIR/webapi-deployment.yaml"
    # Update storage class for IONOS
    sed -i '' "s|storageClassName: managed-csi|storageClassName: ionos-enterprise-ssd|g" "$TEMP_DIR/qdrant.yaml"
    sed -i '' "s|storageClassName: managed-csi|storageClassName: ionos-enterprise-ssd|g" "$TEMP_DIR/ollama.yaml"
    # Enable imagePullSecrets
    sed -i '' 's|# imagePullSecrets:|imagePullSecrets:|g' "$TEMP_DIR/deployment.yaml"
    sed -i '' 's|#   - name: ionos-registry-secret|  - name: ionos-registry-secret|g' "$TEMP_DIR/deployment.yaml"
    sed -i '' 's|# imagePullSecrets:|imagePullSecrets:|g' "$TEMP_DIR/webapi-deployment.yaml"
    sed -i '' 's|#   - name: ionos-registry-secret|  - name: ionos-registry-secret|g' "$TEMP_DIR/webapi-deployment.yaml"
fi

echo "✓ Manifests prepared for IONOS Cloud"
echo ""

# Create IONOS registry secret
echo "Step 4: Creating IONOS registry pull secret..."
kubectl create namespace "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -

# Create or update the registry secret
if [ -n "$IONOS_USERNAME" ] && [ -n "$IONOS_PASSWORD" ]; then
    kubectl create secret docker-registry ionos-registry-secret \
        --docker-server="$IONOS_REGISTRY" \
        --docker-username="$IONOS_USERNAME" \
        --docker-password="$IONOS_PASSWORD" \
        --namespace="$NAMESPACE" \
        --dry-run=client -o yaml | kubectl apply -f -
    echo "✓ IONOS registry secret created from environment variables"
else
    echo "⚠ Note: IONOS_USERNAME and IONOS_PASSWORD not set in environment"
    echo "You may need to create the registry secret manually:"
    echo ""
    echo "  kubectl create secret docker-registry ionos-registry-secret \\"
    echo "    --docker-server=$IONOS_REGISTRY \\"
    echo "    --docker-username=<your-username> \\"
    echo "    --docker-password=<your-password> \\"
    echo "    --namespace=$NAMESPACE"
    echo ""
    read -p "Continue without registry secret? (y/N): " -n 1 -r
    echo ""
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi
echo ""

# Deploy to IONOS Kubernetes
echo "Step 5: Deploying to IONOS Kubernetes cluster..."

# Apply secrets
echo "Creating secrets..."
echo "⚠ Warning: Update secrets in k8s/secrets.yaml before production deployment!"
kubectl apply -f "$K8S_DIR/secrets.yaml"

# Apply configmap
echo "Creating configmap..."
kubectl apply -f "$K8S_DIR/configmap.yaml"

# Deploy dependencies
echo "Deploying dependencies..."
kubectl apply -f "$TEMP_DIR/ollama.yaml"
kubectl apply -f "$TEMP_DIR/qdrant.yaml"
kubectl apply -f "$K8S_DIR/jaeger.yaml"

# Deploy application
echo "Deploying Ouroboros CLI..."
kubectl apply -f "$TEMP_DIR/deployment.yaml"

echo "Deploying Ouroboros Web API..."
kubectl apply -f "$TEMP_DIR/webapi-deployment.yaml"

echo ""
echo "Step 6: Waiting for deployments to be ready..."
echo "This may take a few minutes..."

# Function to wait for deployment with better feedback
wait_for_deployment() {
    local deployment=$1
    local namespace=$2
    local timeout=$3
    
    echo "Waiting for $deployment..."
    if kubectl wait --for=condition=available --timeout="${timeout}s" "deployment/$deployment" -n "$namespace" 2>&1; then
        echo "✓ $deployment is ready"
        return 0
    else
        echo "⚠️  Warning: $deployment did not become ready within ${timeout}s"
        echo "   Check status with: kubectl get deployment $deployment -n $namespace"
        return 1
    fi
}

# Wait for each deployment
DEPLOYMENT_WARNINGS=0
wait_for_deployment "ollama" "$NAMESPACE" 300 || ((DEPLOYMENT_WARNINGS++))
wait_for_deployment "qdrant" "$NAMESPACE" 300 || ((DEPLOYMENT_WARNINGS++))
wait_for_deployment "monadic-pipeline" "$NAMESPACE" 300 || ((DEPLOYMENT_WARNINGS++))
wait_for_deployment "monadic-pipeline-webapi" "$NAMESPACE" 300 || ((DEPLOYMENT_WARNINGS++))

echo ""
echo "================================================"
echo "Deployment Complete!"
echo "================================================"
echo ""
echo "Images deployed:"
echo "  - ${REGISTRY_URL}/monadic-pipeline:latest"
echo "  - ${REGISTRY_URL}/monadic-pipeline-webapi:latest"
echo ""
echo "Storage class: ionos-enterprise-ssd"
echo ""

if [ "$DEPLOYMENT_WARNINGS" -gt 0 ]; then
    echo "⚠️  Warning: $DEPLOYMENT_WARNINGS deployment(s) did not become ready within timeout"
    echo ""
    echo "Recommended next steps:"
    echo "  1. Run diagnostics: ./scripts/check-ionos-deployment.sh $NAMESPACE"
    echo "  2. Check pod status: kubectl get pods -n $NAMESPACE"
    echo "  3. View events: kubectl get events -n $NAMESPACE --sort-by='.lastTimestamp'"
    echo ""
else
    echo "✅ All deployments ready!"
    echo ""
fi

echo "Check deployment status:"
echo "  kubectl get all -n $NAMESPACE"
echo "  kubectl get pvc -n $NAMESPACE"
echo ""
echo "Run diagnostics (recommended):"
echo "  ./scripts/check-ionos-deployment.sh $NAMESPACE"
echo ""
echo "Check pod logs:"
echo "  kubectl logs -f deployment/monadic-pipeline-webapi -n $NAMESPACE"
echo ""
echo "Access Web API (port-forward):"
echo "  kubectl port-forward -n $NAMESPACE service/monadic-pipeline-webapi-service 8080:80"
echo "  Then open: http://localhost:8080"
echo ""
echo "For LoadBalancer access (IONOS):"
echo "  kubectl patch service monadic-pipeline-webapi-service -n $NAMESPACE -p '{\"spec\":{\"type\":\"LoadBalancer\"}}'"
echo "  kubectl get service monadic-pipeline-webapi-service -n $NAMESPACE"
echo ""
echo "Configure Ingress for SSL (optional):"
echo "  1. Update k8s/webapi-deployment.cloud.yaml with your domain"
echo "  2. Configure cert-manager for Let's Encrypt"
echo "  3. Apply: kubectl apply -f k8s/webapi-deployment.cloud.yaml"
echo ""
echo "Delete deployment:"
echo "  kubectl delete namespace $NAMESPACE"
echo ""
