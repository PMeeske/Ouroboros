# MonadicPipeline Deployment Scripts

This directory contains deployment scripts and configuration files for various deployment scenarios.

## Quick Reference

| Scenario | Script | Command |
|----------|--------|---------| 
| **Infrastructure Dependency Validation** | **`validate-infrastructure-dependencies.sh`** | **`./scripts/validate-infrastructure-dependencies.sh`** |
| **Kubernetes Version Check** | **`check-k8s-version.sh`** | **`./scripts/check-k8s-version.sh`** |
| **Terraform Validation** | **`validate-terraform.sh`** | **`./scripts/validate-terraform.sh dev`** |
| **Terraform Management** | **`manage-infrastructure.sh`** | **`./scripts/manage-infrastructure.sh apply dev`** |
| **Check External Access** | **`check-external-access.sh`** | **`./scripts/check-external-access.sh dev`** |
| **IONOS Prerequisites** | **`validate-ionos-prerequisites.sh`** | **`./scripts/validate-ionos-prerequisites.sh`** |
| Local Docker Compose | `deploy-docker.sh` | `./scripts/deploy-docker.sh production` |
| Local Kubernetes | `deploy-k8s.sh` | `./scripts/deploy-k8s.sh` |
| **Azure AKS + ACR** | **`deploy-aks.sh`** | **`./scripts/deploy-aks.sh myregistry`** |
| **IONOS Cloud** | **`deploy-ionos.sh`** | **`./scripts/deploy-ionos.sh monadic-pipeline`** |
| **IONOS Diagnostics** | **`check-ionos-deployment.sh`** | **`./scripts/check-ionos-deployment.sh`** |

## Scripts Overview

### Infrastructure Management

#### `validate-infrastructure-dependencies.sh` - Infrastructure Dependency Validation (NEW)
Validates that all infrastructure dependencies between C#, Kubernetes, and Terraform are correctly configured.

```bash
./scripts/validate-infrastructure-dependencies.sh
```

**What it checks:**
- Terraform configuration and validation
- Kubernetes manifest syntax and structure  
- C# application configuration (appsettings.json)
- Configuration consistency across layers
- Docker and Docker Compose files
- Resource requirements and allocation
- Storage configuration alignment
- Network configuration
- Security configuration (secrets, registry tokens)
- CI/CD workflow configuration

**Output:**
- ✓ Passed checks in green
- ✗ Failed checks in red
- ⚠ Warnings in yellow
- Comprehensive validation summary

**Use case:**
- Before deploying to any environment
- After making infrastructure changes
- When troubleshooting deployment issues
- Before creating a PR with infrastructure changes
- Validating end-to-end configuration

**See also:**
- [Infrastructure Dependencies Guide](../docs/INFRASTRUCTURE_DEPENDENCIES.md)
- [Terraform-K8s Integration Guide](../docs/TERRAFORM_K8S_INTEGRATION.md)
- [Environment Infrastructure Mapping](../docs/ENVIRONMENT_INFRASTRUCTURE_MAPPING.md)

#### `check-k8s-version.sh` - Kubernetes Version Compatibility Check (NEW)
Validates Kubernetes version configuration for IONOS Cloud compatibility and security.

```bash
./scripts/check-k8s-version.sh
```

**What it checks:**
- Kubernetes version in all environments (dev, staging, production)
- Version validation rules in Terraform
- Minimum version requirements (1.29+)
- Deprecated API versions in Kubernetes manifests
- Documentation completeness
- Module configuration

**Output:**
- ✓ Passed checks in green
- ✗ Failed checks in red (e.g., deprecated versions)
- ⚠ Warnings in yellow
- Summary with recommendations

**Use case:**
- Before deploying infrastructure to IONOS Cloud
- When planning Kubernetes version upgrades
- Validating version compatibility
- Ensuring no deprecated API versions in manifests

