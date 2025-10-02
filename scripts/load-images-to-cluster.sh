#!/bin/bash
# Helper script to load Docker images into different types of Kubernetes clusters
# Usage: ./load-images-to-cluster.sh

set -e

echo "================================================"
echo "Load Images to Kubernetes Cluster"
echo "================================================"
echo ""

# Get current cluster context
CLUSTER_CONTEXT=$(kubectl config current-context)
echo "Current cluster context: $CLUSTER_CONTEXT"
echo ""

# Detect cluster type and load images accordingly
if [[ "$CLUSTER_CONTEXT" == *"docker-desktop"* ]]; then
    echo "Detected Docker Desktop Kubernetes"
    echo "Images built with Docker are automatically available in the cluster"
    echo ""
    echo "✓ No additional action needed"
    
elif [[ "$CLUSTER_CONTEXT" == *"minikube"* ]]; then
    echo "Detected Minikube cluster"
    echo "Loading images into minikube..."
    
    minikube image load monadic-pipeline:latest
    minikube image load monadic-pipeline-webapi:latest
    
    echo "✓ Images loaded into minikube"
    
elif [[ "$CLUSTER_CONTEXT" == *"kind"* ]]; then
    echo "Detected Kind cluster"
    echo "Loading images into kind..."
    
    # Extract cluster name from context (e.g., kind-clustername -> clustername)
    CLUSTER_NAME="${CLUSTER_CONTEXT#kind-}"
    
    kind load docker-image monadic-pipeline:latest --name "$CLUSTER_NAME"
    kind load docker-image monadic-pipeline-webapi:latest --name "$CLUSTER_NAME"
    
    echo "✓ Images loaded into kind cluster"
    
else
    echo "⚠ Remote/Cloud Kubernetes cluster detected"
    echo ""
    echo "For remote clusters (AKS, EKS, GKE), you need to:"
    echo ""
    echo "1. Tag images with your registry URL:"
    echo "   docker tag monadic-pipeline:latest YOUR_REGISTRY/monadic-pipeline:latest"
    echo "   docker tag monadic-pipeline-webapi:latest YOUR_REGISTRY/monadic-pipeline-webapi:latest"
    echo ""
    echo "2. Push to your container registry:"
    echo "   # Azure Container Registry (ACR)"
    echo "   az acr login --name YOUR_REGISTRY"
    echo "   docker push YOUR_REGISTRY/monadic-pipeline:latest"
    echo "   docker push YOUR_REGISTRY/monadic-pipeline-webapi:latest"
    echo ""
    echo "   # AWS Elastic Container Registry (ECR)"
    echo "   aws ecr get-login-password --region REGION | docker login --username AWS --password-stdin YOUR_REGISTRY"
    echo "   docker push YOUR_REGISTRY/monadic-pipeline:latest"
    echo "   docker push YOUR_REGISTRY/monadic-pipeline-webapi:latest"
    echo ""
    echo "   # Google Container Registry (GCR)"
    echo "   gcloud auth configure-docker"
    echo "   docker push gcr.io/YOUR_PROJECT/monadic-pipeline:latest"
    echo "   docker push gcr.io/YOUR_PROJECT/monadic-pipeline-webapi:latest"
    echo ""
    echo "   # Docker Hub"
    echo "   docker login"
    echo "   docker push YOUR_USERNAME/monadic-pipeline:latest"
    echo "   docker push YOUR_USERNAME/monadic-pipeline-webapi:latest"
    echo ""
    echo "3. Update image references in k8s/deployment.yaml and k8s/webapi-deployment.yaml"
    echo "   to use the full registry URL"
    echo ""
    echo "4. If using a private registry, configure imagePullSecrets:"
    echo "   kubectl create secret docker-registry regcred \\"
    echo "     --docker-server=YOUR_REGISTRY \\"
    echo "     --docker-username=YOUR_USERNAME \\"
    echo "     --docker-password=YOUR_PASSWORD \\"
    echo "     --namespace=monadic-pipeline"
    echo ""
    echo "   Then add to deployment spec:"
    echo "   spec:"
    echo "     imagePullSecrets:"
    echo "     - name: regcred"
    echo ""
    exit 1
fi

echo ""
echo "================================================"
echo "Images are ready in your cluster!"
echo "================================================"
