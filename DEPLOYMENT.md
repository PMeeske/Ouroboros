# MonadicPipeline Deployment Guide

This guide provides comprehensive instructions for deploying MonadicPipeline in various environments using Docker, Docker Compose, and Kubernetes.

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Docker Deployment](#docker-deployment)
4. [Docker Compose Deployment](#docker-compose-deployment)
5. [Kubernetes Deployment](#kubernetes-deployment)
6. [Configuration](#configuration)
7. [Monitoring and Observability](#monitoring-and-observability)
8. [Troubleshooting](#troubleshooting)
9. [Security Considerations](#security-considerations)

## Overview

MonadicPipeline can be deployed in multiple ways:

- **Docker**: Single container deployment
- **Docker Compose**: Multi-container local/development deployment
- **Kubernetes**: Production-grade orchestrated deployment

The deployment includes the following components:

- **MonadicPipeline CLI**: The main application
- **Ollama**: LLM service for model inference
- **Qdrant**: Vector database for embeddings storage
- **Jaeger**: Distributed tracing (optional)
- **Redis**: Caching layer (optional)

## Prerequisites

### All Deployments

- [Docker](https://docs.docker.com/get-docker/) 20.10+ installed
- [Docker Compose](https://docs.docker.com/compose/install/) 2.0+ installed
- Minimum 8GB RAM available
- 20GB+ free disk space

### Kubernetes Deployments

- [kubectl](https://kubernetes.io/docs/tasks/tools/) installed and configured
- Access to a Kubernetes cluster (local or cloud)
- Minimum 3 worker nodes with 4 CPUs and 8GB RAM each (recommended)

## Docker Deployment

### Building the Docker Image

```bash
# From the project root
docker build -t monadic-pipeline:latest .
```

### Running the Container

```bash
# Basic usage
docker run -it --rm monadic-pipeline:latest --help

# With Ollama service
docker network create pipeline-network

# Start Ollama
docker run -d \
  --name ollama \
  --network pipeline-network \
  -p 11434:11434 \
  -v ollama-data:/root/.ollama \
  ollama/ollama:latest

# Run MonadicPipeline
docker run -it --rm \
  --network pipeline-network \
  -e PIPELINE__LlmProvider__OllamaEndpoint=http://ollama:11434 \
  monadic-pipeline:latest ask -q "What is functional programming?"
```

## Docker Compose Deployment

### Production Deployment

The production deployment includes all services with production-ready configurations.

```bash
# Deploy using the automated script
./scripts/deploy-docker.sh production

# Or manually
docker-compose up -d
```

#### Services Included

- **monadic-pipeline**: Main application
- **ollama**: LLM service (port 11434)
- **qdrant**: Vector database (ports 6333, 6334)
- **jaeger**: Distributed tracing UI (port 16686)
- **redis**: Caching (port 6379)

#### Using the CLI

```bash
# Show help
docker exec -it monadic-pipeline dotnet LangChainPipeline.dll --help

# Ask a question
docker exec -it monadic-pipeline dotnet LangChainPipeline.dll ask -q "Explain monads"

# Run orchestrator
docker exec -it monadic-pipeline dotnet LangChainPipeline.dll orchestrator --goal "Analyze code quality"
```

#### Viewing Logs

```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f monadic-pipeline

# Last 100 lines
docker-compose logs --tail=100 monadic-pipeline
```

#### Stopping Services

```bash
# Stop all services
docker-compose down

# Stop and remove volumes
docker-compose down -v
```

### Development Deployment

The development deployment is optimized for local development with hot reload.

```bash
# Deploy development environment
./scripts/deploy-docker.sh development

# Or manually
docker-compose -f docker-compose.dev.yml up -d
```

#### Features

- Debug logging enabled
- Source code mounted for hot reload
- In-memory vector store (no persistence)
- Simplified service stack

## Kubernetes Deployment

### Important: Container Registry Setup

Before deploying to Kubernetes, understand how images work in your cluster:

#### Local Kubernetes Clusters (Docker Desktop, Minikube, Kind)

For local development clusters, the deployment script will automatically:
- Build images locally
- Load them into your cluster
- Use `imagePullPolicy: Never` to prevent pulling from registries

No additional setup needed! Just run:
```bash
./scripts/deploy-k8s.sh
```

#### Cloud Kubernetes Clusters (AKS, EKS, GKE)

For cloud deployments, you **must** push images to a container registry. We provide automated scripts:

**Option 1: Azure AKS with ACR (Recommended for Azure)**

```bash
# Automated deployment to AKS
./scripts/deploy-aks.sh myregistry monadic-pipeline
```

This script will:
- Build Docker images
- Login to Azure Container Registry
- Push images to ACR
- Update manifests with correct image references
- Deploy to your AKS cluster

**Prerequisites:**
- Azure CLI installed and logged in (`az login`)
- kubectl configured for your AKS cluster
- ACR already created

**Option 2: Other Cloud Providers (EKS, GKE, Docker Hub)**

```bash
# For AWS EKS with ECR
./scripts/deploy-cloud.sh 123456789.dkr.ecr.us-east-1.amazonaws.com

# For GCP GKE with GCR
./scripts/deploy-cloud.sh gcr.io/my-project

# For Docker Hub
./scripts/deploy-cloud.sh docker.io/myusername
```

This script will handle authentication, building, pushing, and deployment.

**Option 3: IONOS Cloud (Recommended by Adaptive Systems Inc.)**

IONOS Cloud provides enterprise-grade Kubernetes with European data sovereignty and cost-effective pricing:

```bash
# Automated IONOS Cloud deployment
./scripts/deploy-ionos.sh monadic-pipeline
```

This script will:
- Authenticate with IONOS Container Registry
- Build and push images to adaptive-systems.cr.de-fra.ionos.com
- Configure IONOS-specific storage classes (ionos-enterprise-ssd)
- Create registry pull secrets
- Deploy all components with IONOS-optimized settings

**Prerequisites:**
- IONOS Cloud account and Managed Kubernetes cluster
- kubectl configured for IONOS cluster (download kubeconfig from IONOS Console)
- IONOS Container Registry credentials

**Environment Variables (optional):**
```bash
export IONOS_REGISTRY="adaptive-systems.cr.de-fra.ionos.com"
export IONOS_USERNAME="your-username"
export IONOS_PASSWORD="your-password"

./scripts/deploy-ionos.sh
```

For detailed IONOS deployment instructions, see [IONOS Cloud Deployment Guide](docs/IONOS_DEPLOYMENT_GUIDE.md).

#### Manual Cloud Deployment

If you prefer manual control:

1. **Build and tag images with your registry URL:**
   ```bash
   # Azure Container Registry (ACR)
   docker build -t myregistry.azurecr.io/monadic-pipeline:latest .
   docker build -f Dockerfile.webapi -t myregistry.azurecr.io/monadic-pipeline-webapi:latest .
   
   # AWS Elastic Container Registry (ECR)
   docker build -t 123456789.dkr.ecr.us-east-1.amazonaws.com/monadic-pipeline:latest .
   docker build -f Dockerfile.webapi -t 123456789.dkr.ecr.us-east-1.amazonaws.com/monadic-pipeline-webapi:latest .
   
   # IONOS Cloud Container Registry
   docker build -t adaptive-systems.cr.de-fra.ionos.com/monadic-pipeline:latest .
   docker build -f Dockerfile.webapi -t adaptive-systems.cr.de-fra.ionos.com/monadic-pipeline-webapi:latest .
   
   # Docker Hub
   docker build -t your-username/monadic-pipeline:latest .
   docker build -f Dockerfile.webapi -t your-username/monadic-pipeline-webapi:latest .
   ```

2. **Push to your registry:**
   ```bash
   # Azure
   az acr login --name myregistry
   docker push myregistry.azurecr.io/monadic-pipeline:latest
   docker push myregistry.azurecr.io/monadic-pipeline-webapi:latest
   
   # AWS
   aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin 123456789.dkr.ecr.us-east-1.amazonaws.com
   docker push 123456789.dkr.ecr.us-east-1.amazonaws.com/monadic-pipeline:latest
   docker push 123456789.dkr.ecr.us-east-1.amazonaws.com/monadic-pipeline-webapi:latest
   
   # IONOS Cloud
   docker login adaptive-systems.cr.de-fra.ionos.com
   docker push adaptive-systems.cr.de-fra.ionos.com/monadic-pipeline:latest
   docker push adaptive-systems.cr.de-fra.ionos.com/monadic-pipeline-webapi:latest
   
   # Docker Hub
   docker login
   docker push your-username/monadic-pipeline:latest
   docker push your-username/monadic-pipeline-webapi:latest
   ```

3. **Update image references in `k8s/deployment.yaml` and `k8s/webapi-deployment.yaml`:**
   ```yaml
   containers:
   - name: webapi
     image: myregistry.azurecr.io/monadic-pipeline-webapi:latest
     imagePullPolicy: Always  # or IfNotPresent
   ```

4. **For private registries, create imagePullSecrets:**
   ```bash
   kubectl create secret docker-registry regcred \
     --docker-server=myregistry.azurecr.io \
     --docker-username=myusername \
     --docker-password=mypassword \
     --namespace=monadic-pipeline
   ```
   
   Then add to your deployment:
   ```yaml
   spec:
     imagePullSecrets:
     - name: regcred
     containers:
     - name: webapi
       ...
   ```

### Automated Deployment

Use the provided deployment script:

```bash
# Deploy to default namespace (monadic-pipeline)
./scripts/deploy-k8s.sh

# Deploy to custom namespace
./scripts/deploy-k8s.sh my-namespace
```

### Manual Deployment

#### Step 1: Create Namespace

```bash
kubectl apply -f k8s/namespace.yaml
```

#### Step 2: Configure Secrets

⚠️ **Important**: Update `k8s/secrets.yaml` with your actual secrets before deploying to production.

```bash
kubectl apply -f k8s/secrets.yaml
```

#### Step 3: Apply Configuration

```bash
kubectl apply -f k8s/configmap.yaml
```

#### Step 4: Deploy Services

```bash
# Deploy Ollama
kubectl apply -f k8s/ollama.yaml

# Deploy Qdrant
kubectl apply -f k8s/qdrant.yaml

# Deploy Jaeger (optional)
kubectl apply -f k8s/jaeger.yaml

# Deploy MonadicPipeline CLI
kubectl apply -f k8s/deployment.yaml

# Deploy MonadicPipeline Web API
kubectl apply -f k8s/webapi-deployment.yaml
```

#### Step 5: Verify Deployment

```bash
# Check all resources
kubectl get all -n monadic-pipeline

# Check pod status
kubectl get pods -n monadic-pipeline

# View deployment status
kubectl rollout status deployment/monadic-pipeline -n monadic-pipeline
```

### Accessing Services

#### Port Forwarding

```bash
# Jaeger UI
kubectl port-forward -n monadic-pipeline service/jaeger-ui 16686:16686

# Qdrant Dashboard
kubectl port-forward -n monadic-pipeline service/qdrant-service 6333:6333
```

#### Execute CLI Commands

```bash
# Interactive shell
kubectl exec -it deployment/monadic-pipeline -n monadic-pipeline -- /bin/bash

# Run CLI command directly
kubectl exec -it deployment/monadic-pipeline -n monadic-pipeline -- \
  dotnet LangChainPipeline.dll --help
```

### Scaling

```bash
# Scale MonadicPipeline deployment
kubectl scale deployment/monadic-pipeline --replicas=3 -n monadic-pipeline

# Autoscaling (requires metrics server)
kubectl autoscale deployment/monadic-pipeline \
  --cpu-percent=80 --min=2 --max=10 -n monadic-pipeline
```

### Updating Deployment

```bash
# Update image
kubectl set image deployment/monadic-pipeline \
  monadic-pipeline=monadic-pipeline:v2.0.0 -n monadic-pipeline

# Rollback to previous version
kubectl rollout undo deployment/monadic-pipeline -n monadic-pipeline
```

## Configuration

### Environment Variables

Configuration can be overridden using environment variables:

```bash
# Docker
docker run -e PIPELINE__LlmProvider__OllamaEndpoint=http://custom-ollama:11434 ...

# Docker Compose (add to docker-compose.yml)
environment:
  - PIPELINE__LlmProvider__OllamaEndpoint=http://custom-ollama:11434
  - PIPELINE__Execution__MaxTurns=10

# Kubernetes (add to deployment.yaml)
env:
- name: PIPELINE__LlmProvider__OllamaEndpoint
  value: "http://custom-ollama:11434"
```

### Configuration Files

The application uses layered configuration:

1. `appsettings.json` - Base configuration
2. `appsettings.Production.json` - Production overrides
3. Environment variables - Runtime overrides

See [CONFIGURATION_AND_SECURITY.md](CONFIGURATION_AND_SECURITY.md) for detailed configuration options.

## Monitoring and Observability

### Jaeger Tracing

Access the Jaeger UI to view distributed traces:

- **Docker Compose**: http://localhost:16686
- **Kubernetes**: Port-forward service and access http://localhost:16686

### Logs

#### Docker Compose

```bash
# View logs
docker-compose logs -f monadic-pipeline

# Logs are also persisted to ./logs/
tail -f logs/pipeline-*.log
```

#### Kubernetes

```bash
# View logs
kubectl logs -f deployment/monadic-pipeline -n monadic-pipeline

# View logs from specific pod
kubectl logs -f <pod-name> -n monadic-pipeline

# View previous container logs
kubectl logs <pod-name> -n monadic-pipeline --previous
```

### Metrics

Metrics are exposed when enabled in configuration:

```json
{
  "Pipeline": {
    "Observability": {
      "EnableMetrics": true,
      "MetricsExportEndpoint": "/metrics"
    }
  }
}
```

## Troubleshooting

### Common Issues

#### Ollama Not Ready

```bash
# Check Ollama status
curl http://localhost:11434/api/tags

# Pull required models
docker exec ollama ollama pull llama3
docker exec ollama ollama pull nomic-embed-text
```

#### Out of Memory

Increase Docker memory allocation:

- Docker Desktop: Settings → Resources → Memory (increase to 8GB+)
- Docker Engine: Update daemon.json

#### Connection Refused

Ensure services are on the same network:

```bash
# Docker Compose automatically creates network
# For manual Docker, create network:
docker network create pipeline-network
```

#### Kubernetes Pod CrashLoopBackOff

```bash
# View logs
kubectl logs <pod-name> -n monadic-pipeline

# Describe pod for events
kubectl describe pod <pod-name> -n monadic-pipeline

# Check resource limits
kubectl top pods -n monadic-pipeline
```

### Debug Mode

Enable debug logging:

```bash
# Docker
docker run -e PIPELINE__Observability__MinimumLogLevel=Debug ...

# Kubernetes
kubectl set env deployment/monadic-pipeline \
  PIPELINE__Observability__MinimumLogLevel=Debug -n monadic-pipeline
```

## Security Considerations

### Production Checklist

- [ ] Update secrets in `k8s/secrets.yaml` with actual values
- [ ] Use external secret management (Azure Key Vault, AWS Secrets Manager, etc.)
- [ ] Enable TLS/SSL for all services
- [ ] Configure network policies in Kubernetes
- [ ] Set resource limits and requests
- [ ] Enable pod security policies
- [ ] Use read-only root filesystem where possible
- [ ] Implement authentication for Jaeger and Qdrant dashboards
- [ ] Regular security updates and image scanning
- [ ] Configure firewall rules

### Secret Management

**Never commit secrets to version control!**

Use environment-specific secret management:

- **Development**: .NET User Secrets
- **Docker**: Docker Secrets or environment variables
- **Kubernetes**: Kubernetes Secrets or external secret operators

Example using Kubernetes external secrets:

```yaml
apiVersion: external-secrets.io/v1beta1
kind: ExternalSecret
metadata:
  name: monadic-pipeline-secrets
  namespace: monadic-pipeline
spec:
  secretStoreRef:
    name: azure-key-vault
    kind: SecretStore
  target:
    name: monadic-pipeline-secrets
  data:
    - secretKey: openai-api-key
      remoteRef:
        key: openai-api-key
```

## Additional Resources

- [IMPLEMENTATION_GUIDE.md](IMPLEMENTATION_GUIDE.md) - Implementation details
- [CONFIGURATION_AND_SECURITY.md](CONFIGURATION_AND_SECURITY.md) - Configuration reference
- [README.md](README.md) - Project overview
- [Docker Documentation](https://docs.docker.com/)
- [Kubernetes Documentation](https://kubernetes.io/docs/)

## Support

For issues and questions:

- GitHub Issues: https://github.com/PMeeske/MonadicPipeline/issues
- Documentation: See project README and guides

---

**Version**: 1.0.0  
**Last Updated**: 2025-01-01