**See also:**
- [Kubernetes Version Compatibility Guide](../docs/K8S_VERSION_COMPATIBILITY.md)
- [IONOS IAC Guide](../docs/IONOS_IAC_GUIDE.md)

#### `validate-terraform.sh` - Terraform Validation (NEW)
Validates your Terraform infrastructure setup before provisioning.

```bash
./scripts/validate-terraform.sh [environment]
```

**What it checks:**
- Terraform installation and version
- IONOS Cloud credentials configuration
- Terraform directory structure
- Module completeness
- Configuration formatting
- Environment-specific settings
- GitHub Actions workflow

**Example:**
```bash
./scripts/validate-terraform.sh production
```

**Output:**
- ✓ Passed checks in green
- ✗ Failed checks in red
- ⚠ Warnings in yellow
- Summary with recommendations

**Use case:**
- Before first-time infrastructure provisioning
- After modifying Terraform configuration
- Troubleshooting setup issues

#### `manage-infrastructure.sh` - Terraform Infrastructure Management (NEW)
Manage IONOS Cloud infrastructure using Terraform with a simple command-line interface.

```bash
./scripts/manage-infrastructure.sh [command] [environment]
```

**Commands:**
- `init`: Initialize Terraform
- `plan`: Preview infrastructure changes
- `apply`: Apply infrastructure changes
- `destroy`: Destroy infrastructure
- `output`: Show Terraform outputs
- `kubeconfig`: Save kubeconfig file

**Environments:**
- `dev`: Development (minimal resources, ~€50-80/month)
- `staging`: Staging (medium resources, ~€100-150/month)
- `production`: Production (full resources, ~€150-250/month)

**Examples:**
```bash
# Initialize Terraform
./scripts/manage-infrastructure.sh init

# Preview changes for production
./scripts/manage-infrastructure.sh plan production

# Apply infrastructure for development
./scripts/manage-infrastructure.sh apply dev

# Get kubeconfig for production
./scripts/manage-infrastructure.sh kubeconfig production

# Destroy development infrastructure
./scripts/manage-infrastructure.sh destroy dev
```

**Prerequisites:**
- Terraform >= 1.5.0 installed
- IONOS Cloud credentials (`IONOS_TOKEN` or `IONOS_USERNAME`/`IONOS_PASSWORD`)

**Features:**
- Color-coded output for easy reading
- Safety checks for production destruction
- Automatic prerequisite validation
- Simplified Terraform workflow

See also:
- [Terraform IaC Guide](../docs/IONOS_IAC_GUIDE.md)
- [Terraform README](../terraform/README.md)

#### `check-external-access.sh` - External Accessibility Validation (NEW)
Checks and validates external accessibility of deployed IONOS infrastructure.

```bash
./scripts/check-external-access.sh [environment]
```

**What it checks:**
- Terraform state and deployment status
- Container registry accessibility and hostname
- Kubernetes cluster state (ACTIVE/INACTIVE)
- Node pool state and health
- Public IP addresses assigned to nodes
- Kubernetes API access configuration
- Network (LAN) public/private status
- Kubeconfig availability and cluster connectivity
- Node count and status via kubectl

**Examples:**
```bash
# Check development environment
./scripts/check-external-access.sh dev

# Check production infrastructure
./scripts/check-external-access.sh production
```

**Output:**
- ✓ Passed checks in green
- ✗ Failed checks in red
- ⚠ Warnings in yellow
- Summary with recommendations

**Use cases:**
- Verify infrastructure is accessible after deployment
- Troubleshoot external connectivity issues
- Validate network configuration
- Confirm infrastructure is ready for application deployment

**Prerequisites:**
- Terraform state must exist (infrastructure deployed)
- Optional: kubectl for Kubernetes connectivity tests
- Optional: curl for HTTP connectivity tests

### Local Development

#### `qdrant-setup.sh` / `qdrant-setup.ps1` - Qdrant Vector Store Setup (NEW)
Comprehensive convenience script for setting up and managing Qdrant vector store locally.

