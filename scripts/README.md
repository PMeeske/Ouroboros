# MonadicPipeline Deployment Scripts

This directory contains deployment scripts and configuration files for various deployment scenarios.

## Scripts Overview

### Local Development

#### `deploy-local.sh`
Publishes the application to a local directory for manual deployment or systemd service installation.

```bash
./scripts/deploy-local.sh ./publish
```

#### `deploy-docker.sh`
Automated Docker Compose deployment with all dependencies.

```bash
./scripts/deploy-docker.sh production
```

### Kubernetes Deployments

#### `validate-deployment.sh` - Pre-Deployment Validation (NEW)
Validates your deployment setup before deploying to prevent ImagePullBackOff errors.

```bash
./scripts/validate-deployment.sh [namespace]
```

**What it does:**
- Detects cluster type (local vs cloud)
- Checks if you're using the correct manifests
- Verifies local images exist (for local clusters)
- Provides specific guidance for your cluster type
- Prevents common deployment mistakes

**Example:**
```bash
./scripts/validate-deployment.sh monadic-pipeline
```

#### `deploy-k8s.sh` - Local Kubernetes
For local Kubernetes clusters (Docker Desktop, minikube, kind).

```bash
./scripts/deploy-k8s.sh [namespace]
```

**What it does:**
- Builds Docker images locally
- Detects cluster type
- Loads images into cluster
- Deploys with `imagePullPolicy: Never`

#### `deploy-aks.sh` - Azure AKS (NEW)
Automated deployment to Azure Kubernetes Service with Azure Container Registry.

```bash
./scripts/deploy-aks.sh <acr-name> [namespace]
```

**What it does:**
- Logs into ACR
- Builds and pushes images
- Updates manifests with ACR image references
- Deploys to AKS with `imagePullPolicy: Always`

**Example:**
```bash
./scripts/deploy-aks.sh myregistry monadic-pipeline
```

**Prerequisites:**
- Azure CLI installed and logged in (`az login`)
- kubectl configured for your AKS cluster
- ACR already created

#### `deploy-ionos.sh` - IONOS Cloud (NEW)
Automated deployment to IONOS Cloud Kubernetes with IONOS Container Registry.

```bash
./scripts/deploy-ionos.sh [namespace]
```

**What it does:**
- Authenticates with IONOS Container Registry
- Builds and pushes images to IONOS registry
- Updates manifests for IONOS Cloud (storage class, imagePullSecrets)
- Deploys all components (Ollama, Qdrant, Jaeger, Web API, CLI)
- Configures IONOS-specific settings

**Example:**
```bash
./scripts/deploy-ionos.sh monadic-pipeline
```

**Prerequisites:**
- kubectl configured for your IONOS Kubernetes cluster
- Docker installed
- IONOS Container Registry credentials (via env vars or interactive login)

**Environment variables:**
- `IONOS_REGISTRY`: Registry URL (default: adaptive-systems.cr.de-fra.ionos.com)
- `IONOS_USERNAME`: Registry username (optional)
- `IONOS_PASSWORD`: Registry password (optional)

#### `check-ionos-deployment.sh` - IONOS Deployment Diagnostics (NEW)
Comprehensive diagnostics tool for troubleshooting IONOS Web API deployments.

```bash
./scripts/check-ionos-deployment.sh [namespace]
```

**What it checks:**
- Deployment and pod status
- Recent Kubernetes events
- Common errors (ImagePullBackOff, CrashLoopBackOff, pending PVCs)
- Container logs (last 50 lines)
- Service and LoadBalancer status
- Registry secrets and ConfigMaps

**Output includes:**
- ✅ Health status indicators
- ⚠️  Warning messages for issues
- ❌ Error detection with solutions
- Actionable troubleshooting recommendations

**Example:**
```bash
./scripts/check-ionos-deployment.sh monadic-pipeline
```

**Use cases:**
- Quick health check after deployment
- Troubleshooting deployment failures
- Identifying configuration issues
- Checking service availability

#### `deploy-cloud.sh` - Generic Cloud (NEW)
Universal deployment script for any cloud Kubernetes (AWS EKS, GCP GKE, Docker Hub).

```bash
./scripts/deploy-cloud.sh <registry-url> [namespace]
```

**Examples:**
```bash
# AWS EKS with ECR
./scripts/deploy-cloud.sh 123456789.dkr.ecr.us-east-1.amazonaws.com

# GCP GKE with GCR
./scripts/deploy-cloud.sh gcr.io/my-project

# Docker Hub
./scripts/deploy-cloud.sh docker.io/myusername
```

**What it does:**
- Prompts for registry authentication
- Builds and pushes images
- Optionally creates imagePullSecrets
- Deploys to cluster

#### `load-images-to-cluster.sh`
Helper script to load pre-built images into different cluster types.

```bash
./scripts/load-images-to-cluster.sh
```

## Quick Reference

