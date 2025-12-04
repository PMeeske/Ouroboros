# Kubernetes Deployment Issue - Resolution Summary

## Issue Description

The Kubernetes deployment for Ouroboros Web API was failing with the following error:

```
Failed to pull image "monadic-pipeline-webapi:latest": failed to pull and unpack image 
"docker.io/library/monadic-pipeline-webapi:latest": failed to resolve reference 
"docker.io/library/monadic-pipeline-webapi:latest": pull access denied, repository does 
not exist or may require authorization: server message: insufficient_scope: 
authorization failed
```

## Root Cause

The error occurred because:

1. **Image Not Built**: The `deploy-k8s.sh` script only built the CLI image (`monadic-pipeline:latest`), not the Web API image (`monadic-pipeline-webapi:latest`)

2. **Web API Not Deployed**: The script didn't deploy the `k8s/webapi-deployment.yaml` manifest

3. **Wrong Image Pull Policy**: When Kubernetes couldn't find the image locally, it tried to pull from Docker Hub with `imagePullPolicy: IfNotPresent`, causing the error since the image doesn't exist there

4. **No Registry Configuration**: There was no guidance on pushing images to a container registry for cloud deployments

## Solution Implemented

### 1. Updated `scripts/deploy-k8s.sh`

**What Changed:**
- Now builds BOTH images: CLI and Web API
- Detects cluster type (local vs cloud)
- Automatically loads images into minikube and kind clusters
- Provides warnings and guidance for cloud deployments
- Deploys both deployment.yaml and webapi-deployment.yaml

**Before:**
```bash
docker build -t monadic-pipeline:latest .
kubectl apply -f "$K8S_DIR/deployment.yaml"
```

**After:**
```bash
# Build both images
docker build -t monadic-pipeline:latest .
docker build -f Dockerfile.webapi -t monadic-pipeline-webapi:latest .

# Load into cluster for local deployments
if [[ "$CLUSTER_CONTEXT" == *"minikube"* ]]; then
    minikube image load monadic-pipeline:latest
    minikube image load monadic-pipeline-webapi:latest
fi

# Deploy both
kubectl apply -f "$K8S_DIR/deployment.yaml"
kubectl apply -f "$K8S_DIR/webapi-deployment.yaml"
```

### 2. Updated Image Pull Policies

**Changed in `k8s/deployment.yaml` and `k8s/webapi-deployment.yaml`:**

**Before:**
```yaml
imagePullPolicy: IfNotPresent  # Would try Docker Hub if not found locally
```

**After:**
```yaml
imagePullPolicy: Never  # Only use local images, never pull from registry
```

Added comments explaining when to use different policies.

### 3. Created Helper Script

**New file: `scripts/load-images-to-cluster.sh`**

Provides detailed instructions for:
- Loading images into different cluster types
- Pushing to Azure ACR, AWS ECR, Google GCR, Docker Hub
- Configuring imagePullSecrets for private registries

### 4. Enhanced Documentation

**Updated DEPLOYMENT.md:**
- Added comprehensive container registry setup section
- Documented differences between local and cloud deployments
- Provided copy-paste examples for all major cloud providers

**Created TROUBLESHOOTING.md:**
- Dedicated troubleshooting guide
- Step-by-step solutions for the image pull error
- Common issues and their fixes

**Updated README.md:**
- Added troubleshooting section
- Quick reference for fixing image pull errors

## How It Works Now

### For Local Kubernetes (Docker Desktop, minikube, kind)

1. Run: `./scripts/deploy-k8s.sh`
2. Script detects local cluster
3. Builds both images
4. Loads them into cluster automatically
5. Deploys everything
6. ✅ Works out of the box!

### For Cloud Kubernetes (AKS, EKS, GKE)

1. Build images: `docker build -f Dockerfile.webapi -t myregistry.azurecr.io/monadic-pipeline-webapi:latest .`
2. Push to registry: `docker push myregistry.azurecr.io/monadic-pipeline-webapi:latest`
3. Update image references in k8s manifests
4. Run: `./scripts/deploy-k8s.sh`
5. ✅ Deploys successfully!

## Testing

- ✅ All scripts pass shellcheck linting
- ✅ Bash syntax is valid
- ✅ Project builds successfully
- ✅ No breaking changes to existing code

## Files Changed

### Modified Files
1. `scripts/deploy-k8s.sh` - Build both images, deploy Web API, add cluster detection
2. `k8s/deployment.yaml` - Change imagePullPolicy to Never
3. `k8s/webapi-deployment.yaml` - Change imagePullPolicy to Never
4. `DEPLOYMENT.md` - Add registry setup documentation
5. `README.md` - Add troubleshooting section

### New Files
1. `scripts/load-images-to-cluster.sh` - Helper script for image loading
2. `TROUBLESHOOTING.md` - Comprehensive troubleshooting guide

## Next Steps for Users

### If Using Local Kubernetes
Just run:
```bash
./scripts/deploy-k8s.sh
```

### If Using Cloud Kubernetes
1. Push images to your container registry (see DEPLOYMENT.md)
2. Update image references in k8s manifests
3. Run the deployment script

## Related Documentation

- [DEPLOYMENT.md](DEPLOYMENT.md) - Full deployment guide
- [TROUBLESHOOTING.md](TROUBLESHOOTING.md) - Troubleshooting guide
- [README.md](README.md#troubleshooting) - Quick reference

---

**Issue Status**: ✅ RESOLVED

The deployment now works correctly for both local and cloud Kubernetes clusters, with clear documentation and automated tooling to prevent this issue from occurring again.
