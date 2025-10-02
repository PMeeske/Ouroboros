# MonadicPipeline Troubleshooting Guide

This guide provides solutions to common issues encountered when deploying and running MonadicPipeline.

## Table of Contents

- [Kubernetes Deployment Issues](#kubernetes-deployment-issues)
- [Docker Issues](#docker-issues)
- [Build Issues](#build-issues)
- [Runtime Issues](#runtime-issues)

## Kubernetes Deployment Issues

### Image Pull Errors

#### Symptom
```
Failed to pull image "monadic-pipeline-webapi:latest": failed to pull and unpack image 
"docker.io/library/monadic-pipeline-webapi:latest": failed to resolve reference 
"docker.io/library/monadic-pipeline-webapi:latest": pull access denied, repository does 
not exist or may require authorization: server message: insufficient_scope: 
authorization failed
```

Or in Kubernetes events:
```bash
kubectl get events -n monadic-pipeline --sort-by='.lastTimestamp'
# Shows: Error: ImagePullBackOff
```

#### Root Cause
The Kubernetes pod is trying to pull the Docker image from Docker Hub (docker.io/library/), but the image doesn't exist there. This happens when:
1. **Local clusters**: The image wasn't built locally or wasn't loaded into the cluster
2. **Cloud clusters (AKS/EKS/GKE)**: The image wasn't pushed to a container registry, or the deployment is configured to look in the wrong place
3. The `imagePullPolicy` is incompatible with your deployment type

#### Solution

**For Local Kubernetes Clusters** (Docker Desktop, minikube, kind):

1. Use the automated deployment script which handles everything:
   ```bash
   ./scripts/deploy-k8s.sh
   ```

2. Or manually build and load images:
   ```bash
   # Build images
   docker build -t monadic-pipeline:latest .
   docker build -f Dockerfile.webapi -t monadic-pipeline-webapi:latest .
   
   # Load into minikube
   minikube image load monadic-pipeline:latest
   minikube image load monadic-pipeline-webapi:latest
   
   # Load into kind
   kind load docker-image monadic-pipeline:latest
   kind load docker-image monadic-pipeline-webapi:latest
   
   # Docker Desktop - images are automatically available
   ```

3. Ensure `imagePullPolicy: Never` is set in your deployment manifests

**For Cloud Kubernetes Clusters** (AKS, EKS, GKE):

**Quick Solution - Use Automated Deployment Scripts:**

The easiest way to deploy to cloud Kubernetes is to use our automated deployment scripts:

```bash
# For Azure AKS with ACR
./scripts/deploy-aks.sh myregistry monadic-pipeline

# For other cloud providers (AWS EKS, GCP GKE, Docker Hub)
./scripts/deploy-cloud.sh 123456789.dkr.ecr.us-east-1.amazonaws.com monadic-pipeline
./scripts/deploy-cloud.sh gcr.io/my-project monadic-pipeline
./scripts/deploy-cloud.sh docker.io/myusername monadic-pipeline
```

These scripts will:
- Build the Docker images
- Push them to your container registry
- Update the Kubernetes manifests with correct image references
- Deploy everything to your cluster
- Configure imagePullSecrets if needed

**Manual Solution:**

If you prefer to deploy manually or need to troubleshoot:

1. **Build and tag images with your registry URL:**
   ```bash
   # Azure Container Registry (ACR)
   docker build -t myregistry.azurecr.io/monadic-pipeline:latest .
   docker build -f Dockerfile.webapi -t myregistry.azurecr.io/monadic-pipeline-webapi:latest .
   
   # AWS Elastic Container Registry (ECR)
   docker build -t 123456789.dkr.ecr.us-east-1.amazonaws.com/monadic-pipeline:latest .
   docker build -f Dockerfile.webapi -t 123456789.dkr.ecr.us-east-1.amazonaws.com/monadic-pipeline-webapi:latest .
   
   # GCP Google Container Registry (GCR)
   docker build -t gcr.io/my-project/monadic-pipeline:latest .
   docker build -f Dockerfile.webapi -t gcr.io/my-project/monadic-pipeline-webapi:latest .
   
   # Docker Hub
   docker build -t yourusername/monadic-pipeline:latest .
   docker build -f Dockerfile.webapi -t yourusername/monadic-pipeline-webapi:latest .
   ```

2. **Push to your registry:**
   ```bash
   # Azure ACR
   az acr login --name myregistry
   docker push myregistry.azurecr.io/monadic-pipeline:latest
   docker push myregistry.azurecr.io/monadic-pipeline-webapi:latest
   
   # AWS ECR
   aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin 123456789.dkr.ecr.us-east-1.amazonaws.com
   docker push 123456789.dkr.ecr.us-east-1.amazonaws.com/monadic-pipeline:latest
   docker push 123456789.dkr.ecr.us-east-1.amazonaws.com/monadic-pipeline-webapi:latest
   
   # GCP GCR
   gcloud auth configure-docker
   docker push gcr.io/my-project/monadic-pipeline:latest
   docker push gcr.io/my-project/monadic-pipeline-webapi:latest
   
   # Docker Hub
   docker login
   docker push yourusername/monadic-pipeline:latest
   docker push yourusername/monadic-pipeline-webapi:latest
   ```

3. **Use cloud-specific deployment manifests:**
   
   The repository includes cloud-ready manifests in `k8s/deployment.cloud.yaml` and `k8s/webapi-deployment.cloud.yaml`.
   
   Update the image references in these files:
   ```yaml
   # In k8s/deployment.cloud.yaml
   containers:
   - name: monadic-pipeline
     image: myregistry.azurecr.io/monadic-pipeline:latest
     imagePullPolicy: Always
   ```
   
   ```yaml
   # In k8s/webapi-deployment.cloud.yaml
   containers:
   - name: webapi
     image: myregistry.azurecr.io/monadic-pipeline-webapi:latest
     imagePullPolicy: Always
   ```

4. **For private registries, create imagePullSecrets:**
   ```bash
   kubectl create secret docker-registry regcred \
     --docker-server=myregistry.azurecr.io \
     --docker-username=myusername \
     --docker-password=mypassword \
     --namespace=monadic-pipeline
   ```
   
   Then uncomment imagePullSecrets in the deployment manifests:
   ```yaml
   spec:
     imagePullSecrets:
     - name: regcred
     containers:
     - name: webapi
       ...
   ```

5. **For AKS, ensure the cluster has permissions to pull from ACR:**
   ```bash
   # Attach ACR to AKS cluster
   az aks update -n myAKSCluster -g myResourceGroup --attach-acr myregistry
   
   # Or verify existing permissions
   az aks check-acr --resource-group myResourceGroup --name myAKSCluster --acr myregistry.azurecr.io
   ```

### Pod CrashLoopBackOff

#### Symptom
```bash
kubectl get pods -n monadic-pipeline
NAME                                      READY   STATUS             RESTARTS   AGE
monadic-pipeline-webapi-7ddcb5c887-xyz    0/1     CrashLoopBackOff   5          3m
```

#### Solution

1. **Check pod logs:**
   ```bash
   kubectl logs -n monadic-pipeline <pod-name>
   kubectl logs -n monadic-pipeline <pod-name> --previous
   ```

2. **Describe pod for events:**
   ```bash
   kubectl describe pod -n monadic-pipeline <pod-name>
   ```

3. **Common causes:**
   - Missing environment variables
   - Cannot connect to dependencies (Ollama, Qdrant)
   - Configuration errors
   - Insufficient resources

4. **Verify dependencies are running:**
   ```bash
   kubectl get pods -n monadic-pipeline
   ```

### AKS-Specific Issues

#### ImagePullBackOff on AKS

**Symptom:**
Pod cannot pull images from Azure Container Registry (ACR).

**Common Causes:**
1. AKS cluster doesn't have permission to pull from ACR
2. Image not pushed to ACR
3. Wrong image reference in deployment

**Solutions:**

1. **Attach ACR to AKS (Recommended):**
   ```bash
   # This grants AKS managed identity permission to pull from ACR
   az aks update -n myAKSCluster -g myResourceGroup --attach-acr myACRName
   ```

2. **Verify ACR permissions:**
   ```bash
   az aks check-acr --resource-group myResourceGroup --name myAKSCluster --acr myACRName.azurecr.io
   ```

3. **Check if images exist in ACR:**
   ```bash
   az acr repository list --name myACRName
   az acr repository show-tags --name myACRName --repository monadic-pipeline-webapi
   ```

4. **Use the automated deployment script:**
   ```bash
   ./scripts/deploy-aks.sh myACRName monadic-pipeline
   ```

5. **Manual imagePullSecret (if not using managed identity):**
   ```bash
   # Get ACR credentials
   ACR_LOGIN_SERVER=$(az acr show --name myACRName --query loginServer --output tsv)
   ACR_USERNAME=$(az acr credential show --name myACRName --query username --output tsv)
   ACR_PASSWORD=$(az acr credential show --name myACRName --query "passwords[0].value" --output tsv)
   
   # Create secret
   kubectl create secret docker-registry regcred \
     --docker-server=$ACR_LOGIN_SERVER \
     --docker-username=$ACR_USERNAME \
     --docker-password=$ACR_PASSWORD \
     --namespace=monadic-pipeline
   ```

#### AKS Node Issues

**Symptom:**
Pods stuck in Pending state.

**Solution:**
```bash
# Check node status
kubectl get nodes

# Check node resources
kubectl describe node <node-name>

# Scale the node pool
az aks nodepool scale --resource-group myResourceGroup \
  --cluster-name myAKSCluster \
  --name nodepool1 \
  --node-count 3
```

### Service Unavailable

#### Symptom
Services cannot communicate with each other.

#### Solution

1. **Check service endpoints:**
   ```bash
   kubectl get endpoints -n monadic-pipeline
   ```

2. **Test service connectivity:**
   ```bash
   kubectl run -it --rm debug --image=curlimages/curl --restart=Never -n monadic-pipeline -- \
     curl http://ollama-service:11434/api/tags
   ```

3. **Verify service names in environment variables match service definitions**

## Docker Issues

### Build Failures

#### Solution

1. **Clear Docker cache:**
   ```bash
   docker builder prune
   docker system prune -a
   ```

2. **Rebuild without cache:**
   ```bash
   docker build --no-cache -t monadic-pipeline:latest .
   docker build --no-cache -f Dockerfile.webapi -t monadic-pipeline-webapi:latest .
   ```

### Out of Memory

#### Symptom
Docker builds fail with out of memory errors.

#### Solution

1. **Increase Docker memory:**
   - Docker Desktop: Settings → Resources → Memory (set to 8GB+)
   - Docker Engine: Edit `/etc/docker/daemon.json`:
     ```json
     {
       "default-ulimits": {
         "memlock": {
           "hard": -1,
           "soft": -1
         }
       }
     }
     ```

2. **Free up disk space:**
   ```bash
   docker system prune -a --volumes
   ```

## Build Issues

### NuGet Restore Failures

#### Solution

1. **Clear NuGet cache:**
   ```bash
   dotnet nuget locals all --clear
   ```

2. **Restore with verbose output:**
   ```bash
   dotnet restore -v detailed
   ```

3. **Check internet connectivity and proxy settings**

### Compilation Errors

#### Solution

1. **Clean and rebuild:**
   ```bash
   dotnet clean
   dotnet build
   ```

2. **Check .NET SDK version:**
   ```bash
   dotnet --version
   # Should be 8.0 or later
   ```

## Runtime Issues

### Ollama Connection Failures

#### Symptom
```
Cannot connect to Ollama at http://ollama:11434
```

#### Solution

1. **Verify Ollama is running:**
   ```bash
   # Docker Compose
   docker-compose ps ollama
   
   # Kubernetes
   kubectl get pods -n monadic-pipeline -l app=ollama
   ```

2. **Check Ollama health:**
   ```bash
   curl http://localhost:11434/api/tags
   ```

3. **Pull required models:**
   ```bash
   docker exec ollama ollama pull llama3
   docker exec ollama ollama pull nomic-embed-text
   ```

### Vector Store Connection Issues

#### Solution

1. **Verify Qdrant is running:**
   ```bash
   kubectl get pods -n monadic-pipeline -l app=qdrant
   ```

2. **Check connection string in environment variables**

3. **Test Qdrant connectivity:**
   ```bash
   curl http://localhost:6333/health
   ```

### Health Check Failures

#### Solution

1. **Verify health endpoint:**
   ```bash
   curl http://localhost:8080/health
   ```

2. **Check application logs:**
   ```bash
   kubectl logs -n monadic-pipeline deployment/monadic-pipeline-webapi
   ```

3. **Adjust health check timing in deployment manifests if startup is slow**

## Getting Help

If you continue to experience issues:

1. **Check logs with verbose output:**
   ```bash
   # Docker
   docker-compose logs -f --tail=100
   
   # Kubernetes
   kubectl logs -f deployment/monadic-pipeline-webapi -n monadic-pipeline
   ```

2. **Verify configuration:**
   ```bash
   kubectl get configmap monadic-pipeline-config -n monadic-pipeline -o yaml
   kubectl get secrets monadic-pipeline-secrets -n monadic-pipeline -o yaml
   ```

3. **Review the complete deployment guide:** [DEPLOYMENT.md](DEPLOYMENT.md)

4. **Open an issue:** [GitHub Issues](https://github.com/PMeeske/MonadicPipeline/issues)

## Related Documentation

- [DEPLOYMENT.md](DEPLOYMENT.md) - Comprehensive deployment guide
- [CONFIGURATION_AND_SECURITY.md](CONFIGURATION_AND_SECURITY.md) - Configuration reference
- [README.md](README.md) - Project overview