| Scenario | Script | Command |
|----------|--------|---------|
| Local Docker Compose | `deploy-docker.sh` | `./scripts/deploy-docker.sh production` |
| Local Kubernetes | `deploy-k8s.sh` | `./scripts/deploy-k8s.sh` |
| **Azure AKS + ACR** | **`deploy-aks.sh`** | **`./scripts/deploy-aks.sh myregistry`** |
| **IONOS Cloud** | **`deploy-ionos.sh`** | **`./scripts/deploy-ionos.sh monadic-pipeline`** |
| **IONOS Diagnostics** | **`check-ionos-deployment.sh`** | **`./scripts/check-ionos-deployment.sh`** |
| **AWS EKS + ECR** | **`deploy-cloud.sh`** | **`./scripts/deploy-cloud.sh 123.dkr.ecr.us-east-1.amazonaws.com`** |
| **GCP GKE + GCR** | **`deploy-cloud.sh`** | **`./scripts/deploy-cloud.sh gcr.io/my-project`** |

## Troubleshooting ImagePullBackOff

If you encounter `ImagePullBackOff` errors in Kubernetes:

**On Local Clusters:**
```bash
# Use the local deployment script
./scripts/deploy-k8s.sh
```

**On IONOS Cloud:**
```bash
# Deploy to IONOS Cloud
./scripts/deploy-ionos.sh monadic-pipeline

# Check deployment status and diagnose issues
./scripts/check-ionos-deployment.sh monadic-pipeline
```

**On Cloud Clusters (AKS/EKS/GKE):**
```bash
# For Azure AKS
./scripts/deploy-aks.sh myregistry

# For other clouds
./scripts/deploy-cloud.sh <your-registry-url>
```

**Manual Verification:**
```bash
# Check pod events
kubectl describe pod <pod-name> -n monadic-pipeline
kubectl get events -n monadic-pipeline --sort-by='.lastTimestamp'

# For IONOS Cloud
kubectl get secret ionos-registry-secret -n monadic-pipeline

# Verify images in registry (Azure)
az acr repository list --name myregistry

# For AKS, attach ACR
az aks update -n myCluster -g myResourceGroup --attach-acr myregistry
```

See [TROUBLESHOOTING.md](../TROUBLESHOOTING.md) for detailed solutions.

## Service Files

### `monadic-pipeline.service`
Systemd service unit file for running MonadicPipeline as a Linux service.

**Installation:**
```bash
# 1. Publish application
./scripts/deploy-local.sh /opt/monadic-pipeline

# 2. Create service user
sudo useradd -r -s /bin/false monadic

# 3. Set permissions
sudo chown -R monadic:monadic /opt/monadic-pipeline
sudo chmod +x /opt/monadic-pipeline/LangChainPipeline

# 4. Install service
sudo cp scripts/monadic-pipeline.service /etc/systemd/system/

# 5. Enable and start
sudo systemctl daemon-reload
sudo systemctl enable monadic-pipeline
sudo systemctl start monadic-pipeline

# 6. Check status
sudo systemctl status monadic-pipeline
sudo journalctl -u monadic-pipeline -f
```

## Windows Service

For Windows deployments, use the Windows Service wrapper:

```powershell
# Publish application
dotnet publish src/MonadicPipeline.CLI/MonadicPipeline.CLI.csproj -c Release -o C:\MonadicPipeline

# Install as Windows Service using sc.exe
sc.exe create MonadicPipeline binPath= "C:\MonadicPipeline\LangChainPipeline.exe" start= auto
sc.exe start MonadicPipeline
```

Or use [NSSM (Non-Sucking Service Manager)](https://nssm.cc/):
```powershell
nssm install MonadicPipeline "C:\MonadicPipeline\LangChainPipeline.exe"
nssm start MonadicPipeline
```

## Azure Deployment

For Azure App Service deployment:

```bash
# Login to Azure
az login

# Create resource group
az group create --name monadic-pipeline-rg --location eastus

# Create App Service plan
az appservice plan create --name monadic-pipeline-plan --resource-group monadic-pipeline-rg --is-linux --sku B1

# Create web app
az webapp create --name monadic-pipeline --resource-group monadic-pipeline-rg --plan monadic-pipeline-plan --runtime "DOTNETCORE:8.0"

# Deploy application
az webapp deploy --name monadic-pipeline --resource-group monadic-pipeline-rg --src-path ./publish.zip --type zip
```

## AWS Deployment

For AWS ECS deployment, see [DEPLOYMENT.md](../DEPLOYMENT.md) for detailed instructions on:
- Creating ECR repository
- Pushing Docker image
- Creating ECS task definition
- Creating ECS service

## See Also

- [DEPLOYMENT.md](../DEPLOYMENT.md) - Comprehensive deployment guide
- [TROUBLESHOOTING.md](../TROUBLESHOOTING.md) - Detailed troubleshooting guide
- [CONFIGURATION_AND_SECURITY.md](../CONFIGURATION_AND_SECURITY.md) - Configuration reference
- [README.md](../README.md) - Project overview