**Linux/macOS:**
```bash
./scripts/qdrant-setup.sh [command]
```

**Windows:**
```powershell
.\scripts\qdrant-setup.ps1 [command]
```

**Commands:**
- `setup` - Full setup: start Qdrant and configure MonadicPipeline
- `start` - Start Qdrant container
- `stop` - Stop Qdrant container
- `restart` - Restart Qdrant container
- `status` - Check Qdrant status and show info
- `logs` - View Qdrant logs (follow mode)
- `configure` - Configure MonadicPipeline to use Qdrant
- `list` - List all collections
- `delete-collection <name>` - Delete a specific collection
- `clean` - Delete all Qdrant data (WARNING: destructive!)
- `help` - Show help message

**Quick Start:**
```bash
# Complete setup (Linux/macOS)
./scripts/qdrant-setup.sh setup

# Complete setup (Windows)
.\scripts\qdrant-setup.ps1 setup

# Or step by step:
./scripts/qdrant-setup.sh start       # Start Qdrant
./scripts/qdrant-setup.sh configure   # Configure app
./scripts/qdrant-setup.sh status      # Check status
```

**What it does:**
- Checks Docker and Docker Compose installation
- Starts Qdrant container with health checks
- Automatically configures .env file for Qdrant
- Lists collections and their statistics
- Provides collection management (list, delete)
- Shows Qdrant dashboard URL
- Manages Qdrant data cleanup

**Features:**
- ✓ Color-coded output for easy reading
- ✓ Automatic health checking with retries
- ✓ Safe collection deletion with confirmation
- ✓ Comprehensive status information
- ✓ Cross-platform support (Linux/macOS/Windows)

**Qdrant URLs (when running):**
- HTTP API: `http://localhost:6333`
- gRPC API: `localhost:6334`
- Dashboard: `http://localhost:6333/dashboard`

**See also:**
- [Qdrant Quick Start Guide](../docs/VECTOR_STORES_QUICKSTART.md)
- [Vector Stores Documentation](../docs/VECTOR_STORES.md)

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

#### `validate-ionos-prerequisites.sh` - IONOS Prerequisites Validation (NEW)
Comprehensive validation of all prerequisites before deploying to IONOS Cloud.

```bash
./scripts/validate-ionos-prerequisites.sh [namespace]
```

**What it checks:**
- kubectl installation and version
- Docker installation and daemon status
- Kubernetes cluster connection
- IONOS-specific cluster detection
- Cluster resources (nodes, metrics)
- IONOS storage class availability
- Namespace existence and existing resources
- Registry secret configuration
- Environment variables (IONOS_USERNAME, IONOS_PASSWORD, IONOS_TOKEN)
- Network connectivity to IONOS API and registry

**Output includes:**
- ✓ Passed checks in green
- ✗ Failed checks in red  
- ⚠ Warnings in yellow
- Comprehensive validation summary
- Actionable recommendations

**Example:**
```bash
./scripts/validate-ionos-prerequisites.sh monadic-pipeline
```

**Use cases:**
- Before first deployment to IONOS Cloud
- After cluster configuration changes
- When troubleshooting deployment issues
- As part of pre-deployment checklist

**Recommended workflow:**
1. Run validation script first
2. Address any failures or warnings
3. Proceed with deployment

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
| **Terraform Validation** | **`validate-terraform.sh`** | **`./scripts/validate-terraform.sh dev`** |
| **Terraform Management** | **`manage-infrastructure.sh`** | **`./scripts/manage-infrastructure.sh apply dev`** |
| **Check External Access** | **`check-external-access.sh`** | **`./scripts/check-external-access.sh dev`** |
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
# Step 1: Validate prerequisites
./scripts/validate-ionos-prerequisites.sh monadic-pipeline

# Step 2: Deploy to IONOS Cloud
./scripts/deploy-ionos.sh monadic-pipeline

# Step 3: Check deployment status and diagnose issues
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
