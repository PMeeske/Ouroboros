#!/bin/bash
# Pre-deployment validation script for MonadicPipeline
# This script checks if you're deploying to the right type of cluster
# with the right manifests to prevent ImagePullBackOff errors
#
# Usage: ./validate-deployment.sh [namespace]

set -e

NAMESPACE="${1:-monadic-pipeline}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

echo "================================================"
echo "MonadicPipeline Deployment Validation"
echo "================================================"
echo ""

# Check if kubectl is configured
if ! command -v kubectl &> /dev/null; then
    echo "❌ Error: kubectl is not installed"
    echo "Install from: https://kubernetes.io/docs/tasks/tools/"
    exit 1
fi

# Get current context
CONTEXT=$(kubectl config current-context 2>&1)
if [ $? -ne 0 ]; then
    echo "❌ Error: kubectl is not configured"
    echo "Run 'kubectl config view' to check your configuration"
    exit 1
fi

echo "Current kubectl context: $CONTEXT"
echo ""

# Detect cluster type
CLUSTER_TYPE="unknown"
if [[ "$CONTEXT" == *"minikube"* ]]; then
    CLUSTER_TYPE="minikube"
elif [[ "$CONTEXT" == *"docker-desktop"* ]] || [[ "$CONTEXT" == *"docker-for-desktop"* ]]; then
    CLUSTER_TYPE="docker-desktop"
elif [[ "$CONTEXT" == *"kind"* ]]; then
    CLUSTER_TYPE="kind"
elif [[ "$CONTEXT" == *"aks"* ]] || [[ "$CONTEXT" == *"azure"* ]]; then
    CLUSTER_TYPE="aks"
elif [[ "$CONTEXT" == *"eks"* ]] || [[ "$CONTEXT" == *"aws"* ]]; then
    CLUSTER_TYPE="eks"
elif [[ "$CONTEXT" == *"gke"* ]] || [[ "$CONTEXT" == *"gcloud"* ]]; then
    CLUSTER_TYPE="gke"
else
    # Try to determine from node names
    NODE_NAME=$(kubectl get nodes -o jsonpath='{.items[0].metadata.name}' 2>/dev/null || echo "")
    if [[ "$NODE_NAME" == *"aks"* ]]; then
        CLUSTER_TYPE="aks"
    elif [[ "$NODE_NAME" == *"eks"* ]] || [[ "$NODE_NAME" == *"ip-"* ]]; then
        CLUSTER_TYPE="eks"
    elif [[ "$NODE_NAME" == *"gke"* ]]; then
        CLUSTER_TYPE="gke"
    elif [[ "$NODE_NAME" == "minikube" ]]; then
        CLUSTER_TYPE="minikube"
    elif [[ "$NODE_NAME" == *"kind"* ]]; then
        CLUSTER_TYPE="kind"
    fi
fi

echo "Detected cluster type: $CLUSTER_TYPE"
echo ""

# Determine if this is a local or cloud cluster
IS_LOCAL=false
IS_CLOUD=false

case $CLUSTER_TYPE in
    minikube|docker-desktop|kind)
        IS_LOCAL=true
        ;;
    aks|eks|gke)
        IS_CLOUD=true
        ;;
    *)
        echo "⚠️  Warning: Could not definitively determine cluster type"
        echo "Please specify if this is a local or cloud cluster:"
        read -p "Is this a cloud cluster (AKS/EKS/GKE)? (y/n): " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            IS_CLOUD=true
        else
            IS_LOCAL=true
        fi
        ;;
esac

# Provide guidance based on cluster type
echo "================================================"
echo "Deployment Guidance"
echo "================================================"
echo ""

if [ "$IS_LOCAL" = true ]; then
    echo "✅ LOCAL CLUSTER DETECTED"
    echo ""
    echo "Recommended deployment method:"
    echo "  ./scripts/deploy-k8s.sh $NAMESPACE"
    echo ""
    echo "This script will:"
    echo "  - Build Docker images locally"
    echo "  - Load images into your cluster"
    echo "  - Deploy with imagePullPolicy: Never"
    echo ""
    echo "Manifests to use:"
    echo "  ✅ k8s/deployment.yaml"
    echo "  ✅ k8s/webapi-deployment.yaml"
    echo ""
    echo "⚠️  DO NOT USE:"
    echo "  ❌ k8s/deployment.cloud.yaml"
    echo "  ❌ k8s/webapi-deployment.cloud.yaml"
    echo "  (These are for cloud clusters only)"
    echo ""
    
