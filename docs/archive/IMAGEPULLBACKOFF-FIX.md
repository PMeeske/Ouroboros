# Quick Fix: ImagePullBackOff on Cloud Kubernetes

This guide provides a quick solution for the `ImagePullBackOff` error when deploying Ouroboros to cloud Kubernetes clusters (AKS, EKS, GKE).

## The Problem

When deploying to cloud Kubernetes, you may see this error:

```bash
kubectl get events -n monadic-pipeline
# Error: ImagePullBackOff
# Failed to pull image "monadic-pipeline-webapi:latest"
```

**Root Cause:** The image doesn't exist in a container registry accessible by your cloud cluster.

## Pre-Deployment Validation

**Before deploying, validate your setup:**

```bash
./scripts/validate-deployment.sh
```

This script will:
- Detect your cluster type (local vs cloud)
- Check if you're using the correct manifests
- Provide specific guidance for your environment
- Prevent common ImagePullBackOff errors

## Quick Solutions

### For Azure AKS

```bash
# 1. Login to Azure (if not already)
az login

# 2. Configure kubectl for your cluster
az aks get-credentials --resource-group myResourceGroup --name myAKSCluster

# 3. Run the automated deployment script
./scripts/deploy-aks.sh myACRName monadic-pipeline
```

That's it! The script will:
- Build the Docker images
- Push them to your Azure Container Registry (ACR)
- Deploy to AKS with correct image references

### For AWS EKS

```bash
# 1. Authenticate to ECR
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin 123456789.dkr.ecr.us-east-1.amazonaws.com

# 2. Configure kubectl for your cluster
aws eks update-kubeconfig --region us-east-1 --name myEKSCluster

# 3. Run the automated deployment script
./scripts/deploy-cloud.sh 123456789.dkr.ecr.us-east-1.amazonaws.com monadic-pipeline
```

### For GCP GKE

```bash
# 1. Authenticate to GCR
gcloud auth configure-docker

# 2. Configure kubectl for your cluster
gcloud container clusters get-credentials myGKECluster --region us-central1

# 3. Run the automated deployment script
./scripts/deploy-cloud.sh gcr.io/my-project monadic-pipeline
```

### For Docker Hub

```bash
# 1. Login to Docker Hub
docker login

# 2. Configure kubectl for your cluster (varies by provider)

# 3. Run the automated deployment script
./scripts/deploy-cloud.sh docker.io/myusername monadic-pipeline
```

## Manual Fix (If Scripts Don't Work)

### Step 1: Build and Push Images

```bash
# Build with your registry URL
docker build -t YOUR_REGISTRY/monadic-pipeline:latest .
docker build -f Dockerfile.webapi -t YOUR_REGISTRY/monadic-pipeline-webapi:latest .

# Push to registry
docker push YOUR_REGISTRY/monadic-pipeline:latest
docker push YOUR_REGISTRY/monadic-pipeline-webapi:latest
```

### Step 2: Use Cloud Manifests

```bash
# Copy cloud templates
cp k8s/deployment.cloud.yaml k8s/deployment-custom.yaml
cp k8s/webapi-deployment.cloud.yaml k8s/webapi-deployment-custom.yaml

# Update REGISTRY_URL in both files
# On Linux (GNU sed):
sed -i 's|REGISTRY_URL|YOUR_REGISTRY|g' k8s/deployment-custom.yaml
sed -i 's|REGISTRY_URL|YOUR_REGISTRY|g' k8s/webapi-deployment-custom.yaml

# On macOS (BSD sed):
# sed -i '' 's|REGISTRY_URL|YOUR_REGISTRY|g' k8s/deployment-custom.yaml
# sed -i '' 's|REGISTRY_URL|YOUR_REGISTRY|g' k8s/webapi-deployment-custom.yaml
```

### Step 3: Deploy

```bash
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/ollama.yaml
kubectl apply -f k8s/qdrant.yaml
kubectl apply -f k8s/jaeger.yaml
kubectl apply -f k8s/deployment-custom.yaml
kubectl apply -f k8s/webapi-deployment-custom.yaml
```

## Verification

```bash
# Check pod status
kubectl get pods -n monadic-pipeline

# If still failing, check events
kubectl describe pod <pod-name> -n monadic-pipeline

# View detailed events
kubectl get events -n monadic-pipeline --sort-by='.lastTimestamp'
```

## Common Issues

### Issue: "unauthorized: authentication required"
**Solution:** Make sure you're logged into your container registry (step 1 in Quick Solutions)

### Issue: "repository does not exist"
**Solution:** Create the repository in your registry first, or push the images (step 1 in Manual Fix)

### Issue (AKS): "pull access denied"
**Solution:** Attach ACR to AKS cluster:
```bash
az aks update -n myCluster -g myResourceGroup --attach-acr myACR
```

## Need More Help?

- **Detailed troubleshooting:** See [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
- **Full deployment guide:** See [DEPLOYMENT.md](DEPLOYMENT.md)
- **Script documentation:** See [scripts/README.md](scripts/README.md)

## Key Points

1. **Local Kubernetes** (minikube/kind) - Use `./scripts/deploy-k8s.sh`
2. **Cloud Kubernetes** (AKS/EKS/GKE) - Use `./scripts/deploy-aks.sh` or `./scripts/deploy-cloud.sh`
3. **Never use local deployment on cloud** - It won't work because images aren't in a registry
4. **Always push images to registry** - Cloud clusters can't access local Docker images