elif [ "$IS_CLOUD" = true ]; then
    echo "☁️  CLOUD CLUSTER DETECTED"
    echo ""
    echo "⚠️  WARNING: You must push images to a container registry!"
    echo ""
    
    case $CLUSTER_TYPE in
        aks)
            echo "Recommended deployment method for AKS:"
            echo "  ./scripts/deploy-aks.sh <registry-name> $NAMESPACE"
            echo ""
            echo "Example:"
            echo "  ./scripts/deploy-aks.sh myregistry $NAMESPACE"
            echo ""
            echo "This script will:"
            echo "  - Login to Azure Container Registry"
            echo "  - Build and push images to ACR"
            echo "  - Deploy with correct registry URLs"
            echo "  - Use imagePullPolicy: Always"
            ;;
        eks)
            echo "Recommended deployment method for EKS:"
            echo "  ./scripts/deploy-cloud.sh <ecr-url> $NAMESPACE"
            echo ""
            echo "Example:"
            echo "  aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin 123456789.dkr.ecr.us-east-1.amazonaws.com"
            echo "  ./scripts/deploy-cloud.sh 123456789.dkr.ecr.us-east-1.amazonaws.com $NAMESPACE"
            ;;
        gke)
            echo "Recommended deployment method for GKE:"
            echo "  ./scripts/deploy-cloud.sh <gcr-url> $NAMESPACE"
            echo ""
            echo "Example:"
            echo "  gcloud auth configure-docker"
            echo "  ./scripts/deploy-cloud.sh gcr.io/my-project $NAMESPACE"
            ;;
        *)
            echo "Recommended deployment method:"
            echo "  ./scripts/deploy-cloud.sh <registry-url> $NAMESPACE"
            echo ""
            echo "Examples:"
            echo "  AWS ECR:    ./scripts/deploy-cloud.sh 123456789.dkr.ecr.us-east-1.amazonaws.com"
            echo "  GCP GCR:    ./scripts/deploy-cloud.sh gcr.io/my-project"
            echo "  Docker Hub: ./scripts/deploy-cloud.sh docker.io/myusername"
            ;;
    esac
    
    echo ""
    echo "Manifests to use:"
    echo "  ✅ k8s/deployment.cloud.yaml (with REGISTRY_URL replaced)"
    echo "  ✅ k8s/webapi-deployment.cloud.yaml (with REGISTRY_URL replaced)"
    echo ""
    echo "⚠️  DO NOT USE:"
    echo "  ❌ k8s/deployment.yaml"
    echo "  ❌ k8s/webapi-deployment.yaml"
    echo "  (These use imagePullPolicy: Never and will cause ImagePullBackOff)"
    echo ""
fi

# Check if images exist in cluster (for local clusters)
if [ "$IS_LOCAL" = true ]; then
    echo "================================================"
    echo "Checking Local Images"
    echo "================================================"
    echo ""
    
    IMAGES_EXIST=true
    
    # Check if images are available
    case $CLUSTER_TYPE in
        minikube)
            if ! minikube image ls | grep -q "monadic-pipeline:latest"; then
                echo "❌ Image 'monadic-pipeline:latest' not found in minikube"
                IMAGES_EXIST=false
            else
                echo "✅ Image 'monadic-pipeline:latest' found"
            fi
            
            if ! minikube image ls | grep -q "monadic-pipeline-webapi:latest"; then
                echo "❌ Image 'monadic-pipeline-webapi:latest' not found in minikube"
                IMAGES_EXIST=false
            else
                echo "✅ Image 'monadic-pipeline-webapi:latest' found"
            fi
            ;;
        kind)
            # For kind, check if docker images exist
            if ! docker images | grep -q "monadic-pipeline.*latest"; then
                echo "⚠️  Local Docker images not found"
                echo "   Images need to be loaded into kind cluster"
                IMAGES_EXIST=false
            else
                echo "✅ Local Docker images found"
            fi
            ;;
        docker-desktop)
            if ! docker images | grep -q "monadic-pipeline.*latest"; then
                echo "❌ Local Docker images not found"
                IMAGES_EXIST=false
            else
                echo "✅ Local Docker images found"
            fi
            ;;
    esac
    
    if [ "$IMAGES_EXIST" = false ]; then
        echo ""
        echo "⚠️  Warning: Images not found. Run deploy-k8s.sh to build and load them."
    fi
fi

# Check if namespace exists
echo ""
echo "================================================"
echo "Checking Namespace"
echo "================================================"
echo ""

if kubectl get namespace "$NAMESPACE" &> /dev/null; then
    echo "✅ Namespace '$NAMESPACE' exists"
    
    # Check if there are existing deployments
    if kubectl get deployments -n "$NAMESPACE" &> /dev/null; then
        DEPLOYMENT_COUNT=$(kubectl get deployments -n "$NAMESPACE" -o name 2>/dev/null | wc -l)
        if [ "$DEPLOYMENT_COUNT" -gt 0 ]; then
            echo "⚠️  Warning: Namespace contains $DEPLOYMENT_COUNT existing deployment(s)"
            echo ""
            kubectl get deployments -n "$NAMESPACE"
        fi
    fi
else
    echo "⚠️  Namespace '$NAMESPACE' does not exist"
    echo "   It will be created during deployment"
fi

echo ""
echo "================================================"
echo "Validation Complete"
echo "================================================"
echo ""
echo "Next steps:"
if [ "$IS_LOCAL" = true ]; then
    echo "  1. Run: ./scripts/deploy-k8s.sh $NAMESPACE"
    echo "  2. Verify: kubectl get pods -n $NAMESPACE"
    echo "  3. Check logs: kubectl logs -f deployment/monadic-pipeline-webapi -n $NAMESPACE"
else
    echo "  1. Ensure you're logged into your container registry"
    echo "  2. Run the appropriate deployment script (see above)"
    echo "  3. Verify: kubectl get pods -n $NAMESPACE"
    echo "  4. Check events: kubectl get events -n $NAMESPACE --sort-by='.lastTimestamp'"
fi
echo ""
echo "For troubleshooting:"
echo "  - IMAGEPULLBACKOFF-FIX.md - Quick fix guide"
echo "  - TROUBLESHOOTING.md - Detailed troubleshooting"
echo "  - INCIDENT-RESPONSE-IMAGEPULLBACKOFF.md - Real incident analysis"
echo ""
